# VCBMoney Integration v4.2 - Handover Document

## 1. Thông tin dự án

| Item | Value |
|------|-------|
| Tên dự án | VCBMoney Integration |
| Version | 4.2 |
| Ngày bàn giao | 2025-01-22 |
| Technology | .NET 8.0, SQL Server, Hangfire |

---

## 2. Danh sách deliverables

### 2.1 Source Code

```
vcbmoney-v4.2/
├── src/
│   ├── VCBMoney.Core/           # Domain entities, interfaces
│   ├── VCBMoney.Infrastructure/ # Data access, services
│   └── VCBMoney.Web/            # API, Jobs
├── database/
│   ├── create_database.sql      # VCB_MONEY database
│   └── create_payorder_domestic.sql # DBTRUNGGIAN database
└── docs/
    ├── SRS_VCBMoney_v4.2.md
    ├── TECHNICAL_DOCUMENTATION.md
    └── HANDOVER_DOCUMENT.md
```

### 2.2 Database Scripts

| File | Database | Tables |
|------|----------|--------|
| create_database.sql | VCB_MONEY | FILE_UPLOAD, TRANSACTIONS, TRANSACTIONS_IMPORT, DOMESTIC_TXT, AUDIT_LOG |
| create_payorder_domestic.sql | DBTRUNGGIAN | PAYORDER, DOMESTIC_RESULT |

---

## 3. Tính năng chính

### 3.1 File Processing
- ✅ Import MT103 files (SWIFT + Plaintext format)
- ✅ 5-level validation (L1-L5)
- ✅ Batch processing 1000 records/batch
- ✅ Support 100,000+ transactions/file

### 3.2 PayOrder Processing
- ✅ Sync từ DBTRUNGGIAN.PAYORDER
- ✅ Batch processing 1000 records/batch
- ✅ Support 100,000+ records/batch

### 3.3 Data Storage
- ✅ DOMESTIC_TXT - Lưu MT103 raw data để search
- ✅ TRANSACTIONS - Valid records
- ✅ TRANSACTIONS_IMPORT - Invalid records

### 3.4 API
- ✅ Search DOMESTIC_TXT với nhiều filter
- ✅ Statistics endpoint
- ✅ Health check

---

## 4. Luồng xử lý

### 4.1 File Flow

```
FolderInput/
    │
    ├─[Job1]─► FolderProcessing/ ─► L1+L2 OK
    │                 │
    │                 └─► L1+L2 FAIL ─► FolderError/
    │
    └─[Job3]─► Parse + L3+L4+L5
                  │
                  ├─► OK ─► TRANSACTIONS + DOMESTIC_TXT ─► FolderArchive/
                  │
                  └─► Error ─► TRANSACTIONS_IMPORT + DOMESTIC_TXT ─► FolderError/
```

### 4.2 PayOrder Flow

```
PAYORDER (UpvcbMoney=0)
    │
    ├─[Job2]─► L1+L2 OK ─► UpvcbMoney=1
    │
    └─[Job4]─► L3+L4+L5
                  │
                  ├─► OK ─► TRANSACTIONS (UpvcbMoney=2)
                  │
                  └─► Error ─► TRANSACTIONS_IMPORT (UpvcbMoney=-1)
```

---

## 5. Jobs Schedule

| Job | Schedule | Timeout | Function |
|-----|----------|---------|----------|
| Job1 | 30s | 2 min | Scan files + L1+L2 |
| Job2 | 30s | 5 min | Scan PayOrder + L1+L2 |
| Job3 | 1 min | 30 min | Parse files + L3+L4+L5 |
| Job4 | 1 min | 30 min | Transform PayOrder + L3+L4+L5 |
| Job5 | 1 min | 30 min | Sync to DOMESTIC_RESULT |
| Job6 | 5 min | 5 min | Retry failed |
| Job7 | 2 AM | - | Cleanup old files |
| Job8 | 6 AM | - | Daily report |

---

## 6. Configuration

### 6.1 Connection Strings

```json
{
  "ConnectionStrings": {
    "VcbMoney": "Server=.;Database=VCB_MONEY;...",
    "TrungGian": "Server=.;Database=DBTRUNGGIAN;..."
  }
}
```

### 6.2 File Paths

```json
{
  "VcbMoney": {
    "Files": {
      "FolderInput": "D:\\VCBMoney\\Input",
      "FolderProcessing": "D:\\VCBMoney\\Processing",
      "FolderArchive": "D:\\VCBMoney\\Archive",
      "FolderError": "D:\\VCBMoney\\Error",
      "AllowedExtensions": [".txt", ".mt103"]
    }
  }
}
```

---

## 7. Deployment Steps

### Step 1: Database
```sql
-- Run scripts in order:
1. create_database.sql
2. create_payorder_domestic.sql
```

### Step 2: Folders
```powershell
mkdir D:\VCBMoney\Input
mkdir D:\VCBMoney\Processing
mkdir D:\VCBMoney\Archive
mkdir D:\VCBMoney\Error
```

### Step 3: Application
```bash
dotnet publish -c Release
# Copy to IIS or run as Windows Service
```

### Step 4: Verify
```
GET http://localhost:5000/health
GET http://localhost:5000/hangfire
```

---

## 8. API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| /api/file/upload | POST | Upload file |
| /api/file/{id} | GET | File status |
| /api/transactions | GET | Search transactions |
| /api/transactions/{id} | GET | Transaction detail |
| /api/domestictxt | GET | Search MT103 data |
| /api/domestictxt/{id} | GET | MT103 detail |
| /api/domestictxt/stats | GET | Statistics |
| /health | GET | Health check |
| /hangfire | GET | Job dashboard |

---

## 9. Troubleshooting

### 9.1 File not processing
1. Check FolderInput permissions
2. Check file extension in AllowedExtensions
3. Check Hangfire dashboard for Job1 errors

### 9.2 PayOrder not syncing
1. Check PAYORDER.UpvcbMoney = 0 or 1
2. Check Job2/Job4 in Hangfire
3. Check connection to DBTRUNGGIAN

### 9.3 Performance issues
1. Check batch size (default 1000)
2. Check indexes on database
3. Check SQL Server command timeout

---

## 10. Contact

| Role | Name | Email |
|------|------|-------|
| Developer | - | - |
| Support | - | - |

---

## 11. Change Log

### v4.2 (2025-01-22)
- ✅ Batch processing cho tất cả Jobs (1000 records/batch)
- ✅ Support 100,000+ transactions
- ✅ DOMESTIC_TXT table cho search
- ✅ Remove RAW_CONTENT từ FILE_UPLOAD
- ✅ Fix file flow: Input → Processing → Archive/Error
- ✅ Plaintext MT103 format support
