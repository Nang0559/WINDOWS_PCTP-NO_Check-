# Trạng thái 6 Gate — bằng chứng thực tế

Nguyên tắc: Gate chỉ PASS khi có log chạy thật gắn với commit trong `artifacts\gates\<commit>\<Gate>\gate-summary.json`.
Bảng dưới tách rõ **bằng chứng đã có** và **việc đã vá nhưng chưa chạy thật**.

Commit nền của bản vá này: `9bcf9c9`. Bằng chứng thật mới nhất (G2/G3/G4/G5) thuộc commit `9bcf9c9`, chạy ngày 2026-10-24 (xem bảng). Mọi thay đổi sau commit này cần chạy lại Gate trước khi công bố.

| Gate | Trạng thái | Bằng chứng / việc còn lại |
|---|---|---|
| G1 | **FAIL (còn gap)** | Trước bản vá: 126 gap trên `9bcf9c9` (phân tích tĩnh). Sau bản vá tài liệu `TRANSACTION_BOUNDARIES.md` (mục "G1 repository transaction contracts") và phân loại `DELEGATES` trong `Build-TraceabilityMatrix.py`: **105 gap** (đo tĩnh ngoài máy Windows, chưa chạy lại bằng `-Gate G1` trên máy bạn). Còn lại: ~71 dòng thiếu test, 4 dòng chỉ có test giả, và các dòng ma trận/SQL script planned hoặc không tham chiếu. 6 finding F1-F6 đã có bản vá code (xem `TRANSACTION_BOUNDARIES.md`), test hợp đồng nguồn (G2) và scenario `InTransaction` (G3); đo tĩnh sau vá: gap `transaction-boundary(NONE)` của `TableOrderRepo` biến mất, các dòng khác chuyển từ `test` sang `sql-test(only-fakes)`; chưa chạy lại Gate trên máy Windows. PASS khi `gaps = 0` |
| G2 | **PASS** (commit `9bcf9c9`) | `artifacts\gates\9bcf9c9\G2`: 225/225 pass, 0 fail, 0 skipped, có `.trx`, working tree sạch |
| G3 | **PASS (phạm vi tempdb/fixture)** (commit `9bcf9c9`) | `artifacts\gates\9bcf9c9\G3`: preflight + toàn bộ scenario PASS, SQL 57 / `B7R2_FCC`, chỉ bảng tạm, working tree sạch |
| G4 | **PASS (commit `9bcf9c9`)** | `artifacts\gates\9bcf9c9\G4`: preflight PASS, DEL-003/DEL-004/DEL-007 PASS trên `Usp_Qrcode_Update_Stock2405` thật (STT 32001, mọi ghi đều rollback), working tree sạch. Không phủ `PhieuKhoRepository` end-to-end. Sau khi chạy xong phải chạy `SQL\QC_G4_DeliveryFixture_Cleanup.sql` (cả hai cột phải = 0) |
| G5 | **FAIL / chưa hoàn tất (commit `9bcf9c9`)** | Journey `receive-navigation` fail ở bước `receiving-screen-visible`: sau khi bấm menu "Nhập thành phẩm - QR", tiến trình PCTP thoát với exit code 0 trước khi thấy cửa sổ "Quản lý tiến trình Nhập kho" (45 s). Chưa có `gate-summary.json` và các journey còn lại chưa chạy xong. Chưa rõ nguyên nhân: cần `PCTP\bin\Debug\qc-ui-bridge.log`, Event Viewer, và kết quả bấm tay không bridge; build lại Debug đúng commit trước khi chạy lại |
| G6 | **Có khung chạy, chưa có journey nghiệp vụ** | Runner có `sql-seed` / `sql-assert` / `sql-cleanup` (cleanup luôn chạy trong `finally`, kiểm tra `Remaining = 0`). `g6-selftest` kiểm cơ chế bằng bảng `#temp`. 4 journey nghiệp vụ là **khung chưa cấu hình** (`"configured": false`) nên Gate trả `BLOCKED/FAIL`, không thể PASS giả. Cần seed SQL theo schema thật + AutomationId thao tác UI (xem `journeys\g6\README.md`) |

Ghi chú G3: log của chính nó nêu rõ các phần còn lại là SP thật trên bản sao schema cô lập, `PhieuKhoRepository` đầy đủ và journey UI nghiệp vụ.
