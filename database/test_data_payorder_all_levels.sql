-- ============================================================
-- VCBMoney v4.2 - Test Data PAYORDER với các Validation Levels
-- Database: DBTRUNGGIAN
-- ============================================================

USE DBTRUNGGIAN;
GO

-- ============================================================
-- CLEANUP: Xóa dữ liệu test cũ (optional)
-- ============================================================
-- DELETE FROM PAYORDER WHERE SOGD LIKE 'TEST%';
-- GO

-- ============================================================
-- SECTION 1: VALID RECORDS (10 records) - Pass all levels
-- ============================================================
PRINT '=== [1] Inserting VALID records (L1+L2+L3+L4+L5 OK) ===';

INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES
('TEST_VALID_001', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'NGUYỄN VĂN A', N'123 Lê Lợi, Q1, HCM', 'OUR', N'VIETCOMBANK CN HCM', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TEST_VALID_002', '2025-01-23', '1234567890123', 'VND', 75000000, 1, '9876543210002', N'TRẦN THỊ B', N'456 Nguyễn Huệ, Q1, HCM', 'OUR', N'VIETINBANK CN HCM', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TEST_VALID_003', '2025-01-23', '1234567890123', 'VND', 60000000, 1, '9876543210003', N'LÊ VĂN C', N'789 Hai Bà Trưng, Q3, HCM', 'OUR', N'BIDV CN Q3', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TEST_VALID_004', '2025-01-23', '1234567890123', 'VND', 45000000, 1, '9876543210004', N'PHẠM THỊ D', N'12 Pasteur, Q3, HCM', 'OUR', N'AGRIBANK CN Q3', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TEST_VALID_005', '2025-01-23', '1234567890123', 'VND', 80000000, 1, '9876543210005', N'HOÀNG VĂN E', N'34 Nam Kỳ Khởi Nghĩa, Q1, HCM', 'OUR', N'TECHCOMBANK CN HCM', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TEST_VALID_006', '2025-01-23', '1234567890123', 'VND', 55000000, 1, '9876543210006', N'VŨ THỊ F', N'56 Lý Tự Trọng, Q1, HCM', 'OUR', N'MB BANK CN HCM', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TEST_VALID_007', '2025-01-23', '1234567890123', 'VND', 42000000, 1, '9876543210007', N'ĐẶNG VĂN G', N'78 Trần Hưng Đạo, Q5, HCM', 'OUR', N'ACB CN Q5', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TEST_VALID_008', '2025-01-23', '1234567890123', 'VND', 95000000, 1, '9876543210008', N'BÙI THỊ H', N'90 Võ Văn Tần, Q3, HCM', 'OUR', N'SACOMBANK CN Q3', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TEST_VALID_009', '2025-01-23', '1234567890123', 'VND', 38000000, 1, '9876543210009', N'ĐINH VĂN I', N'123 Cách Mạng Tháng 8, Q10, HCM', 'OUR', N'VPBANK CN Q10', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0),
('TEST_VALID_010', '2025-01-23', '1234567890123', 'VND', 67000000, 1, '9876543210010', N'NGÔ THỊ K', N'456 Ba Tháng Hai, Q10, HCM', 'OUR', N'EXIMBANK CN Q10', N'Thanh toán lương tháng 1/2025', N'CÔNG TY TNHH ABC', 0);

PRINT 'Inserted 10 VALID records.';
GO

-- ============================================================
-- SECTION 2: L1 ERRORS - Structure/Basic errors
-- (Job2 sẽ validate và set UPVCBMONEY = -1)
-- ============================================================
PRINT '=== [2] Inserting L1 ERROR records (Structure errors) ===';

-- L1 Error: SOGD quá dài (max 20)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, TKNH, TENNH, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L1_ERR_SOGD_TOO_LONG_12345', '2025-01-23', '1234567890123', 'VND', 50000000, '9876543210001', N'Test L1', N'VIETCOMBANK', N'Test', N'Company', 0);
-- Lưu ý: SQL sẽ truncate nếu column cho phép, hoặc báo lỗi nếu không

-- L1 Error: TKNO quá dài (max 15)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, TKNH, TENNH, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L1_ERR_001', '2025-01-23', '12345678901234567890', 'VND', 50000000, '9876543210001', N'Test L1 TKNO quá dài', N'VIETCOMBANK', N'Test', N'Company', 0);

PRINT 'Inserted L1 ERROR records.';
GO

-- ============================================================
-- SECTION 3: L2 ERRORS - Missing required fields
-- (Job2 sẽ validate và set UPVCBMONEY = -1)
-- ============================================================
PRINT '=== [3] Inserting L2 ERROR records (Missing required fields) ===';

-- L2 Error: Thiếu TKNH (tài khoản thụ hưởng) - required for transfer
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L2_ERR_001', '2025-01-23', '1234567890123', 'VND', 50000000, 1, NULL, N'Người thụ hưởng', N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Test thiếu TK thụ hưởng', N'Company', 0);

-- L2 Error: Thiếu TENNH (tên người thụ hưởng)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L2_ERR_002', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', NULL, N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Test thiếu tên thụ hưởng', N'Company', 0);

-- L2 Error: Thiếu NHNH (ngân hàng thụ hưởng)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L2_ERR_003', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'Người thụ hưởng', N'Địa chỉ', 'OUR', NULL, N'Test thiếu ngân hàng', N'Company', 0);

-- L2 Error: Thiếu ND (nội dung)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L2_ERR_004', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'Người thụ hưởng', N'Địa chỉ', 'OUR', N'VIETCOMBANK', NULL, N'Company', 0);

-- L2 Error: Thiếu TKNH + TENNH + NHNH (combo)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L2_ERR_005', '2025-01-23', '1234567890123', 'VND', 50000000, 1, NULL, NULL, N'Địa chỉ', 'OUR', NULL, N'Test thiếu nhiều fields', N'Company', 0);

PRINT 'Inserted 5 L2 ERROR records.';
GO

-- ============================================================
-- SECTION 4: L3 ERRORS - Format validation
-- (Job4 sẽ validate và đưa vào TRANSACTIONS_IMPORT)
-- ============================================================
PRINT '=== [4] Inserting L3 ERROR records (Format errors) ===';

-- Các lỗi L3 thường liên quan đến format không hợp lệ
-- Trong PAYORDER, các fields đã có kiểu dữ liệu SQL nên format errors ít xảy ra
-- Tuy nhiên, có thể test với các giá trị edge cases

-- L3 Test: Số tiền có format lạ (SQL sẽ tự convert nếu được)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L3_EDGE_001', '2025-01-23', '1234567890123', 'VND', 50000000.999, 1, '9876543210001', N'Test L3 decimal amount', N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Test số tiền có decimal', N'Company', 0);

-- L3 Test: Currency không chuẩn
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L3_ERR_001', '2025-01-23', '1234567890123', 'XXX', 50000000, 1, '9876543210001', N'Test L3 invalid currency', N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Test loại tiền không hợp lệ', N'Company', 0);

-- L3 Test: Charge code không chuẩn
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L3_ERR_002', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'Test L3 invalid charge', N'Địa chỉ', 'XXX', N'VIETCOMBANK', N'Test phí không hợp lệ', N'Company', 0);

PRINT 'Inserted 3 L3 ERROR/EDGE records.';
GO

-- ============================================================
-- SECTION 5: L4 WARNINGS - Data truncation
-- (Job4 sẽ truncate và cho vào TRANSACTIONS với warning)
-- ============================================================
PRINT '=== [5] Inserting L4 WARNING records (Data too long - truncation) ===';

-- L4 Warning: TENNH quá dài (max 144)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L4_WARN_001', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', 
    N'NGUYỄN VĂN A CÓ TÊN RẤT DÀI ĐỂ TEST TRUNCATION LEVEL 4 - NGUYỄN VĂN A CÓ TÊN RẤT DÀI ĐỂ TEST TRUNCATION - TIẾP TỤC DÀI HƠN NỮA ĐỂ VƯỢT QUA 144 KÝ TỰ', 
    N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Test L4 tên thụ hưởng dài', N'Company', 0);

-- L4 Warning: ND quá dài (max 140)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L4_WARN_002', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'Test L4 nội dung dài', N'Địa chỉ', 'OUR', N'VIETCOMBANK', 
    N'NỘI DUNG CHUYỂN TIỀN RẤT DÀI ĐỂ TEST TRUNCATION LEVEL 4 - NỘI DUNG CHUYỂN TIỀN RẤT DÀI ĐỂ TEST TRUNCATION - TIẾP TỤC DÀI HƠN NỮA ĐỂ VƯỢT QUA 140 KÝ TỰ CHO PHÉP', 
    N'Company', 0);

-- L4 Warning: NHNH quá dài (max 100)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L4_WARN_003', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'Test L4', N'Địa chỉ', 'OUR', 
    N'NGÂN HÀNG THƯƠNG MẠI CỔ PHẦN NGOẠI THƯƠNG VIỆT NAM - CHI NHÁNH THÀNH PHỐ HỒ CHÍ MINH - PHÒNG GIAO DỊCH QUẬN 1 SỐ 123 NGUYỄN HUỆ', 
    N'Test L4 ngân hàng dài', N'Company', 0);

-- L4 Warning: Nhiều fields cùng lúc quá dài
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L4_WARN_004', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', 
    N'TÊN NGƯỜI THỤ HƯỞNG RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI QUÁ 144 KÝ TỰ', 
    N'Địa chỉ người thụ hưởng rất dài rất dài rất dài rất dài rất dài rất dài quá 70 ký tự cho phép', 
    'OUR', 
    N'NGÂN HÀNG THƯƠNG MẠI CỔ PHẦN NGOẠI THƯƠNG VIỆT NAM CHI NHÁNH RẤT DÀI VƯỢT QUÁ 100 KÝ TỰ CHO PHÉP', 
    N'NỘI DUNG CHUYỂN TIỀN RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI QUÁ 140 KÝ TỰ CHO PHÉP', 
    N'CÔNG TY TNHH ABC RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI RẤT DÀI QUÁ 144 KÝ TỰ CHO PHÉP', 0);

PRINT 'Inserted 4 L4 WARNING records.';
GO

-- ============================================================
-- SECTION 6: L5 ERRORS - Business rules
-- (Job4 sẽ validate và đưa vào TRANSACTIONS_IMPORT)
-- ============================================================
PRINT '=== [6] Inserting L5 ERROR records (Business rules) ===';

-- L5 Error: Số tiền = 0
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L5_ERR_001', '2025-01-23', '1234567890123', 'VND', 0, 1, '9876543210001', N'Test L5 zero amount', N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Số tiền = 0', N'Company', 0);

-- L5 Error: Số tiền âm
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L5_ERR_002', '2025-01-23', '1234567890123', 'VND', -50000000, 1, '9876543210001', N'Test L5 negative amount', N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Số tiền âm', N'Company', 0);

-- L5 Error: Số tiền quá nhỏ (< 1000 VND)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L5_ERR_003', '2025-01-23', '1234567890123', 'VND', 500, 1, '9876543210001', N'Test L5 too small amount', N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Số tiền quá nhỏ', N'Company', 0);

-- L5 Error: Số tiền quá lớn (> limit)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_L5_ERR_004', '2025-01-23', '1234567890123', 'VND', 999999999999999, 1, '9876543210001', N'Test L5 too large amount', N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Số tiền quá lớn', N'Company', 0);

PRINT 'Inserted 4 L5 ERROR records.';
GO

-- ============================================================
-- SECTION 7: L5 DUPLICATE TEST
-- (Chạy section này 2 lần để test duplicate check)
-- ============================================================
PRINT '=== [7] Inserting records for DUPLICATE test (run twice) ===';

-- Lần đầu: Insert records này sẽ pass
-- Lần 2: Chạy lại sẽ bị lỗi duplicate SOGD (PK)
-- Hoặc: Nếu đã có trong TRANSACTIONS/DOMESTIC_RESULT thì Job4 sẽ báo L5 duplicate

INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_DUP_001', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'Test Duplicate 1', N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Test duplicate check', N'Company', 0);

INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_DUP_002', '2025-01-23', '1234567890123', 'VND', 75000000, 1, '9876543210002', N'Test Duplicate 2', N'Địa chỉ', 'OUR', N'VIETINBANK', N'Test duplicate check', N'Company', 0);

PRINT 'Inserted 2 DUPLICATE TEST records.';
GO

-- ============================================================
-- SECTION 8: MIXED RECORDS (Valid + Various Errors)
-- ============================================================
PRINT '=== [8] Inserting MIXED records (Valid + Errors) ===';

INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY) VALUES
-- Valid
('TEST_MIX_001', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'Record hợp lệ 1', N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Valid record 1', N'Company', 0),
-- L2 Error - missing TKNH
('TEST_MIX_002', '2025-01-23', '1234567890123', 'VND', 50000000, 1, NULL, N'Thiếu TK thụ hưởng', N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'L2 Error', N'Company', 0),
-- Valid
('TEST_MIX_003', '2025-01-23', '1234567890123', 'VND', 75000000, 1, '9876543210003', N'Record hợp lệ 2', N'Địa chỉ', 'OUR', N'VIETINBANK', N'Valid record 2', N'Company', 0),
-- L5 Error - zero amount
('TEST_MIX_004', '2025-01-23', '1234567890123', 'VND', 0, 1, '9876543210004', N'Số tiền = 0', N'Địa chỉ', 'OUR', N'BIDV', N'L5 Error zero', N'Company', 0),
-- Valid
('TEST_MIX_005', '2025-01-23', '1234567890123', 'VND', 60000000, 1, '9876543210005', N'Record hợp lệ 3', N'Địa chỉ', 'OUR', N'TECHCOMBANK', N'Valid record 3', N'Company', 0),
-- L2 Error - missing NHNH
('TEST_MIX_006', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210006', N'Thiếu ngân hàng', N'Địa chỉ', 'OUR', NULL, N'L2 Error', N'Company', 0),
-- Valid
('TEST_MIX_007', '2025-01-23', '1234567890123', 'VND', 80000000, 1, '9876543210007', N'Record hợp lệ 4', N'Địa chỉ', 'OUR', N'MB BANK', N'Valid record 4', N'Company', 0),
-- L5 Error - negative amount
('TEST_MIX_008', '2025-01-23', '1234567890123', 'VND', -100000, 1, '9876543210008', N'Số tiền âm', N'Địa chỉ', 'OUR', N'ACB', N'L5 Error negative', N'Company', 0),
-- Valid
('TEST_MIX_009', '2025-01-23', '1234567890123', 'VND', 45000000, 1, '9876543210009', N'Record hợp lệ 5', N'Địa chỉ', 'OUR', N'SACOMBANK', N'Valid record 5', N'Company', 0),
-- Valid
('TEST_MIX_010', '2025-01-23', '1234567890123', 'VND', 55000000, 1, '9876543210010', N'Record hợp lệ 6', N'Địa chỉ', 'OUR', N'VPBANK', N'Valid record 6', N'Company', 0);

PRINT 'Inserted 10 MIXED records (6 valid, 4 errors).';
GO

-- ============================================================
-- SECTION 9: Special test cases
-- ============================================================
PRINT '=== [9] Inserting SPECIAL test cases ===';

-- Test: Số tiền rất lớn (1 tỷ)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_SPEC_001', '2025-01-23', '1234567890123', 'VND', 1000000000, 2, '9876543210001', N'Test 1 tỷ VND', N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Chuyển 1 tỷ VND', N'Company', 0);

-- Test: Có đầy đủ thông tin BenID
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY, BEN_IDNO, BEN_ID_ISSUE_DATE, BEN_ID_ISSUE_PLACE)
VALUES ('TEST_SPEC_002', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'NGUYỄN VĂN A', N'123 ABC', 'OUR', N'VIETCOMBANK', N'Test với CMND/CCCD', N'Company', 0, '079123456789', '2020-01-15', N'CA TP.HCM');

-- Test: Transfer flags
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY, BANK_TO_BANK, BANK_TO_BANK_SBV, INQUIRY_BEN_NAME)
VALUES ('TEST_SPEC_003', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'Test Bank to Bank', N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Test chuyển liên NH', N'Company', 0, 1, 0, 1);

-- Test: Priority = 2 (high)
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_SPEC_004', '2025-01-23', '1234567890123', 'VND', 100000000, 2, '9876543210001', N'Test High Priority', N'Địa chỉ', 'OUR', N'VIETCOMBANK', N'Ưu tiên cao', N'Company', 0);

-- Test: Charge = BEN
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_SPEC_005', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'Test Charge BEN', N'Địa chỉ', 'BEN', N'VIETCOMBANK', N'Phí người nhận chịu', N'Company', 0);

-- Test: Charge = SHA
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_SPEC_006', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'Test Charge SHA', N'Địa chỉ', 'SHA', N'VIETCOMBANK', N'Phí chia đôi', N'Company', 0);

-- Test: Unicode đặc biệt trong tên
INSERT INTO PAYORDER (SOGD, NGAY, TKNO, LNT, SOTIEN, UUTIEN, TKNH, TENNH, DCNH, PHI, NHNH, ND, TENNRL, UPVCBMONEY)
VALUES ('TEST_SPEC_007', '2025-01-23', '1234567890123', 'VND', 50000000, 1, '9876543210001', N'NGUYỄN THỊ PHƯỢNG UYÊN ĐỖ', N'123 ĐỖ XUÂN HỢP', 'OUR', N'VIETCOMBANK', N'Tên có dấu đặc biệt', N'Company', 0);

PRINT 'Inserted 7 SPECIAL test records.';
GO

-- ============================================================
-- VERIFY: Summary of test data
-- ============================================================
PRINT '';
PRINT '=== DATA SUMMARY ===';

SELECT 
    'Total TEST records' AS Category,
    COUNT(*) AS Count
FROM PAYORDER 
WHERE SOGD LIKE 'TEST%'

UNION ALL

SELECT 'VALID records', COUNT(*) FROM PAYORDER WHERE SOGD LIKE 'TEST_VALID%'
UNION ALL
SELECT 'L1 ERROR records', COUNT(*) FROM PAYORDER WHERE SOGD LIKE 'TEST_L1%'
UNION ALL
SELECT 'L2 ERROR records', COUNT(*) FROM PAYORDER WHERE SOGD LIKE 'TEST_L2%'
UNION ALL
SELECT 'L3 ERROR records', COUNT(*) FROM PAYORDER WHERE SOGD LIKE 'TEST_L3%'
UNION ALL
SELECT 'L4 WARNING records', COUNT(*) FROM PAYORDER WHERE SOGD LIKE 'TEST_L4%'
UNION ALL
SELECT 'L5 ERROR records', COUNT(*) FROM PAYORDER WHERE SOGD LIKE 'TEST_L5%'
UNION ALL
SELECT 'DUPLICATE TEST records', COUNT(*) FROM PAYORDER WHERE SOGD LIKE 'TEST_DUP%'
UNION ALL
SELECT 'MIXED records', COUNT(*) FROM PAYORDER WHERE SOGD LIKE 'TEST_MIX%'
UNION ALL
SELECT 'SPECIAL records', COUNT(*) FROM PAYORDER WHERE SOGD LIKE 'TEST_SPEC%';

GO

-- ============================================================
-- QUICK REFERENCE: Expected results after Job2 + Job4
-- ============================================================
PRINT '';
PRINT '=== EXPECTED RESULTS ===';
PRINT 'After Job2 (L1+L2 validation):';
PRINT '  - TEST_VALID_*   : UPVCBMONEY = 1 (Valid)';
PRINT '  - TEST_L1_*      : UPVCBMONEY = -1 (Error)';
PRINT '  - TEST_L2_*      : UPVCBMONEY = -1 (Error)';
PRINT '  - Others         : UPVCBMONEY = 1 (Valid, proceed to Job4)';
PRINT '';
PRINT 'After Job4 (L3+L4+L5 validation):';
PRINT '  - TEST_VALID_*   : UPVCBMONEY = 2 (Imported to TRANSACTIONS)';
PRINT '  - TEST_L3_*      : UPVCBMONEY = -1 (Error in TRANSACTIONS_IMPORT)';
PRINT '  - TEST_L4_*      : UPVCBMONEY = 2 (Imported with truncation warnings)';
PRINT '  - TEST_L5_*      : UPVCBMONEY = -1 (Error in TRANSACTIONS_IMPORT)';
PRINT '  - TEST_DUP_*     : First run = 2, Second run = -1 (duplicate)';
PRINT '';
GO
