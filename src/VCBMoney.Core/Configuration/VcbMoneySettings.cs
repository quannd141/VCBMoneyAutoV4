namespace VCBMoney.Core.Configuration;

public class VcbMoneySettings
{
    public const string SectionName = "VcbMoney";
    
    public DatabaseConfig VcbMoneyDb { get; set; } = new();
    public DatabaseConfig TrungGianDb { get; set; } = new();
    public FileSettings Files { get; set; } = new();
    public JobSettings Jobs { get; set; } = new();
    public BusinessSettings Business { get; set; } = new();
    public AlertSettings Alert { get; set; } = new();
}

public class DatabaseConfig
{
    public string Server { get; set; } = "localhost";
    public int Port { get; set; } = 1433;
    public string DatabaseName { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? Password { get; set; }
    public bool UseWindowsAuth { get; set; } = true;
    public bool TrustServerCertificate { get; set; } = true;
    public int CommandTimeout { get; set; } = 120;
    
    public string GetConnectionString()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = Port == 1433 ? Server : $"{Server},{Port}",
            InitialCatalog = DatabaseName,
            TrustServerCertificate = TrustServerCertificate,
            ConnectTimeout = 30,
            MultipleActiveResultSets = true
        };

        if (UseWindowsAuth)
            builder.IntegratedSecurity = true;
        else
        {
            builder.UserID = UserId;
            builder.Password = Password;
        }

        return builder.ConnectionString;
    }
}

public class FileSettings
{
    /// <summary>Folder scan file mới</summary>
    public string FolderInput { get; set; } = @"D:\VCBMoney\Output\Input";
    
    /// <summary>Folder file đang xử lý</summary>
    public string FolderProcessing { get; set; } = @"D:\VCBMoney\Output\Processing";
    
    /// <summary>Folder file đã xử lý thành công</summary>
    public string FolderSuccess { get; set; } = @"D:\VCBMoney\Output\Success";
    
    /// <summary>Folder file lỗi</summary>
    public string FolderError { get; set; } = @"D:\VCBMoney\Output\Error";
    
    /// <summary>Các extension được phép</summary>
    public string[] AllowedExtensions { get; set; } = { ".txt" };
    
    /// <summary>Encoding mặc định</summary>
    public string DefaultEncoding { get; set; } = "UTF-8";
}

public class JobSettings
{
    public JobConfig Job1_ScanFiles { get; set; } = new() { CronExpression = "*/1 * * * *" };
    public JobConfig Job2_ScanPayOrder { get; set; } = new() { CronExpression = "*/1 * * * *" };
    public JobConfig Job3_ParseFiles { get; set; } = new() { CronExpression = "*/1 * * * *" };
    public JobConfig Job4_TransformPayOrder { get; set; } = new() { CronExpression = "*/1 * * * *" };
    public JobConfig Job5_SyncResult { get; set; } = new() { CronExpression = "*/2 * * * *" };
    public JobConfig Job6_Cleanup { get; set; } = new() { CronExpression = "0 2 * * *", BatchSize = 1000 };

}

public class JobConfig
{
    public bool Enabled { get; set; } = true;
    public string CronExpression { get; set; } = "*/5 * * * *";
    public int BatchSize { get; set; } = 100;
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxRetry { get; set; } = 3;
}

public class BusinessSettings
{
    /// <summary>Mã khách hàng mặc định</summary>
    public string CustomerCode { get; set; } = "VCB001";
    
    /// <summary>Số ngày giữ file</summary>
    public int FileRetentionDays { get; set; } = 90;
    
    /// <summary>Số lần retry tối đa cho transaction lỗi</summary>
    public int MaxRetryCount { get; set; } = 3;
    
    /// <summary>Số phút chờ giữa các lần retry</summary>
    public int RetryDelayMinutes { get; set; } = 30;
    
    /// <summary>Limit amount không cần approval (VND)</summary>
    public decimal AmountLimitNoApproval { get; set; } = 500_000_000;
    
    /// <summary>Có validate account tồn tại không</summary>
    public bool ValidateAccountExists { get; set; } = false;
    
    /// <summary>Có validate bank code không</summary>
    public bool ValidateBankCode { get; set; } = false;
}

public class AlertSettings
{
    public bool Enabled { get; set; } = false;
    
    /// <summary>Email recipients (comma separated)</summary>
    public string EmailRecipients { get; set; } = string.Empty;
    
    /// <summary>Slack webhook URL</summary>
    public string SlackWebhook { get; set; } = string.Empty;
    
    /// <summary>Ngưỡng error rate để alert (%)</summary>
    public int ErrorRateThreshold { get; set; } = 10;
    
    /// <summary>SMTP settings</summary>
    public SmtpSettings Smtp { get; set; } = new();
}

public class SmtpSettings
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "VCBMoney Alert";
}
