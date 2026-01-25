# VCBMoney Integration v4.2 - Enhanced Features

## 🆕 Điểm mới v4.2

| Feature | Mô tả |
|---------|-------|
| ✅ **Bug fixes** | Fix entity fields (DebitAmount, ValidationWarnings...) |
| ✅ **Audit Log** | Theo dõi ai approve/reject record nào |
| ✅ **Dashboard API** | Statistics, Chart data cho UI |
| ✅ **Approve/Reject** | API manual approve invalid records |
| ✅ **Bulk Operations** | Bulk approve/reject multiple records |
| ✅ **Export CSV** | Export invalid records to CSV |
| ✅ **Health Check** | Check DB connections, folders |

## 🎯 Flow mới theo yêu cầu

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           PHASE 1: INGEST DATA                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   📁 Files                    🗄️ PAYORDER                                   │
│      │                            │                                         │
│      ▼                            ▼                                         │
│   [Job1: File Scanner]       [Job2: DB Scanner]                             │
│   • Doc file, Check Hash     • Quét PAYORDER                                │
│   • L1: Validate Structure   • L2: Validate Required                        │
│   • L2: Validate Required                                                   │
│      │                            │                                         │
│      ▼                            ▼                                         │
│   FILE_UPLOAD                PAYORDER.UPVCBMONEY=1                          │
│   (status=Valid)             (Valid, ready to transform)                    │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    PHASE 2: PARSE & BUSINESS VALIDATE                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│              [Job3: Parse Files]      [Job4: Transform PayOrder]            │
│              • Xử lý FILE_UPLOAD      • Xử lý PAYORDER (Valid)              │
│              • L3: Validate Format    • L3: Validate Format                 │
│              • L4: Check Length       • L4: Check Length                    │
│              • L5: Check Duplicate    • L5: Check Duplicate                 │
│              • L5: Check Amount       • L5: Check Amount                    │
│                       │                        │                            │
│                       └──────────┬─────────────┘                            │
│                                  │                                          │
│                    ┌─────────────┴─────────────┐                            │
│                    │                           │                            │
│                    ▼                           ▼                            │
│           ┌───────────────┐          ┌────────────────────┐                 │
│           │  TRANSACTIONS │          │ TRANSACTIONS_IMPORT│                 │
│           │    (VALID)    │          │     (INVALID)      │                 │
│           │ L3+L4+L5 OK   │          │  Lỗi để kiểm tra   │                 │
│           └───────┬───────┘          └────────────────────┘                 │
│                   │                                                         │
└───────────────────┼─────────────────────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      PHASE 3: FINALIZE & SYNC RESULT                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│                         [Job5: Sync Process]                                │
│                         • Đọc TRANSACTIONS (Ready)                          │
│                         • Insert DOMESTIC_RESULT                            │
│                         • Update status = Synced                            │
│                                  │                                          │
│                                  ▼                                          │
│                          DOMESTIC_RESULT                                    │
│                          Kết quả cuối cùng                                  │
│                          • Đã map fields                                    │
│                          • Status: Synced                                   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## 📊 Bảng dữ liệu

| Database | Table | Mô tả |
|----------|-------|-------|
| VCB_MONEY | FILE_UPLOAD | Metadata file, kết quả L1+L2 |
| VCB_MONEY | **TRANSACTIONS** | ✅ Dữ liệu **HỢP LỆ** (L3+L4+L5 OK) |
| VCB_MONEY | **TRANSACTIONS_IMPORT** | ❌ Dữ liệu **LỖI** (cần kiểm tra) |
| DBTRUNGGIAN | PAYORDER | Input từ hệ thống cũ |
| DBTRUNGGIAN | DOMESTIC_RESULT | Output - kết quả sync |

## 🔄 Jobs

| Job | Tên | Input | Output | Validate |
|-----|-----|-------|--------|----------|
| **1** | File Scanner | Folder | FILE_UPLOAD | L1+L2 |
| **2** | DB Scanner | PAYORDER | PAYORDER (flagged) | L2 |
| **3** | Parse & Validate | FILE_UPLOAD | TRANSACTIONS / TRANSACTIONS_IMPORT | L3+L4+L5 |
| **4** | Transform & Validate | PAYORDER | TRANSACTIONS / TRANSACTIONS_IMPORT | L3+L4+L5 |
| **5** | Sync Result | TRANSACTIONS | DOMESTIC_RESULT | - |
| **6** | Retry Failed | TRANSACTIONS_IMPORT | TRANSACTIONS (if recovered) | L5 |
| **7** | Cleanup | Files > 90 days | Delete | - |
| **8** | Report | All | Log | - |

## 🔍 TRANSACTIONS_IMPORT - Error Tracking

Bảng này lưu chi tiết lỗi để dễ dàng review và fix:

```sql
-- Raw data giữ nguyên để so sánh
BEN_ACCOUNT_NAME_RAW  -- Giá trị gốc
BEN_ACCOUNT_NAME      -- Giá trị đã truncate

-- Field-level error flags (cho UI highlight)
ERR_TRANS_REF    -- Lỗi TransRef
ERR_AMOUNT       -- Lỗi Amount
ERR_BEN_NAME     -- Lỗi BenName
ERR_DUPLICATE    -- Lỗi trùng
ERR_BUSINESS     -- Lỗi business rule

-- JSON chi tiết
VALIDATION_ERRORS  -- Full JSON errors
```

## 🚀 Cài đặt

```bash
# 1. Setup database
sqlcmd -i database/create_database.sql

# 2. Run
cd src/VCBMoney.Web
dotnet run
```

## 🔗 API Endpoints

### Dashboard
| Endpoint | Mô tả |
|----------|-------|
| `GET /api/dashboard/stats` | Full statistics |
| `GET /api/dashboard/chart/daily?days=7` | Daily chart data |
| `GET /api/dashboard/chart/errors` | Errors by level |

### Jobs
| Endpoint | Mô tả |
|----------|-------|
| `POST /api/jobs/job{1-6}/trigger` | Trigger single job |
| `POST /api/jobs/all/trigger` | Trigger all jobs |

### Transactions
| Endpoint | Mô tả |
|----------|-------|
| `GET /api/transactions` | List valid transactions |
| `GET /api/transactions/{id}` | Get detail |

### Invalid Transactions
| Endpoint | Mô tả |
|----------|-------|
| `GET /api/invalid-transactions` | List invalid records |
| `GET /api/invalid-transactions/{id}` | Get detail |
| `GET /api/invalid-transactions/{id}/errors` | Get validation errors |
| `POST /api/invalid-transactions/{id}/approve` | Approve → TRANSACTIONS |
| `POST /api/invalid-transactions/{id}/reject` | Reject |
| `POST /api/invalid-transactions/bulk-approve` | Bulk approve |
| `POST /api/invalid-transactions/bulk-reject` | Bulk reject |
| `GET /api/invalid-transactions/export` | Export to CSV |

### Health & Audit
| Endpoint | Mô tả |
|----------|-------|
| `GET /api/health` | Health check all components |
| `GET /api/audit` | Audit logs |

## ✅ So sánh v4.0 vs v4.1 vs v4.2

| Feature | v4.0 | v4.1 | v4.2 |
|---------|------|------|------|
| Valid data | TRANSACTION_IMPORT | **TRANSACTIONS** | **TRANSACTIONS** |
| Invalid data | TRANSACTION_IMPORT | **TRANSACTIONS_IMPORT** | **TRANSACTIONS_IMPORT** |
| Separate tables | ❌ | ✅ | ✅ |
| Raw value preserved | ❌ | ✅ | ✅ |
| Field-level error flags | ❌ | ✅ | ✅ |
| Audit Log | ❌ | ❌ | ✅ |
| Dashboard API | ❌ | ❌ | ✅ |
| Approve/Reject API | ❌ | ❌ | ✅ |
| Bulk Operations | ❌ | ❌ | ✅ |
| Export CSV | ❌ | ❌ | ✅ |
| Health Check | ❌ | ❌ | ✅ |
