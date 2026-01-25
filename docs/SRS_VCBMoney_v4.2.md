# VCBMoney Integration v4.2 - Software Requirements Specification

## 1. Giới thiệu

### 1.1 Mục đích
VCBMoney Integration là hệ thống xử lý giao dịch chuyển tiền nội địa, hỗ trợ:
- Import file MT103 (SWIFT/Plaintext format)
- Đồng bộ từ database trung gian (PAYORDER)
- Validate 5 cấp độ (L1-L5)
- Sync kết quả sang DOMESTIC_RESULT

### 1.2 Phạm vi
- Xử lý file MT103 với **100,000+ transactions/file** (Samsung payroll scale)
- Đồng bộ PayOrder với **100,000+ records/batch**
- Batch processing tự động scale (1000 records/batch)

### 1.3 Định nghĩa

| Thuật ngữ | Mô tả |
|-----------|-------|
| MT103 | Định dạng SWIFT chuyển tiền |
| Plaintext | MT103 không có block headers |
| PAYORDER | Table giao dịch từ hệ thống khách |
| DOMESTIC_RESULT | Table kết quả gửi ngân hàng |
| DOMESTIC_TXT | Table lưu raw MT103 để search |

---

## 2. Mô tả tổng quan

### 2.1 Kiến trúc hệ thống

```
┌─────────────────────────────────────────────────────────────────┐
│                      VCBMoney Integration                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐  │
│  │  Job 1   │    │  Job 2   │    │  Job 3   │    │  Job 4   │  │
│  │  Scan    │    │  Scan    │    │  Parse   │    │Transform │  │
│  │  Files   │    │ PayOrder │    │  Files   │    │ PayOrder │  │
│  │  +L1+L2  │    │  +L1+L2  │    │+L3+L4+L5 │    │+L3+L4+L5 │  │
│  └────┬─────┘    └────┬─────┘    └────┬─────┘    └────┬─────┘  │
│       │               │               │               │        │
│       ▼               ▼               ▼               ▼        │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                    VCB_MONEY Database                    │   │
│  │  FILE_UPLOAD | TRANSACTIONS | TRANSACTIONS_IMPORT       │   │
│  │  DOMESTIC_TXT | AUDIT_LOG                               │   │
│  └─────────────────────────────────────────────────────────┘   │
│                              │                                  │
│                              ▼                                  │
│  ┌──────────┐          ┌──────────┐          ┌──────────┐      │
│  │  Job 5   │          │  Job 6   │          │  Job 7/8 │      │
│  │  Sync    │          │  Retry   │          │ Cleanup  │      │
│  │  Result  │          │  Failed  │          │  Report  │      │
│  └────┬─────┘          └──────────┘          └──────────┘      │
│       │                                                        │
│       ▼                                                        │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                  DBTRUNGGIAN Database                    │   │
│  │              PAYORDER | DOMESTIC_RESULT                  │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Luồng xử lý File (Chi tiết)

```
┌────────────────────────────────────────────────────────────────────┐
│                          JOB 1: Scan Files                          │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  FolderInput/                                                      │
│      │                                                             │
│      ▼                                                             │
│  ┌──────────────────┐                                              │
│  │ 1. Check locked  │──── Locked ──► Skip (try next scan)          │
│  └────────┬─────────┘                                              │
│           │                                                        │
│           ▼                                                        │
│  ┌──────────────────┐                                              │
│  │ 2. Compute hash  │──── Duplicate ──► FolderError/               │
│  │    Check dup     │                                              │
│  └────────┬─────────┘                                              │
│           │                                                        │
│           ▼                                                        │
│  ┌──────────────────────────────────────────────────────┐          │
│  │ 3. MOVE to FolderProcessing/ (lock file)             │          │
│  └────────┬─────────────────────────────────────────────┘          │
│           │                                                        │
│           ▼                                                        │
│  ┌──────────────────┐                                              │
│  │ 4. L1: Structure │──── FAIL ──► DB + FolderError/               │
│  └────────┬─────────┘                                              │
│           │ PASS                                                   │
│           ▼                                                        │
│  ┌──────────────────┐                                              │
│  │ 5. L2: Required  │──── FAIL ──► DB + FolderError/               │
│  │    Fields        │                                              │
│  └────────┬─────────┘                                              │
│           │ PASS                                                   │
│           ▼                                                        │
│  ┌──────────────────────────────────────────────────────┐          │
│  │ 6. Save to FILE_UPLOAD (Status=Valid)                │          │
│  │    File stays in FolderProcessing for Job3           │          │
│  │    Note: RawContent NOT saved - Job3 reads from file │          │
│  └──────────────────────────────────────────────────────┘          │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────┐
│                   JOB 3: Parse Files (Batch 1000)                   │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  Read FILE_UPLOAD where Status=Valid                               │
│      │                                                             │
│      ▼                                                             │
│  ┌──────────────────────────────────────────────────────┐          │
│  │ Read file content from FolderProcessing/             │          │
│  │ (FilePhysical path in DB)                            │          │
│  └────────┬─────────────────────────────────────────────┘          │
│           │                                                        │
│           ▼                                                        │
│  ┌──────────────────────────────────────────────────────┐          │
│  │ Parse MT103 → List<ParsedTransaction>                │          │
│  └────────┬─────────────────────────────────────────────┘          │
│           │                                                        │
│           ▼                                                        │
│  ┌──────────────────────────────────────────────────────┐          │
│  │ Process in batches (1000 records/batch)              │          │
│  │                                                      │          │
│  │  For each batch:                                     │          │
│  │    - Begin Transaction                               │          │
│  │    - Batch duplicate check (1 query)                 │          │
│  │    - L3+L4+L5 validate each record                   │          │
│  │    - Bulk insert DOMESTIC_TXT                        │          │
│  │    - Bulk insert TRANSACTIONS (valid)                │          │
│  │    - Bulk insert TRANSACTIONS_IMPORT (invalid)       │          │
│  │    - Commit Transaction                              │          │
│  └────────┬─────────────────────────────────────────────┘          │
│           │                                                        │
│           ▼                                                        │
│  ┌─────────────────┐      ┌─────────────────┐                      │
│  │ Success         │      │ Error           │                      │
│  │ → FolderArchive │      │ → FolderError   │                      │
│  └─────────────────┘      └─────────────────┘                      │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

### 2.3 Luồng xử lý PayOrder (Chi tiết)

```
┌────────────────────────────────────────────────────────────────────┐
│                     JOB 2: Scan PayOrder                            │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  DBTRUNGGIAN.PAYORDER (UpvcbMoney=0 or NULL)                       │
│      │                                                             │
│      ▼                                                             │
│  ┌──────────────────────────────────────────────────────┐          │
│  │ L1+L2: Validate required fields                      │          │
│  └────────┬─────────────────────────────────────────────┘          │
│           │                                                        │
│     ┌─────┴─────┐                                                  │
│     ▼           ▼                                                  │
│   PASS        FAIL                                                 │
│     │           │                                                  │
│     ▼           ▼                                                  │
│ UpvcbMoney=1  UpvcbMoney=-1                                        │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────┐
│                JOB 4: Transform PayOrder (Batch 1000)               │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  DBTRUNGGIAN.PAYORDER (UpvcbMoney=1)                               │
│      │                                                             │
│      ▼                                                             │
│  ┌──────────────────────────────────────────────────────┐          │
│  │ Process in batches (1000 records/batch)              │          │
│  │                                                      │          │
│  │  For each batch:                                     │          │
│  │    - Begin Transaction                               │          │
│  │    - Batch duplicate check (1 query)                 │          │
│  │    - L3+L4+L5 validate each record                   │          │
│  │    - Bulk insert TRANSACTIONS (valid)                │          │
│  │    - Bulk insert TRANSACTIONS_IMPORT (invalid)       │          │
│  │    - Update PAYORDER status                          │          │
│  │    - Commit Transaction                              │          │
│  └────────┬─────────────────────────────────────────────┘          │
│           │                                                        │
│     ┌─────┴─────┐                                                  │
│     ▼           ▼                                                  │
│   Valid       Invalid                                              │
│     │           │                                                  │
│     ▼           ▼                                                  │
│ UpvcbMoney=2  UpvcbMoney=-1                                        │
│ →TRANSACTIONS →TRANSACTIONS_IMPORT                                 │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

---

## 3. Yêu cầu chức năng

### 3.1 Job 1: Scan Files

**Schedule:** Every 30 seconds
**Timeout:** 2 minutes

| Step | Action | On Fail |
|------|--------|---------|
| 1 | Check file locked | Skip |
| 2 | Compute SHA256 hash | - |
| 3 | Check duplicate in DB | → FolderError |
| 4 | Move to FolderProcessing | - |
| 5 | L1: Validate structure | → FolderError + DB |
| 6 | L2: Validate required fields | → FolderError + DB |
| 7 | Save to FILE_UPLOAD (Status=Valid) | - |

**Note:** RawContent KHÔNG lưu vào DB - Job3 sẽ đọc file từ FilePhysical path

### 3.2 Job 2: Scan PayOrder

**Schedule:** Every 30 seconds
**Timeout:** 5 minutes

| Validation | Rule |
|------------|------|
| SoGd | Required, max 20 chars |
| TkNo | Required, max 15 chars |
| SoTien | Required, > 0 |
| TkNh | Required for transfer |

### 3.3 Job 3: Parse Files

**Schedule:** Every 1 minute
**Timeout:** 30 minutes
**Batch Size:** 1000 records

| Level | Type | Action |
|-------|------|--------|
| L3 | Format validation | Error if invalid |
| L4 | Length validation | Truncate + Warning |
| L5 | Business validation | Error if duplicate/invalid amount |

**Output:**
- DOMESTIC_TXT (all records - for search)
- TRANSACTIONS (valid records)
- TRANSACTIONS_IMPORT (invalid records)
- File moved to Archive/Error

### 3.4 Job 4: Transform PayOrder

**Schedule:** Every 1 minute
**Timeout:** 30 minutes
**Batch Size:** 1000 records

Same validation as Job 3.

### 3.5 Job 5: Sync Result

**Schedule:** Every 1 minute
**Timeout:** 30 minutes
**Batch Size:** 1000 records

Sync TRANSACTIONS → DOMESTIC_RESULT

### 3.6 Job 6-8

| Job | Schedule | Function |
|-----|----------|----------|
| Job 6 | Every 5 min | Retry failed (L5 errors only) |
| Job 7 | Daily 2 AM | Cleanup old files |
| Job 8 | Daily 6 AM | Daily report |

---

## 4. Performance Requirements

### 4.1 Throughput

| Scenario | Records | Target Time |
|----------|---------|-------------|
| File processing | 100,000 tx | < 20 minutes |
| PayOrder processing | 100,000 records | < 15 minutes |
| Sync to DOMESTIC_RESULT | 100,000 records | < 10 minutes |

### 4.2 Batch Processing Benefits

| Aspect | Per-record | Batch (1000) |
|--------|------------|--------------|
| Duplicate check | 1 query/record | 1 query/batch |
| Insert | 1 insert/record | Bulk insert |
| Transaction | 1 tx/record | 1 tx/batch |
| Rollback impact | N/A | Only 1 batch |

---

## 5. Database Tables

### 5.1 VCB_MONEY Database

| Table | Purpose |
|-------|---------|
| FILE_UPLOAD | File metadata (no RawContent) |
| TRANSACTIONS | Valid transactions |
| TRANSACTIONS_IMPORT | Invalid transactions |
| DOMESTIC_TXT | MT103 raw data for search |
| AUDIT_LOG | Audit trail |

### 5.2 DBTRUNGGIAN Database

| Table | Purpose |
|-------|---------|
| PAYORDER | Input from customer |
| DOMESTIC_RESULT | Output to bank |

---

## 6. API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| /api/file/upload | POST | Upload MT103 file |
| /api/file/{id} | GET | Get file status |
| /api/transactions | GET | Search transactions |
| /api/domestictxt | GET | Search MT103 data |
| /api/domestictxt/stats | GET | Statistics |
| /health | GET | Health check |
| /hangfire | GET | Job dashboard |

---

## 7. MT103 Format Support

### 7.1 SWIFT Format
```
{1:F01...}{2:O103...}{4:
:20:TT123456
:32A:251003VND100000000
:50K:/1234567890
COMPANY NAME
:59:/9876543210
BENEFICIARY NAME
:70:PAYMENT CONTENT
:71A:OUR
-}
```

### 7.2 Plaintext Format (New in v4.2)
```
:20:TT123456
:32A:251003VND100000000
:50R:H12345678901
:50S:COMPANY NAME
:53B:/1234567890
:57A:VIETCOMBANK
:59:/9876543210
BENEFICIARY NAME
:59O:021612345678
:70:PAYMENT CONTENT
:71A:OUR
```

---

## 8. Configuration

```json
{
  "ConnectionStrings": {
    "VcbMoney": "Server=.;Database=VCB_MONEY;...",
    "TrungGian": "Server=.;Database=DBTRUNGGIAN;..."
  },
  "VcbMoney": {
    "Files": {
      "FolderInput": "D:\\VCBMoney\\Input",
      "FolderProcessing": "D:\\VCBMoney\\Processing",
      "FolderArchive": "D:\\VCBMoney\\Archive",
      "FolderError": "D:\\VCBMoney\\Error",
      "AllowedExtensions": [".txt", ".mt103"]
    },
    "Business": {
      "CustomerCode": "SAMSUNG",
      "MaxRetryCount": 3,
      "FileRetentionDays": 90
    }
  }
}
```
