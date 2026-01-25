using System.Diagnostics;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VCBMoney.Core.Configuration;
using VCBMoney.Core.Entities;
using VCBMoney.Core.Enums;
using VCBMoney.Core.Interfaces;
using VCBMoney.Infrastructure.Data;

namespace VCBMoney.Web.Jobs;

#region Job1: Scan Files + L1/L2 Validation

/// <summary>
/// Job 1: Scan files và validate L1+L2
/// Flow: FolderInput → FolderProcessing (lock + L1+L2 validate)
/// - L1+L2 PASS: File stays in FolderProcessing, Status=Valid (ready for Job3)
/// - L1+L2 FAIL: Move to FolderError
/// Note: RawContent NOT saved to DB - Job3 will read from FolderProcessing
/// </summary>
public class Job1_ScanFilesJob
{
    private readonly IServiceProvider _sp;
    private readonly VcbMoneySettings _settings;
    private readonly ILogger<Job1_ScanFilesJob> _logger;

    public Job1_ScanFilesJob(IServiceProvider sp, IOptions<VcbMoneySettings> settings, ILogger<Job1_ScanFilesJob> logger)
    {
        _sp = sp;
        _settings = settings.Value;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    [Queue("default")]
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var inputFolder = _settings.Files.FolderInput;
        var processingFolder = _settings.Files.FolderProcessing;
        var errorFolder = _settings.Files.FolderError;
        var SuccessFolder = _settings.Files.FolderSuccess;
        string dateFolder = DateTime.Now.ToString("yyyyMMdd");

        _logger.LogInformation("[Job1] Scanning: {Folder}", inputFolder);

        // Ensure folders exist
       

        // Get files from Input folder
        var files = Directory.GetFiles(inputFolder)
            .Where(f => _settings.Files.AllowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => new FileInfo(f).CreationTime)
            .ToArray();

        if (files.Length == 0) return;

        using var scope = _sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VcbMoneyDbContext>();
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        var structureValidator = scope.ServiceProvider.GetRequiredService<IStructureValidator>();

        int valid = 0, invalid = 0;

        foreach (var inputPath in files)
        {
            if (ct.IsCancellationRequested) break;
            
            var fileName = Path.GetFileName(inputPath);
            string? processingPath = null;
            
            try
            {
                // Step 1: Check if file is locked (still being written)
                if (fileService.IsFileLocked(inputPath))
                {
                    _logger.LogDebug("[Job1] File locked, skip: {File}", fileName);
                    continue;
                }

                // Step 2: Compute hash and check duplicate
                var hash = fileService.ComputeFileHash(inputPath);
                if (await context.FileUploads.AsNoTracking().AnyAsync(f => f.FileHash == hash, ct))
                {
                    _logger.LogWarning("[Job1] Duplicate file: {File}", fileName);                                 

                    await fileService.MoveFileAsync(inputPath, Path.Combine(errorFolder, dateFolder), true, ct);
                    continue;
                }

                // Step 3: MOVE to Processing folder (lock the file)
                processingPath = await fileService.MoveFileAsync(inputPath, processingFolder, false, ct);
                _logger.LogInformation("[Job1] Moved to processing: {File}", fileName);

                // Step 4: Read content for validation only (NOT saving to DB)
                var content = await fileService.ReadFileContentAsync(processingPath, ct);
                var fileSize = new FileInfo(processingPath).Length;
                
                // Step 5: L1 Validation - Structure
                var l1Result = structureValidator.ValidateStructure(content);
                if (l1Result.HasCriticalError)
                {
                    _logger.LogWarning("[Job1] L1 failed: {File} - {Error}", fileName, l1Result.Errors.First().Message);
                    
                    // Save to DB with error status (NO RawContent)
                    await SaveFileUpload(context, processingPath, fileName, hash, fileSize,
                        FileStatus.InvalidStructure, 1, l1Result, ct);
                    
                    // Move to Error folder
                    var errorPath = await fileService.MoveFileAsync(processingPath, Path.Combine(errorFolder, dateFolder), true, ct);
                    await UpdateFilePhysicalPath(context, hash, errorPath, ct);
                    
                    invalid++;
                    continue;
                }

                // Step 6: L2 Validation - Required Fields
                var l2Result = structureValidator.ValidateRequiredFields(content);
                if (l2Result.HasCriticalError)
                {
                    _logger.LogWarning("[Job1] L2 failed: {File} - {Error}", fileName, l2Result.Errors.First().Message);
                    
                    // Save to DB with error status (NO RawContent)
                    await SaveFileUpload(context, processingPath, fileName, hash, fileSize,
                        FileStatus.MissingFields, 2, l2Result, ct);
                    
                    // Move to Error folder
                    var errorPath = await fileService.MoveFileAsync(processingPath, Path.Combine(errorFolder, dateFolder), true, ct);
                    await UpdateFilePhysicalPath(context, hash, errorPath, ct);
                    
                    invalid++;
                    continue;
                }

                // Step 7: L1+L2 PASSED - File STAYS in FolderProcessing for Job3 to read
                _logger.LogInformation("[Job1] L1+L2 passed: {File} (ready for Job3)", fileName);
                
                // Save to DB with Valid status - Job3 will read file from FolderProcessing
                // NO RawContent saved - Job3 reads from FilePhysical path
                await SaveFileUpload(context, processingPath, fileName, hash, fileSize,
                    FileStatus.Valid, 2, new ValidationResult { IsValid = true, HighestLevelPassed = 2 }, ct);
                
                valid++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Job1] Error processing: {File}", fileName);
                
                // Try to move to Error folder
                try 
                { 
                    if (!string.IsNullOrEmpty(processingPath) && File.Exists(processingPath))
                        await fileService.MoveFileAsync(processingPath, Path.Combine(errorFolder, dateFolder), true, ct);
                    else if (File.Exists(inputPath))
                        await fileService.MoveFileAsync(inputPath, Path.Combine(errorFolder, dateFolder), true, ct);
                } 
                catch { }
            }
        }

        _logger.LogInformation("[Job1] Done: Valid={V}, Invalid={I}", valid, invalid);
    }

    private void EnsureFoldersExist(params string[] folders)
    {
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
    }

    private async Task SaveFileUpload(VcbMoneyDbContext context, string filePath, string fileName, 
        string hash, long fileSize, FileStatus status, int validationLevel, ValidationResult validation, CancellationToken ct)
    {
        var fileUpload = new FileUpload
        {
            FileId = Guid.NewGuid().ToString(), // Key là string
            FilePhysical = filePath,
            FileNameUpload = fileName, // FIXED: Đổi từ FileName sang FileNameUpload
            FilePath = Path.GetDirectoryName(filePath),
            FileExtension = Path.GetExtension(fileName),
            FileSize = fileSize,
            FileHash = hash,
            BatchId = Guid.NewGuid(),
            Status = (int)status,
            ValidationLevel = validationLevel,
            ValidationErrors = validation.HasError ? JsonSerializer.Serialize(validation.Errors) : null,
            ErrorMessage = validation.HasError ? validation.Errors.First().Message : null,
            CreatedDate = DateTime.Now,
            CreatedBy = "Job1"
        };
        
        await context.FileUploads.AddAsync(fileUpload, ct);
        await context.SaveChangesAsync(ct);
    }

    private async Task UpdateFilePhysicalPath(VcbMoneyDbContext context, string hash, string newPath, CancellationToken ct)
    {
        var file = await context.FileUploads.FirstOrDefaultAsync(f => f.FileHash == hash, ct);
        if (file != null)
        {
            file.FilePhysical = newPath;
            file.FilePath = Path.GetDirectoryName(newPath);
            file.UpdatedDate = DateTime.Now;
            file.UpdatedBy = "Job1";
            await context.SaveChangesAsync(ct);
        }
    }
}

#endregion

#region Job2: Scan PayOrder

public class Job2_ScanPayOrderJob
{
    private readonly IServiceProvider _sp;
    private readonly VcbMoneySettings _settings;
    private readonly ILogger<Job2_ScanPayOrderJob> _logger;

    public Job2_ScanPayOrderJob(IServiceProvider sp, IOptions<VcbMoneySettings> settings, ILogger<Job2_ScanPayOrderJob> logger)
    {
        _sp = sp;
        _settings = settings.Value;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [Queue("default")]
    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Job2] Scanning PayOrder...");

        using var scope = _sp.CreateScope();
        var trungGianContext = scope.ServiceProvider.GetRequiredService<TrungGianDbContext>();
        var payOrderValidator = scope.ServiceProvider.GetRequiredService<IPayOrderValidator>();

        var payOrders = await trungGianContext.PayOrders
            .Where(p => p.UpvcbMoney == null || p.UpvcbMoney == (short)PayOrderStatus.New)
            .OrderBy(p => p.Ngay)
            .Take(_settings.Jobs.Job2_ScanPayOrder.BatchSize)
            .ToListAsync(ct);

        if (!payOrders.Any()) return;

        int valid = 0, invalid = 0;

        foreach (var po in payOrders)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var l2Result = payOrderValidator.ValidateRequiredFields(po);
                
                if (l2Result.HasError)
                {
                    po.UpvcbMoney = (short)PayOrderStatus.Error;
                    po.ErrCode = "L2E";
                    po.ErrDes = l2Result.Errors.First().Message.Length > 250 
                        ? l2Result.Errors.First().Message.Substring(0, 250) 
                        : l2Result.Errors.First().Message;
                    invalid++;
                }
                else
                {
                    po.UpvcbMoney = (short)PayOrderStatus.Valid;
                    po.ErrCode = "0";
                    valid++;
                }
                po.UpDatetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Job2] Error: {SoGd}", po.SoGd);
                po.UpvcbMoney = (short)PayOrderStatus.Error;
                po.ErrCode = "EXC";
            }
        }

        await trungGianContext.SaveChangesAsync(ct);
        _logger.LogInformation("[Job2] Done: Valid={V}, Invalid={I}", valid, invalid);
    }
}

#endregion
