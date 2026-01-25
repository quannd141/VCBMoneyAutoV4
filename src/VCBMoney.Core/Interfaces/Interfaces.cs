using VCBMoney.Core.Entities;

namespace VCBMoney.Core.Interfaces;

#region Validators

public interface IStructureValidator
{
    ValidationResult ValidateStructure(string content);
    ValidationResult ValidateRequiredFields(string content);
}

public interface IPayOrderValidator
{
    ValidationResult ValidateRequiredFields(PayOrder po);
    ValidationResult ValidateFormatAndLength(PayOrder po);
}

public interface IFormatValidator
{
    ValidationResult ValidateFormat(ParsedTransaction tx);
    ValidationResult ValidateFormat(ParsedMt103 tx);
    (ValidationResult result, ParsedTransaction transaction) ValidateAndTruncate(ParsedTransaction tx);
    (ValidationResult result, ParsedMt103 transaction) ValidateAndTruncate(ParsedMt103 tx);
}

public interface IBusinessValidator
{
    Task<ValidationResult> ValidateAsync(
        string? transRef, 
        decimal? amount,
        string? excludeTransRef = null,
        CancellationToken ct = default);
}

#endregion

#region Services

public interface IFileService
{
    string ComputeFileHash(string filePath);
    Task<string> ReadFileContentAsync(string filePath, CancellationToken ct = default);
    Task<string> MoveFileAsync(string sourcePath, string destFolder, bool addTimestamp, CancellationToken ct = default);
    bool IsFileLocked(string filePath);
    Task<bool> CopyFileAsync(string sourcePath, string destPath, CancellationToken ct = default);
    long GetFileSize(string filePath);
    void MoveToArchive(string filePath);
    void MoveToError(string filePath);
}

public interface IParserService
{
    Task<IList<ParsedTransaction>> ParseMT103FileAsync(string content, CancellationToken ct = default);
    List<ParsedMt103> ParseMt103(string content);
    
    // File source - FileUploadId là string
    Transaction ToTransaction(ParsedTransaction parsed, string fileUploadId);
    TransactionImport ToTransactionImport(ParsedTransaction parsed, string fileUploadId);
    
    // PayOrder source
    Transaction ToTransaction(PayOrder po);
    TransactionImport ToTransactionImport(PayOrder po, List<ValidationError> errors);
    
    // Recovery
    Transaction ImportToTransaction(TransactionImport import);
}

public interface IAuditService
{
    Task LogAsync(string action, string entityType, long entityId, object? oldValues, object? newValues, string? userName, CancellationToken ct = default);
}

#endregion

#region Repositories

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);
    Task<IList<T>> GetAllAsync(CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
}

public interface IFileUploadRepository : IRepository<FileUpload>
{
    Task<FileUpload?> GetByIdAsync(string fileId, CancellationToken ct = default);
    Task<FileUpload?> GetByHashAsync(string hash, CancellationToken ct = default);
    Task<IList<FileUpload>> GetByStatusAsync(int status, int take = 100, CancellationToken ct = default);
    Task<IList<FileUpload>> GetValidFilesForParsingAsync(int take = 50, CancellationToken ct = default);
}

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<Transaction?> GetByTransRefAsync(string transRef, CancellationToken ct = default);
    Task<bool> ExistsByTransRefAsync(string transRef, CancellationToken ct = default);
    Task<IList<Transaction>> GetReadyForSyncAsync(int take = 100, CancellationToken ct = default);
    Task<DashboardStats> GetDashboardStatsAsync(CancellationToken ct = default);
    Task<ChartData> GetDailyChartAsync(int days = 7, CancellationToken ct = default);
}

public interface ITransactionImportRepository : IRepository<TransactionImport>
{
    Task<IList<TransactionImport>> GetPendingAsync(int take = 100, CancellationToken ct = default);
    Task<IList<TransactionImport>> GetByFailedLevelAsync(int level, int take = 100, CancellationToken ct = default);
    Task<IList<TransactionImport>> GetForRetryAsync(int maxRetry, int delayMinutes, int take = 50, CancellationToken ct = default);
}

public interface IPayOrderRepository : IRepository<PayOrder>
{
    Task<IList<PayOrder>> GetNewPayOrdersAsync(int take = 100, CancellationToken ct = default);
    Task<IList<PayOrder>> GetValidPayOrdersAsync(int take = 100, CancellationToken ct = default);
}

public interface IDomesticResultRepository : IRepository<DomesticResult>
{
    Task<bool> ExistsByTransRefAsync(string transRef, CancellationToken ct = default);
}

#endregion
