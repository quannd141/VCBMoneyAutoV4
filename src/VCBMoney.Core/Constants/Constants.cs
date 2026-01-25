namespace VCBMoney.Core.Constants;

/// <summary>
/// Field length constraints - CHÍNH XÁC theo SQL Schema
/// </summary>
public static class FieldLength
{
    // Transaction reference - SQL: TRANS_REF nvarchar(21)
    public const int TransRefMin = 1;
    public const int TransRefMax = 21;
    
    // Account numbers - SQL: nvarchar(15) - nvarchar(50)
    public const int AccountMin = 6;
    public const int AccountMax = 50;
    public const int DebitAccountMax = 15;
    public const int BenAccountMax = 50;
    
    // Names - SQL: BEN_ACCOUNT_NAME nvarchar(200)
    public const int BenNameMax = 200;
    public const int BenBankMax = 150;       // SQL: BEN_BANK nvarchar(150)
    public const int OrderingCustMax = 255;  // SQL: ORDERING_CUST nvarchar(255)
    
    // Address - SQL: BEN_ADDRESS nvarchar(200)
    public const int AddressMax = 200;
    public const int OrderingCustAddressMax = 100; // SQL: ORDERING_CUST_ADDRESS nvarchar(100)
    
    // Content/Description - SQL: CONTENT nvarchar(255)
    public const int ContentMax = 255;
    
    // Remark - SQL: REMARK nvarchar(550)
    public const int RemarkMax = 550;
    
    // Currency - SQL: nvarchar(3)
    public const int CurrencyLength = 3;
    
    // ID Numbers
    public const int IdNoMax = 35;           // SQL: BEN_IDNO nvarchar(35)
    public const int IdNoPlaceMax = 100;     // SQL: BEN_IDNO_PLACE nvarchar(100)
    
    // Charge - SQL: CHARGE nvarchar(3)
    public const int ChargeMax = 3;
    
    // Error messages
    public const int ErrorMessageMax = 2000; // SQL: ERR_DESC nvarchar(2000)
    public const int ValidationDescMax = 2000;
    public const int CheckedNoteMax = 1500;  // SQL: CHECKED_NOTE nvarchar(1500)
}

/// <summary>
/// MT103 Required Tags - dùng cho validation L2
/// </summary>
public static class MT103Tags
{
    // Block identifiers
    public const string Block1Start = "{1:";
    public const string Block2Start = "{2:";
    public const string Block3Start = "{3:";
    public const string Block4Start = "{4:";
    public const string BlockEnd = "-}";
    
    // Required fields trong Block 4
    public const string TransRef = ":20:";           // Transaction Reference
    public const string ValueDateAmount = ":32A:";   // Value Date, Currency, Amount
    public const string OrderingCustomer = ":50K:";  // Ordering Customer (hoặc :50A:, :50F:)
    public const string Beneficiary = ":59:";        // Beneficiary (hoặc :59A:, :59F:)
    public const string ChargeDetails = ":71A:";     // Details of Charges
    
    // Optional but important
    public const string SendersCorr = ":53A:";       // Sender's Correspondent
    public const string AccountWithInst = ":57A:";   // Account with Institution
    public const string RemittanceInfo = ":70:";     // Remittance Information
    public const string SenderToReceiver = ":72:";   // Sender to Receiver Info
    
    // Alternative ordering customer tags
    public static readonly string[] OrderingCustomerTags = { ":50K:", ":50A:", ":50F:" };
    
    // Alternative beneficiary tags  
    public static readonly string[] BeneficiaryTags = { ":59:", ":59A:", ":59F:" };
}

/// <summary>
/// PayOrder field constraints - dùng cho validation L2 từ DB
/// </summary>
public static class PayOrderFields
{
    // Required fields
    public static readonly string[] RequiredFields = 
    {
        "SOGD",     // Transaction Reference
        "NGAY",     // Date
        "TKNO",     // Debit Account
        "SOTIEN",   // Amount
        "TKNH",     // Ben Account
        "TENNH"     // Ben Name
    };
    
    // Field max lengths
    public const int SoGdMax = 20;
    public const int TkNoMax = 15;
    public const int TkNhMax = 35;
    public const int TenNhMax = 144;
    public const int DcNhMax = 70;
    public const int NdMax = 140;
    public const int TenNrlMax = 144;
    public const int DcNrlMax = 180;
}

/// <summary>
/// Validation patterns (Regex)
/// </summary>
public static class ValidationPatterns
{
    // Date format YYMMDD hoặc YYYYMMDD
    public const string DateYYMMDD = @"^\d{6}$";
    public const string DateYYYYMMDD = @"^\d{8}$";
    public const string DateDDMMYYYY = @"^\d{2}/\d{2}/\d{4}$";
    
    // Amount: số dương, có thể có dấu phẩy và chấm
    public const string Amount = @"^[\d,]+\.?\d*$";
    
    // Account number: chỉ số và chữ
    public const string AccountNumber = @"^[A-Za-z0-9]+$";
    
    // Currency code: 3 chữ cái
    public const string CurrencyCode = @"^[A-Z]{3}$";
    
    // Charge type
    public const string ChargeType = @"^(OUR|BEN|SHA)$";
}

/// <summary>
/// Business validation limits
/// </summary>
public static class BusinessLimits
{
    // Amount limits (VND)
    public const decimal MinAmount = 1000;
    public const decimal MaxAmountNoApproval = 500_000_000;      // 500 triệu
    public const decimal MaxAmountWithApproval = 10_000_000_000; // 10 tỷ
    
    // Retry limits
    public const int MaxRetryCount = 3;
    public const int RetryDelayMinutes = 30;
}
