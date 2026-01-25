using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VCBMoney.Core.Entities;

/// <summary>
/// TRANSACTION_IMPORT - CHÍNH XÁC theo SQL Schema (Table name là [TRANSACTION_IMPORT])
/// Lưu ý: Nhiều column là nvarchar(4000) theo SQL Schema gốc
/// </summary>
[Table("TRANSACTION_IMPORT")]
public class TransactionImport
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("STATUS")]
    public int Status { get; set; } = 0;
    
    [MaxLength(4000)]
    [Column("TRANS_REF")]
    public string? TransRef { get; set; }
    
    [MaxLength(4000)]
    [Column("DEBIT_ACCOUNT_NO")]
    public string? DebitAccountNo { get; set; }
    
    /// <summary>SQL: [DEBIT_AMOUNT] [nvarchar](4000) NULL - lưu dạng string</summary>
    [MaxLength(4000)]
    [Column("DEBIT_AMOUNT")]
    public string? DebitAmount { get; set; }
    
    [MaxLength(50)]
    [Column("DEBIT_AMOUNT_RAW")]
    public string? DebitAmountRaw { get; set; }
    
    [MaxLength(3)]
    [Column("DEBIT_CURRENCY")]
    public string DebitCurrency { get; set; } = "VND";
    
    [MaxLength(4000)]
    [Column("BEN_ACCOUNT_NO")]
    public string? BenAccountNo { get; set; }
    
    [MaxLength(4000)]
    [Column("BEN_ACCOUNT_NAME")]
    public string? BenAccountName { get; set; }
    
    [MaxLength(500)]
    [Column("BEN_ACCOUNT_NAME_RAW")]
    public string? BenAccountNameRaw { get; set; }
    
    [MaxLength(4000)]
    [Column("BEN_BANK")]
    public string? BenBank { get; set; }
    
    [MaxLength(500)]
    [Column("BEN_BANK_RAW")]
    public string? BenBankRaw { get; set; }
    
    [MaxLength(4000)]
    [Column("BEN_ADDRESS")]
    public string? BenAddress { get; set; }
    
    [MaxLength(4000)]
    [Column("BEN_IDNO")]
    public string? BenIdNo { get; set; }
    
    [MaxLength(4000)]
    [Column("BEN_IDNO_CREATE_DATE")]
    public string? BenIdNoCreateDate { get; set; }
    
    [MaxLength(4000)]
    [Column("BEN_IDNO_PLACE")]
    public string? BenIdNoPlace { get; set; }
    
    [MaxLength(4000)]
    [Column("CONTENT")]
    public string? Content { get; set; }
    
    [MaxLength(1000)]
    [Column("CONTENT_RAW")]
    public string? ContentRaw { get; set; }
    
    [MaxLength(4000)]
    [Column("CHARGE")]
    public string? Charge { get; set; }
    
    /// <summary>SQL: [VALUE_DATE] [date] NULL</summary>
    [Column("VALUE_DATE", TypeName = "date")]
    public DateTime? ValueDate { get; set; }
    
    [MaxLength(4000)]
    [Column("PRIORITY")]
    public string? Priority { get; set; }
    
    [MaxLength(4000)]
    [Column("ORDERING_CUST")]
    public string? OrderingCust { get; set; }
    
    [MaxLength(4000)]
    [Column("ORDERING_CUST_ACCOUNT_NO")]
    public string? OrderingCustAccountNo { get; set; }
    
    [MaxLength(4000)]
    [Column("ORDERING_CUST_ADDRESS")]
    public string? OrderingCustAddress { get; set; }
    
    [Column("CHECK_BEN_NAME")]
    public bool CheckBenName { get; set; } = false;
    
    [MaxLength(20)]
    [Column("SOURCE_TYPE", TypeName = "varchar(20)")]
    public string SourceType { get; set; } = "FILE";
    
    [MaxLength(100)]
    [Column("SOURCE_REF")]
    public string? SourceRef { get; set; }
    
    [MaxLength(36)]
    [Column("FILE_UPLOAD_ID")]
    public string? FileUploadId { get; set; }
    
    [Column("VALIDATION_ERRORS")]
    public string? ValidationErrors { get; set; }
    
    [Column("VALIDATION_WARNINGS")]
    public string? ValidationWarnings { get; set; }
    
    [MaxLength(20)]
    [Column("ERR_CODE")]
    public string? ErrCode { get; set; }
    
    [MaxLength(2000)]
    [Column("ERR_DESC")]
    public string? ErrDesc { get; set; }
    
    [Column("ERR_TRANS_REF")]
    public bool ErrTransRef { get; set; } = false;
    
    [Column("ERR_AMOUNT")]
    public bool ErrAmount { get; set; } = false;
    
    [Column("ERR_DEBIT_ACCOUNT")]
    public bool ErrDebitAccount { get; set; } = false;
    
    [Column("ERR_BEN_ACCOUNT")]
    public bool ErrBenAccount { get; set; } = false;
    
    [Column("ERR_BEN_NAME")]
    public bool ErrBenName { get; set; } = false;
    
    [Column("ERR_BEN_BANK")]
    public bool ErrBenBank { get; set; } = false;
    
    [Column("ERR_CONTENT")]
    public bool ErrContent { get; set; } = false;
    
    [Column("ERR_VALUE_DATE")]
    public bool ErrValueDate { get; set; } = false;
    
    [Column("ERR_DUPLICATE")]
    public bool ErrDuplicate { get; set; } = false;
    
    [Column("ERR_BUSINESS")]
    public bool ErrBusiness { get; set; } = false;
    
    [Column("HAS_FORMAT_ERROR")]
    public bool HasFormatError { get; set; } = false;
    
    [Column("HAS_LENGTH_WARNING")]
    public bool HasLengthWarning { get; set; } = false;
    
    [Column("BEN_NAME_TRUNCATED")]
    public bool BenNameTruncated { get; set; } = false;
    
    [Column("CONTENT_TRUNCATED")]
    public bool ContentTruncated { get; set; } = false;
    
    [Column("RETRY_COUNT")]
    public int RetryCount { get; set; } = 0;
    
    [Column("LAST_RETRY_DATE")]
    public DateTime? LastRetryDate { get; set; }
    
    [Column("MOVED_TO_TRANSACTION_ID")]
    public long? MovedToTransactionId { get; set; }
    
    [Column("FAILED_LEVEL")]
    public int? FailedLevel { get; set; }
    
    [Column("FILE_STATUS")]
    public int FileStatus { get; set; } = 0;
    
    [Required]
    [Column("CREATED_DATE")]
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    [Required]
    [MaxLength(64)]
    [Column("CREATED_BY")]
    public string CreatedBy { get; set; } = string.Empty;
    
    [Column("UPDATED_DATE")]
    public DateTime? UpdatedDate { get; set; }
    
    [MaxLength(50)]
    [Column("UPDATED_BY")]
    public string? UpdatedBy { get; set; }
}
