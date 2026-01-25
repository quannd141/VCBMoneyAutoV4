-- =====================================================
-- VCBMoney Integration v4.2 - Database Tables
-- Based on VCBMoney_Auto Excel Specification
-- =====================================================

-- =====================================================
-- DATABASE: DBTRUNGGIAN
-- Tables: PAYORDER, DOMESTIC_RESULT
-- =====================================================
USE master;
GO
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'DBTRUNGGIAN')
    CREATE DATABASE DBTRUNGGIAN;
GO
USE DBTRUNGGIAN;
GO

-- =====================================================
-- TABLE: PAYORDER
-- Ủy nhiệm chi - Bảng SQL Payment cũ (Auto scan from MSSQL)
-- =====================================================
IF OBJECT_ID('PAYORDER', 'U') IS NOT NULL
    DROP TABLE PAYORDER;
GO

CREATE TABLE PAYORDER (
    -- Core fields (NOT NULL)
    SOGD                NVARCHAR(20)    NOT NULL,           -- Số giao dịch (Transaction Reference)
    NGAY                SMALLDATETIME   NOT NULL,           -- Ngày giao dịch (Transaction Date)
    TKNO                NVARCHAR(15)    NOT NULL,           -- Tài khoản trích Nợ (Debit Account)
    LNT                 NVARCHAR(3)     NOT NULL,           -- Loại Ngoại tệ ghi có (Currency)
    SOTIEN              FLOAT           NOT NULL,           -- Số tiền (Amount)
    
    -- Beneficiary info (Nullable)
    UUTIEN              SMALLINT        NULL,               -- Ưu tiên (1: ưu tiên) (Priority)
    TKNH                NVARCHAR(35)    NULL,               -- Tài khoản người hưởng (Beneficiary Account)
    TENNH               NVARCHAR(144)   NULL,               -- Tên Người hưởng (Beneficiary Name)
    DCNH                NVARCHAR(70)    NULL,               -- Địa chỉ người hưởng (Beneficiary Address)
    PHI                 NVARCHAR(3)     NULL,               -- Phí: EXC/INC (Charge)
    NHNH                NVARCHAR(100)   NULL,               -- Ngân hàng người hưởng (Beneficiary Bank)
    ND                  NVARCHAR(140)   NULL,               -- Nội dung (Content)
    
    -- Ordering customer info
    TENNRL              NVARCHAR(144)   NULL,               -- Tên người ra lệnh (Ordering Customer Name)
    DCNRL               NVARCHAR(180)   NULL,               -- Địa chỉ người ra lệnh (duplicate in spec)
    SOTK_NRL            NVARCHAR(34)    NULL,               -- Số tài khoản người ra lệnh (Ordering Account)
    DC_NRL              NVARCHAR(180)   NULL,               -- Địa chỉ người ra lệnh (Ordering Address)
    
    -- Beneficiary ID info
    BEN_IDNO            NVARCHAR(35)    NULL,               -- (13) Số giấy tờ tùy thân
    BEN_ID_ISSUE_DATE   NVARCHAR(20)    NULL,               -- (14) Ngày cấp
    BEN_ID_ISSUE_PLACE  NVARCHAR(100)   NULL,               -- (15) Nơi cấp
    
    -- Transfer flags
    BANK_TO_BANK        BIT             NULL,               -- (18) Chuyển tiền giữa các tổ chức tài chính
    BANK_TO_BANK_SBV    BIT             NULL,               -- (19) Điều vốn đến ngân hàng nhà nước
    INQUIRY_BEN_NAME    BIT             NULL,               -- Kiểm tra tên người hưởng
    
    -- VCBMoney processing fields
    UPVCBMONEY          SMALLINT        NULL,               -- Cờ đã quét VCBMoney (0/null: chưa, 1: đã)
    ErrCode             NVARCHAR(3)     NULL,               -- Mã lỗi quét điện
    ErrDes              NVARCHAR(250)   NULL,               -- Mô tả thông tin lỗi
    upDatetime          NVARCHAR(100)   NULL,               -- Thời gian -> VCB
    
    -- Bank info fields (Chưa dùng)
    BICCODE_NHNH        NVARCHAR(50)    NULL,               -- Chưa dùng
    BICNAME_NHNH        NVARCHAR(100)   NULL,               -- Chưa dùng
    BANKCODE_NHNH       NVARCHAR(50)    NULL,               -- Chưa dùng
    BANKNAME_NHNH       NVARCHAR(100)   NULL,               -- Chưa dùng
    TENDC_NHNH          NVARCHAR(180)   NULL,               -- Chưa dùng
    
    -- NRL info fields (Chưa dùng)
    HC_NRL              NVARCHAR(50)    NULL,               -- Chưa dùng
    CMT_NRL             NVARCHAR(50)    NULL,               -- Chưa dùng
    DKKD_NRL            NVARCHAR(70)    NULL,               -- Chưa dùng
    MAQG_NRL            NVARCHAR(50)    NULL,               -- Chưa dùng
    
    -- NHTG info fields (Chưa dùng)
    BICCODE_NHTG        NVARCHAR(50)    NULL,               -- Chưa dùng
    BICNAME_NHTG        NVARCHAR(100)   NULL,               -- Chưa dùng
    BANKCODE_NHTG       NVARCHAR(50)    NULL,               -- Chưa dùng
    BANKNAME_NHTG       NVARCHAR(100)   NULL,               -- Chưa dùng
    TENDC_NHTG          NVARCHAR(180)   NULL,               -- Chưa dùng
    
    -- Other reserved fields
    TTGUI_NNL           NVARCHAR(200)   NULL,               -- Chưa dùng
    CHECKS              NUMERIC(18,0)   NULL,               -- Chưa dùng
    TENTKNO             NVARCHAR(100)   NULL,               -- Chưa dùng
    DCNGL               NVARCHAR(140)   NULL,               -- Chưa dùng
    TG_Status           NVARCHAR(2)     NULL,               -- Chưa dùng
    ChkS                NVARCHAR(100)   NULL,               -- Chưa dùng
    
    -- Primary Key
    CONSTRAINT PK_PAYORDER PRIMARY KEY (SOGD)
);

-- Indexes
CREATE INDEX IX_PAYORDER_UPVCBMONEY ON PAYORDER(UPVCBMONEY);
CREATE INDEX IX_PAYORDER_NGAY ON PAYORDER(NGAY);
GO

PRINT 'Table PAYORDER created successfully.';
GO

-- =====================================================
-- TABLE: DOMESTIC_RESULT
-- Kết quả điện chuyển tiền trong nước
-- =====================================================
IF OBJECT_ID('DOMESTIC_RESULT', 'U') IS NOT NULL
    DROP TABLE DOMESTIC_RESULT;
GO

CREATE TABLE DOMESTIC_RESULT (
    -- Primary Key
    ID                      BIGINT          IDENTITY(1,1) NOT NULL,
    
    -- Customer & Reference
    CUSTOMER_CODE           NVARCHAR(20)    NOT NULL,           -- Mã khách hàng
    TRANS_REF               NVARCHAR(50)    NULL,               -- Mã tham chiếu của khách
    
    -- Debit info
    DEBIT_ACCOUNT           NVARCHAR(20)    NULL,               -- Số tài khoản trích nợ
    DEBIT_NAME              NVARCHAR(200)   NULL,               -- Tên chủ tài khoản nợ
    AMOUNT                  DECIMAL(18,2)   NULL,               -- Số tiền giao dịch
    CURRENCY                NVARCHAR(3)     NULL,               -- Mã tiền tệ (ISO 4217)
    CONTENT                 NVARCHAR(500)   NULL,               -- Nội dung giao dịch
    
    -- Beneficiary info
    BEN_ACCOUNT_NO          NVARCHAR(50)    NOT NULL,           -- Số tài khoản thụ hưởng
    BEN_ACCOUNT_NAME        NVARCHAR(200)   NULL,               -- Tên tài khoản thụ hưởng
    BEN_BANK                NVARCHAR(150)   NOT NULL,           -- Ngân hàng thụ hưởng
    BEN_BANK_CODE           NVARCHAR(15)    NULL,               -- Mã ngân hàng thụ hưởng (CITAD/SWIFT)
    BEN_CURRENCY            NVARCHAR(3)     NOT NULL,           -- Loại tiền người thụ hưởng
    BEN_ADDRESS             NVARCHAR(200)   NULL,               -- Địa chỉ người thụ hưởng
    BEN_IDNO                NVARCHAR(35)    NULL,               -- Số CMND/CCCD người thụ hưởng
    
    -- Transaction details
    CHARGE                  NVARCHAR(10)    NULL,               -- Loại phí (INC/EXC)
    VALUE_DATE              DATE            NULL,               -- Ngày giá trị
    PRIORITY                NVARCHAR(10)    NULL,               -- Độ ưu tiên (HIGH/NORMAL/LOW)
    
    -- Banking references
    BANK_TRAN_REF           NVARCHAR(50)    NULL,               -- Mã tham chiếu Core Banking
    CREDIT_AMOUNT           DECIMAL(20,2)   NULL,               -- Số tiền ghi có
    FEE_AMOUNT              DECIMAL(20,2)   NULL,               -- Số tiền phí
    VAT_AMOUNT              DECIMAL(20,2)   NULL,               -- Số tiền VAT
    
    -- Status & Error
    STATUS                  CHAR(3)         NOT NULL DEFAULT '201', -- Trạng thái giao dịch
    ERROR_CODE              NVARCHAR(10)    NULL,               -- Mã lỗi nếu thất bại
    ERROR_MESSAGE           NVARCHAR(500)   NULL,               -- Chi tiết lỗi
    ERROR_SUGGESTION        NVARCHAR(500)   NULL,               -- Gợi ý cách sửa lỗi
    
    -- Timestamps
    IMPORTED_TIME           DATETIME        NOT NULL DEFAULT GETDATE(), -- Thời gian import bản ghi
    PROCESSED_TIME          DATETIME        NULL,               -- Thời gian xử lý xong
    BANK_PROCESSED_TIME     DATETIME        NULL,               -- Thời gian ngân hàng xử lý
    
    -- Bank remarks
    BANK_REMARK             NVARCHAR(500)   NULL,               -- Ghi chú nội bộ ngân hàng
    
    -- Reserved fields (text)
    REV1                    NVARCHAR(500)   NULL,               -- Trường dự trữ 1 (text)
    REV2                    NVARCHAR(500)   NULL,               -- Trường dự trữ 2 (text)
    REV3                    NVARCHAR(500)   NULL,               -- Trường dự trữ 3 (text)
    REV4                    NVARCHAR(500)   NULL,               -- Trường dự trữ 4 (text)
    REV5                    NVARCHAR(500)   NULL,               -- Trường dự trữ 5 (text)
    
    -- Reserved fields (numeric)
    REV_NUM1                DECIMAL(18,2)   NULL,               -- Trường dự trữ số 1
    REV_NUM2                DECIMAL(18,2)   NULL,               -- Trường dự trữ số 2
    
    -- Reserved fields (datetime)
    REV_DATE1               DATETIME        NULL,               -- Trường dự trữ ngày 1
    REV_DATE2               DATETIME        NULL,               -- Trường dự trữ ngày 2
    
    -- Reserved fields (amount)
    REV_AMT1                DECIMAL(18,2)   NULL,               -- Trường dự trữ tiền 1
    
    -- Customer defined fields
    ERRNUM                  INT             NULL,               -- Mã lỗi KH tự định nghĩa
    ERRDES                  NVARCHAR(500)   NULL,               -- Mô tả lỗi KH tự định nghĩa
    UPCORE                  INT             NULL,               -- Cờ đã hạch toán Core (null: chưa, 1: đã)
    
    -- Audit fields
    CREATED_DATE            DATETIME        NOT NULL DEFAULT GETDATE(),
    CREATED_BY              NVARCHAR(64)    NOT NULL DEFAULT 'SYSTEM',
    UPDATED_DATE            DATETIME        NOT NULL DEFAULT GETDATE(),
    UPDATED_BY              NVARCHAR(64)    NOT NULL DEFAULT 'SYSTEM',
    
    -- Approval workflow
    CHECKED_BY              NVARCHAR(64)    NULL,               -- Người kiểm soát
    CHECKED_DATE            DATETIME        NULL,               -- Ngày kiểm soát
    CHECKED_NOTE            NVARCHAR(1500)  NULL,               -- Lý do từ chối (checker)
    VERIFIED_BY             NVARCHAR(64)    NULL,               -- Người xác minh
    VERIFIED_DATE           DATETIME        NULL,               -- Ngày xác minh
    APPROVED_BY             NVARCHAR(64)    NULL,               -- Người phê duyệt
    APPROVED_DATE           DATETIME        NULL,               -- Ngày phê duyệt
    APPROVED_NOTE           NVARCHAR(1500)  NULL,               -- Lý do từ chối (approver)
    
    -- VCB processing fields
    PCTIME                  DATETIME        NULL,               -- Core posting date
    TELLER_ID               NVARCHAR(10)    NULL,               -- VCB teller ID
    HOSTDATE                DATETIME        NULL,               -- Host date
    VDATE                   DATETIME        NULL,               -- Value date (datetime)
    SEQUENCE_NO             INT             NULL,               -- Số thứ tự voucher
    RTL_REF                 NVARCHAR(50)    NULL,               -- Số hiệu giao dịch
    
    -- Primary Key
    CONSTRAINT PK_DOMESTIC_RESULT PRIMARY KEY (ID)
);

-- Indexes
CREATE INDEX IX_DOMESTIC_RESULT_TRANS_REF ON DOMESTIC_RESULT(TRANS_REF);
CREATE INDEX IX_DOMESTIC_RESULT_CUSTOMER ON DOMESTIC_RESULT(CUSTOMER_CODE);
CREATE INDEX IX_DOMESTIC_RESULT_STATUS ON DOMESTIC_RESULT(STATUS);
CREATE INDEX IX_DOMESTIC_RESULT_BEN_ACCOUNT ON DOMESTIC_RESULT(BEN_ACCOUNT_NO);
CREATE INDEX IX_DOMESTIC_RESULT_IMPORTED ON DOMESTIC_RESULT(IMPORTED_TIME);
GO

PRINT 'Table DOMESTIC_RESULT created successfully.';
GO

-- =====================================================
-- TABLE: DOMESTIC_TXT (VCB_MONEY database)
-- Chuyển tiền trong nước (Plaintext format based on SWIFT MT103)
-- =====================================================
USE master;
GO
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'VCB_MONEY')
    CREATE DATABASE VCB_MONEY;
GO
USE VCB_MONEY;
GO

IF OBJECT_ID('DOMESTIC_TXT', 'U') IS NOT NULL
    DROP TABLE DOMESTIC_TXT;
GO

CREATE TABLE DOMESTIC_TXT (
    -- Primary Key
    ID                  BIGINT          IDENTITY(1,1) NOT NULL,
    FILE_UPLOAD_ID      BIGINT          NOT NULL,           -- FK to FILE_UPLOAD
    
    -- MT103 Fields
    FIELD_20            NVARCHAR(50)    NULL,               -- Sender's Reference (Customer ref)
    FIELD_32A           NVARCHAR(50)    NULL,               -- Value Date/Currency/Amount (YYMMDD + CCY + Amount)
    FIELD_50K           NVARCHAR(500)   NULL,               -- Ordering Customer (175x)
    FIELD_50R           NVARCHAR(35)    NULL,               -- Ordering Customer Account
    FIELD_50S           NVARCHAR(70)    NULL,               -- Ordering Customer Name
    FIELD_50T           NVARCHAR(70)    NULL,               -- Ordering Customer Address
    FIELD_53            NVARCHAR(35)    NULL,               -- Sender's Correspondent (Debit Account)
    FIELD_57            NVARCHAR(70)    NULL,               -- Beneficiary Address
    FIELD_57A           NVARCHAR(140)   NULL,               -- Account With Institution (Ben Bank)
    FIELD_59            NVARCHAR(250)   NULL,               -- Beneficiary Customer (Account + Name)
    FIELD_59O           NVARCHAR(35)    NULL,               -- Beneficiary ID Number
    FIELD_59P           NVARCHAR(20)    NULL,               -- ID Issue Date
    FIELD_59Q           NVARCHAR(100)   NULL,               -- ID Issue Place
    FIELD_70            NVARCHAR(500)   NULL,               -- Remittance Information (Content)
    FIELD_71A           NVARCHAR(3)     NULL,               -- Details of Charges (BEN/OUR/SHA)
    FIELD_72A           CHAR(1)         NULL,               -- Bank to Bank Transfer (Y/N)
    FIELD_72B           CHAR(1)         NULL,               -- Transfer to SBV (Y/N)
    FIELD_72C           CHAR(1)         NULL,               -- Check Ben Name (Y/N)
    FIELD_72D           CHAR(1)         NULL,               -- Priority (Y/N)
    
    -- Parsed values
    VALUE_DATE          DATE            NULL,               -- Parsed from FIELD_32A
    CURRENCY            NVARCHAR(3)     NULL,               -- Parsed from FIELD_32A
    AMOUNT              DECIMAL(18,2)   NULL,               -- Parsed from FIELD_32A
    DEBIT_ACCOUNT       NVARCHAR(35)    NULL,               -- Parsed from FIELD_53
    BEN_ACCOUNT         NVARCHAR(35)    NULL,               -- Parsed from FIELD_59 (first line)
    BEN_NAME            NVARCHAR(140)   NULL,               -- Parsed from FIELD_59 (second line)
    
    -- Processing status
    STATUS              INT             NOT NULL DEFAULT 0, -- 0:New, 1:Valid, 2:Processing, 3:Done, -1:Error
    ERR_CODE            NVARCHAR(20)    NULL,               -- Error code
    ERR_DESC            NVARCHAR(500)   NULL,               -- Error description
    
    -- Audit
    CREATED_DATE        DATETIME        NOT NULL DEFAULT GETDATE(),
    CREATED_BY          NVARCHAR(64)    NULL,
    UPDATED_DATE        DATETIME        NULL,
    UPDATED_BY          NVARCHAR(64)    NULL,
    
    -- Primary Key
    CONSTRAINT PK_DOMESTIC_TXT PRIMARY KEY (ID)
);

-- Indexes
CREATE INDEX IX_DOMESTIC_TXT_FILE ON DOMESTIC_TXT(FILE_UPLOAD_ID);
CREATE INDEX IX_DOMESTIC_TXT_STATUS ON DOMESTIC_TXT(STATUS);
CREATE INDEX IX_DOMESTIC_TXT_FIELD20 ON DOMESTIC_TXT(FIELD_20);
GO

PRINT 'Table DOMESTIC_TXT created successfully.';
GO

-- =====================================================
-- Summary
-- =====================================================
PRINT '';
PRINT '=====================================================';
PRINT 'Database tables created:';
PRINT '  DBTRUNGGIAN.PAYORDER       - Ủy nhiệm chi';
PRINT '  DBTRUNGGIAN.DOMESTIC_RESULT - Kết quả chuyển tiền';
PRINT '  VCB_MONEY.DOMESTIC_TXT     - MT103 parsed data';
PRINT '=====================================================';
GO
