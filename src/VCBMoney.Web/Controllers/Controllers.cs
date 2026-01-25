using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VCBMoney.Core.Entities;
using VCBMoney.Core.Interfaces;
using VCBMoney.Infrastructure.Data;
using VCBMoney.Web.Jobs;

namespace VCBMoney.Web.Controllers;

#region Jobs Controller

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    [HttpPost("job1/trigger")]
    public IActionResult TriggerJob1() => Ok(new { jobId = BackgroundJob.Enqueue<Job1_ScanFilesJob>(j => j.ExecuteAsync(CancellationToken.None)), message = "Job1 triggered" });

    [HttpPost("job2/trigger")]
    public IActionResult TriggerJob2() => Ok(new { jobId = BackgroundJob.Enqueue<Job2_ScanPayOrderJob>(j => j.ExecuteAsync(CancellationToken.None)), message = "Job2 triggered" });

    [HttpPost("job3/trigger")]
    public IActionResult TriggerJob3() => Ok(new { jobId = BackgroundJob.Enqueue<Job3_ParseFilesJob>(j => j.ExecuteAsync(CancellationToken.None)), message = "Job3 triggered" });

    [HttpPost("job4/trigger")]
    public IActionResult TriggerJob4() => Ok(new { jobId = BackgroundJob.Enqueue<Job4_TransformPayOrderJob>(j => j.ExecuteAsync(CancellationToken.None)), message = "Job4 triggered" });

    [HttpPost("job5/trigger")]
    public IActionResult TriggerJob5() => Ok(new { jobId = BackgroundJob.Enqueue<Job5_SyncResultJob>(j => j.ExecuteAsync(CancellationToken.None)), message = "Job5 triggered" });

    [HttpPost("job6/trigger")]
    public IActionResult TriggerJob6() => Ok(new { jobId = BackgroundJob.Enqueue<Job6_CleanupJob>(j => j.ExecuteAsync(CancellationToken.None)), message = "Job6 triggered" });

    [HttpPost("all/trigger")]
    public IActionResult TriggerAll()
    {
        var jobs = new List<string>
        {
            BackgroundJob.Enqueue<Job1_ScanFilesJob>(j => j.ExecuteAsync(CancellationToken.None)),
            BackgroundJob.Enqueue<Job2_ScanPayOrderJob>(j => j.ExecuteAsync(CancellationToken.None)),
            BackgroundJob.Enqueue<Job3_ParseFilesJob>(j => j.ExecuteAsync(CancellationToken.None)),
            BackgroundJob.Enqueue<Job4_TransformPayOrderJob>(j => j.ExecuteAsync(CancellationToken.None)),
            BackgroundJob.Enqueue<Job5_SyncResultJob>(j => j.ExecuteAsync(CancellationToken.None))
        };
        return Ok(new { jobIds = jobs, message = "All jobs triggered" });
    }
}

#endregion

#region Transactions Controller

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly VcbMoneyDbContext _context;

    public TransactionsController(VcbMoneyDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] int? status = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var query = _context.Transactions.AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(t => t.TransRef!.Contains(search) || t.BenAccountName!.Contains(search));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedDate)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(t => new
            {
                t.TransRef, t.DebitAccountNo, t.DebitAmount, t.DebitCurrency,
                t.BenAccountNo, t.BenAccountName, t.BenBank, t.Content,
                t.Status, t.ValueDate, t.SyncDate, t.CreatedDate, t.SourceType
            })
            .ToListAsync(ct);

        return Ok(new { total, page, size, items });
    }

    [HttpGet("{transRef}")]
    public async Task<IActionResult> GetByTransRef(string transRef, CancellationToken ct)
    {
        var item = await _context.Transactions.FindAsync(new object[] { transRef }, ct);
        return item == null ? NotFound() : Ok(item);
    }
}

#endregion

#region Invalid Transactions Controller

[ApiController]
[Route("api/invalid-transactions")]
public class InvalidTransactionsController : ControllerBase
{
    private readonly VcbMoneyDbContext _vcbContext;
    private readonly TrungGianDbContext _trungGianContext;
    private readonly IParserService _parserService;
    private readonly IAuditService _auditService;

    public InvalidTransactionsController(
        VcbMoneyDbContext vcbContext,
        TrungGianDbContext trungGianContext,
        IParserService parserService,
        IAuditService auditService)
    {
        _vcbContext = vcbContext;
        _trungGianContext = trungGianContext;
        _parserService = parserService;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] int? status = null,
        [FromQuery] int? failedLevel = null,
        [FromQuery] string? errCode = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var query = _vcbContext.TransactionImports.AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);
        else
            query = query.Where(t => t.Status == 0); // Default: pending only

        if (failedLevel.HasValue)
            query = query.Where(t => t.FailedLevel == failedLevel.Value);

        if (!string.IsNullOrEmpty(errCode))
            query = query.Where(t => t.ErrCode == errCode);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(t => t.TransRef!.Contains(search) || t.BenAccountName!.Contains(search));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedDate)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(t => new
            {
                t.Id, t.TransRef, t.DebitAccountNo, t.DebitAmount, t.DebitCurrency,
                t.BenAccountNo, t.BenAccountName, t.BenBank, t.Content,
                t.Status, t.FailedLevel, t.ErrCode, t.ErrDesc,
                t.ErrTransRef, t.ErrAmount, t.ErrDuplicate, t.ErrBusiness,
                t.RetryCount, t.ValueDate, t.CreatedDate, t.SourceType
            })
            .ToListAsync(ct);

        return Ok(new { total, page, size, items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var item = await _vcbContext.TransactionImports.FindAsync(new object[] { id }, ct);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpGet("{id}/errors")]
    public async Task<IActionResult> GetErrors(long id, CancellationToken ct)
    {
        var item = await _vcbContext.TransactionImports.FindAsync(new object[] { id }, ct);
        if (item == null) return NotFound();

        var errors = string.IsNullOrEmpty(item.ValidationErrors)
            ? new List<ValidationError>()
            : JsonSerializer.Deserialize<List<ValidationError>>(item.ValidationErrors);

        var warnings = string.IsNullOrEmpty(item.ValidationWarnings)
            ? new List<ValidationError>()
            : JsonSerializer.Deserialize<List<ValidationError>>(item.ValidationWarnings);

        return Ok(new { errors, warnings });
    }    
}

public class RejectRequest
{
    public string? Reason { get; set; }
}

public class BulkRequest
{
    public List<long> Ids { get; set; } = new();
}

public class BulkRejectRequest
{
    public List<long> Ids { get; set; } = new();
    public string? Reason { get; set; }
}

#endregion

#region Health Controller

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly VcbMoneyDbContext _vcbContext;
    private readonly TrungGianDbContext _trungGianContext;
    private readonly IConfiguration _configuration;

    public HealthController(VcbMoneyDbContext vcbContext, TrungGianDbContext trungGianContext, IConfiguration configuration)
    {
        _vcbContext = vcbContext;
        _trungGianContext = trungGianContext;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Check(CancellationToken ct)
    {
        var checks = new List<HealthCheckResult>();

        // Check VCB_MONEY database
        try
        {
            await _vcbContext.Database.CanConnectAsync(ct);
            checks.Add(new HealthCheckResult { Name = "VCB_MONEY Database", Status = "Healthy" });
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheckResult { Name = "VCB_MONEY Database", Status = "Unhealthy", Error = ex.Message });
        }

        // Check DBTRUNGGIAN database
        try
        {
            await _trungGianContext.Database.CanConnectAsync(ct);
            checks.Add(new HealthCheckResult { Name = "DBTRUNGGIAN Database", Status = "Healthy" });
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheckResult { Name = "DBTRUNGGIAN Database", Status = "Unhealthy", Error = ex.Message });
        }

        // Check folders
        var folders = new[] { "FolderInput", "FolderProcessing", "FolderArchive", "FolderError" };
        foreach (var folder in folders)
        {
            var path = _configuration[$"VcbMoney:Files:{folder}"];
            if (string.IsNullOrEmpty(path))
            {
                checks.Add(new HealthCheckResult { Name = $"Folder {folder}", Status = "Unhealthy", Error = "Not configured" });
            }
            else if (!Directory.Exists(path))
            {
                checks.Add(new HealthCheckResult { Name = $"Folder {folder}", Status = "Warning", Error = "Does not exist" });
            }
            else
            {
                checks.Add(new HealthCheckResult { Name = $"Folder {folder}", Status = "Healthy", Details = path });
            }
        }

        var overallStatus = checks.All(c => c.Status == "Healthy") ? "Healthy"
            : checks.Any(c => c.Status == "Unhealthy") ? "Unhealthy" : "Degraded";

        return Ok(new
        {
            status = overallStatus,
            timestamp = DateTime.Now,
            checks
        });
    }
}

public class HealthCheckResult
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? Details { get; set; }
}

#endregion

#region Audit Controller

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly VcbMoneyDbContext _context;

    public AuditController(VcbMoneyDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        [FromQuery] string? entityType = null,
        [FromQuery] string? action = null,
        CancellationToken ct = default)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedDate)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return Ok(new { total, page, size, items });
    }
}

#endregion

#region DomesticTxt Controller - Search MT103 Data

/// <summary>
/// API để tìm kiếm dữ liệu MT103 đã parse
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DomesticTxtController : ControllerBase
{
    private readonly VcbMoneyDbContext _context;

    public DomesticTxtController(VcbMoneyDbContext context) => _context = context;

    /// <summary>
    /// Search MT103 records với nhiều filter
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        [FromQuery] string? transRef = null,        // :20: field
        [FromQuery] string? debitAccount = null,    // Tài khoản trích nợ
        [FromQuery] string? benAccount = null,      // Tài khoản người hưởng
        [FromQuery] string? benName = null,         // Tên người hưởng
        [FromQuery] string? content = null,         // Nội dung
        [FromQuery] decimal? amountFrom = null,     // Số tiền từ
        [FromQuery] decimal? amountTo = null,       // Số tiền đến
        [FromQuery] DateTime? dateFrom = null,      // Ngày từ
        [FromQuery] DateTime? dateTo = null,        // Ngày đến
        [FromQuery] int? status = null,             // 0:New, 1:Valid, 2:Transformed, 3:Error
        [FromQuery] string? fileId = null,            // FileUploadId
        CancellationToken ct = default)
    {
        var query = _context.DomesticTxts.AsNoTracking().AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(transRef))
            query = query.Where(d => d.Field20 != null && d.Field20.Contains(transRef));

        if (!string.IsNullOrEmpty(debitAccount))
            query = query.Where(d => d.DebitAccount != null && d.DebitAccount.Contains(debitAccount));

        if (!string.IsNullOrEmpty(benAccount))
            query = query.Where(d => d.BenAccount != null && d.BenAccount.Contains(benAccount));

        if (!string.IsNullOrEmpty(benName))
            query = query.Where(d => d.BenName != null && d.BenName.Contains(benName));

        if (!string.IsNullOrEmpty(content))
            query = query.Where(d => d.Field70 != null && d.Field70.Contains(content));

        if (amountFrom.HasValue)
            query = query.Where(d => d.Amount >= amountFrom);

        if (amountTo.HasValue)
            query = query.Where(d => d.Amount <= amountTo);

        if (dateFrom.HasValue)
            query = query.Where(d => d.ValueDate >= dateFrom);

        if (dateTo.HasValue)
            query = query.Where(d => d.ValueDate <= dateTo);

        if (status.HasValue)
            query = query.Where(d => d.Status == status);

        if (!string.IsNullOrEmpty(fileId))
            query = query.Where(d => d.FileUploadId == fileId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.CreatedDate)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(d => new 
            {
                d.Id,
                d.FileUploadId,
                TransRef = d.Field20,
                d.ValueDate,
                d.Currency,
                d.Amount,
                d.DebitAccount,
                d.BenAccount,
                d.BenName,
                BenBank = d.Field57A,
                Content = d.Field70,
                Charge = d.Field71A,
                d.Status,
                d.ErrCode,
                d.ErrDesc,           
                d.CreatedDate
            })
            .ToListAsync(ct);

        return Ok(new { total, page, size, items });
    }

    /// <summary>
    /// Get detail of one record with all raw fields
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var item = await _context.DomesticTxts
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (item == null)
            return NotFound(new { message = "Record not found" });

        return Ok(item);
    }

    /// <summary>
    /// Get all records by FileUploadId
    /// </summary>
    [HttpGet("by-file/{fileId}")]
    public async Task<IActionResult> GetByFile(string fileId, CancellationToken ct)
    {
        var items = await _context.DomesticTxts
            .AsNoTracking()
            .Where(d => d.FileUploadId == fileId)
            .OrderBy(d => d.Id)
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>
    /// Get statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var today = DateTime.Today;
        
        var stats = new
        {
            Total = await _context.DomesticTxts.CountAsync(ct),
            TotalValid = await _context.DomesticTxts.CountAsync(d => d.Status == 1, ct),
            TotalError = await _context.DomesticTxts.CountAsync(d => d.Status == 3, ct),
            TodayCount = await _context.DomesticTxts.CountAsync(d => d.CreatedDate >= today, ct),
            TodayAmount = await _context.DomesticTxts
                .Where(d => d.CreatedDate >= today && d.Amount.HasValue)
                .SumAsync(d => d.Amount!.Value, ct),
            ByStatus = await _context.DomesticTxts
                .GroupBy(d => d.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct),
            ByCurrency = await _context.DomesticTxts
                .Where(d => d.Currency != null)
                .GroupBy(d => d.Currency)
                .Select(g => new { Currency = g.Key, Count = g.Count() })
                .ToListAsync(ct)
        };

        return Ok(stats);
    }
}

#endregion
