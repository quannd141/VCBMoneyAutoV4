using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Serilog;
using VCBMoney.Core.Configuration;
using VCBMoney.Core.Interfaces;
using VCBMoney.Infrastructure.Data;
using VCBMoney.Infrastructure.Repositories;
using VCBMoney.Infrastructure.Services;
using VCBMoney.Infrastructure.Validators;
using VCBMoney.Web.Jobs;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Hangfire", Serilog.Events.LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/vcbmoney-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    Log.Information("═══════════════════════════════════════════════════════════");
    Log.Information("  VCBMoney Integration");
    Log.Information("═══════════════════════════════════════════════════════════");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.Configure<VcbMoneySettings>(builder.Configuration.GetSection(VcbMoneySettings.SectionName));
    var settings = builder.Configuration.GetSection(VcbMoneySettings.SectionName).Get<VcbMoneySettings>() ?? new();

    builder.Services.AddDbContext<VcbMoneyDbContext>(options =>
        options.UseSqlServer(settings.VcbMoneyDb.GetConnectionString()));
    builder.Services.AddDbContext<TrungGianDbContext>(options =>
        options.UseSqlServer(settings.TrungGianDb.GetConnectionString()));

    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(settings.VcbMoneyDb.GetConnectionString(), new SqlServerStorageOptions
        {
            SchemaName = "Hangfire",
            PrepareSchemaIfNecessary = true,
            QueuePollInterval = TimeSpan.FromSeconds(15),
            DisableGlobalLocks = true
        }));

    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = Math.Max(Environment.ProcessorCount * 2, 4);
        options.Queues = new[] { "critical", "default", "sync", "low" };
    });

    // Validators & Services
    builder.Services.AddSingleton<IStructureValidator, StructureValidator>();
    builder.Services.AddSingleton<IPayOrderValidator, PayOrderValidator>();
    builder.Services.AddSingleton<IFormatValidator, FormatValidator>();
    builder.Services.AddScoped<IBusinessValidator, BusinessValidator>();
    builder.Services.AddSingleton<IFileService, FileService>();
    builder.Services.AddSingleton<IParserService, ParserService>();
    builder.Services.AddScoped<IAuditService, AuditService>();

    // Jobs
    builder.Services.AddScoped<Job1_ScanFilesJob>();
    builder.Services.AddScoped<Job2_ScanPayOrderJob>();
    builder.Services.AddScoped<Job3_ParseFilesJob>();
    builder.Services.AddScoped<Job4_TransformPayOrderJob>();
    builder.Services.AddScoped<Job5_SyncResultJob>();
    builder.Services.AddScoped<Job6_CleanupJob>();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "VCBMoney Integration API v4.2", Version = "v4.2" }));
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    EnsureDirectories(settings);

    app.UseSerilogRequestLogging();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHangfireDashboard("/hangfire", new DashboardOptions { DashboardTitle = "VCBMoney Integration" });
    app.UseRouting();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapGet("/", () => Results.Redirect("/hangfire"));

    RegisterJobs(settings);

    Log.Information("───────────────────────────────────────────────────────────");
    Log.Information("  Dashboard: http://localhost:5000/hangfire");
    Log.Information("  Swagger:   http://localhost:5000/swagger");
    Log.Information("  API:       http://localhost:5000/api/dashboard/stats");
    Log.Information("───────────────────────────────────────────────────────────");
    Log.Information("  FLOW: Job1/2 (Scan+L1L2) → Job3/4 (Parse+L3L4L5)");
    Log.Information("        → TRANSACTIONS (valid) / TRANSACTIONS_IMPORT (invalid)");
    Log.Information("        → Job5 (Sync) → DOMESTIC_RESULT");
    Log.Information("═══════════════════════════════════════════════════════════");

    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "Application terminated!"); }
finally { Log.CloseAndFlush(); }

void EnsureDirectories(VcbMoneySettings s)
{
    var dirs = new[] { s.Files.FolderInput, s.Files.FolderProcessing, s.Files.FolderSuccess, s.Files.FolderError };
    foreach (var dir in dirs.Where(d => !string.IsNullOrEmpty(d)))
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
}

void RegisterJobs(VcbMoneySettings s)
{
    if (s.Jobs.Job1_ScanFiles.Enabled)
        RecurringJob.AddOrUpdate<Job1_ScanFilesJob>("job1-scan-files", j => j.ExecuteAsync(CancellationToken.None), s.Jobs.Job1_ScanFiles.CronExpression);
    if (s.Jobs.Job2_ScanPayOrder.Enabled)
        RecurringJob.AddOrUpdate<Job2_ScanPayOrderJob>("job2-scan-payorder", j => j.ExecuteAsync(CancellationToken.None), s.Jobs.Job2_ScanPayOrder.CronExpression);
    if (s.Jobs.Job3_ParseFiles.Enabled)
        RecurringJob.AddOrUpdate<Job3_ParseFilesJob>("job3-parse-validate", j => j.ExecuteAsync(CancellationToken.None), s.Jobs.Job3_ParseFiles.CronExpression);
    if (s.Jobs.Job4_TransformPayOrder.Enabled)
        RecurringJob.AddOrUpdate<Job4_TransformPayOrderJob>("job4-transform-validate", j => j.ExecuteAsync(CancellationToken.None), s.Jobs.Job4_TransformPayOrder.CronExpression);
    if (s.Jobs.Job5_SyncResult.Enabled)
        RecurringJob.AddOrUpdate<Job5_SyncResultJob>("job5-sync-result", j => j.ExecuteAsync(CancellationToken.None), s.Jobs.Job5_SyncResult.CronExpression);
    if (s.Jobs.Job6_Cleanup.Enabled)
        RecurringJob.AddOrUpdate<Job6_CleanupJob>("job6-cleanup", j => j.ExecuteAsync(CancellationToken.None), s.Jobs.Job6_Cleanup.CronExpression);

}