using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VCBMoney.Core.Constants;
using VCBMoney.Core.Entities;
using VCBMoney.Core.Enums;
using VCBMoney.Core.Interfaces;
using VCBMoney.Infrastructure.Data;

namespace VCBMoney.Infrastructure.Validators;

/// <summary>
/// L1 + L2: Validate cấu trúc file và required fields
/// Supports both:
/// - SWIFT MT103 format (with {1:, {2:, {4: blocks)
/// - Plaintext format (just :XX: tags without block headers)
/// </summary>
public class StructureValidator : IStructureValidator
{
    /// <summary>
    /// Detect file format: SWIFT (has blocks) or Plaintext (no blocks)
    /// </summary>
    private bool IsSwiftFormat(string content)
    {
        return content.Contains(MT103Tags.Block1Start) || 
               content.Contains(MT103Tags.Block4Start);
    }

    public ValidationResult ValidateStructure(string content)
    {
        var result = new ValidationResult();
        
        if (string.IsNullOrWhiteSpace(content))
        {
            result.AddError(1, "Content", "EMPTY_FILE", "File content is empty", severity: "Critical");
            return result;
        }
        
        // Check if it's SWIFT format or Plaintext format
        if (IsSwiftFormat(content))
        {
            // SWIFT MT103 format validation
            ValidateSwiftStructure(content, result);
        }
        else
        {
            // Plaintext format - just need to have at least one tag
            ValidatePlaintextStructure(content, result);
        }
        
        if (!result.HasCriticalError)
            result.HighestLevelPassed = 1;
        
        return result;
    }

    private void ValidateSwiftStructure(string content, ValidationResult result)
    {
        // Check Block 1 - Basic Header
        if (!content.Contains(MT103Tags.Block1Start))
        {
            result.AddError(1, "Block1", ValidationErrorCode.MissingBlock1.ToString(), 
                "Missing Block 1 (Basic Header) - {1:...}", severity: "Critical");
        }
        
        // Check Block 2 - Application Header (optional warning)
        if (!content.Contains(MT103Tags.Block2Start))
        {
            result.AddWarning(1, "Block2", ValidationErrorCode.MissingBlock2.ToString(), 
                "Missing Block 2 (Application Header) - {2:...}");
        }
        
        // Check Block 4 - Text Block
        if (!content.Contains(MT103Tags.Block4Start))
        {
            result.AddError(1, "Block4", ValidationErrorCode.MissingBlock4.ToString(), 
                "Missing Block 4 (Text Block) - {4:...}", severity: "Critical");
        }
        
        // Check Block 4 ending
        if (content.Contains(MT103Tags.Block4Start) && !content.Contains(MT103Tags.BlockEnd))
        {
            result.AddError(1, "Block4", ValidationErrorCode.InvalidBlockFormat.ToString(), 
                "Block 4 not properly terminated with -}", severity: "Critical");
        }
        
        // Validate block order
        var block1Pos = content.IndexOf(MT103Tags.Block1Start);
        var block2Pos = content.IndexOf(MT103Tags.Block2Start);
        var block4Pos = content.IndexOf(MT103Tags.Block4Start);
        
        if (block1Pos >= 0 && block2Pos >= 0 && block4Pos >= 0)
        {
            if (!(block1Pos < block2Pos && block2Pos < block4Pos))
            {
                result.AddWarning(1, "BlockOrder", "INVALID_ORDER", 
                    "Blocks are not in expected order: {1} -> {2} -> {4}");
            }
        }
    }

    private void ValidatePlaintextStructure(string content, ValidationResult result)
    {
        // Plaintext format: just check if it has at least one valid MT103 tag
        var hasAnyTag = content.Contains(":20:") || 
                        content.Contains(":32A:") || 
                        content.Contains(":50") || 
                        content.Contains(":59:");
        
        if (!hasAnyTag)
        {
            result.AddError(1, "Content", "NO_VALID_TAGS", 
                "File does not contain any valid MT103 tags (:20:, :32A:, :50x:, :59:)", 
                severity: "Critical");
        }
        
        // Check if content looks like structured data
        var tagPattern = new Regex(@"^:[0-9]{2}[A-Z]?:", RegexOptions.Multiline);
        var matches = tagPattern.Matches(content);
        
        if (matches.Count == 0)
        {
            result.AddError(1, "Content", "INVALID_FORMAT", 
                "File content is not in valid MT103 tag format", 
                severity: "Critical");
        }
    }

    public ValidationResult ValidateRequiredFields(string content)
    {
        var result = new ValidationResult();
        
        // Determine content to validate based on format
        string contentToValidate;
        
        if (IsSwiftFormat(content))
        {
            // Extract Block 4 content for SWIFT format
            var block4Start = content.IndexOf(MT103Tags.Block4Start);
            var block4End = content.LastIndexOf(MT103Tags.BlockEnd);
            
            if (block4Start < 0 || block4End < 0 || block4End <= block4Start)
            {
                result.AddError(2, "Block4", "INVALID_BLOCK4", "Cannot extract Block 4 content", severity: "Critical");
                return result;
            }
            
            contentToValidate = content.Substring(block4Start, block4End - block4Start + 2);
        }
        else
        {
            // For plaintext, validate entire content
            contentToValidate = content;
        }
        
        // :20: Transaction Reference
        if (!contentToValidate.Contains(MT103Tags.TransRef))
        {
            result.AddError(2, "TransRef", ValidationErrorCode.MissingTransRef.ToString(), 
                "Missing required field :20: (Transaction Reference)", severity: "Critical");
        }
        
        // :32A: Value Date, Currency, Amount
        if (!contentToValidate.Contains(MT103Tags.ValueDateAmount))
        {
            result.AddError(2, "ValueDateAmount", ValidationErrorCode.MissingAmount.ToString(), 
                "Missing required field :32A: (Value Date/Currency/Amount)", severity: "Critical");
        }
        
        // :53B: or :53A: for Debit Account (Sender's Correspondent)
        var hasDebitAccount = contentToValidate.Contains(":53B:") || 
                              contentToValidate.Contains(":53A:") ||
                              contentToValidate.Contains(":53D:");
        if (!hasDebitAccount)
        {
            // Also check :50K:, :50R: for ordering customer with account info
            var hasOrderingWithAccount = contentToValidate.Contains(":50R:") ||
                                         contentToValidate.Contains(":50K:");
            if (!hasOrderingWithAccount)
            {
                result.AddWarning(2, "DebitAccount", "MISSING_DEBIT_ACC", 
                    "Missing debit account field :53B: or :50R:");
            }
        }
        
        // :50K: or alternatives for Ordering Customer
        var hasOrderingCustomer = MT103Tags.OrderingCustomerTags.Any(tag => contentToValidate.Contains(tag)) ||
                                  contentToValidate.Contains(":50R:") ||
                                  contentToValidate.Contains(":50S:") ||
                                  contentToValidate.Contains(":50T:");
        if (!hasOrderingCustomer)
        {
            result.AddError(2, "OrderingCustomer", ValidationErrorCode.MissingDebitAccount.ToString(), 
                "Missing required field for Ordering Customer (:50K:/:50R:/:50S:/:50T:)", severity: "Error");
        }
        
        // :57A: for Beneficiary Bank
        var hasBenBank = contentToValidate.Contains(":57A:") || 
                         contentToValidate.Contains(":57B:") ||
                         contentToValidate.Contains(":57D:");
        if (!hasBenBank)
        {
            result.AddWarning(2, "BenBank", "MISSING_BEN_BANK", 
                "Missing beneficiary bank field :57A:");
        }
        
        // :59: or alternatives for Beneficiary
        var hasBeneficiary = MT103Tags.BeneficiaryTags.Any(tag => contentToValidate.Contains(tag)) ||
                             contentToValidate.Contains(":59O:"); // ID field also counts
        if (!hasBeneficiary)
        {
            result.AddError(2, "Beneficiary", ValidationErrorCode.MissingBeneficiary.ToString(), 
                "Missing required field :59: (Beneficiary)", severity: "Critical");
        }
        
        if (!result.HasCriticalError && !result.HasError)
            result.HighestLevelPassed = 2;
        
        return result;
    }
}

/// <summary>
/// L2 + L3 + L4: Validate PayOrder
/// </summary>
public class PayOrderValidator : IPayOrderValidator
{
    public ValidationResult ValidateRequiredFields(PayOrder po)
    {
        var result = new ValidationResult();
        
        if (string.IsNullOrWhiteSpace(po.SoGd))
            result.AddError(2, "SoGd", ValidationErrorCode.MissingTransRef.ToString(), 
                "Missing SOGD (Transaction Reference)", severity: "Critical");
        
        if (string.IsNullOrWhiteSpace(po.TkNo))
            result.AddError(2, "TkNo", ValidationErrorCode.MissingDebitAccount.ToString(), 
                "Missing TKNO (Debit Account)", severity: "Critical");
        
        if (po.SoTien <= 0)
            result.AddError(2, "SoTien", ValidationErrorCode.MissingAmount.ToString(), 
                "Invalid SOTIEN (Amount must be > 0)", po.SoTien.ToString(), severity: "Critical");
        
        if (string.IsNullOrWhiteSpace(po.TkNh))
            result.AddError(2, "TkNh", ValidationErrorCode.MissingBenAccount.ToString(), 
                "Missing TKNH (Beneficiary Account)", severity: "Error");
        
        if (string.IsNullOrWhiteSpace(po.TenNh))
            result.AddError(2, "TenNh", ValidationErrorCode.MissingBeneficiary.ToString(), 
                "Missing TENNH (Beneficiary Name)", severity: "Error");
        
        if (po.Ngay == default)
            result.AddError(2, "Ngay", ValidationErrorCode.MissingValueDate.ToString(), 
                "Missing NGAY (Transaction Date)", severity: "Error");
        
        if (!result.HasCriticalError && !result.HasError)
            result.HighestLevelPassed = 2;
        
        return result;
    }

    public ValidationResult ValidateFormatAndLength(PayOrder po)
    {
        var result = new ValidationResult { HighestLevelPassed = 2 };
        
        // L3: Format validation
        if (!string.IsNullOrEmpty(po.TkNo) && !Regex.IsMatch(po.TkNo, ValidationPatterns.AccountNumber))
            result.AddError(3, "TkNo", ValidationErrorCode.InvalidAccountFormat.ToString(), 
                "Invalid TKNO format (only alphanumeric allowed)", po.TkNo, severity: "Error");
        
        if (!string.IsNullOrEmpty(po.TkNh) && !Regex.IsMatch(po.TkNh, ValidationPatterns.AccountNumber))
            result.AddError(3, "TkNh", ValidationErrorCode.InvalidAccountFormat.ToString(), 
                "Invalid TKNH format (only alphanumeric allowed)", po.TkNh, severity: "Error");
        
        if (!string.IsNullOrEmpty(po.Lnt) && !Regex.IsMatch(po.Lnt, ValidationPatterns.CurrencyCode))
            result.AddError(3, "Lnt", ValidationErrorCode.InvalidCurrencyCode.ToString(), 
                "Invalid LNT format (must be 3 letters)", po.Lnt, severity: "Error");
        
        if (!string.IsNullOrEmpty(po.Phi) && !new[] { "OUR", "BEN", "SHA" }.Contains(po.Phi.ToUpper()))
            result.AddWarning(3, "Phi", ValidationErrorCode.InvalidChargeType.ToString(), 
                "Invalid PHI value (expected OUR/BEN/SHA)", po.Phi);
        
        // L4: Length validation
        if (!string.IsNullOrEmpty(po.TkNo) && po.TkNo.Length > FieldLength.AccountMax)
            result.AddError(4, "TkNo", ValidationErrorCode.AccountTooLong.ToString(), 
                $"TKNO exceeds {FieldLength.AccountMax} chars", $"Length: {po.TkNo.Length}", severity: "Error");
        
        if (!string.IsNullOrEmpty(po.TkNh) && po.TkNh.Length > FieldLength.AccountMax)
            result.AddError(4, "TkNh", ValidationErrorCode.AccountTooLong.ToString(), 
                $"TKNH exceeds {FieldLength.AccountMax} chars", $"Length: {po.TkNh.Length}", severity: "Error");
        
        if (!string.IsNullOrEmpty(po.TenNh) && po.TenNh.Length > FieldLength.BenNameMax)
            result.AddWarning(4, "TenNh", ValidationErrorCode.BenNameTooLong.ToString(), 
                $"TENNH exceeds {FieldLength.BenNameMax} chars, will be truncated", $"Length: {po.TenNh.Length}");
        
        if (!string.IsNullOrEmpty(po.Nd) && po.Nd.Length > FieldLength.ContentMax)
            result.AddWarning(4, "Nd", ValidationErrorCode.ContentTooLong.ToString(), 
                $"ND exceeds {FieldLength.ContentMax} chars, will be truncated", $"Length: {po.Nd.Length}");
        
        if (!result.HasError)
            result.HighestLevelPassed = result.HasWarning ? 3 : 4;
        
        return result;
    }
}

/// <summary>
/// L3 + L4: Format and Length validator for MT103
/// </summary>
public class FormatValidator : IFormatValidator
{
    public ValidationResult ValidateFormat(ParsedTransaction tx)
    {
        var result = new ValidationResult { HighestLevelPassed = 2 };
        
        // Date format in :32A:
        if (!string.IsNullOrEmpty(tx.Field32A))
        {
            var dateMatch = Regex.Match(tx.Field32A, @"^(\d{6})");
            if (dateMatch.Success)
            {
                var dateStr = dateMatch.Groups[1].Value;
                if (!TryParseYYMMDD(dateStr, out var date))
                {
                    result.AddError(3, "ValueDate", ValidationErrorCode.InvalidDateFormat.ToString(), 
                        "Invalid date format in :32A:, expected YYMMDD", dateStr, severity: "Error");
                }
                else
                {
                    tx.ValueDate = date;
                }
            }
        }
        
        // Amount format in :32A:
        if (!string.IsNullOrEmpty(tx.Field32A))
        {
            var amountMatch = Regex.Match(tx.Field32A, @"[A-Z]{3}([\d,]+\.?\d*)$");
            if (amountMatch.Success)
            {
                var amountStr = amountMatch.Groups[1].Value.Replace(",", "");
                if (!decimal.TryParse(amountStr, out var amount))
                {
                    result.AddError(3, "Amount", ValidationErrorCode.InvalidAmountFormat.ToString(), 
                        "Invalid amount format in :32A:", amountStr, severity: "Error");
                }
                else
                {
                    tx.Amount = amount;
                }
            }
            
            var currencyMatch = Regex.Match(tx.Field32A, @"\d{6}([A-Z]{3})");
            if (currencyMatch.Success)
            {
                tx.Currency = currencyMatch.Groups[1].Value;
            }
        }
        
        // Charge type in :71A:
        if (!string.IsNullOrEmpty(tx.Field71A))
        {
            var chargeType = tx.Field71A.Trim().ToUpper();
            if (!new[] { "OUR", "BEN", "SHA" }.Contains(chargeType))
            {
                result.AddWarning(3, "ChargeType", ValidationErrorCode.InvalidChargeType.ToString(), 
                    "Invalid charge type in :71A:, expected OUR/BEN/SHA", tx.Field71A);
            }
        }
        
        if (!result.HasError)
            result.HighestLevelPassed = 3;
        
        return result;
    }

    public ValidationResult ValidateFormat(ParsedMt103 tx)
    {
        return ValidateFormat((ParsedTransaction)tx);
    }

    public (ValidationResult result, ParsedTransaction transaction) ValidateAndTruncate(ParsedTransaction tx)
    {
        var result = new ValidationResult { HighestLevelPassed = 3 };
        
        // TransRef - cannot truncate
        if (!string.IsNullOrEmpty(tx.Field20) && tx.Field20.Length > FieldLength.TransRefMax)
        {
            result.AddError(4, "TransRef", ValidationErrorCode.TransRefTooLong.ToString(), 
                $"TransRef exceeds {FieldLength.TransRefMax} chars, cannot truncate", 
                $"Length: {tx.Field20.Length}", severity: "Error");
        }
        
        // BenName - truncate with warning
        if (!string.IsNullOrEmpty(tx.Field59) && tx.Field59.Length > FieldLength.BenNameMax)
        {
            result.AddWarning(4, "BenName", ValidationErrorCode.BenNameTooLong.ToString(), 
                $"BenName exceeds {FieldLength.BenNameMax} chars, truncating", 
                $"Length: {tx.Field59.Length}");
            tx.Field59 = tx.Field59.Substring(0, FieldLength.BenNameMax);
        }
        
        // Content - truncate with warning
        if (!string.IsNullOrEmpty(tx.Field70) && tx.Field70.Length > FieldLength.ContentMax)
        {
            result.AddWarning(4, "Content", ValidationErrorCode.ContentTooLong.ToString(), 
                $"Content exceeds {FieldLength.ContentMax} chars, truncating", 
                $"Length: {tx.Field70.Length}");
            tx.Field70 = tx.Field70.Substring(0, FieldLength.ContentMax);
        }
        
        // BenAccount - cannot truncate
        if (!string.IsNullOrEmpty(tx.Field59O) && tx.Field59O.Length > FieldLength.AccountMax)
        {
            result.AddError(4, "BenAccount", ValidationErrorCode.AccountTooLong.ToString(), 
                $"BenAccount exceeds {FieldLength.AccountMax} chars, cannot truncate", 
                $"Length: {tx.Field59O.Length}", severity: "Error");
        }
        
        if (!result.HasError)
            result.HighestLevelPassed = 4;
        
        return (result, tx);
    }

    public (ValidationResult result, ParsedMt103 transaction) ValidateAndTruncate(ParsedMt103 tx)
    {
        var (result, _) = ValidateAndTruncate((ParsedTransaction)tx);
        return (result, tx);
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
}

/// <summary>
/// L5: Business validation
/// </summary>
public class BusinessValidator : IBusinessValidator
{
    private readonly IServiceProvider _sp;
    
    public BusinessValidator(IServiceProvider sp)
    {
        _sp = sp;
    }

    public async Task<ValidationResult> ValidateAsync(
        string? transRef, 
        decimal? amount,
        long? excludeId = null,
        CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var vcbContext = scope.ServiceProvider.GetRequiredService<VcbMoneyDbContext>();
        var trungGianContext = scope.ServiceProvider.GetRequiredService<TrungGianDbContext>();
        
        var result = new ValidationResult { HighestLevelPassed = 4 };
        
        // Check duplicate TransRef
        if (!string.IsNullOrEmpty(transRef))
        {
            // Check in TRANSACTIONS
            var existsInTrans = await vcbContext.Transactions
                .AsNoTracking()
                .AnyAsync(t => t.TransRef == transRef, ct);
            
            if (existsInTrans)
            {
                result.AddError(5, "TransRef", ValidationErrorCode.DuplicateTransRef.ToString(), 
                    "Duplicate TransRef in TRANSACTIONS", transRef, severity: "Error");
            }
            
            // Check in DOMESTIC_RESULT
            var existsInResult = await trungGianContext.DomesticResults
                .AsNoTracking()
                .AnyAsync(r => r.TransRef == transRef, ct);
            
            if (existsInResult)
            {
                result.AddError(5, "TransRef", ValidationErrorCode.DuplicateTransRef.ToString(), 
                    "Duplicate TransRef in DOMESTIC_RESULT (already synced)", transRef, severity: "Error");
            }
        }
        
        // Check amount > 0
        if (!amount.HasValue || amount <= 0)
        {
            result.AddError(5, "Amount", ValidationErrorCode.AmountZeroOrNegative.ToString(), 
                "Amount must be greater than 0", amount?.ToString() ?? "null", severity: "Error");
        }
        
        // Check amount limit (warning only)
        if (amount.HasValue && amount > BusinessLimits.MaxAmountNoApproval)
        {
            result.AddWarning(5, "Amount", ValidationErrorCode.AmountExceedsLimit.ToString(), 
                $"Amount exceeds {BusinessLimits.MaxAmountNoApproval:N0}, needs approval", 
                amount.Value.ToString("N2"));
        }
        
        if (!result.HasError)
            result.HighestLevelPassed = 5;
        
        return result;
    }

    // Add explicit implementation for IBusinessValidator.ValidateAsync(string? transRef, decimal? amount, string? excludeTransRef, CancellationToken ct)
    public async Task<ValidationResult> ValidateAsync(
        string? transRef,
        decimal? amount,
        string? excludeTransRef,
        CancellationToken ct = default)
    {
        // The current implementation uses excludeId, but the interface expects excludeTransRef.
        // For now, just call the existing implementation, ignoring excludeTransRef.
        // If excludeTransRef logic is needed, you should implement it here.
        return await ValidateAsync(transRef, amount, (long?)null, ct);
    }
}
