using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("FILE_UPLOAD")]
public class FileUpload
{
    [Key]
    [Required]
    [MaxLength(50)]
    [Column("FILE_UPLOAD_ID")]
    public string FileId { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(500)]
    [Column("FILE_PHYSICAL")]
    public string FilePhysical { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("FILE_NAME_UPLOAD")]
    public string? FileNameUpload { get; set; }

    [MaxLength(1000)]
    [Column("FILE_PATH")]
    public string? FilePath { get; set; }

    [MaxLength(10)]
    [Column("FILE_EXTENSION")]
    public string? FileExtension { get; set; }

    [Column("FILE_SIZE")]
    public long FileSize { get; set; } = 0;

    [Required]
    [MaxLength(64)]
    [Column("FILE_HASH", TypeName = "varchar(64)")]
    public string FileHash { get; set; } = string.Empty;

    [Column("BATCH_ID")]
    public Guid BatchId { get; set; }

    [Column("LAST_PROCESSED_BATCH")]
    public int LastProcessedBatch { get; set; } = 0;

    [Column("STATUS")]
    public int Status { get; set; } = 0;

    [Column("VALIDATION_LEVEL")]
    public int ValidationLevel { get; set; } = 0;

    [Column("VALIDATION_ERRORS")]
    public string? ValidationErrors { get; set; }

    [Column("TRANSACTION_COUNT")]
    public int TransactionCount { get; set; } = 0;

    [Column("SUCCESS_COUNT")]
    public int SuccessCount { get; set; } = 0;

    [Column("ERROR_COUNT")]
    public int ErrorCount { get; set; } = 0;

    [MaxLength(2000)]
    [Column("ERROR_MESSAGE")]
    public string? ErrorMessage { get; set; }

    [Column("STARTED_AT")]
    public DateTime? StartedAt { get; set; }

    [Column("COMPLETED_AT")]
    public DateTime? CompletedAt { get; set; }

    [Column("PROCESSING_TIME_MS")]
    public long? ProcessingTimeMs { get; set; }

    [Column("CREATED_DATE")]
    public DateTime? CreatedDate { get; set; } = DateTime.Now;

    [MaxLength(64)]
    [Column("CREATED_BY")]
    public string? CreatedBy { get; set; }

    [Column("UPDATED_DATE")]
    public DateTime? UpdatedDate { get; set; }

    [MaxLength(64)]
    [Column("UPDATED_BY")]
    public string? UpdatedBy { get; set; }
}
