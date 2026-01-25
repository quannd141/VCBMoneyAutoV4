using Hangfire;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using VCBMoney.Core.Configuration;
using VCBMoney.Core.Entities;
using VCBMoney.Core.Enums;
using VCBMoney.Core.Interfaces;
using VCBMoney.Infrastructure.Data;
using VCBMoney.Infrastructure.Services;

namespace VCBMoney.Web.Jobs;

#region Performance Configuration

public static class JobPerfConfig
{
    public const int BATCH_SIZE = 1000; 
    public static int ParallelDegree => Math.Max(2, Environment.ProcessorCount);
    public const int JOB_TIMEOUT = 3600;
    public const int BULK_COPY_BATCH = 5000;
    public const bool USE_BULK_COPY = true;
}

#endregion

#region Job3: Parse Files

public class Job3_ParseFilesJob
{
    private readonly IServiceProvider _sp;
    private readonly VcbMoneySettings _settings;
    private readonly ILogger<Job3_ParseFilesJob> _logger;
   
    public Job3_ParseFilesJob(IServiceProvider sp, IOptions<VcbMoneySettings> settings, ILogger<Job3_ParseFilesJob> logger)
    {
        _sp = sp;
        _settings = settings.Value;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: JobPerfConfig.JOB_TIMEOUT)]
    [Queue("default")]
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[Job3] Starting (Parallel={P}, Batch={B})", JobPerfConfig.ParallelDegree, JobPerfConfig.BATCH_SIZE);

        using var scope = _sp.CreateScope();
        var vcbContext = scope.ServiceProvider.GetRequiredService<VcbMoneyDbContext>();

        var files = await vcbContext.FileUploads
            .Where(f => f.Status == (int)FileStatus.Valid && f.ValidationLevel >= 2)
            .OrderBy(f => f.CreatedDate).Take(10).ToListAsync(ct);

        if (!files.Any()) return;

        int totalFiles = 0, totalRecords = 0;
        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) break;

            // Tạo scope mới cho mỗi file để tránh DbContext threading issues
            using var fileScope = _sp.CreateScope();
            var fileVcbContext = fileScope.ServiceProvider.GetRequiredService<VcbMoneyDbContext>();   
            var fileService = fileScope.ServiceProvider.GetRequiredService<IFileService>();
            var parserService = fileScope.ServiceProvider.GetRequiredService<IParserService>();
            var formatValidator = fileScope.ServiceProvider.GetRequiredService<IFormatValidator>();

            // Re-attach file entity to new context
            var trackedFile = await fileVcbContext.FileUploads.FindAsync(new object[] { file.FileId }, ct);
            if (trackedFile == null) continue;

            totalRecords += await ProcessFileAsync(fileVcbContext, fileService, parserService, formatValidator, trackedFile, ct);
            totalFiles++;
        }
        _logger.LogInformation("[Job3] DONE: Files={F}, Records={R}, Time={T}ms", totalFiles, totalRecords, sw.ElapsedMilliseconds);
    }

    private async Task<int> ProcessFileAsync(VcbMoneyDbContext vcbContext, IFileService fileService, IParserService parserService, IFormatValidator formatValidator, FileUpload file, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Check if resuming from previous run
            int startBatch = file.LastProcessedBatch ;
            bool isResume = startBatch > 0;

            if (isResume)
                _logger.LogInformation("[Job3] RESUMING file {N} from batch {B}", file.FileNameUpload, startBatch + 1);

            file.Status = (int)FileStatus.Processing;
            file.StartedAt ??= DateTime.Now;  // Keep original start time if resuming
            await vcbContext.SaveChangesAsync(ct);

            var content = await File.ReadAllTextAsync(file.FilePhysical, ct);
            var parsedList = parserService.ParseMt103(content);
            if (!parsedList.Any())
            {
                await CompleteFileAsync(vcbContext, fileService, file, 0, 0, 0, sw.ElapsedMilliseconds, true,ct);
                return 0;
            }

            var allTransRefs = parsedList.Where(p => !string.IsNullOrEmpty(p.Field20)).Select(p => p.Field20!).Distinct().ToList();
            var existingRefs = await GetExistingRefsAsync(vcbContext,  allTransRefs, ct);

            int totalValid = file.SuccessCount;  // Resume from previous count
            int totalInvalid = file.ErrorCount;
            var processedRefs = new HashSet<string>(existingRefs);  // Include already processed
            int batchNum = 0;
            int totalBatches = (int)Math.Ceiling((double)parsedList.Count / JobPerfConfig.BATCH_SIZE);

            foreach (var batch in parsedList.Chunk(JobPerfConfig.BATCH_SIZE))
            {
                batchNum++;

                // Skip already processed batches (resume logic)
                if (batchNum <= startBatch)
                {
                    _logger.LogDebug("[Job3] Skipping batch {N} (already processed)", batchNum);
                    continue;
                }

                var batchSw = Stopwatch.StartNew();
                var allExisting = new HashSet<string>(existingRefs.Concat(processedRefs));

                // Process batch with transaction - rollback on error
                var (valid, invalid, newRefs) = await ProcessBatchWithTransactionAsync(
                    vcbContext, formatValidator, file.FileId, batch.ToList(), allExisting, ct);

                totalValid += valid;
                totalInvalid += invalid;
                foreach (var r in newRefs) processedRefs.Add(r);

                // Update checkpoint after each successful batch
                file.LastProcessedBatch = batchNum;
                file.SuccessCount = totalValid;
                file.ErrorCount = totalInvalid;
                file.UpdatedDate = DateTime.Now;
                await vcbContext.SaveChangesAsync(ct);

                _logger.LogInformation("[Job3] Batch {N}/{T}: Valid={V}, Invalid={I}, Time={Ms}ms",
                    batchNum, totalBatches, valid, invalid, batchSw.ElapsedMilliseconds);
            }

            // Clear checkpoint on completion
            file.LastProcessedBatch = 0;
            await CompleteFileAsync(vcbContext, fileService, file, parsedList.Count, totalValid, totalInvalid, sw.ElapsedMilliseconds, totalInvalid == 0,ct);
            _logger.LogInformation("[Job3] File: {N}, Total={T}, Valid={V}, Invalid={I}, Time={Ms}ms",
                file.FileNameUpload, parsedList.Count, totalValid, totalInvalid, sw.ElapsedMilliseconds);
            return parsedList.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Job3] Error: {N} at batch {B}", file.FileNameUpload, file.LastProcessedBatch);
            // Keep checkpoint so we can resume later
            file.Status = (int)FileStatus.Processing;  // Keep as Processing, not Error
            file.ErrorMessage = $"Stopped at batch {file.LastProcessedBatch}: {ex.Message}";
            file.UpdatedDate = DateTime.Now;
            await vcbContext.SaveChangesAsync(ct);
            // Don't move to error folder - we'll resume later
            return 0;
        }
    }

    private async Task<HashSet<string>> GetExistingRefsAsync(VcbMoneyDbContext vcb, List<string> refs, CancellationToken ct)
    {
        // Filter out null/empty refs
        var validRefs = refs.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct().ToList();
        if (!validRefs.Any()) return new HashSet<string>();

        // FIX: Execute queries SEQUENTIALLY to avoid DbContext concurrency issues
        // DbContext is NOT thread-safe - cannot run parallel queries on same instance
        var transRefs = await vcb.Transactions.AsNoTracking()
            .Where(t => t.TransRef != null && validRefs.Contains(t.TransRef))
            .Select(t => t.TransRef!)
            .ToListAsync(ct);
            
        var importRefs = await vcb.TransactionImports.AsNoTracking()
            .Where(t => t.TransRef != null && validRefs.Contains(t.TransRef))
            .Select(t => t.TransRef!)
            .ToListAsync(ct);

        // Filter results to exclude null/empty
        return new HashSet<string>(
            transRefs.Concat(importRefs).Where(r => !string.IsNullOrWhiteSpace(r))
        );
    }

    private async Task<(int valid, int invalid, List<string> newRefs)> ProcessBatchWithTransactionAsync(VcbMoneyDbContext vcbContext,
        IFormatValidator formatValidator, string fileId, List<ParsedMt103> batch, HashSet<string> existingRefs, CancellationToken ct)
    {
        var domesticTxtList = new ConcurrentBag<DomesticTxt>();
        var transactionsList = new ConcurrentBag<Transaction>();
        var importsList = new ConcurrentBag<TransactionImport>();
        // FIX: Use ConcurrentDictionary for atomic check-and-add to prevent duplicate TransRef in same batch
        var newRefsDict = new ConcurrentDictionary<string, byte>();

        // Parallel validation (CPU-bound, no DB)
        Parallel.ForEach(batch, new ParallelOptions { MaxDegreeOfParallelism = JobPerfConfig.ParallelDegree, CancellationToken = ct }, parsed =>
        {
            var domesticTxt = new DomesticTxt
            {
                FileUploadId = fileId,
                Field20 = parsed.Field20,
                Field32A = parsed.Field32A,
                Field50K = parsed.Field50K,
                Field50R = parsed.Field50R,
                Field50S = parsed.Field50S,
                Field50T = parsed.Field50T,
                Field53 = parsed.Field53,
                Field57 = parsed.Field57,
                Field57A = parsed.Field57A,
                Field59 = parsed.Field59,
                Field59O = parsed.Field59O,
                Field59P = parsed.Field59P,
                Field59Q = parsed.Field59Q,
                Field70 = parsed.Field70,
                Field71A = parsed.Field71A,
                Field72A = parsed.Field72A,
                Field72B = parsed.Field72B,
                Field72C = parsed.Field72C,
                Field72D = parsed.Field72D,
                ValueDate = parsed.ValueDate,
                Currency = parsed.Currency,
                Amount = parsed.Amount,
                DebitAccount = StringHelper.ExtractAccount(parsed.Field53),
                BenAccount = StringHelper.ExtractAccount(parsed.Field59),
                BenName = StringHelper.ExtractBenName(parsed.Field59),
                Status = 0,
                CreatedDate = DateTime.Now,
                CreatedBy = "Job3"
            };

            var l3 = formatValidator.ValidateFormat(parsed);
            var (l4, truncated) = formatValidator.ValidateAndTruncate(parsed);
            parsed.Validation.Errors.AddRange(l3.Errors);
            parsed.Validation.Errors.AddRange(l4.Errors);

            // L5: Check duplicate - trim và check có giá trị thực sự
            var transRef = truncated.Field20?.Trim();
            bool isDuplicateInBatch = false;
            if (!string.IsNullOrEmpty(transRef))
            {
                // Check duplicate in DB first
                if (existingRefs.Contains(transRef))
                {
                    parsed.Validation.Errors.Add(new ValidationError { Level = 5, Field = "TransRef", Code = "DUPLICATE_DB", Message = "Duplicate TransRef in DB", OriginalValue = transRef, Severity = "Error" });
                }
                // FIX: Atomic check-and-add to prevent duplicate within same batch
                // TryAdd returns false if key already exists (another thread added it first)
                else if (!newRefsDict.TryAdd(transRef, 0))
                {
                    parsed.Validation.Errors.Add(new ValidationError { Level = 5, Field = "TransRef", Code = "DUPLICATE_BATCH", Message = "Duplicate TransRef in same batch/file", OriginalValue = transRef, Severity = "Error" });
                    isDuplicateInBatch = true;
                }
            }
            if (!truncated.Amount.HasValue || truncated.Amount <= 0)
                parsed.Validation.Errors.Add(new ValidationError { Level = 5, Field = "Amount", Code = "INVALID_AMOUNT", Message = "Amount <= 0", OriginalValue = truncated.Amount?.ToString(), Severity = "Error" });

            if (!parsed.Validation.HasError)
            {
                domesticTxt.Status = 1;
                transactionsList.Add(new Transaction
                {
                    TransRef = transRef ?? Guid.NewGuid().ToString("N").Substring(0, 21),
                    SourceType = "FILE",
                    SourceRef = fileId,
                    FileUploadId = fileId,
                    DebitAccountNo = StringHelper.ExtractAccount(truncated.Field53) ?? "",
                    DebitAmount = truncated.Amount,
                    DebitCurrency = truncated.Currency ?? "VND",
                    BenAccountNo = StringHelper.ExtractAccount(truncated.Field59),
                    BenAccountName = StringHelper.Truncate(StringHelper.ExtractBenName(truncated.Field59), 200),
                    BenBank = StringHelper.Truncate(truncated.Field57A, 150),
                    BenAddress = StringHelper.Truncate(truncated.Field57, 200),
                    BenIdNo = truncated.Field59O,
                    BenIdNoPlace = StringHelper.Truncate(truncated.Field59Q, 100),
                    Content = StringHelper.Truncate(truncated.Field70, 255),
                    Charge = StringHelper.ExtractAndMapCharge(truncated.Field71A?.Trim().ToUpper()),
                    TransferType = truncated.Field72A == "Y" ? 1 : 0,
                    CheckBenName = truncated.Field72C == "Y",
                    Priority = truncated.Field72D == "Y" ? 1 : 0,
                    ValueDate = truncated.ValueDate,
                    Status = 0,
                    CreatedDate = DateTime.Now,
                    CreatedBy = "Job3"
                });
            }
            else
            {
                domesticTxt.Status = 3; domesticTxt.ErrCode = parsed.Validation.Errors.First().Code;
                domesticTxt.ErrDesc = parsed.Validation.Errors.First().Message;
                importsList.Add(new TransactionImport
                {
                    SourceType = "FILE",
                    SourceRef = fileId,
                    FileUploadId = fileId,
                    TransRef = truncated.Field20?.Trim(),
                    DebitAccountNo = StringHelper.ExtractAccount(truncated.Field53),
                    DebitAmountRaw = truncated.Field32A,
                    DebitAmount = truncated.Amount?.ToString(),
                    DebitCurrency = truncated.Currency ?? "VND",
                    BenAccountNo = StringHelper.ExtractAccount(truncated.Field59),
                    BenAccountNameRaw = truncated.Field59,
                    BenAccountName = StringHelper.Truncate(StringHelper.ExtractBenName(truncated.Field59), 200),
                    BenBankRaw = truncated.Field57A,
                    BenBank = StringHelper.Truncate(truncated.Field57A, 150),
                    ContentRaw = truncated.Field70,
                    Content = StringHelper.Truncate(truncated.Field70, 255),
                    Charge = StringHelper.ExtractAndMapCharge(truncated.Field71A?.Trim().ToUpper()),
                    ValueDate = truncated.ValueDate,
                    Status = 0,
                    FailedLevel = parsed.Validation.Errors.Max(e => e.Level),
                    ValidationErrors = JsonSerializer.Serialize(parsed.Validation.Errors),
                    ErrCode = parsed.Validation.Errors.First().Code,
                    ErrDesc = StringHelper.Truncate(parsed.Validation.Errors.First().Message, 2000),
                    ErrDuplicate = parsed.Validation.Errors.Any(e => e.Code == "DUPLICATE"),
                    CreatedDate = DateTime.Now,
                    CreatedBy = "Job3"
                });
            }
            domesticTxtList.Add(domesticTxt);
        });

        // Insert with transaction - ALL or NOTHING per batch
        if (JobPerfConfig.USE_BULK_COPY)
        {
            var conn = vcbContext.Database.GetConnectionString()!;
            await BulkHelper.InsertBatchWithTransactionAsync(conn,
                domesticTxtList.ToList(), transactionsList.ToList(), importsList.ToList(), ct);
        }
        else
        {
            using var transaction = await vcbContext.Database.BeginTransactionAsync(ct);
            try
            {
                if (domesticTxtList.Any()) await vcbContext.DomesticTxts.AddRangeAsync(domesticTxtList, ct);
                if (transactionsList.Any()) await vcbContext.Transactions.AddRangeAsync(transactionsList, ct);
                if (importsList.Any()) await vcbContext.TransactionImports.AddRangeAsync(importsList, ct);
                await vcbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
        return (transactionsList.Count, importsList.Count, newRefsDict.Keys.ToList());
    }

    private async Task<(int valid, int invalid, List<string> newRefs)> ProcessBatchAsync(VcbMoneyDbContext vcbContext,
        IFormatValidator formatValidator, string fileId, List<ParsedMt103> batch, HashSet<string> existingRefs, CancellationToken ct)
    {
        var domesticTxtList = new ConcurrentBag<DomesticTxt>();
        var transactionsList = new ConcurrentBag<Transaction>();
        var importsList = new ConcurrentBag<TransactionImport>();
        // FIX: Use ConcurrentDictionary for atomic check-and-add to prevent duplicate TransRef in same batch
        var newRefsDict = new ConcurrentDictionary<string, byte>();

        Parallel.ForEach(batch, new ParallelOptions { MaxDegreeOfParallelism = JobPerfConfig.ParallelDegree, CancellationToken = ct }, parsed =>
        {
            var domesticTxt = new DomesticTxt
            {
                FileUploadId = fileId,
                Field20 = parsed.Field20,
                Field32A = parsed.Field32A,
                Field50K = parsed.Field50K,
                Field50R = parsed.Field50R,
                Field50S = parsed.Field50S,
                Field50T = parsed.Field50T,
                Field53 = parsed.Field53,
                Field57 = parsed.Field57,
                Field57A = parsed.Field57A,
                Field59 = parsed.Field59,
                Field59O = parsed.Field59O,
                Field59P = parsed.Field59P,
                Field59Q = parsed.Field59Q,
                Field70 = parsed.Field70,
                Field71A = parsed.Field71A,
                Field72A = parsed.Field72A,
                Field72B = parsed.Field72B,
                Field72C = parsed.Field72C,
                Field72D = parsed.Field72D,
                ValueDate = parsed.ValueDate,
                Currency = parsed.Currency,
                Amount = parsed.Amount,
                DebitAccount = StringHelper.ExtractAccount(parsed.Field53),
                BenAccount = StringHelper.ExtractAccount(parsed.Field59),
                BenName = StringHelper.ExtractBenName(parsed.Field59),
                Status = 0,
                CreatedDate = DateTime.Now,
                CreatedBy = "Job3"
            };

            var l3 = formatValidator.ValidateFormat(parsed);
            var (l4, truncated) = formatValidator.ValidateAndTruncate(parsed);
            parsed.Validation.Errors.AddRange(l3.Errors);
            parsed.Validation.Errors.AddRange(l4.Errors);

            // L5: Check duplicate - trim và check có giá trị thực sự
            var transRef2 = truncated.Field20?.Trim();
            if (!string.IsNullOrEmpty(transRef2))
            {
                // Check duplicate in DB first
                if (existingRefs.Contains(transRef2))
                {
                    parsed.Validation.Errors.Add(new ValidationError { Level = 5, Field = "TransRef", Code = "DUPLICATE_DB", Message = "Duplicate TransRef in DB", OriginalValue = transRef2, Severity = "Error" });
                }
                // FIX: Atomic check-and-add to prevent duplicate within same batch
                else if (!newRefsDict.TryAdd(transRef2, 0))
                {
                    parsed.Validation.Errors.Add(new ValidationError { Level = 5, Field = "TransRef", Code = "DUPLICATE_BATCH", Message = "Duplicate TransRef in same batch/file", OriginalValue = transRef2, Severity = "Error" });
                }
            }
            if (!truncated.Amount.HasValue || truncated.Amount <= 0)
                parsed.Validation.Errors.Add(new ValidationError { Level = 5, Field = "Amount", Code = "INVALID_AMOUNT", Message = "Amount <= 0", OriginalValue = truncated.Amount?.ToString(), Severity = "Error" });

            if (!parsed.Validation.HasError)
            {
                domesticTxt.Status = 1;
                transactionsList.Add(new Transaction
                {
                    TransRef = transRef2 ?? Guid.NewGuid().ToString("N").Substring(0, 21),
                    SourceType = "FILE",
                    SourceRef = fileId,
                    FileUploadId = fileId,
                    DebitAccountNo = StringHelper.ExtractAccount(truncated.Field53) ?? "",
                    DebitAmount = truncated.Amount,
                    DebitCurrency = truncated.Currency ?? "VND",
                    BenAccountNo = StringHelper.ExtractAccount(truncated.Field59),
                    BenAccountName = StringHelper.Truncate(StringHelper.ExtractBenName(truncated.Field59), 200),
                    BenBank = StringHelper.Truncate(truncated.Field57A, 150),
                    BenAddress = StringHelper.Truncate(truncated.Field57, 200),
                    BenIdNo = truncated.Field59O,
                    BenIdNoPlace = StringHelper.Truncate(truncated.Field59Q, 100),
                    Content = StringHelper.Truncate(truncated.Field70, 255),
                    Charge = StringHelper.ExtractAndMapCharge(truncated.Field71A?.Trim().ToUpper()),
                    TransferType = truncated.Field72A == "Y" ? 1 : 0,
                    CheckBenName = truncated.Field72C == "Y",
                    Priority = truncated.Field72D == "Y" ? 1 : 0,
                    ValueDate = truncated.ValueDate,
                    Status = 0,
                    CreatedDate = DateTime.Now,
                    CreatedBy = "Job3"
                });
            }
            else
            {
                domesticTxt.Status = 3; domesticTxt.ErrCode = parsed.Validation.Errors.First().Code;
                domesticTxt.ErrDesc = parsed.Validation.Errors.First().Message;
                importsList.Add(new TransactionImport
                {
                    SourceType = "FILE",
                    SourceRef = fileId,
                    FileUploadId = fileId,
                    TransRef = truncated.Field20?.Trim(),
                    DebitAccountNo = StringHelper.ExtractAccount(truncated.Field53),
                    DebitAmountRaw = truncated.Field32A,
                    DebitAmount = truncated.Amount?.ToString(),
                    DebitCurrency = truncated.Currency ?? "VND",
                    BenAccountNo = StringHelper.ExtractAccount(truncated.Field59),
                    BenAccountNameRaw = truncated.Field59,
                    BenAccountName = StringHelper.Truncate(StringHelper.ExtractBenName(truncated.Field59), 200),
                    BenBankRaw = truncated.Field57A,
                    BenBank = StringHelper.Truncate(truncated.Field57A, 150),
                    ContentRaw = truncated.Field70,
                    Content = StringHelper.Truncate(truncated.Field70, 255),
                    Charge = StringHelper.ExtractAndMapCharge(truncated.Field71A?.Trim().ToUpper()),
                    ValueDate = truncated.ValueDate,
                    Status = 0,
                    FailedLevel = parsed.Validation.Errors.Max(e => e.Level),
                    ValidationErrors = JsonSerializer.Serialize(parsed.Validation.Errors),
                    ErrCode = parsed.Validation.Errors.First().Code,
                    ErrDesc = StringHelper.Truncate(parsed.Validation.Errors.First().Message, 2000),
                    ErrDuplicate = parsed.Validation.Errors.Any(e => e.Code.Contains("DUPLICATE")),
                    CreatedDate = DateTime.Now,
                    CreatedBy = "Job3"
                });
            }
            domesticTxtList.Add(domesticTxt);
        });

        if (JobPerfConfig.USE_BULK_COPY)
        {
            var conn = vcbContext.Database.GetConnectionString()!;
            await BulkHelper.InsertDomesticTxtAsync(conn, domesticTxtList.ToList(), ct);
            await BulkHelper.InsertTransactionsAsync(conn, transactionsList.ToList(), ct);
            await BulkHelper.InsertImportsAsync(conn, importsList.ToList(), ct);
        }
        else
        {
            if (domesticTxtList.Any()) await vcbContext.DomesticTxts.AddRangeAsync(domesticTxtList, ct);
            if (transactionsList.Any()) await vcbContext.Transactions.AddRangeAsync(transactionsList, ct);
            if (importsList.Any()) await vcbContext.TransactionImports.AddRangeAsync(importsList, ct);
            await vcbContext.SaveChangesAsync(ct);
        }
        return (transactionsList.Count, importsList.Count, newRefsDict.Keys.ToList());
    }

    private async Task CompleteFileAsync(VcbMoneyDbContext ctx, IFileService fs, FileUpload f, int total, int valid, int invalid, long ms, bool ok, CancellationToken ct)
    {
        f.Status = ok ? (int)FileStatus.Done : (int)FileStatus.PartialError;
        f.CompletedAt = DateTime.Now; f.ProcessingTimeMs = ms; f.TransactionCount = total; f.SuccessCount = valid; f.ErrorCount = invalid;
        string dateFolder = DateTime.Now.ToString("yyyyMMdd");
        await ctx.SaveChangesAsync();
        if (ok)
        {
          await  fs.MoveFileAsync(f.FilePhysical, Path.Combine(_settings.Files.FolderSuccess, dateFolder) , true, ct);
        }
        else

            await fs.MoveFileAsync(f.FilePhysical, Path.Combine(_settings.Files.FolderError, dateFolder), true, ct);    
        }         
    }


#endregion

#region Job4: Transform PayOrder

public class Job4_TransformPayOrderJob
{
    private readonly IServiceProvider _sp;
    private readonly VcbMoneySettings _settings;
    private readonly ILogger<Job4_TransformPayOrderJob> _logger;

    public Job4_TransformPayOrderJob(IServiceProvider sp, IOptions<VcbMoneySettings> settings, ILogger<Job4_TransformPayOrderJob> logger)
    { _sp = sp; _settings = settings.Value; _logger = logger; }

    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: JobPerfConfig.JOB_TIMEOUT)]
    [Queue("default")]
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[Job4] Starting Transform PayOrder");

        using var scope = _sp.CreateScope();
        var vcbContext = scope.ServiceProvider.GetRequiredService<VcbMoneyDbContext>();
        var trungGianContext = scope.ServiceProvider.GetRequiredService<TrungGianDbContext>();

        int totalProcessed = 0, totalValid = 0, totalInvalid = 0;

        while (!ct.IsCancellationRequested)
        {
            var payOrders = await trungGianContext.PayOrders.Where(p => p.UpvcbMoney == 1)
                .OrderBy(p => p.Ngay).Take(JobPerfConfig.BATCH_SIZE).ToListAsync(ct);
            if (!payOrders.Any()) break;

            var soGds = payOrders.Select(p => p.SoGd).ToList();
            var existingRefs = await GetExistingRefsAsync(vcbContext,  soGds, ct);
            var (valid, invalid) = await ProcessBatchAsync(vcbContext, trungGianContext, payOrders, existingRefs, ct);

            totalProcessed += payOrders.Count; totalValid += valid; totalInvalid += invalid;
            _logger.LogInformation("[Job4] Batch: {C}, Valid={V}, Invalid={I}", payOrders.Count, valid, invalid);
        }

        if (totalProcessed > 0)
            _logger.LogInformation("[Job4] DONE: Total={T}, Valid={V}, Invalid={I}, Time={Ms}ms", totalProcessed, totalValid, totalInvalid, sw.ElapsedMilliseconds);
    }

    private async Task<HashSet<string>> GetExistingRefsAsync(VcbMoneyDbContext vcb,  List<string> refs, CancellationToken ct)
    {
        // Filter out null/empty refs (Job4 version)
        var validRefs = refs.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct().ToList();
        if (!validRefs.Any()) return new HashSet<string>();

        // FIX: Execute queries SEQUENTIALLY to avoid DbContext concurrency issues
        // DbContext is NOT thread-safe - cannot run parallel queries on same instance
        var transRefs = await vcb.Transactions.AsNoTracking()
            .Where(t => t.TransRef != null && validRefs.Contains(t.TransRef))
            .Select(t => t.TransRef!)
            .ToListAsync(ct);
            
        var importRefs = await vcb.TransactionImports.AsNoTracking()
            .Where(t => t.TransRef != null && validRefs.Contains(t.TransRef))
            .Select(t => t.TransRef!)
            .ToListAsync(ct);

        return new HashSet<string>(
            transRefs.Concat(importRefs).Where(r => !string.IsNullOrWhiteSpace(r))
        );
    }

    private async Task<(int valid, int invalid)> ProcessBatchAsync(VcbMoneyDbContext vcbContext, TrungGianDbContext trungGianContext,
        List<PayOrder> payOrders, HashSet<string> existingRefs, CancellationToken ct)
    {
        var transList = new ConcurrentBag<Transaction>();
        var importsList = new ConcurrentBag<TransactionImport>();
        // FIX: Use ConcurrentDictionary for atomic check-and-add to prevent duplicate TransRef in same batch
        var processedRefsInBatch = new ConcurrentDictionary<string, byte>();

        Parallel.ForEach(payOrders, new ParallelOptions { MaxDegreeOfParallelism = JobPerfConfig.ParallelDegree, CancellationToken = ct }, po =>
        {
            var errors = new List<ValidationError>();
            
            // Check duplicate in DB
            if (existingRefs.Contains(po.SoGd)) 
                errors.Add(new ValidationError { Level = 5, Code = "DUPLICATE_DB", Message = "Duplicate in DB" });
            // FIX: Check duplicate within same batch (atomic operation)
            else if (!processedRefsInBatch.TryAdd(po.SoGd, 0))
                errors.Add(new ValidationError { Level = 5, Code = "DUPLICATE_BATCH", Message = "Duplicate in same batch" });
                
            if (po.SoTien <= 0) errors.Add(new ValidationError { Level = 5, Code = "INVALID_AMOUNT", Message = "Amount <= 0" });
            if (string.IsNullOrWhiteSpace(po.TkNh)) errors.Add(new ValidationError { Level = 2, Code = "MISSING_BEN_ACCOUNT", Message = "Missing" });
            if (string.IsNullOrWhiteSpace(po.TenNh)) errors.Add(new ValidationError { Level = 2, Code = "MISSING_BEN_NAME", Message = "Missing" });
            if (string.IsNullOrWhiteSpace(po.NhNh)) errors.Add(new ValidationError { Level = 2, Code = "MISSING_BEN_BANK", Message = "Missing" });

            if (!errors.Any())
            {
                transList.Add(new Transaction
                {
                    TransRef = po.SoGd,
                    SourceType = "PAYORDER",
                    SourceRef = po.SoGd,
                    DebitAccountNo = po.TkNo,
                    DebitAmount = (decimal)po.SoTien,
                    DebitCurrency = po.Lnt ?? "VND",
                    BenAccountNo = po.TkNh,
                    BenAccountName = StringHelper.Truncate(po.TenNh, 200),
                    BenBank = StringHelper.Truncate(po.NhNh, 150),
                    BenAddress = StringHelper.Truncate(po.DcNh, 200),
                    BenIdNo = po.BenIdNo,
                    BenIdNoPlace = po.BenIdIssuePlace,
                    OrderingCust = StringHelper.Truncate(po.TenNrl, 255),
                    OrderingCustAddress = StringHelper.Truncate(po.DcNrl, 100),
                    Content = StringHelper.Truncate(po.Nd, 255),
                    Charge = StringHelper.Truncate(po.Phi, 3),
                    Priority = po.UuTien == 2 ? 1 : 0,
                    ValueDate = po.Ngay,
                    TransferType = po.BankToBank == true ? 1 : 0,
                    CheckBenName = po.InquiryBenName ?? false,
                    Status = 0,
                    CreatedDate = DateTime.Now,
                    CreatedBy = "Job4"
                });
                po.UpvcbMoney = 2;
            }
            else
            {
                importsList.Add(new TransactionImport
                {
                    SourceType = "PAYORDER",
                    SourceRef = po.SoGd,
                    TransRef = po.SoGd,
                    DebitAccountNo = po.TkNo,
                    DebitAmount = po.SoTien.ToString("F2"),
                    DebitCurrency = po.Lnt ?? "VND",
                    BenAccountNo = po.TkNh,
                    BenAccountName = StringHelper.Truncate(po.TenNh, 200),
                    BenBank = StringHelper.Truncate(po.NhNh, 150),
                    Content = StringHelper.Truncate(po.Nd, 255),
                    Charge = StringHelper.Truncate(po.Phi, 10),
                    ValueDate = po.Ngay,
                    Status = 0,
                    FailedLevel = errors.Max(e => e.Level),
                    ValidationErrors = JsonSerializer.Serialize(errors),
                    ErrCode = errors.First().Code,
                    ErrDesc = StringHelper.Truncate(errors.First().Message, 2000),
                    ErrDuplicate = errors.Any(e => e.Code.Contains("DUPLICATE")),
                    CreatedDate = DateTime.Now,
                    CreatedBy = "Job4"
                });
                po.UpvcbMoney = -1; po.ErrCode = errors.First().Code; po.ErrDes = errors.First().Message;
            }
            po.UpDatetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        });

        if (JobPerfConfig.USE_BULK_COPY)
        {
            var conn = vcbContext.Database.GetConnectionString()!;
            await BulkHelper.InsertTransactionsAsync(conn, transList.ToList(), ct);
            await BulkHelper.InsertImportsAsync(conn, importsList.ToList(), ct);
        }
        else
        {
            if (transList.Any()) await vcbContext.Transactions.AddRangeAsync(transList, ct);
            if (importsList.Any()) await vcbContext.TransactionImports.AddRangeAsync(importsList, ct);
            await vcbContext.SaveChangesAsync(ct);
        }
        await trungGianContext.SaveChangesAsync(ct);
        return (transList.Count, importsList.Count);
    }
}

#endregion

#region Helpers

public static class StringHelper
{
    public static string? ExtractAccount(string? f) { if (string.IsNullOrEmpty(f)) return null; foreach (var l in f.Split('\n', '\r')) { var t = l.Trim(); if (t.StartsWith("/")) return t[1..]; if (Regex.IsMatch(t, @"^\d+$")) return t; } return null; }
    public static string? ExtractBenName(string? f) { if (string.IsNullOrEmpty(f)) return null; var l = f.Split('\n', '\r').Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(); return l.Length > 1 ? l[1].Trim() : l.FirstOrDefault()?.Trim(); }
    public static string? Truncate(string? v, int m) => string.IsNullOrEmpty(v) ? v : v.Length > m ? v[..m] : v;
    public static string? ExtractAndMapCharge(string? f)
    {
        if (string.IsNullOrEmpty(f)) return null;

        foreach (var l in f.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = l.Trim();
            string rawCharge = t.StartsWith("/") ? t[1..] : t;     
            return rawCharge switch
            {
                "BEN" or "SHA" => "INC",
                "OUR" => "EXC",
                _ => rawCharge // "else" giữ nguyên giá trị gốc
            };
        }

        return null;
    }
}

public static class BulkHelper
{
    /// <summary>
    /// Insert all 3 tables in ONE transaction - ALL or NOTHING
    /// </summary>
    public static async Task InsertBatchWithTransactionAsync(string conn,
        List<DomesticTxt> domesticTxts, List<Transaction> transactions, List<TransactionImport> imports, CancellationToken ct)
    {
        using var connection = new SqlConnection(conn);
        await connection.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        try
        {
            if (domesticTxts.Any())
                await BulkCopyWithTransactionAsync(connection, transaction, "DOMESTIC_TXT", BuildDomesticTxtTable(domesticTxts), ct);

            if (transactions.Any())
                await BulkCopyWithTransactionAsync(connection, transaction, "[TRANSACTION]", BuildTransactionsTable(transactions), ct);

            if (imports.Any())
                await BulkCopyWithTransactionAsync(connection, transaction, "[TRANSACTION_IMPORT]", BuildImportsTable(imports), ct);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task BulkCopyWithTransactionAsync(SqlConnection conn, SqlTransaction trans, string table, DataTable dt, CancellationToken ct)
    {
        using var bc = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock, trans)
        {
            DestinationTableName = table,
            BatchSize = JobPerfConfig.BULK_COPY_BATCH,
            BulkCopyTimeout = 300
        };
        foreach (DataColumn col in dt.Columns) bc.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        await bc.WriteToServerAsync(dt, ct);
    }

    private static DataTable BuildDomesticTxtTable(List<DomesticTxt> items)
    {
        var dt = new DataTable();
        dt.Columns.Add("FILE_UPLOAD_ID", typeof(string)); dt.Columns.Add("FIELD_20", typeof(string)); dt.Columns.Add("FIELD_32A", typeof(string));
        dt.Columns.Add("FIELD_50K", typeof(string)); dt.Columns.Add("FIELD_50R", typeof(string)); dt.Columns.Add("FIELD_50S", typeof(string));
        dt.Columns.Add("FIELD_50T", typeof(string)); dt.Columns.Add("FIELD_53", typeof(string)); dt.Columns.Add("FIELD_57", typeof(string));
        dt.Columns.Add("FIELD_57A", typeof(string)); dt.Columns.Add("FIELD_59", typeof(string)); dt.Columns.Add("FIELD_59O", typeof(string));
        dt.Columns.Add("FIELD_59P", typeof(string)); dt.Columns.Add("FIELD_59Q", typeof(string)); dt.Columns.Add("FIELD_70", typeof(string));
        dt.Columns.Add("FIELD_71A", typeof(string)); dt.Columns.Add("FIELD_72A", typeof(string)); dt.Columns.Add("FIELD_72B", typeof(string));
        dt.Columns.Add("FIELD_72C", typeof(string)); dt.Columns.Add("FIELD_72D", typeof(string)); dt.Columns.Add("VALUE_DATE", typeof(DateTime));
        dt.Columns.Add("CURRENCY", typeof(string)); dt.Columns.Add("AMOUNT", typeof(decimal)); dt.Columns.Add("DEBIT_ACCOUNT", typeof(string));
        dt.Columns.Add("BEN_ACCOUNT", typeof(string)); dt.Columns.Add("BEN_NAME", typeof(string)); dt.Columns.Add("STATUS", typeof(int));
        dt.Columns.Add("ERR_CODE", typeof(string)); dt.Columns.Add("ERR_DESC", typeof(string));
        dt.Columns.Add("CREATED_DATE", typeof(DateTime)); dt.Columns.Add("CREATED_BY", typeof(string));
        foreach (var i in items) dt.Rows.Add(i.FileUploadId ?? (object)DBNull.Value, i.Field20 ?? (object)DBNull.Value, i.Field32A ?? (object)DBNull.Value,
            i.Field50K ?? (object)DBNull.Value, i.Field50R ?? (object)DBNull.Value, i.Field50S ?? (object)DBNull.Value,
            i.Field50T ?? (object)DBNull.Value, i.Field53 ?? (object)DBNull.Value, i.Field57 ?? (object)DBNull.Value,
            i.Field57A ?? (object)DBNull.Value, i.Field59 ?? (object)DBNull.Value, i.Field59O ?? (object)DBNull.Value,
            i.Field59P ?? (object)DBNull.Value, i.Field59Q ?? (object)DBNull.Value, i.Field70 ?? (object)DBNull.Value,
            i.Field71A ?? (object)DBNull.Value, i.Field72A ?? (object)DBNull.Value, i.Field72B ?? (object)DBNull.Value,
            i.Field72C ?? (object)DBNull.Value, i.Field72D ?? (object)DBNull.Value, i.ValueDate ?? (object)DBNull.Value,
            i.Currency ?? (object)DBNull.Value, i.Amount ?? (object)DBNull.Value, i.DebitAccount ?? (object)DBNull.Value,
            i.BenAccount ?? (object)DBNull.Value, i.BenName ?? (object)DBNull.Value, i.Status,
            i.ErrCode ?? (object)DBNull.Value, i.ErrDesc ?? (object)DBNull.Value, i.CreatedDate, i.CreatedBy ?? "Job3");
        return dt;
    }

    private static DataTable BuildTransactionsTable(List<Transaction> items)
    {
        var dt = new DataTable();
        dt.Columns.Add("TRANS_REF", typeof(string)); // Primary Key
        dt.Columns.Add("SOURCE_TYPE", typeof(string)); dt.Columns.Add("SOURCE_REF", typeof(string)); dt.Columns.Add("FILE_UPLOAD_ID", typeof(string));
        dt.Columns.Add("DEBIT_ACCOUNT_NO", typeof(string)); dt.Columns.Add("DEBIT_AMOUNT", typeof(decimal));
        dt.Columns.Add("DEBIT_CURRENCY", typeof(string)); dt.Columns.Add("BEN_ACCOUNT_NO", typeof(string)); dt.Columns.Add("BEN_ACCOUNT_NAME", typeof(string));
        dt.Columns.Add("BEN_BANK", typeof(string)); dt.Columns.Add("BEN_ADDRESS", typeof(string)); dt.Columns.Add("BEN_CURRENCY", typeof(string));
        dt.Columns.Add("BEN_IDNO", typeof(string)); dt.Columns.Add("BEN_IDNO_PLACE", typeof(string));
        dt.Columns.Add("CONTENT", typeof(string)); dt.Columns.Add("CHARGE", typeof(string)); dt.Columns.Add("PRIORITY", typeof(int));
        dt.Columns.Add("VALUE_DATE", typeof(DateTime)); dt.Columns.Add("TRANSFER_TYPE", typeof(int));
        dt.Columns.Add("CHECK_BEN_NAME", typeof(bool)); dt.Columns.Add("ORDERING_CUST", typeof(string)); dt.Columns.Add("ORDERING_CUST_ADDRESS", typeof(string));
        dt.Columns.Add("STATUS", typeof(int)); dt.Columns.Add("CREATED_DATE", typeof(DateTime)); dt.Columns.Add("CREATED_BY", typeof(string));
        foreach (var i in items) dt.Rows.Add(i.TransRef ?? Guid.NewGuid().ToString("N").Substring(0, 21),
            i.SourceType ?? "FILE", i.SourceRef ?? (object)DBNull.Value, i.FileUploadId ?? (object)DBNull.Value,
            i.DebitAccountNo ?? (object)DBNull.Value, i.DebitAmount ?? (object)DBNull.Value,
            i.DebitCurrency ?? "VND", i.BenAccountNo ?? (object)DBNull.Value, i.BenAccountName ?? (object)DBNull.Value,
            i.BenBank ?? (object)DBNull.Value, i.BenAddress ?? (object)DBNull.Value, i.BenCurrency ?? "VND",
            i.BenIdNo ?? (object)DBNull.Value, i.BenIdNoPlace ?? (object)DBNull.Value,
            i.Content ?? (object)DBNull.Value, i.Charge ?? (object)DBNull.Value, i.Priority ?? 0,
            i.ValueDate ?? (object)DBNull.Value, i.TransferType ?? 0, i.CheckBenName,
            i.OrderingCust ?? (object)DBNull.Value, i.OrderingCustAddress ?? (object)DBNull.Value,
            i.Status, i.CreatedDate ?? DateTime.Now, i.CreatedBy ?? "Job");
        return dt;
    }

    private static DataTable BuildImportsTable(List<TransactionImport> items)
    {
        var dt = new DataTable();
        dt.Columns.Add("SOURCE_TYPE", typeof(string)); dt.Columns.Add("SOURCE_REF", typeof(string)); dt.Columns.Add("FILE_UPLOAD_ID", typeof(string));
        dt.Columns.Add("TRANS_REF", typeof(string)); dt.Columns.Add("DEBIT_ACCOUNT_NO", typeof(string)); dt.Columns.Add("DEBIT_AMOUNT_RAW", typeof(string));
        dt.Columns.Add("DEBIT_AMOUNT", typeof(string)); dt.Columns.Add("DEBIT_CURRENCY", typeof(string)); dt.Columns.Add("BEN_ACCOUNT_NO", typeof(string));
        dt.Columns.Add("BEN_ACCOUNT_NAME_RAW", typeof(string)); dt.Columns.Add("BEN_ACCOUNT_NAME", typeof(string)); dt.Columns.Add("BEN_BANK_RAW", typeof(string));
        dt.Columns.Add("BEN_BANK", typeof(string)); dt.Columns.Add("CONTENT_RAW", typeof(string)); dt.Columns.Add("CONTENT", typeof(string));
        dt.Columns.Add("CHARGE", typeof(string)); dt.Columns.Add("VALUE_DATE", typeof(DateTime)); dt.Columns.Add("STATUS", typeof(int));
        dt.Columns.Add("FAILED_LEVEL", typeof(int)); dt.Columns.Add("VALIDATION_ERRORS", typeof(string)); dt.Columns.Add("ERR_CODE", typeof(string));
        dt.Columns.Add("ERR_DESC", typeof(string)); dt.Columns.Add("ERR_DUPLICATE", typeof(bool));
        dt.Columns.Add("CREATED_DATE", typeof(DateTime)); dt.Columns.Add("CREATED_BY", typeof(string));
        foreach (var i in items) dt.Rows.Add(i.SourceType ?? "FILE", i.SourceRef ?? (object)DBNull.Value, i.FileUploadId ?? (object)DBNull.Value,
            i.TransRef ?? (object)DBNull.Value, i.DebitAccountNo ?? (object)DBNull.Value, i.DebitAmountRaw ?? (object)DBNull.Value,
            i.DebitAmount ?? (object)DBNull.Value, i.DebitCurrency ?? "VND", i.BenAccountNo ?? (object)DBNull.Value,
            i.BenAccountNameRaw ?? (object)DBNull.Value, i.BenAccountName ?? (object)DBNull.Value, i.BenBankRaw ?? (object)DBNull.Value,
            i.BenBank ?? (object)DBNull.Value, i.ContentRaw ?? (object)DBNull.Value, i.Content ?? (object)DBNull.Value,
            i.Charge ?? (object)DBNull.Value, i.ValueDate ?? (object)DBNull.Value, i.Status, i.FailedLevel ?? 0,
            i.ValidationErrors ?? (object)DBNull.Value, i.ErrCode ?? (object)DBNull.Value, i.ErrDesc ?? (object)DBNull.Value,
            i.ErrDuplicate, i.CreatedDate, i.CreatedBy ?? "Job");
        return dt;
    }

    // Legacy methods for backward compatibility
    public static async Task InsertDomesticTxtAsync(string conn, List<DomesticTxt> items, CancellationToken ct)
    {
        if (!items.Any()) return;
        var dt = new DataTable();
        dt.Columns.Add("FILE_UPLOAD_ID", typeof(string)); dt.Columns.Add("FIELD_20", typeof(string)); dt.Columns.Add("FIELD_32A", typeof(string));
        dt.Columns.Add("FIELD_50K", typeof(string)); dt.Columns.Add("FIELD_50R", typeof(string)); dt.Columns.Add("FIELD_50S", typeof(string));
        dt.Columns.Add("FIELD_50T", typeof(string)); dt.Columns.Add("FIELD_53", typeof(string)); dt.Columns.Add("FIELD_57", typeof(string));
        dt.Columns.Add("FIELD_57A", typeof(string)); dt.Columns.Add("FIELD_59", typeof(string)); dt.Columns.Add("FIELD_59O", typeof(string));
        dt.Columns.Add("FIELD_59P", typeof(string)); dt.Columns.Add("FIELD_59Q", typeof(string)); dt.Columns.Add("FIELD_70", typeof(string));
        dt.Columns.Add("FIELD_71A", typeof(string)); dt.Columns.Add("FIELD_72A", typeof(string)); dt.Columns.Add("FIELD_72B", typeof(string));
        dt.Columns.Add("FIELD_72C", typeof(string)); dt.Columns.Add("FIELD_72D", typeof(string)); dt.Columns.Add("VALUE_DATE", typeof(DateTime));
        dt.Columns.Add("CURRENCY", typeof(string)); dt.Columns.Add("AMOUNT", typeof(decimal)); dt.Columns.Add("DEBIT_ACCOUNT", typeof(string));
        dt.Columns.Add("BEN_ACCOUNT", typeof(string)); dt.Columns.Add("BEN_NAME", typeof(string)); dt.Columns.Add("STATUS", typeof(int));
        dt.Columns.Add("ERR_CODE", typeof(string)); dt.Columns.Add("ERR_DESC", typeof(string));
        dt.Columns.Add("CREATED_DATE", typeof(DateTime)); dt.Columns.Add("CREATED_BY", typeof(string));
        foreach (var i in items) dt.Rows.Add(i.FileUploadId ?? (object)DBNull.Value, i.Field20 ?? (object)DBNull.Value, i.Field32A ?? (object)DBNull.Value,
            i.Field50K ?? (object)DBNull.Value, i.Field50R ?? (object)DBNull.Value, i.Field50S ?? (object)DBNull.Value,
            i.Field50T ?? (object)DBNull.Value, i.Field53 ?? (object)DBNull.Value, i.Field57 ?? (object)DBNull.Value,
            i.Field57A ?? (object)DBNull.Value, i.Field59 ?? (object)DBNull.Value, i.Field59O ?? (object)DBNull.Value,
            i.Field59P ?? (object)DBNull.Value, i.Field59Q ?? (object)DBNull.Value, i.Field70 ?? (object)DBNull.Value,
            i.Field71A ?? (object)DBNull.Value, i.Field72A ?? (object)DBNull.Value, i.Field72B ?? (object)DBNull.Value,
            i.Field72C ?? (object)DBNull.Value, i.Field72D ?? (object)DBNull.Value, i.ValueDate ?? (object)DBNull.Value,
            i.Currency ?? (object)DBNull.Value, i.Amount ?? (object)DBNull.Value, i.DebitAccount ?? (object)DBNull.Value,
            i.BenAccount ?? (object)DBNull.Value, i.BenName ?? (object)DBNull.Value, i.Status,
            i.ErrCode ?? (object)DBNull.Value, i.ErrDesc ?? (object)DBNull.Value, DateTime.Now, i.CreatedBy ?? "Job3");
        await BulkCopyAsync(conn, "DOMESTIC_TXT", dt, ct);
    }

    public static async Task InsertTransactionsAsync(string conn, List<Transaction> items, CancellationToken ct)
    {
        if (!items.Any()) return;
        var dt = new DataTable();
        dt.Columns.Add("TRANS_REF", typeof(string)); // Primary Key first
        dt.Columns.Add("SOURCE_TYPE", typeof(string)); dt.Columns.Add("SOURCE_REF", typeof(string)); dt.Columns.Add("FILE_UPLOAD_ID", typeof(string));
        dt.Columns.Add("DEBIT_ACCOUNT_NO", typeof(string)); dt.Columns.Add("DEBIT_AMOUNT", typeof(decimal));
        dt.Columns.Add("DEBIT_CURRENCY", typeof(string)); dt.Columns.Add("BEN_ACCOUNT_NO", typeof(string)); dt.Columns.Add("BEN_ACCOUNT_NAME", typeof(string));
        dt.Columns.Add("BEN_BANK", typeof(string)); dt.Columns.Add("BEN_ADDRESS", typeof(string)); dt.Columns.Add("BEN_CURRENCY", typeof(string));
        dt.Columns.Add("BEN_IDNO", typeof(string)); dt.Columns.Add("BEN_IDNO_PLACE", typeof(string));
        dt.Columns.Add("CONTENT", typeof(string)); dt.Columns.Add("CHARGE", typeof(string)); dt.Columns.Add("PRIORITY", typeof(int));
        dt.Columns.Add("VALUE_DATE", typeof(DateTime)); dt.Columns.Add("TRANSFER_TYPE", typeof(int));
        dt.Columns.Add("CHECK_BEN_NAME", typeof(bool)); dt.Columns.Add("ORDERING_CUST", typeof(string)); dt.Columns.Add("ORDERING_CUST_ADDRESS", typeof(string));
        dt.Columns.Add("STATUS", typeof(int)); dt.Columns.Add("CREATED_DATE", typeof(DateTime)); dt.Columns.Add("CREATED_BY", typeof(string));
        foreach (var i in items) dt.Rows.Add(i.TransRef ?? Guid.NewGuid().ToString("N").Substring(0, 21),
            i.SourceType ?? "FILE", i.SourceRef ?? (object)DBNull.Value, i.FileUploadId ?? (object)DBNull.Value,
            i.DebitAccountNo ?? (object)DBNull.Value, i.DebitAmount ?? (object)DBNull.Value,
            i.DebitCurrency ?? "VND", i.BenAccountNo ?? (object)DBNull.Value, i.BenAccountName ?? (object)DBNull.Value,
            i.BenBank ?? (object)DBNull.Value, i.BenAddress ?? (object)DBNull.Value, i.BenCurrency ?? "VND",
            i.BenIdNo ?? (object)DBNull.Value, i.BenIdNoPlace ?? (object)DBNull.Value,
            i.Content ?? (object)DBNull.Value, i.Charge ?? (object)DBNull.Value, i.Priority ?? 0,
            i.ValueDate ?? (object)DBNull.Value, i.TransferType ?? 0, i.CheckBenName,
            i.OrderingCust ?? (object)DBNull.Value, i.OrderingCustAddress ?? (object)DBNull.Value,
            i.Status, i.CreatedDate ?? DateTime.Now, i.CreatedBy ?? "Job");
        await BulkCopyAsync(conn, "[TRANSACTION]", dt, ct);  // FIXED: Table name
    }

    public static async Task InsertImportsAsync(string conn, List<TransactionImport> items, CancellationToken ct)
    {
        if (!items.Any()) return;
        var dt = new DataTable();
        dt.Columns.Add("SOURCE_TYPE", typeof(string)); dt.Columns.Add("SOURCE_REF", typeof(string)); dt.Columns.Add("FILE_UPLOAD_ID", typeof(string));
        dt.Columns.Add("TRANS_REF", typeof(string)); dt.Columns.Add("DEBIT_ACCOUNT_NO", typeof(string)); dt.Columns.Add("DEBIT_AMOUNT_RAW", typeof(string));
        dt.Columns.Add("DEBIT_AMOUNT", typeof(string)); dt.Columns.Add("DEBIT_CURRENCY", typeof(string)); dt.Columns.Add("BEN_ACCOUNT_NO", typeof(string));
        dt.Columns.Add("BEN_ACCOUNT_NAME_RAW", typeof(string)); dt.Columns.Add("BEN_ACCOUNT_NAME", typeof(string)); dt.Columns.Add("BEN_BANK_RAW", typeof(string));
        dt.Columns.Add("BEN_BANK", typeof(string)); dt.Columns.Add("CONTENT_RAW", typeof(string)); dt.Columns.Add("CONTENT", typeof(string));
        dt.Columns.Add("CHARGE", typeof(string)); dt.Columns.Add("VALUE_DATE", typeof(DateTime)); dt.Columns.Add("STATUS", typeof(int));
        dt.Columns.Add("FAILED_LEVEL", typeof(int)); dt.Columns.Add("VALIDATION_ERRORS", typeof(string)); dt.Columns.Add("ERR_CODE", typeof(string));
        dt.Columns.Add("ERR_DESC", typeof(string)); dt.Columns.Add("ERR_DUPLICATE", typeof(bool));
        dt.Columns.Add("CREATED_DATE", typeof(DateTime)); dt.Columns.Add("CREATED_BY", typeof(string));
        foreach (var i in items) dt.Rows.Add(i.SourceType ?? "FILE", i.SourceRef ?? (object)DBNull.Value, i.FileUploadId ?? (object)DBNull.Value,
            i.TransRef ?? (object)DBNull.Value, i.DebitAccountNo ?? (object)DBNull.Value, i.DebitAmountRaw ?? (object)DBNull.Value,
            i.DebitAmount ?? (object)DBNull.Value, i.DebitCurrency ?? "VND", i.BenAccountNo ?? (object)DBNull.Value,
            i.BenAccountNameRaw ?? (object)DBNull.Value, i.BenAccountName ?? (object)DBNull.Value, i.BenBankRaw ?? (object)DBNull.Value,
            i.BenBank ?? (object)DBNull.Value, i.ContentRaw ?? (object)DBNull.Value, i.Content ?? (object)DBNull.Value,
            i.Charge ?? (object)DBNull.Value, i.ValueDate ?? (object)DBNull.Value, i.Status, i.FailedLevel ?? 0,
            i.ValidationErrors ?? (object)DBNull.Value, i.ErrCode ?? (object)DBNull.Value, i.ErrDesc ?? (object)DBNull.Value,
            i.ErrDuplicate, i.CreatedDate, i.CreatedBy ?? "Job");
        await BulkCopyAsync(conn, "[TRANSACTION_IMPORT]", dt, ct);  // FIXED: Table name
    }

    private static async Task BulkCopyAsync(string conn, string table, DataTable dt, CancellationToken ct)
    {
        using var c = new SqlConnection(conn);
        await c.OpenAsync(ct);
        using var bc = new SqlBulkCopy(c, SqlBulkCopyOptions.TableLock, null) { DestinationTableName = table, BatchSize = JobPerfConfig.BULK_COPY_BATCH, BulkCopyTimeout = 600 };
        foreach (DataColumn col in dt.Columns) bc.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        await bc.WriteToServerAsync(dt, ct);
    }
}

#endregion
