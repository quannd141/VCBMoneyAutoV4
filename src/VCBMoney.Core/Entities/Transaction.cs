using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VCBMoney.Core.Entities;

/// <summary>
/// TRANSACTION - CHÍNH XÁC theo SQL Schema (Table name là [TRANSACTION] không phải TRANSACTIONS)
/// Primary Key là TRANS_REF nvarchar(21) NOT NULL
/// </summary>
[Table("TRANSACTION")]
public class Transaction
{
    [Key]
    [Required]
    [MaxLength(21)]
    [Column("TRANS_REF")]
    public string TransRef { get; set; } = string.Empty;
    
    [MaxLength(64)]
    [Column("BATCH_TRANS_REF")]
    public string? BatchTransRef { get; set; }
    
    [MaxLength(64)]
    [Column("BATCH_BANK_TRANS_REF")]
    public string? BatchBankTransRef { get; set; }
    
    [Column("RETRY_NUMBER")]
    public int? RetryNumber { get; set; }
    
    [Column("SYNC_STATUS")]
    public int? SyncStatus { get; set; }
    
    [Column("SYNC_DATE")]
    public DateTime? SyncDate { get; set; }
    
    [Column("SYNC_REPOND", TypeName = "ntext")]
    public string? SyncRepond { get; set; }
    
    [MaxLength(550)]
    [Column("REMARK")]
    public string? Remark { get; set; }
    
    [Column("CREDIT_AMOUNT", TypeName = "decimal(20,2)")]
    public decimal? CreditAmount { get; set; }
    
    [MaxLength(3)]
    [Column("CREDIT_CURRENCY")]
    public string? CreditCurrency { get; set; }
    
    [Column("FEE_AMOUNT", TypeName = "decimal(20,2)")]
    public decimal? FeeAmount { get; set; }
    
    [MaxLength(3)]
    [Column("FEE_CURRENCY")]
    public string? FeeCurrency { get; set; }
    
    [Column("VAT_AMOUNT", TypeName = "decimal(20,2)")]
    public decimal? VatAmount { get; set; }
    
    [MaxLength(10)]
    [Column("TELLER_ID")]
    public string? TellerId { get; set; }
    
    [Column("SEQUENCE")]
    public int? Sequence { get; set; }
    
    [Column("HOST_DATE")]
    public DateTime? HostDate { get; set; }
    
    [MaxLength(10)]
    [Column("TELLER_BANCH")]
    public string? TellerBranch { get; set; }
    
    [MaxLength(10)]
    [Column("PC_TIME")]
    public string? PcTime { get; set; }
    
    [Column("PROCESS_TRANS_ID")]
    public int? ProcessTransId { get; set; }
    
    [MaxLength(30)]
    [Column("BANK_TRANS_REF")]
    public string? BankTransRef { get; set; }
    
    [MaxLength(64)]
    [Column("GROUP_TRANS_NO")]
    public string? GroupTransNo { get; set; }
    
    [MaxLength(20)]
    [Column("CIF_NO")]
    public string? CifNo { get; set; }
    
    [Column("TRANSFER_TYPE")]
    public int? TransferType { get; set; }
    
    [Column("VALUE_DATE")]
    public DateTime? ValueDate { get; set; }
    
    [Column("PRIORITY")]
    public int? Priority { get; set; }
    
    [Required]
    [MaxLength(15)]
    [Column("DEBIT_ACCOUNT_NO")]
    public string DebitAccountNo { get; set; } = string.Empty;
    
    [MaxLength(50)]
    [Column("DEBIT_ACCOUNT_NAME")]
    public string? DebitAccountName { get; set; }
    
    [MaxLength(60)]
    [Column("DEBIT_BANK")]
    public string? DebitBank { get; set; }
    
    [MaxLength(10)]
    [Column("DEBIT_BRANCH_NO")]
    public string? DebitBranchNo { get; set; }
    
    [MaxLength(3)]
    [Column("BEN_CURRENCY")]
    public string? BenCurrency { get; set; }
    
    [Column("DEBIT_AMOUNT", TypeName = "decimal(20,2)")]
    public decimal? DebitAmount { get; set; }
    
    [MaxLength(3)]
    [Column("DEBIT_CURRENCY")]
    public string? DebitCurrency { get; set; }
    
    [MaxLength(3)]
    [Column("CHARGE")]
    public string? Charge { get; set; }
    
    [MaxLength(255)]
    [Column("CONTENT")]
    public string? Content { get; set; }
    
    [MaxLength(20)]
    [Column("PAYMENT_TYPE")]
    public string? PaymentType { get; set; }
    
    [MaxLength(50)]
    [Column("BEN_ACCOUNT_NO")]
    public string? BenAccountNo { get; set; }
    
    [MaxLength(200)]
    [Column("BEN_ACCOUNT_NAME")]
    public string? BenAccountName { get; set; }
    
    [MaxLength(200)]
    [Column("BEN_ADDRESS")]
    public string? BenAddress { get; set; }
    
    [MaxLength(200)]
    [Column("BEN_ADDRESS2")]
    public string? BenAddress2 { get; set; }
    
    [MaxLength(15)]
    [Column("BEN_BANK_CODE")]
    public string? BenBankCode { get; set; }
    
    [MaxLength(150)]
    [Column("BEN_BANK")]
    public string? BenBank { get; set; }
    
    [MaxLength(35)]
    [Column("BEN_IDNO")]
    public string? BenIdNo { get; set; }
    
    [Column("BEN_IDNO_CREATE_DATE")]
    public DateTime? BenIdNoCreateDate { get; set; }
    
    [MaxLength(100)]
    [Column("BEN_IDNO_PLACE")]
    public string? BenIdNoPlace { get; set; }
    
    [MaxLength(255)]
    [Column("ORDERING_CUST")]
    public string? OrderingCust { get; set; }
    
    [MaxLength(35)]
    [Column("ORDERING_CUST_ACCOUNT_NO")]
    public string? OrderingCustAccountNo { get; set; }
    
    [MaxLength(100)]
    [Column("ORDERING_CUST_ADDRESS")]
    public string? OrderingCustAddress { get; set; }
    
    [MaxLength(100)]
    [Column("ORDERING_CUST_ADDRESS2")]
    public string? OrderingCustAddress2 { get; set; }
    
    [Required]
    [Column("STATUS")]
    public int Status { get; set; }
    
    [MaxLength(64)]
    [Column("CHECKED_BY")]
    public string? CheckedBy { get; set; }
    
    [Column("CHECKED_DATE")]
    public DateTime? CheckedDate { get; set; }
    
    [MaxLength(1500)]
    [Column("CHECKED_NOTE")]
    public string? CheckedNote { get; set; }
    
    [MaxLength(64)]
    [Column("VERIFIED_BY")]
    public string? VerifiedBy { get; set; }
    
    [Column("VERIFIED_DATE")]
    public DateTime? VerifiedDate { get; set; }
    
    [MaxLength(64)]
    [Column("APPROVED_BY")]
    public string? ApprovedBy { get; set; }
    
    [Column("APPROVED_DATE")]
    public DateTime? ApprovedDate { get; set; }
    
    [MaxLength(1500)]
    [Column("APPROVED_NOTE")]
    public string? ApprovedNote { get; set; }
    
    [Column("HASH_SIGN", TypeName = "ntext")]
    public string? HashSign { get; set; }
    
    [MaxLength(100)]
    [Column("SERIAL_NUMBER")]
    public string? SerialNumber { get; set; }
    
    [Column("RESPOND", TypeName = "ntext")]
    public string? Respond { get; set; }
    
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
    
    [MaxLength(200)]
    [Column("BEN_ACCOUNT_NAME_INQUIRY")]
    public string? BenAccountNameInquiry { get; set; }
    
    [Column("CANCEL_REQUEST_NUMBER")]
    public int? CancelRequestNumber { get; set; }
    
    [MaxLength(10)]
    [Column("FLAG_TO_BANK")]
    public string? FlagToBank { get; set; }
    
    [MaxLength(250)]
    [Column("CANCEL_REASON")]
    public string? CancelReason { get; set; }
    
    [MaxLength(64)]
    [Column("CREATE_TYPE")]
    public string? CreateType { get; set; }
    
    [Column("ALLOW_EDIT_TRANS_AUTO")]
    public bool? AllowEditTransAuto { get; set; }
    
    [MaxLength(64)]
    [Column("TRANS_TIME")]
    public string? TransTime { get; set; }
    
    [MaxLength(3)]
    [Column("VAT_CURRENCY")]
    public string? VatCurrency { get; set; }
    
    [Column("INQUIRITY_STATUS")]
    public int? InquiryStatus { get; set; }
    
    [MaxLength(20)]
    [Column("SOURCE_TYPE", TypeName = "varchar(20)")]
    public string? SourceType { get; set; } = "FILE";
    
    [MaxLength(36)]
    [Column("FILE_UPLOAD_ID")]
    public string? FileUploadId { get; set; }
    
    [Column("CHECK_BEN_NAME")]
    public bool CheckBenName { get; set; } = false;
    
    [MaxLength(100)]
    [Column("SOURCE_REF", TypeName = "varchar(100)")]
    public string? SourceRef { get; set; }
}
