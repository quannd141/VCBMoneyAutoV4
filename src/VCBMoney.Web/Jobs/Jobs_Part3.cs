using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VCBMoney.Core.Configuration;
using VCBMoney.Core.Entities;
using VCBMoney.Core.Enums;
using VCBMoney.Infrastructure.Data;

namespace VCBMoney.Web.Jobs;

#region Job5: Sync TRANSACTIONS to DOMESTIC_RESULT

/// <summary>
/// Job 5: Sync TRANSACTIONS to DOMESTIC_RESULT
/// 
/// SYNC LOGIC:
/// - Đọc TRANSACTIONS có SyncStatus = 0 (cần sync)
/// - Nếu TransRef CHƯA tồn tại trong DOMESTIC_RESULT → INSERT mới
/// - Nếu TransRef ĐÃ tồn tại trong DOMESTIC_RESULT → UPDATE data
/// - Sau khi sync xong → Set SyncStatus = 1
/// 
/// RE-SYNC FLOW:
/// - Khi Transaction thay đổi status (201→202→203...) hoặc sửa data
/// - User/System set SyncStatus = 0
/// - Job5 sẽ tự động UPDATE data mới nhất vào DOMESTIC_RESULT
/// 
/// STATUS MAPPING:
/// - Transaction.Status = 201, 202, 203... (trạng thái nghiệp vụ)
/// - Transaction.SyncStatus = 0 (cần sync), 1 (đã sync)
/// </summary>
public class Job5_SyncResultJob
{
    private readonly IServiceProvider _sp;
    private readonly VcbMoneySettings _settings;
    private readonly ILogger<Job5_SyncResultJob> _logger;
    private const int BATCH_SIZE = 1000;

    public Job5_SyncResultJob(IServiceProvider sp, IOptions<VcbMoneySettings> settings, ILogger<Job5_SyncResultJob> logger)
    {
        _sp = sp;
        _settings = settings.Value;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    [Queue("sync")]
    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Job5] Syncing to DOMESTIC_RESULT (SyncStatus=0)...");

        int totalInserted = 0, totalUpdated = 0, totalError = 0;
        int batchNumber = 0;

        while (!ct.IsCancellationRequested)
        {
            using var scope = _sp.CreateScope();
            var vcbContext = scope.ServiceProvider.GetRequiredService<VcbMoneyDbContext>();
            var trungGianContext = scope.ServiceProvider.GetRequiredService<TrungGianDbContext>();

            // Lấy transactions cần sync (SyncStatus = 0 hoặc NULL)
            var transactions = await vcbContext.Transactions
                .Where(t => t.SyncStatus == null || t.SyncStatus == 0)
                .OrderBy(t => t.CreatedDate)
                .Take(BATCH_SIZE)
                .ToListAsync(ct);

            if (!transactions.Any()) break;

            batchNumber++;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            using var tgTrans = await trungGianContext.Database.BeginTransactionAsync(ct);
            try
            {
                var (inserted, updated, error) = await ProcessSyncBatchAsync(
                    vcbContext, trungGianContext, transactions, ct);

                totalInserted += inserted;
                totalUpdated += updated;
                totalError += error;

                await tgTrans.CommitAsync(ct);
                await vcbContext.SaveChangesAsync(ct);

                _logger.LogInformation("[Job5] Batch {Batch}: Insert={I}, Update={U}, Err={E}, Time={Ms}ms",
                    batchNumber, inserted, updated, error, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                await tgTrans.RollbackAsync(ct);
                _logger.LogError(ex, "[Job5] Batch {Batch} failed, rolling back", batchNumber);
                throw;
            }
        }

        if (totalInserted > 0 || totalUpdated > 0)
            _logger.LogInformation("[Job5] DONE: Inserted={I}, Updated={U}, Errors={E}",
                totalInserted, totalUpdated, totalError);
    }

    private async Task<(int inserted, int updated, int error)> ProcessSyncBatchAsync(
        VcbMoneyDbContext vcbContext,
        TrungGianDbContext trungGianContext,
        List<Transaction> transactions,
        CancellationToken ct)
    {
        int inserted = 0, updated = 0, error = 0;

        // Lấy danh sách TransRef để check tồn tại trong DOMESTIC_RESULT
        var transRefs = transactions
            .Where(t => !string.IsNullOrEmpty(t.TransRef))
            .Select(t => t.TransRef!)
            .Distinct()
            .ToList();

        // Load existing records để phân biệt INSERT vs UPDATE
        var existingResults = await trungGianContext.DomesticResults
            .Where(r => r.TransRef != null && transRefs.Contains(r.TransRef))
            .ToDictionaryAsync(r => r.TransRef!, ct);

        var newResults = new List<DomesticResult>();

        foreach (var trans in transactions)
        {
            try
            {
                if (string.IsNullOrEmpty(trans.TransRef))
                {
                    _logger.LogWarning("[Job5] Skip transaction with empty TransRef");
                    error++;
                    continue;
                }

                // Check nếu đã tồn tại → UPDATE, chưa tồn tại → INSERT
                if (existingResults.TryGetValue(trans.TransRef, out var existingResult))
                {
                    // UPDATE existing record
                    UpdateDomesticResult(existingResult, trans);
                    existingResult.UpdatedDate = DateTime.Now;
                    existingResult.UpdatedBy = "Job5_ReSync";
                    updated++;
                }
                else
                {
                    // INSERT new record
                    var newResult = CreateDomesticResult(trans);
                    newResults.Add(newResult);
                    inserted++;
                }

                // Mark as synced
                trans.SyncStatus = 1;
                trans.SyncDate = DateTime.Now;
                trans.UpdatedDate = DateTime.Now;
                trans.UpdatedBy = "Job5";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Job5] Error processing: {TransRef}", trans.TransRef);
                error++;
            }
        }

        // Bulk insert new records
        if (newResults.Any())
            await trungGianContext.DomesticResults.AddRangeAsync(newResults, ct);

        // Save changes (includes both INSERT and UPDATE)
        await trungGianContext.SaveChangesAsync(ct);

        return (inserted, updated, error);
    }

    /// <summary>
    /// Create new DomesticResult from Transaction
    /// </summary>
    private DomesticResult CreateDomesticResult(Transaction trans)
    {
        return new DomesticResult
        {
            CustomerCode = _settings.Business.CustomerCode,
            TransRef = trans.TransRef,
            DebitAccount = trans.DebitAccountNo,
            DebitName = trans.OrderingCust,
            Amount = trans.DebitAmount,
            Currency = trans.DebitCurrency,
            Content = trans.Content,
            BenAccountNo = trans.BenAccountNo ?? string.Empty,
            BenAccountName = trans.BenAccountName,
            BenBank = trans.BenBank ?? string.Empty,
            BenBankCode = ExtractBankCode(trans.BenBank),
            BenCurrency = trans.BenCurrency ?? "VND",
            BenAddress = trans.BenAddress,
            BenIdNo = trans.BenIdNo,
            Charge = trans.Charge,
            ValueDate = trans.ValueDate ?? DateTime.Today,
            Priority = trans.Priority?.ToString(),
            Status = trans.Status.ToString(),  // Lưu status hiện tại (201, 202, 203...)
            BankTransRef = trans.BankTransRef,
            Remark = trans.Remark,
            ImportedTime = DateTime.Now,
            CreatedDate = DateTime.Now,
            CreatedBy = "Job5",
            UpdatedDate = DateTime.Now,
            UpdatedBy = "Job5"
        };
    }

    /// <summary>
    /// Update existing DomesticResult with new data from Transaction
    /// </summary>
    private void UpdateDomesticResult(DomesticResult result, Transaction trans)
    {
        // Update các fields có thể thay đổi
        result.DebitAccount = trans.DebitAccountNo;
        result.DebitName = trans.OrderingCust;
        result.Amount = trans.DebitAmount;
        result.Currency = trans.DebitCurrency;
        result.Content = trans.Content;
        result.BenAccountNo = trans.BenAccountNo ?? string.Empty;
        result.BenAccountName = trans.BenAccountName;
        result.BenBank = trans.BenBank ?? string.Empty;
        result.BenBankCode = ExtractBankCode(trans.BenBank);
        result.BenCurrency = trans.BenCurrency ?? "VND";
        result.BenAddress = trans.BenAddress;
        result.BenIdNo = trans.BenIdNo;
        result.Charge = trans.Charge;
        result.ValueDate = trans.ValueDate ?? DateTime.Today;
        result.Priority = trans.Priority?.ToString();
        result.Status = trans.Status.ToString();  // Update status mới (201→202→203...)
        
        // Thêm các fields từ VCB response nếu có
        result.BankTransRef = trans.BankTransRef;
        result.Remark = trans.Remark;
        result.UpdatedDate = DateTime.Now;
        result.UpdatedBy = "Job5";
    }

    private static string? ExtractBankCode(string? benBank)
    {
        if (string.IsNullOrEmpty(benBank)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(benBank, @"\(([^)]+)\)");
        if (match.Success) return match.Groups[1].Value;
        var parts = benBank.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }
}

#endregion

#region Job6: Cleanup

public class Job6_CleanupJob
{
    private readonly VcbMoneySettings _settings;
    private readonly ILogger<Job6_CleanupJob> _logger;

    public Job6_CleanupJob(IOptions<VcbMoneySettings> settings, ILogger<Job6_CleanupJob> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    [Queue("low")]
    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Job6] Cleanup starting...");
        var retentionDate = DateTime.Now.AddDays(-_settings.Business.FileRetentionDays);

        var archiveDeleted = await CleanFolderAsync(_settings.Files.FolderSuccess, retentionDate, ct);
        var errorDeleted = await CleanFolderAsync(_settings.Files.FolderError, retentionDate, ct);

        _logger.LogInformation("[Job6] Done: Archive={A}, Error={E} files deleted", archiveDeleted, errorDeleted);
    }

    private async Task<int> CleanFolderAsync(string folder, DateTime olderThan, CancellationToken ct)
    {
        if (!Directory.Exists(folder)) return 0;

        int deleted = 0;
        await Task.Run(() =>
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var fi = new FileInfo(file);
                    if (fi.CreationTime < olderThan)
                    {
                        File.Delete(file);
                        deleted++;
                    }
                }
                catch { }
            }
        }, ct);

        return deleted;
    }
}

#endregion
