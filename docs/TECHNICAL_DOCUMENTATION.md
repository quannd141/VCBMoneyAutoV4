# VCBMoney Integration v4.2 - Technical Documentation

## 1. Tổng quan kỹ thuật

### 1.1 Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 8.0 |
| Database | SQL Server 2019+ |
| Background Jobs | Hangfire |
| ORM | Entity Framework Core 8 |
| Architecture | Clean Architecture |

### 1.2 Project Structure

```
vcbmoney-v4.2/
├── src/
│   ├── VCBMoney.Core/           # Domain layer
│   │   ├── Entities/            # Entity classes
│   │   ├── Enums/               # Enumerations
│   │   ├── Interfaces/          # Service interfaces
│   │   ├── Configuration/       # Settings classes
│   │   └── Constants/           # Constants
│   │
│   ├── VCBMoney.Infrastructure/ # Infrastructure layer
│   │   ├── Data/                # DbContexts
│   │   ├── Repositories/        # Repository implementations
│   │   ├── Services/            # Service implementations
│   │   └── Validators/          # Validation logic
│   │
│   └── VCBMoney.Web/            # Presentation layer
│       ├── Controllers/         # API Controllers
│       ├── Jobs/                # Hangfire Jobs
│       └── Program.cs           # Application entry
│
├── database/
│   ├── create_database.sql      # VCB_MONEY schema
│   └── create_payorder_domestic.sql # DBTRUNGGIAN schema
│
└── docs/
    ├── SRS_VCBMoney_v4.2.md
    ├── TECHNICAL_DOCUMENTATION.md
    └── HANDOVER_DOCUMENT.md
```

---

## 2. Database Design

### 2.1 VCB_MONEY Database

#### FILE_UPLOAD
```sql
CREATE TABLE FILE_UPLOAD (
    FILE_ID             BIGINT IDENTITY(1,1) PRIMARY KEY,
    FILE_PHYSICAL       NVARCHAR(500) NOT NULL,  -- Current file path
    FILE_NAME           NVARCHAR(255),
    FILE_PATH           NVARCHAR(1000),
    FILE_EXTENSION      NVARCHAR(10),
    FILE_SIZE           BIGINT DEFAULT 0,
    FILE_HASH           VARCHAR(64) NOT NULL,    -- SHA256 duplicate check
    BATCH_ID            UNIQUEIDENTIFIER NOT NULL,
    STATUS              INT DEFAULT 0,           -- 0:New, 1:Valid, 2:Processing, 3:Done, -1/-2/-3:Error
    VALIDATION_LEVEL    INT DEFAULT 0,
    VALIDATION_ERRORS   NVARCHAR(MAX),
    TRANSACTION_COUNT   INT DEFAULT 0,
    SUCCESS_COUNT       INT DEFAULT 0,
    ERROR_COUNT         INT DEFAULT 0,
    ERROR_MESSAGE       NVARCHAR(2000),
    STARTED_AT          DATETIME,
    COMPLETED_AT        DATETIME,
    PROCESSING_TIME_MS  BIGINT
);
-- Note: RAW_CONTENT removed - Job3 reads file from FILE_PHYSICAL
```

#### DOMESTIC_TXT (Search table)
```sql
CREATE TABLE DOMESTIC_TXT (
    ID                  BIGINT IDENTITY(1,1) PRIMARY KEY,
    FILE_UPLOAD_ID      BIGINT NOT NULL,
    -- Raw MT103 fields
    FIELD_20            NVARCHAR(50),       -- Transaction Reference
    FIELD_32A           NVARCHAR(50),       -- Value Date/Currency/Amount
    FIELD_50K           NVARCHAR(500),      -- Ordering Customer
    FIELD_53            NVARCHAR(35),       -- Debit Account
    FIELD_57A           NVARCHAR(200),      -- Beneficiary Bank
    FIELD_59            NVARCHAR(500),      -- Beneficiary
    FIELD_70            NVARCHAR(500),      -- Content
    FIELD_71A           NVARCHAR(3),        -- Charge (BEN/OUR/SHA)
    -- Parsed values for search
    VALUE_DATE          DATE,
    CURRENCY            NVARCHAR(3),
    AMOUNT              DECIMAL(18,2),
    DEBIT_ACCOUNT       NVARCHAR(35),
    BEN_ACCOUNT         NVARCHAR(35),
    BEN_NAME            NVARCHAR(140),
    STATUS              INT DEFAULT 0,
    ERR_CODE            NVARCHAR(20),
    ERR_DESC            NVARCHAR(500)
);
```

### 2.2 Performance Indexes

```sql
-- FILE_UPLOAD
CREATE INDEX IX_FILE_HASH ON FILE_UPLOAD(FILE_HASH);
CREATE INDEX IX_FILE_STATUS ON FILE_UPLOAD(STATUS);

-- DOMESTIC_TXT - Covering indexes for search
CREATE NONCLUSTERED INDEX IX_DOMESTIC_TXT_SEARCH 
ON DOMESTIC_TXT (VALUE_DATE, STATUS)
INCLUDE (FIELD_20, AMOUNT, DEBIT_ACCOUNT, BEN_ACCOUNT, BEN_NAME);

CREATE NONCLUSTERED INDEX IX_DOMESTIC_TXT_AMOUNT 
ON DOMESTIC_TXT (AMOUNT DESC)
INCLUDE (FIELD_20, VALUE_DATE, CURRENCY);

-- TRANSACTIONS
CREATE NONCLUSTERED INDEX IX_TRANS_SEARCH 
ON TRANSACTIONS (STATUS, VALUE_DATE)
INCLUDE (TRANS_REF, DEBIT_AMOUNT, BEN_ACCOUNT_NO, BEN_ACCOUNT_NAME);
```

---

## 3. Job Processing

### 3.1 Batch Processing Architecture

Tất cả các Job đều sử dụng batch processing với cùng pattern:

```csharp
// Constants
private const int BATCH_SIZE = 1000;

// Main loop
while (!ct.IsCancellationRequested)
{
    // Get batch
    var batch = await context.Table
        .Where(x => x.Status == 0)
        .Take(BATCH_SIZE)
        .ToListAsync(ct);
    
    if (!batch.Any()) break;
    
    // Process batch in transaction
    using var trans = await context.Database.BeginTransactionAsync(ct);
    try
    {
        await ProcessBatchAsync(batch, ct);
        await trans.CommitAsync(ct);
    }
    catch
    {
        await trans.RollbackAsync(ct);
        throw;
    }
}
```

### 3.2 Batch Duplicate Check

Thay vì check từng record, query 1 lần cho cả batch:

```csharp
// Get all TransRefs in batch
var transRefs = batch.Select(x => x.TransRef).ToList();

// Single query to check duplicates
var existingRefs = await context.Transactions
    .Where(t => transRefs.Contains(t.TransRef))
    .Select(t => t.TransRef)
    .ToListAsync(ct);

// Use HashSet for O(1) lookup
var existingSet = new HashSet<string>(existingRefs);

// Check each record
foreach (var item in batch)
{
    if (existingSet.Contains(item.TransRef))
    {
        // Duplicate found
    }
}
```

### 3.3 Job Schedule

| Job | Cron | Description |
|-----|------|-------------|
| Job1 | */30 * * * * * | Every 30 seconds |
| Job2 | */30 * * * * * | Every 30 seconds |
| Job3 | */1 * * * * | Every minute |
| Job4 | */1 * * * * | Every minute |
| Job5 | */1 * * * * | Every minute |
| Job6 | 0 */5 * * * | Every 5 minutes |
| Job7 | 0 0 2 * * * | Daily at 2 AM |
| Job8 | 0 0 6 * * * | Daily at 6 AM |

---

## 4. File Processing Flow

### 4.1 Folder Structure

```
D:\VCBMoney\
├── Input\          # New files dropped here
├── Processing\     # Files being processed (locked)
├── Archive\        # Successfully processed
└── Error\          # Failed files
```

### 4.2 File Movement

| Event | From | To |
|-------|------|-----|
| Job1 starts | Input | Processing |
| Job1 L1/L2 fail | Processing | Error |
| Job3 success | Processing | Archive |
| Job3 fail | Processing | Error |

### 4.3 Why No RAW_CONTENT in DB?

**Problem:** 100k transactions × 500 bytes = 50MB per file
- DB size grows rapidly
- Query performance degrades
- Memory pressure on app server

**Solution:** Read file from disk
- FILE_PHYSICAL stores current path
- Job3 reads directly from FolderProcessing
- File moved to Archive/Error after processing

---

## 5. MT103 Parser

### 5.1 Format Detection

```csharp
private bool IsSwiftFormat(string content)
{
    return content.Contains("{1:") || content.Contains("{4:");
}
```

### 5.2 Plaintext Parsing

```csharp
// Split by :20: tags (each transaction starts with :20:)
var pattern = @"(?=:20:)";
var transactions = Regex.Split(content, pattern)
    .Where(s => !string.IsNullOrWhiteSpace(s))
    .ToList();
```

### 5.3 Field Extraction

```csharp
private string? ExtractField(string content, string tag)
{
    var pattern = $@":{tag}:([^\r\n]+(?:\r?\n(?!:[0-9A-Z]{{2,3}}:)[^\r\n]+)*)";
    var match = Regex.Match(content, pattern);
    return match.Success ? match.Groups[1].Value.Trim() : null;
}
```

---

## 6. API Reference

### 6.1 Search DOMESTIC_TXT

```http
GET /api/domestictxt?transRef=TT123&dateFrom=2025-01-01&dateTo=2025-01-31&amountFrom=1000000
```

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| transRef | string | Transaction reference (partial match) |
| debitAccount | string | Debit account (partial match) |
| benAccount | string | Beneficiary account (partial match) |
| benName | string | Beneficiary name (partial match) |
| amountFrom | decimal | Minimum amount |
| amountTo | decimal | Maximum amount |
| dateFrom | date | Start date |
| dateTo | date | End date |
| status | int | 0:New, 1:Valid, 3:Error |

**Response:**
```json
{
  "total": 1500,
  "page": 1,
  "size": 50,
  "items": [
    {
      "id": 12345,
      "transRef": "TT123456.2025",
      "valueDate": "2025-01-15",
      "currency": "VND",
      "amount": 50000000,
      "debitAccount": "1234567890",
      "benAccount": "9876543210",
      "benName": "NGUYEN VAN A",
      "status": 1
    }
  ]
}
```

---

## 7. Error Handling

### 7.1 Validation Error Codes

| Code | Level | Description |
|------|-------|-------------|
| MISSING_FIELD | L2 | Required field missing |
| INVALID_STRUCTURE | L1 | MT103 structure invalid |
| INVALID_FORMAT | L3 | Field format invalid |
| TRUNCATED | L4 | Field truncated (warning) |
| DUPLICATE | L5 | TransRef already exists |
| INVALID_AMOUNT | L5 | Amount <= 0 |

### 7.2 File Status Codes

| Status | Meaning |
|--------|---------|
| 0 | New |
| 1 | Valid (L1+L2 passed) |
| 2 | Processing |
| 3 | Done |
| -1 | Invalid Structure |
| -2 | Missing Fields |
| -3 | Parse Error |

---

## 8. Deployment

### 8.1 Prerequisites

- .NET 8.0 Runtime
- SQL Server 2019+
- Windows Server 2019+ (for file paths)

### 8.2 Configuration

```json
{
  "ConnectionStrings": {
    "VcbMoney": "Server=.;Database=VCB_MONEY;Trusted_Connection=True;TrustServerCertificate=True",
    "TrungGian": "Server=.;Database=DBTRUNGGIAN;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "VcbMoney": {
    "Files": {
      "FolderInput": "D:\\VCBMoney\\Input",
      "FolderProcessing": "D:\\VCBMoney\\Processing",
      "FolderArchive": "D:\\VCBMoney\\Archive",
      "FolderError": "D:\\VCBMoney\\Error"
    }
  }
}
```

### 8.3 SQL Server Settings

For large batches, increase command timeout:
```
Command Timeout=300
```

---

## 9. Monitoring

### 9.1 Hangfire Dashboard

Access at: `/hangfire`

### 9.2 Health Check

```http
GET /health
```

### 9.3 Key Metrics

- Files processed per hour
- Transactions per minute
- Error rate
- Average processing time
