using Microsoft.EntityFrameworkCore;
using VCBMoney.Core.Entities;

namespace VCBMoney.Infrastructure.Data;

/// <summary>
/// DbContext cho database VCB_MONEY
/// Tables: FILE_UPLOAD, DOMESTIC_TXT, TRANSACTION, TRANSACTION_IMPORT, AUDIT_LOG
/// </summary>
public class VcbMoneyDbContext : DbContext
{
    public VcbMoneyDbContext(DbContextOptions<VcbMoneyDbContext> options) : base(options) { }
    
    public DbSet<FileUpload> FileUploads => Set<FileUpload>();
    public DbSet<DomesticTxt> DomesticTxts => Set<DomesticTxt>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionImport> TransactionImports => Set<TransactionImport>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // FILE_UPLOAD - Primary key là FILE_ID nvarchar(50)
        modelBuilder.Entity<FileUpload>(entity =>
        {
            entity.ToTable("FILE_UPLOAD");
            entity.HasKey(e => e.FileId);
            entity.Property(e => e.FileId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FilePhysical).HasMaxLength(500).IsRequired();
            entity.Property(e => e.FileHash).HasColumnType("varchar(64)").IsRequired();
            entity.HasIndex(e => e.FileHash).HasDatabaseName("IX_FILE_HASH");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_FILE_STATUS");
        });
        
        // DOMESTIC_TXT
        modelBuilder.Entity<DomesticTxt>(entity =>
        {
            entity.ToTable("DOMESTIC_TXT");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.HasIndex(e => e.FileUploadId).HasDatabaseName("IX_DOMESTIC_FILE");
            entity.HasIndex(e => e.Field20).HasDatabaseName("IX_DOMESTIC_REF");
        });
        
        // TRANSACTION - Primary key là TRANS_REF nvarchar(21)
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("TRANSACTION");
            entity.HasKey(e => e.TransRef);
            entity.Property(e => e.TransRef).HasMaxLength(21).IsRequired();
            entity.Property(e => e.DebitAccountNo).HasMaxLength(15).IsRequired();
            entity.Property(e => e.DebitAmount).HasColumnType("decimal(20,2)");
            entity.Property(e => e.CreditAmount).HasColumnType("decimal(20,2)");
            entity.Property(e => e.FeeAmount).HasColumnType("decimal(20,2)");
            entity.Property(e => e.VatAmount).HasColumnType("decimal(20,2)");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_TRANS_STATUS");
            entity.HasIndex(e => e.FileUploadId).HasDatabaseName("IX_TRANS_FILE");
        });
        
        // TRANSACTION_IMPORT - Primary key là ID int identity
        modelBuilder.Entity<TransactionImport>(entity =>
        {
            entity.ToTable("TRANSACTION_IMPORT");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ValueDate).HasColumnType("date");
            entity.HasIndex(e => e.TransRef).HasDatabaseName("IX_IMPORT_REF");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_IMPORT_STATUS");
            entity.HasIndex(e => e.FailedLevel).HasDatabaseName("IX_IMPORT_LEVEL");
        });
        
        // AUDIT_LOG
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AUDIT_LOG");
            entity.HasKey(e => e.Id);
        });
    }
}

/// <summary>
/// DbContext cho database DBTRUNGGIAN
/// Tables: PAYORDER, DOMESTIC_RESULT
/// </summary>
public class TrungGianDbContext : DbContext
{
    public TrungGianDbContext(DbContextOptions<TrungGianDbContext> options) : base(options) { }
    
    public DbSet<PayOrder> PayOrders => Set<PayOrder>();
    public DbSet<DomesticResult> DomesticResults => Set<DomesticResult>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<PayOrder>(entity =>
        {
            entity.ToTable("PAYORDER");
            entity.HasKey(e => e.SoGd);
            entity.HasIndex(e => e.UpvcbMoney).HasDatabaseName("IX_PAYORDER_STATUS");
        });
        
        modelBuilder.Entity<DomesticResult>(entity =>
        {
            entity.ToTable("DOMESTIC_RESULT");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(20,2)");
            entity.HasIndex(e => e.TransRef).HasDatabaseName("IX_RESULT_REF");
        });
    }
}
