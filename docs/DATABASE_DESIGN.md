# VCBMoney Integration v4.2 - Database Design

## 1. Tổng quan

### 1.1 Databases

| Database | Purpose |
|----------|---------|
| VCB_MONEY | Main application database |
| DBTRUNGGIAN | Integration database (customer system) |

### 1.2 Entity Relationship

```
┌─────────────────────────────────────────────────────────────────┐
│                        VCB_MONEY                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────┐         ┌──────────────┐                       │
│  │ FILE_UPLOAD │─────────│ DOMESTIC_TXT │                       │
│  │             │ 1    N  │              │                       │
│  └──────┬──────┘         └──────────────┘                       │
│         │                                                       │
│         │ 1                                                     │
│         │                                                       │
│         ▼ N                                                     │
│  ┌─────────────────────────────────────────┐                    │
│  │              TRANSACTIONS                │                    │
│  │     (Valid records - Status >= 0)       │                    │
│  └─────────────────────────────────────────┘                    │
│                                                                 │
│  ┌─────────────────────────────────────────┐                    │
│  │          TRANSACTIONS_IMPORT             │                    │
│  │    (Invalid records - for retry)        │                    │
│  └─────────────────────────────────────────┘                    │
│                                                                 │
│  ┌─────────────┐                                                │
│  │  AUDIT_LOG  │                                                │
│  └─────────────┘                                                │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                       DBTRUNGGIAN                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────┐         ┌─────────────────┐                    │
│  │  PAYORDER   │         │ DOMESTIC_RESULT │                    │
│  │   (Input)   │         │    (Output)     │                    │
│  └─────────────┘         └─────────────────┘                    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. VCB_MONEY Database

### 2.1 FILE_UPLOAD

**Purpose:** Lưu metadata của file upload

```sql
CREATE TABLE FILE_UPLOAD (
    FILE_ID             BIGINT IDENTITY(1,1) PRIMARY KEY,
    FILE_PHYSICAL       NVARCHAR(500) NOT NULL,  -- Current file path
    FILE_NAME           NVARCHAR(255),
    FILE_PATH           NVARCHAR(1000),
    FILE_EXTENSION      NVARCHAR(10),
    FILE_SIZE           BIGINT DEFAULT 0,
    FILE_HASH           VARCHAR(64) NOT NULL,    -- SHA256 for duplicate check
    BATCH_ID            UNIQUEIDENTIFIER NOT NULL,
    STATUS              INT DEFAULT 0,
    VALIDATION_LEVEL    INT DEFAULT 0,
    VALIDATION_ERRORS   NVARCHAR(MAX),
    TRANSACTION_COUNT   INT DEFAULT 0,
    SUCCESS_COUNT       INT DEFAULT 0,
    ERROR_COUNT         INT DEFAULT 0,
    ERROR_MESSAGE       NVARCHAR(2000),
    STARTED_AT          DATETIME,
    COMPLETED_AT        DATETIME,
    PROCESSING_TIME_MS  BIGINT,
    CreatedDate         DATETIME DEFAULT GETDATE(),
    CreatedBy           NVARCHAR(64),
    UpdatedDate         DATETIME,
    UpdatedBy           NVARCHAR(64)
);
```

**Status Values:**

| Status | Meaning |
|--------|---------|
| 0 | New |
| 1 | Valid (L1+L2 passed) |
| 2 | Processing |
| 3 | Done |
| -1 | Invalid Structure |
| -2 | Missing Fields |
| -3 | Parse Error |

**Note:** `RAW_CONTENT` column removed - Job3 reads file from `FILE_PHYSICAL` path

### 2.2 TRANSACTIONS

**Purpose:** Lưu giao dịch valid (L3+L4+L5 passed)

```sql
CREATE TABLE TRANSACTIONS (
    ID                  BIGINT IDENTITY(1,1) PRIMARY KEY,
    SOURCE_TYPE         NVARCHAR(20),           -- 'FILE' or 'PAYORDER'
    SOURCE_REF          NVARCHAR(50),
    FILE_UPLOAD_ID      BIGINT,
    TRANS_REF           NVARCHAR(50),
    DEBIT_ACCOUNT_NO    NVARCHAR(35),
    DEBIT_AMOUNT        DECIMAL(18,2),
    DEBIT_CURRENCY      NVARCHAR(3) DEFAULT 'VND',
    BEN_ACCOUNT_NO      NVARCHAR(50),
    BEN_ACCOUNT_NAME    NVARCHAR(140),
    BEN_BANK            NVARCHAR(200),
    BEN_ADDRESS         NVARCHAR(200),
    BEN_CURRENCY        NVARCHAR(3) DEFAULT 'VND',
    BEN_ID_NO           NVARCHAR(35),
    ORDERING_CUST       NVARCHAR(500),
    ORDERING_CUST_ACCOUNT NVARCHAR(35),
    CONTENT             NVARCHAR(500),
    CHARGE              NVARCHAR(10),
    PRIORITY            NVARCHAR(10),
    VALUE_DATE          DATE,
    IS_BANK_TO_BANK     BIT DEFAULT 0,
    IS_TRANSFER_TO_SBV  BIT DEFAULT 0,
    CHECK_BEN_NAME      BIT DEFAULT 0,
    STATUS              INT DEFAULT 0,          -- 0:Ready, 1:Synced, -1:Error
    SYNCED_DATE         DATETIME,
    ERROR_MESSAGE       NVARCHAR(500),
    CreatedDate         DATETIME DEFAULT GETDATE(),
    CreatedBy           NVARCHAR(64)
);
```

### 2.3 TRANSACTIONS_IMPORT

**Purpose:** Lưu giao dịch invalid (có lỗi L3/L4/L5) để retry hoặc fix manual

```sql
CREATE TABLE TRANSACTIONS_IMPORT (
    ID                  BIGINT IDENTITY(1,1) PRIMARY KEY,
    -- Same fields as TRANSACTIONS
    SOURCE_TYPE         NVARCHAR(20),
    SOURCE_REF          NVARCHAR(50),
    FILE_UPLOAD_ID      BIGINT,
    TRANS_REF           NVARCHAR(50),
    -- Raw values (original)
    DEBIT_AMOUNT_RAW    NVARCHAR(50),
    BEN_ACCOUNT_NAME_RAW NVARCHAR(500),
    BEN_BANK_RAW        NVARCHAR(500),
    CONTENT_RAW         NVARCHAR(MAX),
    -- Processed values
    DEBIT_ACCOUNT_NO    NVARCHAR(35),
    DEBIT_AMOUNT        DECIMAL(18,2),
    BEN_ACCOUNT_NO      NVARCHAR(50),
    BEN_ACCOUNT_NAME    NVARCHAR(140),
    BEN_BANK            NVARCHAR(200),
    CONTENT             NVARCHAR(500),
    -- Error info
    STATUS              INT DEFAULT 0,          -- 0:Pending, 1:Fixed, 2:Reprocessed, -1:Rejected
    FAILED_LEVEL        INT,                    -- 3, 4, or 5
    VALIDATION_ERRORS   NVARCHAR(MAX),          -- JSON array
    ERR_CODE            NVARCHAR(20),
    ERR_DESC            NVARCHAR(500),
    -- Error flags
    ERR_TRANS_REF       BIT DEFAULT 0,
    ERR_AMOUNT          BIT DEFAULT 0,
    ERR_DEBIT_ACCOUNT   BIT DEFAULT 0,
    ERR_BEN_ACCOUNT     BIT DEFAULT 0,
    ERR_BEN_NAME        BIT DEFAULT 0,
    ERR_DUPLICATE       BIT DEFAULT 0,
    ERR_BUSINESS        BIT DEFAULT 0,
    -- Retry tracking
    RETRY_COUNT         INT DEFAULT 0,
    LAST_RETRY_DATE     DATETIME,
    MOVED_TO_TRANSACTION_ID BIGINT,
    CreatedDate         DATETIME DEFAULT GETDATE(),
    CreatedBy           NVARCHAR(64)
);
```

### 2.4 DOMESTIC_TXT

**Purpose:** Lưu raw MT103 data để search và audit

```sql
CREATE TABLE DOMESTIC_TXT (
    ID                  BIGINT IDENTITY(1,1) PRIMARY KEY,
    FILE_UPLOAD_ID      BIGINT NOT NULL,
    -- Raw MT103 Fields
    FIELD_20            NVARCHAR(50),       -- Transaction Reference
    FIELD_32A           NVARCHAR(50),       -- Value Date/Currency/Amount
    FIELD_50K           NVARCHAR(500),      -- Ordering Customer
    FIELD_50R           NVARCHAR(35),       -- Ordering Customer Account
    FIELD_53            NVARCHAR(35),       -- Debit Account
    FIELD_57A           NVARCHAR(200),      -- Beneficiary Bank
    FIELD_59            NVARCHAR(500),      -- Beneficiary
    FIELD_59O           NVARCHAR(35),       -- Beneficiary ID
    FIELD_70            NVARCHAR(500),      -- Content
    FIELD_71A           NVARCHAR(3),        -- Charge
    FIELD_72A           CHAR(1),            -- Bank to Bank
    FIELD_72B           CHAR(1),            -- Transfer to SBV
    -- Parsed values for search
    VALUE_DATE          DATE,
    CURRENCY            NVARCHAR(3),
    AMOUNT              DECIMAL(18,2),
    DEBIT_ACCOUNT       NVARCHAR(35),
    BEN_ACCOUNT         NVARCHAR(35),
    BEN_NAME            NVARCHAR(140),
    -- Status
    STATUS              INT DEFAULT 0,      -- 0:New, 1:Valid, 3:Error
    ERR_CODE            NVARCHAR(20),
    ERR_DESC            NVARCHAR(500),
    -- Links
    TRANSACTION_ID      BIGINT,             -- FK to TRANSACTIONS
    IMPORT_ID           BIGINT,             -- FK to TRANSACTIONS_IMPORT
    CreatedDate         DATETIME DEFAULT GETDATE(),
    CreatedBy           NVARCHAR(64)
);
```

---

## 3. DBTRUNGGIAN Database

### 3.1 PAYORDER

**Purpose:** Input từ hệ thống khách hàng

```sql
CREATE TABLE PAYORDER (
    SOGD                NVARCHAR(20) PRIMARY KEY,  -- Transaction ID
    NGAY                SMALLDATETIME NOT NULL,     -- Date
    TKNO                NVARCHAR(15) NOT NULL,      -- Debit Account
    LNT                 NVARCHAR(3) DEFAULT 'VND',  -- Currency
    SOTIEN              FLOAT NOT NULL,             -- Amount
    UUTIEN              SMALLINT,                   -- Priority
    TKNH                NVARCHAR(35),               -- Ben Account
    TENNH               NVARCHAR(144),              -- Ben Name
    DCNH                NVARCHAR(70),               -- Ben Address
    PHI                 NVARCHAR(3),                -- Charge
    NHNH                NVARCHAR(100),              -- Ben Bank
    ND                  NVARCHAR(140),              -- Content
    TENNRL              NVARCHAR(144),              -- Ordering Customer
    UPVCBMONEY          SMALLINT DEFAULT 0,         -- Status
    ERRCODE             VARCHAR(3),
    ERRDES              NVARCHAR(250),
    UP_DATETIME         VARCHAR(30)
);
```

**UPVCBMONEY Values:**

| Value | Meaning |
|-------|---------|
| 0 or NULL | New |
| 1 | Valid (L1+L2 passed) |
| 2 | Imported to TRANSACTIONS |
| -1 | Error |

### 3.2 DOMESTIC_RESULT

**Purpose:** Output gửi cho ngân hàng xử lý

```sql
CREATE TABLE DOMESTIC_RESULT (
    ID                  BIGINT IDENTITY(1,1) PRIMARY KEY,
    CUSTOMER_CODE       NVARCHAR(20) NOT NULL,
    TRANS_REF           NVARCHAR(50),
    DEBIT_ACCOUNT       NVARCHAR(35),
    DEBIT_NAME          NVARCHAR(200),
    AMOUNT              DECIMAL(18,2),
    CURRENCY            NVARCHAR(3) DEFAULT 'VND',
    CONTENT             NVARCHAR(500),
    BEN_ACCOUNT_NO      NVARCHAR(50) NOT NULL,
    BEN_ACCOUNT_NAME    NVARCHAR(200),
    BEN_BANK            NVARCHAR(200) NOT NULL,
    BEN_BANK_CODE       NVARCHAR(15),
    BEN_CURRENCY        NVARCHAR(3) DEFAULT 'VND',
    BEN_ADDRESS         NVARCHAR(200),
    BEN_IDNO            NVARCHAR(35),
    CHARGE              NVARCHAR(10),
    VALUE_DATE          DATE,
    PRIORITY            NVARCHAR(10),
    STATUS              CHAR(3) DEFAULT '202',      -- Bank response status
    ERROR_CODE          NVARCHAR(10),
    ERROR_MESSAGE       NVARCHAR(500),
    IMPORTED_TIME       DATETIME DEFAULT GETDATE(),
    PROCESSED_TIME      DATETIME,
    CreatedDate         DATETIME DEFAULT GETDATE(),
    CreatedBy           NVARCHAR(64)
);
```

**STATUS Values:**

| Status | Meaning |
|--------|---------|
| 201 | Pending |
| 202 | Processing |
| 000 | Success |
| 4xx | Client Error |
| 5xx | Server Error |

---

## 4. Indexes

### 4.1 Performance Indexes

```sql
-- FILE_UPLOAD
CREATE INDEX IX_FILE_HASH ON FILE_UPLOAD(FILE_HASH);
CREATE INDEX IX_FILE_STATUS ON FILE_UPLOAD(STATUS);

-- TRANSACTIONS
CREATE INDEX IX_TRANS_REF ON TRANSACTIONS(TRANS_REF);
CREATE INDEX IX_TRANS_STATUS ON TRANSACTIONS(STATUS);
CREATE NONCLUSTERED INDEX IX_TRANS_SEARCH 
ON TRANSACTIONS (STATUS, VALUE_DATE)
INCLUDE (TRANS_REF, DEBIT_AMOUNT, BEN_ACCOUNT_NO, BEN_ACCOUNT_NAME);

-- TRANSACTIONS_IMPORT
CREATE INDEX IX_IMPORT_REF ON TRANSACTIONS_IMPORT(TRANS_REF);
CREATE INDEX IX_IMPORT_STATUS ON TRANSACTIONS_IMPORT(STATUS);
CREATE NONCLUSTERED INDEX IX_IMPORT_RETRY 
ON TRANSACTIONS_IMPORT (FAILED_LEVEL, STATUS, RETRY_COUNT)
WHERE STATUS = 0;

-- DOMESTIC_TXT
CREATE INDEX IX_DOMESTIC_FILE ON DOMESTIC_TXT(FILE_UPLOAD_ID);
CREATE INDEX IX_DOMESTIC_REF ON DOMESTIC_TXT(FIELD_20);
CREATE INDEX IX_DOMESTIC_STATUS ON DOMESTIC_TXT(STATUS);
CREATE INDEX IX_DOMESTIC_DEBIT ON DOMESTIC_TXT(DEBIT_ACCOUNT);
CREATE INDEX IX_DOMESTIC_BEN ON DOMESTIC_TXT(BEN_ACCOUNT);
CREATE INDEX IX_DOMESTIC_DATE ON DOMESTIC_TXT(VALUE_DATE);
CREATE NONCLUSTERED INDEX IX_DOMESTIC_TXT_SEARCH 
ON DOMESTIC_TXT (VALUE_DATE, STATUS)
INCLUDE (FIELD_20, AMOUNT, DEBIT_ACCOUNT, BEN_ACCOUNT, BEN_NAME);

-- PAYORDER
CREATE INDEX IX_PAYORDER_STATUS ON PAYORDER(UPVCBMONEY);

-- DOMESTIC_RESULT
CREATE INDEX IX_RESULT_REF ON DOMESTIC_RESULT(TRANS_REF);
```

---

## 5. Data Flow

### 5.1 File → Database

```
File MT103
    │
    ▼
FILE_UPLOAD (metadata)
    │
    ├─► DOMESTIC_TXT (raw MT103 data)
    │
    └─► TRANSACTIONS (valid) or TRANSACTIONS_IMPORT (invalid)
```

### 5.2 PayOrder → Database

```
PAYORDER
    │
    ▼
TRANSACTIONS (valid) or TRANSACTIONS_IMPORT (invalid)
    │
    ▼
DOMESTIC_RESULT
```

---

## 6. Maintenance

### 6.1 Cleanup Old Data

```sql
-- Delete files older than 90 days
DELETE FROM FILE_UPLOAD WHERE CreatedDate < DATEADD(DAY, -90, GETDATE());

-- Delete synced transactions older than 90 days
DELETE FROM TRANSACTIONS WHERE Status = 1 AND SyncedDate < DATEADD(DAY, -90, GETDATE());

-- Delete rejected imports older than 90 days
DELETE FROM TRANSACTIONS_IMPORT WHERE Status = -1 AND UpdatedDate < DATEADD(DAY, -90, GETDATE());
```

### 6.2 Index Maintenance

```sql
-- Rebuild indexes monthly
ALTER INDEX ALL ON TRANSACTIONS REBUILD;
ALTER INDEX ALL ON DOMESTIC_TXT REBUILD;
```
