# Trạng thái 6 Gate — bằng chứng thực tế

Nguyên tắc: Gate chỉ PASS khi có log chạy thật gắn với commit trong `artifacts\gates\<commit>\<Gate>\gate-summary.json`.
Bảng dưới tách rõ **bằng chứng đã có** và **việc đã vá nhưng chưa chạy thật**.

Commit nền của bản vá này: `f98d063`. Bằng chứng thật mới nhất (G2/G3/G4) thuộc commit `f98d063`, chạy ngày 2026-10-24 (xem bảng); các dòng cũ dưới đây thuộc commit `0aaa66d`; các commit sau đó chỉ đổi `.ps1`/tài liệu,
nhưng theo nguyên tắc ở trên vẫn cần chạy lại trên commit hiện tại trước khi công bố.

| Gate | Trạng thái | Bằng chứng / việc còn lại |
|---|---|---|
| G1 | **Chạy được, kết quả FAIL (122 gap)** | `Invoke-PctpQcGates.ps1 -Gate G1` sinh [`TRACEABILITY_MATRIX.md`](TRACEABILITY_MATRIX.md) trên `f4d7215`: 87 dòng repository/service, 75 dòng có gap, 9 script SQL, 122 gap (74 thiếu test, 22 thiếu/chưa ghi transaction boundary, 1 chỉ có test giả). Phân tích tĩnh, không mở DB. PASS khi `gaps = 0` |
| G2 | **PASS** (commit `f98d063`) | `artifacts\gates\f98d063\G2`: 224/224 pass, 0 fail, 0 skipped, có `.trx`, working tree sạch |
| G3 | **PASS (phạm vi tempdb/fixture)** (commit `f98d063`) | `artifacts\gates\f98d063\G3`: preflight + toàn bộ scenario PASS, SQL 57 / `B7R2_FCC`, chỉ bảng tạm |
| G4 | **FAIL (commit `f98d063`) — nguyên nhân: SP đã deploy trên DB test là bản cũ** | `--delivery-preflight` báo 4 lỗi: thiếu `PCTP_DELIVERY:`, `sp_getapplock`, savepoint `UspQrcodeUpdateStock2405`, `@LockOwner = N'Transaction'`. Đây là lỗi triển khai DB, không phải lỗi code. Cách xử lý: `PCTP.QC.UI\Deploy-PctpDeliveryProcedure.ps1 -SqlUser <user> -ConfirmIsolatedDb` (sao lưu định nghĩa cũ, `CREATE OR ALTER`, kiểm tra 4 marker, commit/rollback trong 1 transaction), commit, rồi chạy lại G2 → G3 → G4. Có thể cần thêm `-DeliveryAcceptanceStt <STT>` nếu DB test không có dòng `TMPPHIEUGIAOHANG` chờ giao phù hợp. Không phủ `PhieuKhoRepository` end-to-end |
| G5 | **Đã vá discovery, chưa xác nhận** | Commit `1753667` sửa `Get-PctpWindows` thấy dialog thuộc cửa sổ khác; chưa có báo cáo nào xác nhận. `-Gate G5 -ExePath` chạy 5 journey và chỉ PASS khi cả 5 PASS **và** có ảnh + tree JSON mới sinh trong lần chạy này |
| G6 | **Có khung chạy, chưa có journey nghiệp vụ** | Runner có `sql-seed` / `sql-assert` / `sql-cleanup` (cleanup luôn chạy trong `finally`, kiểm tra `Remaining = 0`). `g6-selftest` kiểm cơ chế bằng bảng `#temp`. 4 journey nghiệp vụ là **khung chưa cấu hình** (`"configured": false`) nên Gate trả `BLOCKED/FAIL`, không thể PASS giả. Cần seed SQL theo schema thật + AutomationId thao tác UI (xem `journeys\g6\README.md`) |

Ghi chú G3: log của chính nó nêu rõ các phần còn lại là SP thật trên bản sao schema cô lập, `PhieuKhoRepository` đầy đủ và journey UI nghiệp vụ.
