using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using VCBMoney.Core.Constants;
using VCBMoney.Core.Entities;
using VCBMoney.Core.Interfaces;

namespace VCBMoney.Infrastructure.Services;

/// <summary>
/// File operations service
/// </summary>
public class FileService : IFileService
{
    private readonly string _archiveFolder;
    private readonly string _errorFolder;
    
    public FileService()
    {
        // Default folders - will be overridden by config in real usage
        _archiveFolder = @"D:\VCBMoney\Ouput\Success";
        _errorFolder = @"D:\VCBMoney\Output\Error";
    }
    
    public FileService(string archiveFolder, string errorFolder)
    {
        _archiveFolder = archiveFolder;
        _errorFolder = errorFolder;
    }

    public string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public async Task<string> ReadFileContentAsync(string filePath, CancellationToken ct = default)
    {
        // Try multiple encodings
        try
        {
            return await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
        }
        catch
        {
            return await File.ReadAllTextAsync(filePath, Encoding.Default, ct);
        }
    }

    public async Task<string> MoveFileAsync(string sourcePath, string destFolder, bool addTimestamp, CancellationToken ct = default)
    {      
        if (!Directory.Exists(destFolder))
            Directory.CreateDirectory(destFolder);

        var fileName = Path.GetFileName(sourcePath);
        //if (addTimestamp)
        //{
        //    var ext = Path.GetExtension(fileName);
        //    var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        //    fileName = $"{nameWithoutExt}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
        //}

        var destPath = Path.Combine(destFolder, fileName);

        // Handle duplicate filename
        var counter = 1;
        while (File.Exists(destPath))
        {
            var ext = Path.GetExtension(fileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            destPath = Path.Combine(destFolder, $"{nameWithoutExt}_{counter++}{ext}");
        }

        await Task.Run(() => File.Move(sourcePath, destPath), ct);
        return destPath;
    }

    public bool IsFileLocked(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException) { return true; }
        catch { return true; }
    }
    
    public async Task<bool> CopyFileAsync(string sourcePath, string destPath, CancellationToken ct = default)
    {
        try
        {
            var destFolder = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destFolder) && !Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);
            
            await Task.Run(() => File.Copy(sourcePath, destPath, overwrite: true), ct);
            return true;
        }
        catch { return false; }
    }
    
    public long GetFileSize(string filePath)
    {
        try { return new FileInfo(filePath).Length; }
        catch { return 0; }
    }
    
    public void MoveToArchive(string filePath)
    {
        MoveFileSync(filePath, _archiveFolder);
    }
    
    public void MoveToError(string filePath)
    {
        MoveFileSync(filePath, _errorFolder);
    }
    
    private void MoveFileSync(string sourcePath, string destFolder)
    {
        try
        {
            if (!File.Exists(sourcePath)) return;
            
            if (!Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);

            var fileName = Path.GetFileName(sourcePath);
            var ext = Path.GetExtension(fileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var newFileName = $"{nameWithoutExt}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
            var destPath = Path.Combine(destFolder, newFileName);

            // Handle duplicate
            var counter = 1;
            while (File.Exists(destPath))
            {
                destPath = Path.Combine(destFolder, $"{nameWithoutExt}_{DateTime.Now:yyyyMMdd_HHmmss}_{counter++}{ext}");
            }

            File.Move(sourcePath, destPath);
        }
        catch { /* Log error */ }
    }
}

/// <summary>
/// MT103 Parser Service - Improved v4.2
/// Supports both SWIFT format (with block headers) and Plaintext format
/// </summary>
public class ParserService : IParserService
{
    /// <summary>
    /// Detect if content is SWIFT format (has block headers) or Plaintext
    /// </summary>
    private bool IsSwiftFormat(string content)
    {
        return content.Contains("{1:") || content.Contains("{4:");
    }

    public Task<IList<ParsedTransaction>> ParseMT103FileAsync(string content, CancellationToken ct = default)
    {
        var result = ParseMt103(content).Cast<ParsedTransaction>().ToList();
        return Task.FromResult<IList<ParsedTransaction>>(result);
    }

    /// <summary>
    /// Parse MT103 content synchronously - returns List<ParsedMt103>
    /// </summary>
    public List<ParsedMt103> ParseMt103(string content)
    {
        var transactions = new List<ParsedMt103>();
        
        if (IsSwiftFormat(content))
        {
            var messagePattern = @"\{1:[^}]+\}.*?\{4:[^-]*-\}";
            var matches = Regex.Matches(content, messagePattern, RegexOptions.Singleline);
            
            if (matches.Count == 0)
            {
                if (content.Contains(MT103Tags.Block4Start))
                {
                    var tx = ParseSwiftMessageToMt103(content);
                    transactions.Add(tx);
                }
            }
            else
            {
                foreach (Match match in matches)
                {
                    var tx = ParseSwiftMessageToMt103(match.Value);
                    transactions.Add(tx);
                }
            }
        }
        else
        {
            var txPattern = @"(?=:20:)";
            var parts = Regex.Split(content, txPattern)
                .Where(p => !string.IsNullOrWhiteSpace(p) && p.Contains(":20:"))
                .ToArray();
            
            foreach (var part in parts)
            {
                var tx = ParsePlaintextMessageToMt103(part);
                transactions.Add(tx);
            }
        }
        
        return transactions;
    }

    private ParsedMt103 ParseSwiftMessageToMt103(string message)
    {
        var tx = new ParsedMt103();
        
        var block4Start = message.IndexOf(MT103Tags.Block4Start);
        var block4End = message.LastIndexOf(MT103Tags.BlockEnd);
        
        if (block4Start < 0 || block4End < 0) return tx;
        
        var block4 = message.Substring(block4Start + 3, block4End - block4Start - 3);
        ParseTagsToMt103(tx, block4);
        
        return tx;
    }

    private ParsedMt103 ParsePlaintextMessageToMt103(string message)
    {
        var tx = new ParsedMt103();
        ParseTagsToMt103(tx, message);
        return tx;
    }

    private void ParseTagsToMt103(ParsedMt103 tx, string content)
    {
        tx.Field20 = ExtractField(content, ":20:");
        tx.Field32A = ExtractField(content, ":32A:");
        tx.Field50K = ExtractMultilineField(content, ":50K:");
        tx.Field50R = ExtractField(content, ":50R:");
        tx.Field50S = ExtractField(content, ":50S:");
        tx.Field50T = ExtractField(content, ":50T:");
        tx.Field53 = ExtractField(content, ":53B:") ?? 
                     ExtractField(content, ":53A:") ??
                     ExtractField(content, ":53D:");
        tx.Field57 = ExtractMultilineField(content, ":57:");
        tx.Field57A = ExtractMultilineField(content, ":57A:") ?? 
                      ExtractMultilineField(content, ":57D:") ?? 
                      ExtractMultilineField(content, ":57B:");
        tx.Field59 = ExtractMultilineField(content, ":59:") ?? 
                     ExtractMultilineField(content, ":59A:");
        tx.Field59O = ExtractField(content, ":59O:");
        tx.Field59P = ExtractField(content, ":59P:");
        tx.Field59Q = ExtractField(content, ":59Q:");
        tx.Field70 = ExtractMultilineField(content, ":70:");
        tx.Field71A = ExtractField(content, ":71A:");
        tx.Field72 = ExtractMultilineField(content, ":72:");
        tx.Field72A = ExtractField(content, ":72A:");
        tx.Field72B = ExtractField(content, ":72B:");
        tx.Field72C = ExtractField(content, ":72C:");
        tx.Field72D = ExtractField(content, ":72D:");       
      
        
    }

    private ParsedTransaction ParseSwiftMessage(string message)
    {
        var tx = new ParsedTransaction();
        
        var block4Start = message.IndexOf(MT103Tags.Block4Start);
        var block4End = message.LastIndexOf(MT103Tags.BlockEnd);
        
        if (block4Start < 0 || block4End < 0) return tx;
        
        var block4 = message.Substring(block4Start + 3, block4End - block4Start - 3);
        ParseTags(tx, block4);
        
        return tx;
    }

    private ParsedTransaction ParsePlaintextMessage(string message)
    {
        var tx = new ParsedTransaction();
        ParseTags(tx, message);
        return tx;
    }

    private void ParseTags(ParsedTransaction tx, string content)
    {
        // Core fields
        tx.Field20 = ExtractField(content, ":20:");
        tx.Field32A = ExtractField(content, ":32A:");
        
        // Ordering Customer (50x)
        tx.Field50K = ExtractMultilineField(content, ":50K:");
        tx.Field50R = ExtractField(content, ":50R:");
        var field50S = ExtractField(content, ":50S:");
        var field50T = ExtractField(content, ":50T:");
        
        // Combine 50R/50S/50T if 50K not present
        if (string.IsNullOrEmpty(tx.Field50K) && (!string.IsNullOrEmpty(field50S) || !string.IsNullOrEmpty(tx.Field50R)))
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(field50S)) parts.Add(field50S);
            if (!string.IsNullOrEmpty(tx.Field50R)) parts.Add($"(ACC NO:{tx.Field50R})");
            if (!string.IsNullOrEmpty(field50T)) parts.Add($"ADD:{field50T}");
            tx.Field50K = string.Join(" ", parts);
        }
        
        // Debit Account (53x)
        tx.Field53 = ExtractField(content, ":53B:") ?? 
                     ExtractField(content, ":53A:") ?? 
                     ExtractField(content, ":53D:");
        
        // Beneficiary Bank (57x)
        tx.Field57A = ExtractMultilineField(content, ":57A:") ?? 
                      ExtractMultilineField(content, ":57D:") ??
                      ExtractMultilineField(content, ":57B:");
        
        // Beneficiary (59)
        tx.Field59 = ExtractMultilineField(content, ":59:");
        if (string.IsNullOrEmpty(tx.Field59))
            tx.Field59 = ExtractMultilineField(content, ":59A:");
        
        // Beneficiary ID (59O, 59P, 59Q)
        tx.Field59O = ExtractField(content, ":59O:");
        // Store 59P, 59Q for future use if needed
        
        // If Field59O not found, extract from Field59 (account number from first line)
        if (string.IsNullOrEmpty(tx.Field59O))
            tx.Field59O = ExtractBenAccount(tx.Field59);
        
        // Content (70)
        tx.Field70 = ExtractMultilineField(content, ":70:");
        
        // Charges (71A)
        tx.Field71A = ExtractField(content, ":71A:");
        
        // Special flags (72x)
        tx.Field72 = ExtractMultilineField(content, ":72:");
        // For plaintext: 72A=BankToBank, 72B=SBV, 72C=CheckBenName, 72D=Priority
        
        // Parse 32A for date/currency/amount
        if (!string.IsNullOrEmpty(tx.Field32A))
            ParseField32A(tx);
    }

    private string? ExtractField(string block, string tag)
    {
        var startIndex = block.IndexOf(tag);
        if (startIndex < 0) return null;
        
        startIndex += tag.Length;
        
        // Find end: next tag or end of content
        var endIndex = block.Length;
        var nextTagMatch = Regex.Match(block.Substring(startIndex), @"\r?\n:[0-9A-Z]{2,3}:");
        if (nextTagMatch.Success)
            endIndex = startIndex + nextTagMatch.Index;
        
        var value = block.Substring(startIndex, endIndex - startIndex);
        return value.Trim('\r', '\n', ' ');
    }

    private string? ExtractMultilineField(string block, string tag)
    {
        var startIndex = block.IndexOf(tag);
        if (startIndex < 0) return null;
        
        startIndex += tag.Length;
        var nextFieldPattern = @"\r?\n:[0-9A-Z]{2,3}:";
        var match = Regex.Match(block.Substring(startIndex), nextFieldPattern);
        
        int endIndex = match.Success ? startIndex + match.Index : block.Length;
        var value = block.Substring(startIndex, endIndex - startIndex);
        
        // Keep newlines for multiline fields (will be processed later)
        return value.Trim('\r', '\n', ' ');
    }

    private string? ExtractBenAccount(string? field59)
    {
        if (string.IsNullOrEmpty(field59)) return null;
        
        var lines = field59.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("/")) return trimmed.TrimStart('/');
            if (Regex.IsMatch(trimmed, @"^\d+$")) return trimmed;
        }
        return null;
    }

    private void ParseField32A(ParsedTransaction tx)
    {
        var field = tx.Field32A!;
        
        if (field.Length >= 6)
        {
            var dateStr = field.Substring(0, 6);
            if (TryParseYYMMDD(dateStr, out var date))
                tx.ValueDate = date;
        }
        
        var currencyMatch = Regex.Match(field, @"(\d{6})([A-Z]{3})");
        if (currencyMatch.Success)
            tx.Currency = currencyMatch.Groups[2].Value;
        
        var amountMatch = Regex.Match(field, @"[A-Z]{3}([\d,]+\.?\d*)$");
        if (amountMatch.Success)
        {
            var amountStr = amountMatch.Groups[1].Value.Replace(",", "");
            if (decimal.TryParse(amountStr, out var amount))
                tx.Amount = amount;
        }
    }

    private static bool TryParseYYMMDD(string dateStr, out DateTime date)
    {
        date = default;
        if (dateStr.Length != 6) return false;
        try
        {
            var year = 2000 + int.Parse(dateStr.Substring(0, 2));
            var month = int.Parse(dateStr.Substring(2, 2));
            var day = int.Parse(dateStr.Substring(4, 2));
            date = new DateTime(year, month, day);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Convert ParsedTransaction to Transaction (valid data)
    /// </summary>
    public Transaction ToTransaction(ParsedTransaction parsed, string fileUploadId)
    {
        return new Transaction
        {
            TransRef = parsed.Field20?.Trim() ?? Guid.NewGuid().ToString("N").Substring(0, 21),
            SourceType = "FILE",
            SourceRef = fileUploadId,
            FileUploadId = fileUploadId,
            DebitAccountNo = ExtractAccountFromField50(parsed.Field50K) ?? "",
            DebitAmount = parsed.Amount,
            DebitCurrency = parsed.Currency ?? "VND",
            BenAccountNo = parsed.Field59O,
            BenAccountName = Truncate(ExtractNameFromField59(parsed.Field59), 200),
            BenBank = Truncate(parsed.Field57A, 150),
            Content = Truncate(parsed.Field70, 255),
            Charge = Truncate(parsed.Field71A?.Trim().ToUpper(), 3),
            ValueDate = parsed.ValueDate,
            Status = 0, // Ready
            CreatedDate = DateTime.Now,
            CreatedBy = "Job3_Parse"
        };
    }

    /// <summary>
    /// Convert ParsedTransaction to TransactionImport (invalid data)
    /// </summary>
    public TransactionImport ToTransactionImport(ParsedTransaction parsed, string fileUploadId)
    {
        var benName = ExtractNameFromField59(parsed.Field59);
        var benNameTruncated = !string.IsNullOrEmpty(benName) && benName.Length > 200;
        var contentTruncated = !string.IsNullOrEmpty(parsed.Field70) && parsed.Field70.Length > 255;
        
        return new TransactionImport
        {
            SourceType = "FILE",
            SourceRef = fileUploadId,
            FileUploadId = fileUploadId,
            TransRef = parsed.Field20?.Trim(),
            DebitAccountNo = ExtractAccountFromField50(parsed.Field50K),
            DebitAmountRaw = parsed.Field32A,
            DebitAmount = parsed.Amount?.ToString(),
            DebitCurrency = parsed.Currency ?? "VND",
            BenAccountNo = parsed.Field59O,
            BenAccountNameRaw = benName,
            BenAccountName = Truncate(benName, 200),
            BenBankRaw = parsed.Field57A,
            BenBank = Truncate(parsed.Field57A, FieldLength.BenBankMax),
            ContentRaw = parsed.Field70,
            Content = Truncate(parsed.Field70, FieldLength.ContentMax),
            Charge = parsed.Field71A?.Trim().ToUpper(),
            ValueDate = parsed.ValueDate,
            Status = 0, // Pending
            FailedLevel = GetHighestErrorLevel(parsed.Validation),
            ValidationErrors = parsed.Validation.HasError ? JsonSerializer.Serialize(parsed.Validation.Errors) : null,
            ValidationWarnings = parsed.Validation.HasWarning ? JsonSerializer.Serialize(parsed.Validation.Warnings) : null,
            ErrCode = parsed.Validation.Errors.FirstOrDefault()?.Code,
            ErrDesc = parsed.Validation.Errors.FirstOrDefault()?.Message,
            // Field-level error flags
            ErrTransRef = parsed.Validation.Errors.Any(e => e.Field == "TransRef"),
            ErrAmount = parsed.Validation.Errors.Any(e => e.Field == "Amount"),
            ErrDebitAccount = parsed.Validation.Errors.Any(e => e.Field == "DebitAccount"),
            ErrBenAccount = parsed.Validation.Errors.Any(e => e.Field == "BenAccount"),
            ErrBenName = parsed.Validation.Errors.Any(e => e.Field == "BenName"),
            ErrBenBank = parsed.Validation.Errors.Any(e => e.Field == "BenBank"),
            ErrContent = parsed.Validation.Errors.Any(e => e.Field == "Content"),
            ErrValueDate = parsed.Validation.Errors.Any(e => e.Field == "ValueDate"),
            ErrDuplicate = parsed.Validation.Errors.Any(e => e.Code.Contains("DUPLICATE")),
            ErrBusiness = parsed.Validation.Errors.Any(e => e.Level == 5),
            // Data quality flags
            HasFormatError = parsed.Validation.Errors.Any(e => e.Level == 3),
            HasLengthWarning = parsed.Validation.Warnings.Any(e => e.Level == 4),
            BenNameTruncated = benNameTruncated,
            ContentTruncated = contentTruncated,
            CreatedDate = DateTime.Now,
            CreatedBy = "Job3_Parse"
        };
    }

    /// <summary>
    /// Convert PayOrder to Transaction (valid data)
    /// </summary>
    public Transaction ToTransaction(PayOrder po)
    {
        return new Transaction
        {
            TransRef = po.SoGd,
            SourceType = "PAYORDER",
            SourceRef = po.SoGd,
            DebitAccountNo = po.TkNo,
            DebitAmount = (decimal)po.SoTien,
            DebitCurrency = po.Lnt ?? "VND",
            BenAccountNo = po.TkNh,
            BenAccountName = Truncate(po.TenNh, 200),
            BenBank = Truncate(po.NhNh, 150),
            BenAddress = Truncate(po.DcNh, 200),
            BenIdNo = po.CmtNrl,
            OrderingCust = Truncate(po.TenNrl, 255),
            OrderingCustAccountNo = po.SoTkNrl,
            Content = Truncate(po.Nd, 255),
            Charge = Truncate(po.Phi?.ToUpper(), 3),
            Priority = po.UuTien.HasValue ? (int)po.UuTien.Value : 0,
            ValueDate = po.Ngay,
            TransferType = po.BankToBank == true ? 1 : 0,
            CheckBenName = po.InquiryBenName ?? false,
            Status = 0, // Ready
            CreatedDate = DateTime.Now,
            CreatedBy = "Job4_Transform"
        };
    }

    /// <summary>
    /// Convert PayOrder to TransactionImport (invalid data)
    /// </summary>
    public TransactionImport ToTransactionImport(PayOrder po, List<ValidationError> errors)
    {
        var benNameTruncated = !string.IsNullOrEmpty(po.TenNh) && po.TenNh.Length > 200;
        var contentTruncated = !string.IsNullOrEmpty(po.Nd) && po.Nd.Length > 255;
        
        return new TransactionImport
        {
            SourceType = "PAYORDER",
            SourceRef = po.SoGd,
            TransRef = po.SoGd,
            DebitAccountNo = po.TkNo,
            DebitAmountRaw = po.SoTien.ToString("F2"),
            DebitAmount = po.SoTien.ToString("F2"),
            DebitCurrency = po.Lnt ?? "VND",
            BenAccountNo = po.TkNh,
            BenAccountNameRaw = po.TenNh,
            BenAccountName = Truncate(po.TenNh, 200),
            BenBankRaw = po.NhNh,
            BenBank = Truncate(po.NhNh, 150),
            BenAddress = Truncate(po.DcNh, 200),
            BenIdNo = po.CmtNrl,
            OrderingCust = Truncate(po.TenNrl, 255),
            OrderingCustAccountNo = po.SoTkNrl,
            ContentRaw = po.Nd,
            Content = Truncate(po.Nd, 255),
            Charge = Truncate(po.Phi?.ToUpper(), 10),
            Priority = po.UuTien?.ToString(),
            ValueDate = po.Ngay,
            CheckBenName = po.InquiryBenName ?? false,
            Status = 0, // Pending
            FailedLevel = errors.Any() ? errors.Max(e => e.Level) : 0,
            ValidationErrors = errors.Any() ? JsonSerializer.Serialize(errors) : null,
            ErrCode = errors.FirstOrDefault()?.Code,
            ErrDesc = Truncate(errors.FirstOrDefault()?.Message, 2000),
            ErrTransRef = errors.Any(e => e.Field == "TransRef"),
            ErrAmount = errors.Any(e => e.Field == "Amount"),
            ErrDuplicate = errors.Any(e => e.Code?.Contains("DUPLICATE") == true),
            ErrBusiness = errors.Any(e => e.Level == 5),
            HasFormatError = errors.Any(e => e.Level == 3),
            HasLengthWarning = errors.Any(e => e.Level == 4),
            BenNameTruncated = benNameTruncated,
            ContentTruncated = contentTruncated,
            CreatedDate = DateTime.Now,
            CreatedBy = "Job4_Transform"
        };
    }

    /// <summary>
    /// Convert TransactionImport to Transaction (when recovered)
    /// </summary>
    public Transaction ImportToTransaction(TransactionImport import)
    {
        return new Transaction
        {
            TransRef = import.TransRef ?? Guid.NewGuid().ToString("N").Substring(0, 21),
            SourceType = import.SourceType,
            SourceRef = import.SourceRef,
            FileUploadId = import.FileUploadId,
            DebitAccountNo = import.DebitAccountNo ?? "",
            DebitAmount = decimal.TryParse(import.DebitAmount, out var amt) ? amt : 0,
            DebitCurrency = import.DebitCurrency,
            BenAccountNo = import.BenAccountNo,
            BenAccountName = import.BenAccountName,
            BenBank = import.BenBank,
            BenAddress = import.BenAddress,
            BenIdNo = import.BenIdNo,
            BenIdNoPlace = import.BenIdNoPlace,
            OrderingCust = import.OrderingCust,
            OrderingCustAccountNo = import.OrderingCustAccountNo,
            OrderingCustAddress = import.OrderingCustAddress,
            Content = import.Content,
            Charge = import.Charge,
            ValueDate = import.ValueDate,
            CheckBenName = import.CheckBenName,
            Status = 0, // Ready
            CreatedDate = DateTime.Now,
            CreatedBy = "Recovered"
        };
    }

    private int GetHighestErrorLevel(ValidationResult validation)
    {
        if (!validation.Errors.Any()) return 0;
        return validation.Errors.Max(e => e.Level);
    }

    private string? ExtractAccountFromField50(string? field50)
    {
        if (string.IsNullOrEmpty(field50)) return null;
        var lines = field50.Split(new[] { '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
            if (line.StartsWith("/")) return line.TrimStart('/');
        return null;
    }

    private string? ExtractNameFromField59(string? field59)
    {
        if (string.IsNullOrEmpty(field59)) return null;
        var lines = field59.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("/") && !Regex.IsMatch(trimmed, @"^\d+$"))
                return trimmed;
        }
        return field59;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}

/// <summary>
/// Audit logging service
/// </summary>
public class AuditService : IAuditService
{
    private readonly IServiceProvider _sp;
    
    public AuditService(IServiceProvider sp)
    {
        _sp = sp;
    }
    
    public async Task LogAsync(string action, string entityType, long entityId, object? oldValues, object? newValues, string? userName, CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VCBMoney.Infrastructure.Data.VcbMoneyDbContext>();
        
        var log = new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
            UserName = userName,
            CreatedDate = DateTime.Now
        };
        
        await context.AuditLogs.AddAsync(log, ct);
        await context.SaveChangesAsync(ct);
    }
}
