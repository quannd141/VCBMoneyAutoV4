using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VCBMoney.Core.Entities;

#region PayOrder - DBTRUNGGIAN

[Table("PAYORDER")]
public class PayOrder
{
    [Key]
    [Required]
    [MaxLength(20)]
    [Column("SOGD")]
    public string SoGd { get; set; } = string.Empty;
    
    [Column("NGAY")]
    public DateTime Ngay { get; set; }
    
    [Required]
    [MaxLength(15)]
    [Column("TKNO")]
    public string TkNo { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(3)]
    [Column("LNT")]
    public string Lnt { get; set; } = "VND";
    
    [Column("SOTIEN")]
    public double SoTien { get; set; }
    
    [Column("UUTIEN")]
    public short? UuTien { get; set; }
    
    [MaxLength(35)]
    [Column("TKNH")]
    public string? TkNh { get; set; }
    
    [MaxLength(144)]
    [Column("TENNH")]
    public string? TenNh { get; set; }
    
    [MaxLength(70)]
    [Column("DCNH")]
    public string? DcNh { get; set; }
    
    [MaxLength(3)]
    [Column("PHI")]
    public string? Phi { get; set; }
    
    [MaxLength(100)]
    [Column("NHNH")]
    public string? NhNh { get; set; }
    
    [MaxLength(140)]
    [Column("ND")]
    public string? Nd { get; set; }
    
    [MaxLength(144)]
    [Column("TENNRL")]
    public string? TenNrl { get; set; }
    
    [MaxLength(180)]
    [Column("DCNRL")]
    public string? DcNrl { get; set; }
    
    [MaxLength(34)]
    [Column("SOTK_NRL")]
    public string? SoTkNrl { get; set; }
    
    [MaxLength(35)]
    [Column("BEN_IDNO")]
    public string? BenIdNo { get; set; }
    
    [MaxLength(20)]
    [Column("BEN_ID_ISSUE_DATE")]
    public string? BenIdIssueDate { get; set; }
    
    [MaxLength(100)]
    [Column("BEN_ID_ISSUE_PLACE")]
    public string? BenIdIssuePlace { get; set; }
    
    [Column("BANK_TO_BANK")]
    public bool? BankToBank { get; set; }
    
    [Column("BANK_TO_BANK_SBV")]
    public bool? BankToBankSbv { get; set; }
    
    [Column("INQUIRY_BEN_NAME")]
    public bool? InquiryBenName { get; set; }
    
    [Column("UPVCBMONEY")]
    public short? UpvcbMoney { get; set; }
    
    [MaxLength(3)]
    [Column("ErrCode")]
    public string? ErrCode { get; set; }
    
    [MaxLength(250)]
    [Column("ErrDes")]
    public string? ErrDes { get; set; }
    
    [MaxLength(100)]
    [Column("upDatetime")]
    public string? UpDatetime { get; set; }
    
    [MaxLength(50)]
    [Column("CMT_NRL")]
    public string? CmtNrl { get; set; }
    
    [MaxLength(100)]
    [Column("TENTKNO")]
    public string? TenTkNo { get; set; }
}

#endregion

#region DomesticResult - DBTRUNGGIAN

[Table("DOMESTIC_RESULT")]
public class DomesticResult
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    
    [Required]
    [MaxLength(20)]
    [Column("CUSTOMER_CODE")]
    public string CustomerCode { get; set; } = string.Empty;
    
    [MaxLength(50)]
    [Column("TRANS_REF")]
    public string? TransRef { get; set; }
    
    [MaxLength(30)]
    [Column("BANK_TRAN_REF")]
    public string? BankTransRef { get; set; }
    
    [MaxLength(20)]
    [Column("DEBIT_ACCOUNT")]
    public string? DebitAccount { get; set; }
    
    [MaxLength(200)]
    [Column("DEBIT_NAME")]
    public string? DebitName { get; set; }
    
    [Column("AMOUNT", TypeName = "decimal(20,2)")]
    public decimal? Amount { get; set; }
    
    [MaxLength(3)]
    [Column("CURRENCY")]
    public string Currency { get; set; } = "VND";
    
    [MaxLength(500)]
    [Column("CONTENT")]
    public string? Content { get; set; }
    
    [Required]
    [MaxLength(50)]
    [Column("BEN_ACCOUNT_NO")]
    public string BenAccountNo { get; set; } = string.Empty;
    
    [MaxLength(200)]
    [Column("BEN_ACCOUNT_NAME")]
    public string? BenAccountName { get; set; }
    
    [Required]
    [MaxLength(150)]
    [Column("BEN_BANK")]
    public string BenBank { get; set; } = string.Empty;
    
    [MaxLength(15)]
    [Column("BEN_BANK_CODE")]
    public string? BenBankCode { get; set; }
    
    [MaxLength(3)]
    [Column("BEN_CURRENCY")]
    public string BenCurrency { get; set; } = "VND";
    
    [MaxLength(200)]
    [Column("BEN_ADDRESS")]
    public string? BenAddress { get; set; }
    
    [MaxLength(35)]
    [Column("BEN_IDNO")]
    public string? BenIdNo { get; set; }
    
    [MaxLength(10)]
    [Column("CHARGE")]
    public string? Charge { get; set; }
    
    [Column("VALUE_DATE")]
    public DateTime? ValueDate { get; set; }
    
    [MaxLength(10)]
    [Column("PRIORITY")]
    public string? Priority { get; set; }
    
    [Required]
    [MaxLength(10)]
    [Column("STATUS")]
    public string Status { get; set; } = "201";
    
    [MaxLength(550)]
    [Column("BANK_REMARK")]
    public string? Remark { get; set; }
    
    [Column("IMPORTED_TIME")]
    public DateTime ImportedTime { get; set; } = DateTime.Now;
    
    [Column("CREATED_DATE")]
    public DateTime? CreatedDate { get; set; }
    
    [MaxLength(64)]
    [Column("CREATED_BY")]
    public string? CreatedBy { get; set; }
    
    [Column("UPDATED_DATE")]
    public DateTime? UpdatedDate { get; set; }
    
    [MaxLength(64)]
    [Column("UPDATED_BY")]
    public string? UpdatedBy { get; set; }
}

#endregion

#region AuditLog

[Table("AUDIT_LOG")]
public class AuditLog
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    [Column("ACTION")]
    public string Action { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    [Column("ENTITY_TYPE")]
    public string EntityType { get; set; } = string.Empty;
    
    [Column("ENTITY_ID")]
    public long EntityId { get; set; }
    
    [MaxLength(4000)]
    [Column("OLD_VALUES")]
    public string? OldValues { get; set; }
    
    [MaxLength(4000)]
    [Column("NEW_VALUES")]
    public string? NewValues { get; set; }
    
    [MaxLength(64)]
    [Column("USER_NAME")]
    public string? UserName { get; set; }
    
    [MaxLength(50)]
    [Column("IP_ADDRESS")]
    public string? IpAddress { get; set; }
    
    [Column("CREATED_DATE")]
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

#endregion

#region Validation DTOs

public class ValidationError
{
    public int Level { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? OriginalValue { get; set; }
    public string Severity { get; set; } = "Error";
}

public class ValidationResult
{
    public bool IsValid { get; set; } = true;
    public int HighestLevelPassed { get; set; } = 0;
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationError> Warnings { get; set; } = new();
    
    public bool HasCriticalError => Errors.Any(e => e.Severity == "Critical");
    public bool HasError => Errors.Any();
    public bool HasWarning => Warnings.Any();
    
    public void AddError(int level, string field, string code, string message, string? originalValue = null, string severity = "Error")
    {
        Errors.Add(new ValidationError { Level = level, Field = field, Code = code, Message = message, OriginalValue = originalValue, Severity = severity });
        if (severity is "Error" or "Critical") IsValid = false;
    }
    
    public void AddWarning(int level, string field, string code, string message, string? originalValue = null)
    {
        Warnings.Add(new ValidationError { Level = level, Field = field, Code = code, Message = message, OriginalValue = originalValue, Severity = "Warning" });
    }
}

public class ParsedTransaction
{
    public string? Field20 { get; set; }
    public string? Field32A { get; set; }
    public string? Field50K { get; set; }
    public string? Field50R { get; set; }
    public string? Field50S { get; set; }
    public string? Field50T { get; set; }
    public string? Field53 { get; set; }
    public string? Field57 { get; set; }
    public string? Field57A { get; set; }
    public string? Field59 { get; set; }
    public string? Field59O { get; set; }
    public string? Field59P { get; set; }
    public string? Field59Q { get; set; }
    public string? Field70 { get; set; }
    public string? Field71A { get; set; }
    public string? Field72 { get; set; }
    public string? Field72A { get; set; }
    public string? Field72B { get; set; }
    public string? Field72C { get; set; }
    public string? Field72D { get; set; }
    public DateTime? ValueDate { get; set; }
    public string? Currency { get; set; }
    public decimal? Amount { get; set; }
    public ValidationResult Validation { get; set; } = new();
}

public class ParsedMt103 : ParsedTransaction { }

#endregion

#region API DTOs

public class DashboardStats
{
    public FileStats Files { get; set; } = new();
    public TransactionStats Transactions { get; set; } = new();
    public ImportStats Imports { get; set; } = new();
    public TodayStats Today { get; set; } = new();
}

public class FileStats
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Processing { get; set; }
    public int Done { get; set; }
    public int Error { get; set; }
}

public class TransactionStats
{
    public int Total { get; set; }
    public int Ready { get; set; }
    public int Synced { get; set; }
    public decimal TotalAmount { get; set; }
}

public class ImportStats
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Fixed { get; set; }
    public int Reprocessed { get; set; }
    public int Rejected { get; set; }
    public Dictionary<int, int> ByFailedLevel { get; set; } = new();
    public Dictionary<string, int> ByErrorCode { get; set; } = new();
}

public class TodayStats
{
    public int FilesProcessed { get; set; }
    public int TransactionsCreated { get; set; }
    public int TransactionsSynced { get; set; }
    public int ErrorsFound { get; set; }
    public decimal AmountProcessed { get; set; }
}

public class ChartData
{
    public List<string> Labels { get; set; } = new();
    public List<ChartSeries> Series { get; set; } = new();
}

public class ChartSeries
{
    public string Name { get; set; } = string.Empty;
    public List<decimal> Data { get; set; } = new();
}

#endregion
