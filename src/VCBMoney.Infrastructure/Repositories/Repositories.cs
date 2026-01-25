using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VCBMoney.Core.Entities;
using VCBMoney.Core.Enums;
using VCBMoney.Core.Interfaces;
using VCBMoney.Infrastructure.Data;

namespace VCBMoney.Infrastructure.Repositories;

#region Generic Repository

public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly DbContext Context;
    protected readonly DbSet<TEntity> DbSet;

    public Repository(DbContext context)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
    }

    public virtual async Task<TEntity?> GetByIdAsync(object id, CancellationToken ct = default)
        => await DbSet.FindAsync(new[] { id }, ct);

    public virtual async Task<IList<TEntity>> GetAllAsync(CancellationToken ct = default)
        => await DbSet.ToListAsync(ct);

    public virtual async Task<IList<TEntity>> GetAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => await DbSet.Where(predicate).ToListAsync(ct);

    public virtual async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(predicate, ct);

    public virtual async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => await DbSet.AnyAsync(predicate, ct);

    public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => await DbSet.CountAsync(predicate, ct);

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await DbSet.AddAsync(entity, ct);
        await Context.SaveChangesAsync(ct);
        return entity;
    }

    public virtual async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        DbSet.Update(entity);
        await Context.SaveChangesAsync(ct);
    }

    public virtual async Task DeleteAsync(TEntity entity, CancellationToken ct = default)
    {
        DbSet.Remove(entity);
        await Context.SaveChangesAsync(ct);
    }
}

#endregion

#region FileUpload Repository - Key là string FileId

public class FileUploadRepository : Repository<FileUpload>, IFileUploadRepository
{
    public FileUploadRepository(VcbMoneyDbContext context) : base(context) { }

    public async Task<FileUpload?> GetByIdAsync(string fileId, CancellationToken ct = default)
        => await DbSet.FindAsync(new object[] { fileId }, ct);

    public async Task<FileUpload?> GetByHashAsync(string hash, CancellationToken ct = default)
        => await DbSet.AsNoTracking().FirstOrDefaultAsync(f => f.FileHash == hash, ct);

    public async Task<IList<FileUpload>> GetByStatusAsync(int status, int take = 100, CancellationToken ct = default)
        => await DbSet
            .Where(f => f.Status == status)
            .OrderBy(f => f.CreatedDate)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IList<FileUpload>> GetValidFilesForParsingAsync(int take = 50, CancellationToken ct = default)
        => await DbSet
            .Where(f => f.Status == (int)FileStatus.Valid && f.ValidationLevel >= 2)
            .OrderBy(f => f.CreatedDate)
            .Take(take)
            .ToListAsync(ct);
}

#endregion

#region Transaction Repository - Key là string TransRef

public class TransactionRepository : Repository<Transaction>, ITransactionRepository
{
    private readonly VcbMoneyDbContext _context;
    
    public TransactionRepository(VcbMoneyDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<Transaction?> GetByTransRefAsync(string transRef, CancellationToken ct = default)
        => await DbSet.FindAsync(new object[] { transRef }, ct);

    public async Task<bool> ExistsByTransRefAsync(string transRef, CancellationToken ct = default)
        => await DbSet.AsNoTracking().AnyAsync(t => t.TransRef == transRef, ct);

    public async Task<IList<Transaction>> GetReadyForSyncAsync(int take = 100, CancellationToken ct = default)
        => await DbSet
            .Where(t => t.Status == 0) // Ready
            .OrderBy(t => t.CreatedDate)
            .Take(take)
            .ToListAsync(ct);

    public async Task<DashboardStats> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;
        
        return new DashboardStats
        {
            Files = new FileStats
            {
                Total = await _context.FileUploads.CountAsync(ct),
                Pending = await _context.FileUploads.CountAsync(f => f.Status == 0, ct),
                Processing = await _context.FileUploads.CountAsync(f => f.Status == 2, ct),
                Done = await _context.FileUploads.CountAsync(f => f.Status == 3, ct),
                Error = await _context.FileUploads.CountAsync(f => f.Status < 0, ct)
            },
            Transactions = new TransactionStats
            {
                Total = await DbSet.CountAsync(ct),
                Ready = await DbSet.CountAsync(t => t.Status == 0, ct),
                Synced = await DbSet.CountAsync(t => t.Status == 1, ct),
                TotalAmount = await DbSet.SumAsync(t => t.DebitAmount ?? 0, ct)
            },
            Imports = new ImportStats
            {
                Total = await _context.TransactionImports.CountAsync(ct),
                Pending = await _context.TransactionImports.CountAsync(t => t.Status == 0, ct),
                Fixed = await _context.TransactionImports.CountAsync(t => t.Status == 1, ct),
                Reprocessed = await _context.TransactionImports.CountAsync(t => t.Status == 2, ct),
                Rejected = await _context.TransactionImports.CountAsync(t => t.Status == -1, ct)
            },
            Today = new TodayStats
            {
                FilesProcessed = await _context.FileUploads.CountAsync(f => f.CompletedAt >= today, ct),
                TransactionsCreated = await DbSet.CountAsync(t => t.CreatedDate >= today, ct),
                TransactionsSynced = await DbSet.CountAsync(t => t.SyncDate >= today, ct),
                ErrorsFound = await _context.TransactionImports.CountAsync(t => t.CreatedDate >= today, ct),
                AmountProcessed = await DbSet.Where(t => t.CreatedDate >= today).SumAsync(t => t.DebitAmount ?? 0, ct)
            }
        };
    }

    public async Task<ChartData> GetDailyChartAsync(int days = 7, CancellationToken ct = default)
    {
        var startDate = DateTime.Today.AddDays(-days + 1);
        var labels = new List<string>();
        var validData = new List<decimal>();
        var invalidData = new List<decimal>();
        var syncedData = new List<decimal>();

        for (int i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);
            var nextDate = date.AddDays(1);
            labels.Add(date.ToString("dd/MM"));

            validData.Add(await DbSet.CountAsync(t => t.CreatedDate >= date && t.CreatedDate < nextDate, ct));
            invalidData.Add(await _context.TransactionImports.CountAsync(t => t.CreatedDate >= date && t.CreatedDate < nextDate, ct));
            syncedData.Add(await DbSet.CountAsync(t => t.SyncDate >= date && t.SyncDate < nextDate, ct));
        }

        return new ChartData
        {
            Labels = labels,
            Series = new List<ChartSeries>
            {
                new() { Name = "Valid", Data = validData },
                new() { Name = "Invalid", Data = invalidData },
                new() { Name = "Synced", Data = syncedData }
            }
        };
    }
}

#endregion

#region TransactionImport Repository

public class TransactionImportRepository : Repository<TransactionImport>, ITransactionImportRepository
{
    public TransactionImportRepository(VcbMoneyDbContext context) : base(context) { }

    public async Task<IList<TransactionImport>> GetPendingAsync(int take = 100, CancellationToken ct = default)
        => await DbSet
            .Where(t => t.Status == 0) // Pending
            .OrderBy(t => t.CreatedDate)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IList<TransactionImport>> GetByFailedLevelAsync(int level, int take = 100, CancellationToken ct = default)
        => await DbSet
            .Where(t => t.Status == 0 && t.FailedLevel == level)
            .OrderBy(t => t.CreatedDate)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IList<TransactionImport>> GetForRetryAsync(int maxRetry, int delayMinutes, int take = 50, CancellationToken ct = default)
    {
        var cutoffTime = DateTime.Now.AddMinutes(-delayMinutes);
        return await DbSet
            .Where(t => t.Status == 0 
                     && t.FailedLevel == 5 
                     && t.ErrDuplicate == true
                     && t.RetryCount < maxRetry
                     && (t.LastRetryDate == null || t.LastRetryDate < cutoffTime))
            .OrderBy(t => t.RetryCount)
            .ThenBy(t => t.CreatedDate)
            .Take(take)
            .ToListAsync(ct);
    }
}

#endregion

#region PayOrder Repository

public class PayOrderRepository : Repository<PayOrder>, IPayOrderRepository
{
    public PayOrderRepository(TrungGianDbContext context) : base(context) { }

    public async Task<IList<PayOrder>> GetNewPayOrdersAsync(int take = 100, CancellationToken ct = default)
        => await DbSet
            .Where(p => p.UpvcbMoney == null || p.UpvcbMoney == (short)PayOrderStatus.New)
            .OrderBy(p => p.Ngay)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IList<PayOrder>> GetValidPayOrdersAsync(int take = 100, CancellationToken ct = default)
        => await DbSet
            .Where(p => p.UpvcbMoney == (short)PayOrderStatus.Valid)
            .OrderBy(p => p.Ngay)
            .Take(take)
            .ToListAsync(ct);
}

#endregion

#region DomesticResult Repository

public class DomesticResultRepository : Repository<DomesticResult>, IDomesticResultRepository
{
    public DomesticResultRepository(TrungGianDbContext context) : base(context) { }

    public async Task<bool> ExistsByTransRefAsync(string transRef, CancellationToken ct = default)
        => await DbSet.AsNoTracking().AnyAsync(r => r.TransRef == transRef, ct);
}

#endregion
