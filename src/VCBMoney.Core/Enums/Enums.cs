namespace VCBMoney.Core.Enums;

/// <summary>
/// Trạng thái file upload
/// </summary>
public enum FileStatus
{
    /// <summary>Mới scan, chưa validate structure</summary>
    New = 0,
    
    /// <summary>Đã validate L1+L2, sẵn sàng parse</summary>
    Valid = 1,
    
    /// <summary>Đang parse</summary>
    Processing = 2,
    
    /// <summary>Parse xong</summary>
    Done = 3,
    
    /// <summary>Parse xong nhưng có một số record lỗi</summary>
    PartialError = 4,
    
    /// <summary>Lỗi structure (L1) - file không đúng format MT103</summary>
    InvalidStructure = -1,
    
    /// <summary>Thiếu required fields (L2)</summary>
    MissingFields = -2,
    
    /// <summary>Lỗi parse (L3/L4)</summary>
    ParseError = -3,
    
    /// <summary>Lỗi khác</summary>
    Error = -9
}

/// <summary>
/// Trạng thái transaction import
/// </summary>
public enum TransactionStatus
{
    /// <summary>Mới import, chưa business validate</summary>
    Pending = 0,
    
    /// <summary>Đang validate</summary>
    Validating = 1,
    
    /// <summary>Đã validate OK, sẵn sàng sync</summary>
    Ready = 2,
    
    /// <summary>Đã sync sang DOMESTIC_RESULT</summary>
    Synced = 3,
    
    /// <summary>Cần review thủ công (account không tồn tại, vượt limit...)</summary>
    NeedReview = 5,
    
    /// <summary>Trùng TransRef</summary>
    Duplicate = 6,
    
    /// <summary>Lỗi validation</summary>
    Error = -1,
    
    /// <summary>Đã retry max lần, không thể xử lý</summary>
    Dead = -2
}

/// <summary>
/// Trạng thái PayOrder trong queue
/// </summary>
public enum PayOrderStatus
{
    /// <summary>Chưa xử lý</summary>
    New = 0,
    
    /// <summary>Đã validate OK (L1+L2)</summary>
    Valid = 1,
    
    /// <summary>Đã xử lý thành công - insert vào TRANSACTION</summary>
    Processed = 2,
    
    /// <summary>Trùng TransRef</summary>
    Duplicate = 3,
    
    /// <summary>Lỗi validation</summary>
    Error = -1
}

/// <summary>
/// Nguồn dữ liệu của transaction
/// </summary>
public enum SourceType
{
    /// <summary>Từ file MT103</summary>
    File = 1,
    
    /// <summary>Từ bảng PAYORDER</summary>
    PayOrder = 2
}

/// <summary>
/// Level validation
/// </summary>
public enum ValidationLevel
{
    /// <summary>Chưa validate</summary>
    None = 0,
    
    /// <summary>L1: Structure - MT103 blocks</summary>
    Structure = 1,
    
    /// <summary>L2: Required fields</summary>
    RequiredFields = 2,
    
    /// <summary>L3: Format & Type</summary>
    FormatType = 3,
    
    /// <summary>L4: Field length</summary>
    FieldLength = 4,
    
    /// <summary>L5: Business rules</summary>
    Business = 5
}

/// <summary>
/// Mã lỗi validation
/// </summary>
public enum ValidationErrorCode
{
    // L1: Structure errors (1xx)
    InvalidFileFormat = 101,
    MissingBlock1 = 102,
    MissingBlock2 = 103,
    MissingBlock3 = 104,
    MissingBlock4 = 105,
    InvalidBlockFormat = 106,
    
    // L2: Required field errors (2xx)
    MissingTransRef = 201,
    MissingAmount = 202,
    MissingDebitAccount = 203,
    MissingBeneficiary = 204,
    MissingBenAccount = 205,
    MissingValueDate = 206,
    
    // L3: Format errors (3xx)
    InvalidDateFormat = 301,
    InvalidAmountFormat = 302,
    InvalidAccountFormat = 303,
    InvalidCurrencyCode = 304,
    InvalidChargeType = 305,
    InvalidPriority = 306,
    
    // L4: Length errors (4xx)
    TransRefTooLong = 401,
    AccountTooLong = 402,
    BenNameTooLong = 403,
    ContentTooLong = 404,
    AddressTooLong = 405,
    
    // L5: Business errors (5xx)
    DuplicateTransRef = 501,
    AccountNotFound = 502,
    InsufficientBalance = 503,
    AmountExceedsLimit = 504,
    InvalidBankCode = 505,
    AmountZeroOrNegative = 506
}

/// <summary>
/// Loại phí chuyển khoản
/// </summary>
public enum ChargeType
{
    /// <summary>Người gửi chịu phí</summary>
    OUR,
    
    /// <summary>Người nhận chịu phí</summary>
    BEN,
    
    /// <summary>Chia đôi phí</summary>
    SHA
}

/// <summary>
/// Mức độ nghiêm trọng của validation error
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Thông tin</summary>
    Info = 0,
    
    /// <summary>Cảnh báo - vẫn import được</summary>
    Warning = 1,
    
    /// <summary>Lỗi - import với flag</summary>
    Error = 2,
    
    /// <summary>Lỗi nghiêm trọng - reject</summary>
    Critical = 3
}
