using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VCBMoney.Core.Entities;

/// <summary>
/// DOMESTIC_TXT - CHÍNH XÁC theo SQL Schema
/// CREATE TABLE [dbo].[DOMESTIC_TXT](
///     [ID] [bigint] IDENTITY(1,1) PRIMARY KEY,
///     [FIELD_20] [nvarchar](50) NULL,
///     [FIELD_32A] [nvarchar](50) NULL,
///     [FIELD_50K] [nvarchar](500) NULL,
///     [FIELD_50R] [nvarchar](35) NULL,
///     [FIELD_50S] [nvarchar](70) NULL,
///     [FIELD_50T] [nvarchar](70) NULL,
///     [FIELD_53] [nvarchar](35) NULL,
///     [FIELD_57] [nvarchar](70) NULL,
///     [FIELD_57A] [nvarchar](140) NULL,
///     [FIELD_59] [nvarchar](250) NULL,
///     [FIELD_59O] [nvarchar](35) NULL,
///     [FIELD_59P] [nvarchar](20) NULL,
///     [FIELD_59Q] [nvarchar](100) NULL,
///     [FIELD_70] [nvarchar](500) NULL,
///     [FIELD_71A] [nvarchar](3) NULL,
///     [FIELD_72A] [char](1) NULL,
///     [FIELD_72B] [char](1) NULL,
///     [FIELD_72C] [char](1) NULL,
///     [FIELD_72D] [char](1) NULL,
///     [CURRENCY] [nvarchar](3) NULL,
///     [AMOUNT] [decimal](18, 2) NULL,
///     [DEBIT_ACCOUNT] [nvarchar](35) NULL,
///     [BEN_ACCOUNT] [nvarchar](35) NULL,
///     [BEN_NAME] [nvarchar](140) NULL,
///     [STATUS] [int] NOT NULL DEFAULT 0,
///     [ERR_CODE] [nvarchar](20) NULL,
///     [ERR_DESC] [nvarchar](500) NULL,
///     [CREATED_DATE] [datetime] NOT NULL DEFAULT GETDATE(),
///     [CREATED_BY] [nvarchar](64) NULL,
///     [UPDATED_DATE] [datetime] NULL,
///     [UPDATED_BY] [nvarchar](64) NULL,
///     [FILE_UPLOAD_ID] [nvarchar](64) NULL,
///     [VALUE_DATE] [datetime] NULL
/// )
/// </summary>
[Table("DOMESTIC_TXT")]
public class DomesticTxt
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    
    [MaxLength(50)]
    [Column("FIELD_20")]
    public string? Field20 { get; set; }
    
    [MaxLength(50)]
    [Column("FIELD_32A")]
    public string? Field32A { get; set; }
    
    [MaxLength(500)]
    [Column("FIELD_50K")]
    public string? Field50K { get; set; }
    
    [MaxLength(35)]
    [Column("FIELD_50R")]
    public string? Field50R { get; set; }
    
    [MaxLength(70)]
    [Column("FIELD_50S")]
    public string? Field50S { get; set; }
    
    [MaxLength(70)]
    [Column("FIELD_50T")]
    public string? Field50T { get; set; }
    
    [MaxLength(35)]
    [Column("FIELD_53")]
    public string? Field53 { get; set; }
    
    [MaxLength(70)]
    [Column("FIELD_57")]
    public string? Field57 { get; set; }
    
    [MaxLength(140)]
    [Column("FIELD_57A")]
    public string? Field57A { get; set; }
    
    [MaxLength(250)]
    [Column("FIELD_59")]
    public string? Field59 { get; set; }
    
    [MaxLength(35)]
    [Column("FIELD_59O")]
    public string? Field59O { get; set; }
    
    [MaxLength(20)]
    [Column("FIELD_59P")]
    public string? Field59P { get; set; }
    
    [MaxLength(100)]
    [Column("FIELD_59Q")]
    public string? Field59Q { get; set; }
    
    [MaxLength(500)]
    [Column("FIELD_70")]
    public string? Field70 { get; set; }
    
    [MaxLength(3)]
    [Column("FIELD_71A")]
    public string? Field71A { get; set; }
    
    [MaxLength(1)]
    [Column("FIELD_72A", TypeName = "char(1)")]
    public string? Field72A { get; set; }
    
    [MaxLength(1)]
    [Column("FIELD_72B", TypeName = "char(1)")]
    public string? Field72B { get; set; }
    
    [MaxLength(1)]
    [Column("FIELD_72C", TypeName = "char(1)")]
    public string? Field72C { get; set; }
    
    [MaxLength(1)]
    [Column("FIELD_72D", TypeName = "char(1)")]
    public string? Field72D { get; set; }
    
    [MaxLength(3)]
    [Column("CURRENCY")]
    public string? Currency { get; set; }
    
    [Column("AMOUNT", TypeName = "decimal(18,2)")]
    public decimal? Amount { get; set; }
    
    [MaxLength(35)]
    [Column("DEBIT_ACCOUNT")]
    public string? DebitAccount { get; set; }
    
    [MaxLength(35)]
    [Column("BEN_ACCOUNT")]
    public string? BenAccount { get; set; }
    
    [MaxLength(140)]
    [Column("BEN_NAME")]
    public string? BenName { get; set; }
    
    [Required]
    [Column("STATUS")]
    public int Status { get; set; } = 0;
    
    [MaxLength(20)]
    [Column("ERR_CODE")]
    public string? ErrCode { get; set; }
    
    [MaxLength(500)]
    [Column("ERR_DESC")]
    public string? ErrDesc { get; set; }
    
    [Required]
    [Column("CREATED_DATE")]
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    [MaxLength(64)]
    [Column("CREATED_BY")]
    public string? CreatedBy { get; set; }
    
    [Column("UPDATED_DATE")]
    public DateTime? UpdatedDate { get; set; }
    
    [MaxLength(64)]
    [Column("UPDATED_BY")]
    public string? UpdatedBy { get; set; }
    
    [MaxLength(50)]
    [Column("FILE_UPLOAD_ID")]
    public string? FileUploadId { get; set; }
    
    [Column("VALUE_DATE")]
    public DateTime? ValueDate { get; set; }
}
