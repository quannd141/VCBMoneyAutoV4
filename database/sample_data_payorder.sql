-- ============================================================
-- VCBMoney v4.2 - Sample Data for PAYORDER Testing
-- Database: DBTRUNGGIAN
-- ============================================================

USE DBTRUNGGIAN;
GO

-- ============================================================
-- SECTION 1: Small batch (10 records) - Quick test
-- ============================================================
PRINT '=== Inserting 10 sample records for quick test ===';

INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES
('TT20250122001', '2025-01-22', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'NGUYỄN VĂN A', N'123 Lê Lợi, Q1, HCM', 'OUR', N'VIETCOMBANK CN HCM', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TT20250122002', '2025-01-22', '1234567890123', 'VND', 75000000, 1, '9876543210002', N'TRẦN THỊ B', N'456 Nguyễn Huệ, Q1, HCM', 'OUR', N'VIETINBANK CN HCM', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TT20250122003', '2025-01-22', '1234567890123', 'VND', 45000000, 1, '9876543210003', N'LÊ VĂN C', N'789 Hai Bà Trưng, Q3, HCM', 'OUR', N'BIDV CN Q3', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TT20250122004', '2025-01-22', '1234567890123', 'VND', 60000000, 1, '9876543210004', N'PHẠM THỊ D', N'12 Pasteur, Q3, HCM', 'OUR', N'AGRIBANK CN Q3', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TT20250122005', '2025-01-22', '1234567890123', 'VND', 55000000, 1, '9876543210005', N'HOÀNG VĂN E', N'34 Nam Kỳ Khởi Nghĩa, Q1, HCM', 'OUR', N'TECHCOMBANK CN HCM', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TT20250122006', '2025-01-22', '1234567890123', 'VND', 80000000, 1, '9876543210006', N'VŨ THỊ F', N'56 Lý Tự Trọng, Q1, HCM', 'OUR', N'MB BANK CN HCM', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TT20250122007', '2025-01-22', '1234567890123', 'VND', 42000000, 1, '9876543210007', N'ĐẶNG VĂN G', N'78 Trần Hưng Đạo, Q5, HCM', 'OUR', N'ACB CN Q5', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TT20250122008', '2025-01-22', '1234567890123', 'VND', 95000000, 1, '9876543210008', N'BÙI THỊ H', N'90 Võ Văn Tần, Q3, HCM', 'OUR', N'SACOMBANK CN Q3', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TT20250122009', '2025-01-22', '1234567890123', 'VND', 38000000, 1, '9876543210009', N'ĐINH VĂN I', N'123 Cách Mạng Tháng 8, Q10, HCM', 'OUR', N'VPBANK CN Q10', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TT20250122010', '2025-01-22', '1234567890123', 'VND', 67000000, 1, '9876543210010', N'NGÔ THỊ K', N'456 Ba Tháng Hai, Q10, HCM', 'OUR', N'EXIMBANK CN Q10', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0);

PRINT 'Inserted 10 records.';
GO

-- ============================================================
-- SECTION 2: Medium batch (100 records)
-- ============================================================
PRINT '=== Inserting 100 sample records ===';

DECLARE @i INT = 11;
DECLARE @sogd NVARCHAR(20);
DECLARE @tknh NVARCHAR(35);
DECLARE @tennh NVARCHAR(144);
DECLARE @sotien FLOAT;
DECLARE @banks TABLE (BankName NVARCHAR(100));

INSERT INTO @banks VALUES 
(N'VIETCOMBANK'), (N'VIETINBANK'), (N'BIDV'), (N'AGRIBANK'), 
(N'TECHCOMBANK'), (N'MB BANK'), (N'ACB'), (N'SACOMBANK'), 
(N'VPBANK'), (N'EXIMBANK'), (N'SHB'), (N'HDBank');

WHILE @i <= 110
BEGIN
    SET @sogd = 'TT20250122' + RIGHT('000' + CAST(@i AS VARCHAR(3)), 3);
    SET @tknh = '98765432' + RIGHT('00000' + CAST(@i AS VARCHAR(5)), 5);
    SET @tennh = N'NGƯỜI THỤ HƯỞNG ' + CAST(@i AS NVARCHAR(10));
    SET @sotien = 30000000 + (RAND() * 70000000); -- 30M - 100M
    
    INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
    VALUES (
        @sogd,
        '2025-01-22',
        '1234567890123',
        'VND',
        @sotien,
        1,
        @tknh,
        @tennh,
        N'Địa chỉ ' + CAST(@i AS NVARCHAR(10)) + N', TP.HCM',
        'OUR',
        (SELECT TOP 1 BankName FROM @banks ORDER BY NEWID()),
        N'Thanh toán lương tháng 1/2025 - NV' + CAST(@i AS NVARCHAR(10)),
        N'CÔNG TY TNHH ABC',
        0
    );
    
    SET @i = @i + 1;
END

PRINT 'Inserted 100 more records (total 110).';
GO

-- ============================================================
-- SECTION 3: Large batch (1000 records) - Performance test
-- ============================================================
PRINT '=== Inserting 1000 sample records for performance test ===';

DECLARE @j INT = 111;
DECLARE @sogd2 NVARCHAR(20);
DECLARE @tknh2 NVARCHAR(35);
DECLARE @tennh2 NVARCHAR(144);
DECLARE @sotien2 FLOAT;

WHILE @j <= 1110
BEGIN
    SET @sogd2 = 'TT20250122' + RIGHT('0000' + CAST(@j AS VARCHAR(4)), 4);
    SET @tknh2 = '9876543' + RIGHT('000000' + CAST(@j AS VARCHAR(6)), 6);
    SET @tennh2 = N'NHÂN VIÊN ' + CAST(@j AS NVARCHAR(10));
    SET @sotien2 = 20000000 + (RAND() * 80000000); -- 20M - 100M
    
    INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
    VALUES (
        @sogd2,
        DATEADD(DAY, -(@j % 30), '2025-01-22'), -- Spread across 30 days
        '1234567890123',
        'VND',
        @sotien2,
        CASE WHEN @j % 10 = 0 THEN 2 ELSE 1 END, -- Some high priority
        @tknh2,
        @tennh2,
        N'Địa chỉ nhân viên ' + CAST(@j AS NVARCHAR(10)),
        CASE WHEN @j % 3 = 0 THEN 'BEN' WHEN @j % 3 = 1 THEN 'SHA' ELSE 'OUR' END,
        CASE 
            WHEN @j % 12 = 0 THEN N'VIETCOMBANK'
            WHEN @j % 12 = 1 THEN N'VIETINBANK'
            WHEN @j % 12 = 2 THEN N'BIDV'
            WHEN @j % 12 = 3 THEN N'AGRIBANK'
            WHEN @j % 12 = 4 THEN N'TECHCOMBANK'
            WHEN @j % 12 = 5 THEN N'MB BANK'
            WHEN @j % 12 = 6 THEN N'ACB'
            WHEN @j % 12 = 7 THEN N'SACOMBANK'
            WHEN @j % 12 = 8 THEN N'VPBANK'
            WHEN @j % 12 = 9 THEN N'EXIMBANK'
            WHEN @j % 12 = 10 THEN N'SHB'
            ELSE N'HDBank'
        END,
        N'Lương T1/2025 - ' + @tennh2,
        N'CÔNG TY SAMSUNG ELECTRONICS VIETNAM',
        0
    );
    
    SET @j = @j + 1;
END

PRINT 'Inserted 1000 more records (total 1110).';
GO

-- ============================================================
-- SECTION 4: Test cases with specific scenarios
-- ============================================================
PRINT '=== Inserting special test cases ===';

-- Test case: Duplicate TransRef (should be caught by Job4)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TT20250122001', '2025-01-22', '1234567890123', 'VND', 50000000, 1, '9999999999999', N'DUPLICATE TEST', N'Test Address', 'OUR', N'VIETCOMBANK', N'Duplicate test', N'TEST COMPANY', 0);
-- Note: This will fail if SOGD is primary key - that's expected behavior

-- Test case: Invalid amount (0)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TTERR001', '2025-01-22', '1234567890123', 'VND', 0, 1, '9999999999001', N'ZERO AMOUNT TEST', N'Test', 'OUR', N'VIETCOMBANK', N'Zero amount', N'TEST', 0);

-- Test case: Missing beneficiary account
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TTERR002', '2025-01-22', '1234567890123', 'VND', 50000000, 1, NULL, N'NO ACCOUNT TEST', N'Test', 'OUR', N'VIETCOMBANK', N'No account', N'TEST', 0);

-- Test case: Very long beneficiary name (should be truncated by L4)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TTERR003', '2025-01-22', '1234567890123', 'VND', 50000000, 1, '9999999999003', 
    N'TÊN RẤT DÀI ĐỂ TEST TRUNCATE - NGUYỄN VĂN ABCDEFGHIJKLMNOPQRSTUVWXYZ ABCDEFGHIJKLMNOPQRSTUVWXYZ ABCDEFGHIJKLMNOPQRSTUVWXYZ ABCDEFGHIJKLMNOPQRSTUVWXYZ', 
    N'Test', 'OUR', N'VIETCOMBANK', N'Long name test', N'TEST', 0);

-- Test case: Special characters in content
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TTERR004', '2025-01-22', '1234567890123', 'VND', 50000000, 1, '9999999999004', N'SPECIAL CHAR TEST', N'Test', 'OUR', N'VIETCOMBANK', 
    N'Nội dung có ký tự đặc biệt: @#$%^&*(){}[]|\\', N'TEST', 0);

-- Test case: Large amount (1 billion)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TTLARGE001', '2025-01-22', '1234567890123', 'VND', 1000000000, 2, '9999999999005', N'LARGE AMOUNT TEST', N'Test', 'OUR', N'VIETCOMBANK', N'1 billion VND', N'TEST', 0);

-- Test case: With BEN_IDNO (CMND/CCCD)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY, BEN_IDNO, BEN_ID_ISSUE_DATE, BEN_ID_ISSUE_PLACE)
VALUES ('TTID001', '2025-01-22', '1234567890123', 'VND', 50000000, 1, '9999999999006', N'PERSON WITH ID', N'Test', 'OUR', N'VIETCOMBANK', N'Test with ID', N'TEST', 0, '079123456789', '2020-01-15', N'CA TP.HCM');

PRINT 'Inserted special test cases.';
GO

-- ============================================================
-- SECTION 5: Verify data
-- ============================================================
PRINT '=== Data Summary ===';

SELECT 
    'Total Records' AS Metric,
    COUNT(*) AS Value
FROM PAYORDER

UNION ALL

SELECT 
    'New (UpvcbMoney=0)',
    COUNT(*)
FROM PAYORDER WHERE UPVCBMONEY = 0 OR UPVCBMONEY IS NULL

UNION ALL

SELECT 
    'Valid (UpvcbMoney=1)',
    COUNT(*)
FROM PAYORDER WHERE UPVCBMONEY = 1

UNION ALL

SELECT 
    'Imported (UpvcbMoney=2)',
    COUNT(*)
FROM PAYORDER WHERE UPVCBMONEY = 2

UNION ALL

SELECT 
    'Error (UpvcbMoney=-1)',
    COUNT(*)
FROM PAYORDER WHERE UPVCBMONEY = -1;

-- Show sample data
SELECT TOP 20 
    SOGD, NGAY, TKNO, LNT, SOTIEN, TKNH, TENNH, NHNH, UPVCBMONEY
FROM PAYORDER
ORDER BY SOGD;

GO

-- ============================================================
-- SECTION 6: Optional - Generate 10,000 records for stress test
-- Uncomment to run
-- ============================================================
/*
PRINT '=== Generating 10,000 records for stress test ===';

DECLARE @k INT = 2000;
WHILE @k <= 12000
BEGIN
    INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
    VALUES (
        'TT2025' + RIGHT('00000000' + CAST(@k AS VARCHAR(8)), 8),
        DATEADD(DAY, -(@k % 60), '2025-01-22'),
        '1234567890123',
        'VND',
        20000000 + (RAND() * 80000000),
        1,
        '987654' + RIGHT('00000000' + CAST(@k AS VARCHAR(8)), 8),
        N'NHÂN VIÊN STRESS TEST ' + CAST(@k AS NVARCHAR(10)),
        'OUR',
        CASE @k % 5 
            WHEN 0 THEN N'VIETCOMBANK'
            WHEN 1 THEN N'VIETINBANK'
            WHEN 2 THEN N'BIDV'
            WHEN 3 THEN N'TECHCOMBANK'
            ELSE N'ACB'
        END,
        N'Stress test payment ' + CAST(@k AS NVARCHAR(10)),
        N'SAMSUNG ELECTRONICS VIETNAM',
        0
    );
    SET @k = @k + 1;
END
PRINT 'Inserted 10,000 stress test records.';
*/

-- ============================================================
-- CLEANUP (if needed)
-- ============================================================
/*
-- Reset all to new status
UPDATE PAYORDER SET UPVCBMONEY = 0, ErrCode = NULL, ErrDes = NULL;

-- Delete all test data
DELETE FROM PAYORDER WHERE SOGD LIKE 'TT2025%';

-- Delete only error test cases
DELETE FROM PAYORDER WHERE SOGD LIKE 'TTERR%';
*/
