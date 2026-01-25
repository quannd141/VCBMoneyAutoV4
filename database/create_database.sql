-- =====================================================
-- VCBMoney Integration v4.2 - Enhanced Features
-- =====================================================

-- =====================================================
-- DATABASE 1: VCB_MONEY
-- Tables: FILE_UPLOAD, TRANSACTIONS, TRANSACTIONS_IMPORT, AUDIT_LOG
-- =====================================================
USE master;
GO
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'VCB_MONEY')
    CREATE DATABASE VCB_MONEY;
GO
USE VCB_MONEY;
GO

-- FILE_UPLOAD
-- Note: RAW_CONTENT removed - Job3 reads file directly from FILE_PHYSICAL path
IF OBJECT_ID('FILE_UPLOAD', 'U') IS NULL
CREATE TABLE FILE_UPLOAD (
    FILE_ID             BIGINT IDENTITY(1,1) PRIMARY KEY,
    FILE_PHYSICAL       NVARCHAR(500) NOT NULL,  -- Current physical path of file
    FILE_NAME           NVARCHAR(255),
    FILE_PATH           NVARCHAR(1000),           -- Folder containing file
    FILE_EXTENSION      NVARCHAR(10),
    FILE_SIZE           BIGINT DEFAULT 0,
    FILE_HASH           VARCHAR(64) NOT NULL,     -- SHA256 hash for duplicate check
    BATCH_ID            UNIQUEIDENTIFIER NOT NULL,
    STATUS              INT DEFAULT 0,            -- 0:New, 1:Valid, 2:Processing, 3:Done, -1:Error
    VALIDATION_LEVEL    INT DEFAULT 0,            -- Highest validation level passed
    VALIDATION_ERRORS   NVARCHAR(MAX),            -- JSON array of errors
    TRANSACTION_COUNT   INT DEFAULT 0,            -- Total transactions in file
    SUCCESS_COUNT       INT DEFAULT 0,            -- Valid transactions
    ERROR_COUNT         INT DEFAULT 0,            -- Invalid transactions
    ERROR_MESSAGE       NVARCHAR(2000),
    STARTED_AT          DATETIME,                 -- Job3 start time
    COMPLETED_AT        DATETIME,                 -- Job3 complete time
    PROCESSING_TIME_MS  BIGINT,
    CreatedDate         DATETIME DEFAULT GETDATE(),
    CreatedBy           NVARCHAR(64),
    UpdatedDate         DATETIME,
    UpdatedBy           NVARCHAR(64)
);
CREATE INDEX IX_FILE_HASH ON FILE_UPLOAD(FILE_HASH);
CREATE INDEX IX_FILE_STATUS ON FILE_UPLOAD(STATUS);
GO

-- TRANSACTIONS - Valid data (L3+L4+L5 OK)
IF OBJECT_ID('TRANSACTIONS', 'U') IS NULL
CREATE TABLE TRANSACTIONS (
    ID                      BIGINT IDENTITY(1,1) PRIMARY KEY,
    SOURCE_TYPE             VARCHAR(20) DEFAULT 'FILE',
    SOURCE_REF              NVARCHAR(100),
    FILE_UPLOAD_ID          BIGINT,
    TRANS_REF               NVARCHAR(50),
    DEBIT_ACCOUNT_NO        NVARCHAR(35),
    DEBIT_AMOUNT            DECIMAL(18,2),
    DEBIT_CURRENCY          NVARCHAR(3) DEFAULT 'VND',
    BEN_ACCOUNT_NO          NVARCHAR(35),
    BEN_ACCOUNT_NAME        NVARCHAR(140),
    BEN_BANK                NVARCHAR(200),
    BEN_ADDRESS             NVARCHAR(200),
    BEN_CURRENCY            NVARCHAR(3),
    BEN_IDNO                NVARCHAR(35),
    BEN_IDNO_CREATE_DATE    NVARCHAR(20),
    BEN_IDNO_PLACE          NVARCHAR(100),
    ORDERING_CUST           NVARCHAR(500),
    ORDERING_CUST_ACCOUNT   NVARCHAR(35),
    ORDERING_CUST_ADDRESS   NVARCHAR(200),
    CONTENT                 NVARCHAR(500),
    CHARGE                  NVARCHAR(10),
    PRIORITY                NVARCHAR(10),
    VALUE_DATE              DATE,
    IS_BANK_TO_BANK         BIT DEFAULT 0,
    IS_TRANSFER_TO_SBV      BIT DEFAULT 0,
    CHECK_BEN_NAME          BIT DEFAULT 0,
    STATUS                  INT DEFAULT 0,
    SYNCED_DATE             DATETIME,
    CreatedDate             DATETIME DEFAULT GETDATE(),
    CreatedBy               NVARCHAR(64),
    UpdatedDate             DATETIME,
    UpdatedBy               NVARCHAR(64)
);
CREATE INDEX IX_TRANS_REF ON TRANSACTIONS(TRANS_REF);
CREATE INDEX IX_TRANS_STATUS ON TRANSACTIONS(STATUS);
CREATE INDEX IX_TRANS_SOURCE ON TRANSACTIONS(SOURCE_TYPE, SOURCE_REF);
GO

-- TRANSACTIONS_IMPORT - Invalid data (L3/L4/L5 failed)
IF OBJECT_ID('TRANSACTIONS_IMPORT', 'U') IS NULL
CREATE TABLE TRANSACTIONS_IMPORT (
    ID                      BIGINT IDENTITY(1,1) PRIMARY KEY,
    SOURCE_TYPE             VARCHAR(20) DEFAULT 'FILE',
    SOURCE_REF              NVARCHAR(100),
    FILE_UPLOAD_ID          BIGINT,
    -- Raw + processed data
    TRANS_REF               NVARCHAR(50),
    DEBIT_ACCOUNT_NO        NVARCHAR(35),
    DEBIT_AMOUNT_RAW        NVARCHAR(50),
    DEBIT_AMOUNT            DECIMAL(18,2),
    DEBIT_CURRENCY          NVARCHAR(3) DEFAULT 'VND',
    BEN_ACCOUNT_NO          NVARCHAR(35),
    BEN_ACCOUNT_NAME_RAW    NVARCHAR(500),
    BEN_ACCOUNT_NAME        NVARCHAR(140),
    BEN_BANK_RAW            NVARCHAR(500),
    BEN_BANK                NVARCHAR(200),
    BEN_ADDRESS             NVARCHAR(200),
    BEN_CURRENCY            NVARCHAR(3),
    BEN_IDNO                NVARCHAR(35),
    BEN_IDNO_CREATE_DATE    NVARCHAR(20),
    BEN_IDNO_PLACE          NVARCHAR(100),
    ORDERING_CUST           NVARCHAR(500),
    ORDERING_CUST_ACCOUNT   NVARCHAR(35),
    ORDERING_CUST_ADDRESS   NVARCHAR(200),
    CONTENT_RAW             NVARCHAR(1000),
    CONTENT                 NVARCHAR(500),
    CHARGE                  NVARCHAR(10),
    PRIORITY                NVARCHAR(10),
    VALUE_DATE              DATE,
    IS_BANK_TO_BANK         BIT DEFAULT 0,
    IS_TRANSFER_TO_SBV      BIT DEFAULT 0,
    CHECK_BEN_NAME          BIT DEFAULT 0,
    -- Error tracking
    STATUS                  INT DEFAULT 0,
    FAILED_LEVEL            INT DEFAULT 0,
    VALIDATION_ERRORS       NVARCHAR(MAX),
    VALIDATION_WARNINGS     NVARCHAR(MAX),
    ERR_CODE                NVARCHAR(20),
    ERR_DESC                NVARCHAR(2000),
    -- Field-level error flags
    ERR_TRANS_REF           BIT DEFAULT 0,
    ERR_AMOUNT              BIT DEFAULT 0,
    ERR_DEBIT_ACCOUNT       BIT DEFAULT 0,
    ERR_BEN_ACCOUNT         BIT DEFAULT 0,
    ERR_BEN_NAME            BIT DEFAULT 0,
    ERR_BEN_BANK            BIT DEFAULT 0,
    ERR_CONTENT             BIT DEFAULT 0,
    ERR_VALUE_DATE          BIT DEFAULT 0,
    ERR_DUPLICATE           BIT DEFAULT 0,
    ERR_BUSINESS            BIT DEFAULT 0,
    -- Data quality flags
    HAS_FORMAT_ERROR        BIT DEFAULT 0,
    HAS_LENGTH_WARNING      BIT DEFAULT 0,
    BEN_NAME_TRUNCATED      BIT DEFAULT 0,
    CONTENT_TRUNCATED       BIT DEFAULT 0,
    -- Retry tracking
    RETRY_COUNT             INT DEFAULT 0,
    LAST_RETRY_DATE         DATETIME,
    MOVED_TO_TRANSACTION_ID BIGINT,
    -- Audit
    APPROVED_BY             NVARCHAR(64),
    APPROVED_DATE           DATETIME,
    REJECT_REASON           NVARCHAR(500),
    CreatedDate             DATETIME DEFAULT GETDATE(),
    CreatedBy               NVARCHAR(64),
    UpdatedDate             DATETIME,
    UpdatedBy               NVARCHAR(64)
);
CREATE INDEX IX_IMPORT_REF ON TRANSACTIONS_IMPORT(TRANS_REF);
CREATE INDEX IX_IMPORT_STATUS ON TRANSACTIONS_IMPORT(STATUS);
CREATE INDEX IX_IMPORT_LEVEL ON TRANSACTIONS_IMPORT(FAILED_LEVEL);
CREATE INDEX IX_IMPORT_SOURCE ON TRANSACTIONS_IMPORT(SOURCE_TYPE, SOURCE_REF);
GO

-- AUDIT_LOG - Track all actions
IF OBJECT_ID('AUDIT_LOG', 'U') IS NULL
CREATE TABLE AUDIT_LOG (
    ID                  BIGINT IDENTITY(1,1) PRIMARY KEY,
    ACTION              NVARCHAR(50) NOT NULL,
    ENTITY_TYPE         NVARCHAR(50) NOT NULL,
    ENTITY_ID           BIGINT NOT NULL,
    OLD_VALUES          NVARCHAR(4000),
    NEW_VALUES          NVARCHAR(4000),
    USER_NAME           NVARCHAR(64),
    IP_ADDRESS          NVARCHAR(50),
    CREATED_DATE        DATETIME DEFAULT GETDATE()
);
CREATE INDEX IX_AUDIT_ENTITY ON AUDIT_LOG(ENTITY_TYPE);
CREATE INDEX IX_AUDIT_DATE ON AUDIT_LOG(CREATED_DATE);
GO

-- DOMESTIC_TXT - Lưu dữ liệu MT103 đã parse để tìm kiếm
IF OBJECT_ID('DOMESTIC_TXT', 'U') IS NULL
CREATE TABLE DOMESTIC_TXT (
    ID                  BIGINT IDENTITY(1,1) PRIMARY KEY,
    FILE_UPLOAD_ID      BIGINT NOT NULL,
    
    -- Raw MT103 Fields (original values)
    FIELD_20            NVARCHAR(50),       -- Sender's Reference
    FIELD_32A           NVARCHAR(50),       -- Value Date/Currency/Amount
    FIELD_50K           NVARCHAR(500),      -- Ordering Customer (combined)
    FIELD_50R           NVARCHAR(35),       -- Ordering Customer Account
    FIELD_50S           NVARCHAR(70),       -- Ordering Customer Name
    FIELD_50T           NVARCHAR(70),       -- Ordering Customer Address
    FIELD_53            NVARCHAR(35),       -- Sender's Correspondent (Debit Account)
    FIELD_57            NVARCHAR(70),       -- Beneficiary Address
    FIELD_57A           NVARCHAR(200),      -- Account With Institution (Ben Bank)
    FIELD_59            NVARCHAR(500),      -- Beneficiary Customer (Account + Name)
    FIELD_59O           NVARCHAR(35),       -- Beneficiary ID Number
    FIELD_59P           NVARCHAR(20),       -- ID Issue Date
    FIELD_59Q           NVARCHAR(100),      -- ID Issue Place
    FIELD_70            NVARCHAR(500),      -- Remittance Information (Content)
    FIELD_71A           NVARCHAR(3),        -- Details of Charges (BEN/OUR/SHA)
    FIELD_72A           CHAR(1),            -- Bank to Bank Transfer (Y/N)
    FIELD_72B           CHAR(1),            -- Transfer to SBV (Y/N)
    FIELD_72C           CHAR(1),            -- Check Ben Name (Y/N)
    FIELD_72D           CHAR(1),            -- Priority (Y/N)
    
    -- Parsed/Computed Values (for search & display)
    VALUE_DATE          DATE,
    CURRENCY            NVARCHAR(3),
    AMOUNT              DECIMAL(18,2),
    DEBIT_ACCOUNT       NVARCHAR(35),       -- Extracted from FIELD_53
    BEN_ACCOUNT         NVARCHAR(35),       -- Extracted from FIELD_59
    BEN_NAME            NVARCHAR(140),      -- Extracted from FIELD_59
    
    -- Processing Status
    STATUS              INT DEFAULT 0,      -- 0:New, 1:Valid, 2:Transformed, 3:Error
    ERR_CODE            NVARCHAR(20),
    ERR_DESC            NVARCHAR(500),
    
    -- Link to results
    TRANSACTION_ID      BIGINT,             -- FK to TRANSACTIONS if valid
    IMPORT_ID           BIGINT,             -- FK to TRANSACTIONS_IMPORT if invalid
    
    -- Audit
    CreatedDate         DATETIME DEFAULT GETDATE(),
    CreatedBy           NVARCHAR(64),
    UpdatedDate         DATETIME,
    UpdatedBy           NVARCHAR(64),
    
    -- Foreign Key
    CONSTRAINT FK_DOMESTIC_TXT_FILE FOREIGN KEY (FILE_UPLOAD_ID) REFERENCES FILE_UPLOAD(FILE_ID)
);
-- Indexes for search
CREATE INDEX IX_DOMESTIC_FILE ON DOMESTIC_TXT(FILE_UPLOAD_ID);
CREATE INDEX IX_DOMESTIC_REF ON DOMESTIC_TXT(FIELD_20);
CREATE INDEX IX_DOMESTIC_STATUS ON DOMESTIC_TXT(STATUS);
CREATE INDEX IX_DOMESTIC_DEBIT ON DOMESTIC_TXT(DEBIT_ACCOUNT);
CREATE INDEX IX_DOMESTIC_BEN ON DOMESTIC_TXT(BEN_ACCOUNT);
CREATE INDEX IX_DOMESTIC_DATE ON DOMESTIC_TXT(VALUE_DATE);
CREATE INDEX IX_DOMESTIC_AMOUNT ON DOMESTIC_TXT(AMOUNT);

-- Performance indexes for large data (100k+ records)
CREATE NONCLUSTERED INDEX IX_DOMESTIC_TXT_SEARCH 
ON DOMESTIC_TXT (VALUE_DATE, STATUS)
INCLUDE (FIELD_20, AMOUNT, DEBIT_ACCOUNT, BEN_ACCOUNT, BEN_NAME);

CREATE NONCLUSTERED INDEX IX_DOMESTIC_TXT_AMOUNT 
ON DOMESTIC_TXT (AMOUNT DESC)
INCLUDE (FIELD_20, VALUE_DATE, CURRENCY);
GO

-- Add covering indexes to TRANSACTIONS for large data
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TRANS_SEARCH')
CREATE NONCLUSTERED INDEX IX_TRANS_SEARCH 
ON TRANSACTIONS (STATUS, VALUE_DATE)
INCLUDE (TRANS_REF, DEBIT_AMOUNT, BEN_ACCOUNT_NO, BEN_ACCOUNT_NAME);
GO

-- Add covering indexes to TRANSACTIONS_IMPORT for retry processing
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_IMPORT_RETRY')
CREATE NONCLUSTERED INDEX IX_IMPORT_RETRY 
ON TRANSACTIONS_IMPORT (FAILED_LEVEL, STATUS, RETRY_COUNT)
WHERE STATUS = 0;
GO

-- =====================================================
-- DATABASE 2: DBTRUNGGIAN
-- Tables: PAYORDER, DOMESTIC_RESULT
-- =====================================================
USE master;
GO
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'DBTRUNGGIAN')
    CREATE DATABASE DBTRUNGGIAN;
GO
USE DBTRUNGGIAN;
GO

-- PAYORDER
IF OBJECT_ID('PAYORDER', 'U') IS NULL
CREATE TABLE PAYORDER (
    SOGD                NVARCHAR(20) PRIMARY KEY,
    NGAY                SMALLDATETIME NOT NULL,
    TKNO                NVARCHAR(15) NOT NULL,
    LNT                 NVARCHAR(3) DEFAULT 'VND',
    SOTIEN              FLOAT NOT NULL,
    UUTIEN              SMALLINT,
    TKNH                NVARCHAR(35),
    TENNH               NVARCHAR(144),
    DCNH                NVARCHAR(70),
    PHI                 NVARCHAR(3),
    NHNH                NVARCHAR(100),
    ND                  NVARCHAR(140),
    TENNRL              NVARCHAR(144),
    UPVCBMONEY          SMALLINT DEFAULT 0,     -- 0=New, 1=Valid, 2=Imported, -1=Error
    DCNRL               NVARCHAR(180),
    SOTK_NRL            NVARCHAR(34),
    DC_NRL              NVARCHAR(180),
    TENTKNO             NVARCHAR(100),
    CMT_NRL             NVARCHAR(50),
    NGAY_GT             DATETIME,
    DKKD_NRL            NVARCHAR(70),
    BANK_TO_BANK        BIT,
    BANK_TO_BANK_SBV    BIT,
    INQUIRY_BEN_NAME    BIT,
    ErrCode             NVARCHAR(3),
    ErrDes              NVARCHAR(250),
    upDatetime          NVARCHAR(100)
);
CREATE INDEX IX_PAYORDER_STATUS ON PAYORDER(UPVCBMONEY);
GO

-- DOMESTIC_RESULT
IF OBJECT_ID('DOMESTIC_RESULT', 'U') IS NULL
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
    STATUS              CHAR(3) DEFAULT '202',
    ERROR_CODE          NVARCHAR(10),
    ERROR_MESSAGE       NVARCHAR(500),
    IMPORTED_TIME       DATETIME DEFAULT GETDATE(),
    PROCESSED_TIME      DATETIME,
    CreatedDate         DATETIME DEFAULT GETDATE(),
    CreatedBy           NVARCHAR(64),
    UpdatedDate         DATETIME,
    UpdatedBy           NVARCHAR(64)
);
CREATE INDEX IX_RESULT_REF ON DOMESTIC_RESULT(TRANS_REF);
GO

-- Sample data
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, TKNH, TENNH, ND, UPVCBMONEY)
VALUES 
('PO20250116001', GETDATE(), '1234567890', 'VND', 1000000, '9876543210', N'NGUYEN VAN A', N'Thanh toan', 0),
('PO20250116002', GETDATE(), '1234567891', 'VND', 2500000, '9876543211', N'TRAN THI B', N'Chuyen khoan', 0);
GO

PRINT 'Database setup completed!';
PRINT 'VCB_MONEY: FILE_UPLOAD, TRANSACTIONS, TRANSACTIONS_IMPORT, DOMESTIC_TXT, AUDIT_LOG';
PRINT 'DBTRUNGGIAN: PAYORDER, DOMESTIC_RESULT';
