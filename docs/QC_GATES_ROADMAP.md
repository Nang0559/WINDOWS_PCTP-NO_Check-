# Lộ trình hoàn thành 6 Gate (PCTP QC)

Nguyên tắc: một Gate chỉ PASS khi có **log chạy thật gắn với commit** (thư mục `artifacts\gates\<commit>\`). Không đánh dấu PASS từ việc đọc code.

| Gate | Trạng thái | Cách chạy / làm | Điều kiện PASS |
|---|---|---|---|
| G1   | Chạy được, đang FAIL (gap thật) | `.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G1` (cần Python 3) → `docs/TRACEABILITY_MATRIX.md` | Mỗi dòng ma trận có file code, SP/SQL, transaction boundary và test tương ứng (gaps = 0) |
| G2 | Đã Chạy  | `.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G2` | Failed = 0, Total > 0, có file `.trx` |
| G3 | Đã Chạy | `.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G3 -SqlUser <user>` | Preflight **và** chạy đầy đủ repository scenario đều exit 0 |
| G4   | FAIL (`f98d063`): SP trên DB test là bản cũ | Chạy `.\PCTP.QC.UI\Deploy-PctpDeliveryProcedure.ps1 -SqlUser <user> -ConfirmIsolatedDb`, commit, rồi `Invoke-PctpQcGates.ps1 -Gate G4 -SqlUser <user>` | SQL hậu điều kiện đúng sau cả thành công lẫn thất bại |
| G5   | Đã vá discovery, chưa xác nhận | `.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G5 -ExePath <PCTP.exe>` | 5 journey PASS, ảnh + tree JSON sinh ra trong chính lần chạy |
| G6   | Có khung, thiếu seed nghiệp vụ | `.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G6 -ExePath <PCTP.exe> -SqlUser <user> -AllowMutations -ConfirmAppUsesIsolatedDb` | UI + SQL + cleanup cùng PASS trên fixture cô lập (selftest + 4 journey) |

## Thứ tự làm
G2 → G3 → G5 (làm song song được) → G1 → G4 → G6. G2/G3 rẻ nhất và cho biết nền test có đáng tin không.

## G1 – Kiểm kê và truy vết
1. Lấy danh sách repository/service từ code: `PCTP\Modules\*\Repositories`, `*\Services`.
2. Lấy danh sách SP/SQL: thư mục `SQL\` và chuỗi SQL trong repository.
3. Đối chiếu với `PCTP.QC.Tests\TestMatrix.json` (đã có `TestMatrixIntegrityTests`). Bổ sung cột: file nguồn, SP, bảng bị ghi, transaction (`TRANSACTION_BOUNDARIES.md`).
4. Mọi dòng không có test → ghi rõ "GAP", không bỏ qua.

**Đã làm:** `PCTP.QC.UI\Build-TraceabilityMatrix.py` (gọi bởi `-Gate G1`) sinh ma trận đủ 4 cột (file nguồn, SP/bảng ghi, transaction, test) cho mọi repository/service trong `PCTP\Modules`, đối chiếu thư mục `SQL\` và `TestMatrix.json`, rồi xuất `gaps.csv`. Giới hạn: SQL ghép động không bắt được bằng regex nên cần review tay.

## G4 – Rollback / duplicate / concurrency
Chạy trên DB cô lập (đã có `TestEnvironmentPolicy` chặn production `192.168.200.14\BRAVO`). Wrapper `-Gate G4` chạy kịch bản SP thật `--delivery-acceptance` (mọi ghi đều rollback) sau khi G3 PASS cùng commit; `PhieuKhoRepository` end-to-end vẫn là GAP. Mỗi case phải có truy vấn hậu điều kiện: tồn kho, SlotLot, history, bảng guard `PCTPStockMovementGuard`. Case thất bại phải chứng minh **không** thay đổi dữ liệu.

## G5 – 5 journey QC.UI
- Dùng cầu nối `Main_APP.QcUiBridge.cs` (đặt `PCTP_QC_UI_BRIDGE=1`, runner tự đặt).
- `qc-direction` và `qc-final` trước đây kỳ vọng form `FormQCDinhHuong`/`FormQCXacNhanCuoi`, nhưng menu thực tế mở màn hình quy trình dùng chung (`FormQuanLyTienTrinhHangLoi`, tiêu đề "Quản lý tiến trình Xử lý hàng lỗi"). Hai manifest đã được sửa cho đúng nguồn.
- Màn hình chi tiết QC chỉ mở (ShowDialog) từ một dòng phiếu đã seed → chuyển sang G6.

## G6 – Journey nghiệp vụ có seed/reset
Mỗi journey cần 4 phần, theo thứ tự: **seed** (SQL tạo dữ liệu duy nhất, ví dụ tiền tố `QCUI_<runId>`) → **thao tác UI** → **SQL hậu điều kiện** → **cleanup** (xoá đúng dữ liệu đã seed, kiểm tra không còn dòng nào).
Thứ tự đề xuất: (1) QC định hướng trên phiếu seed, (2) QC xác nhận cuối, (3) nhập kho một phiếu, (4) giao hàng HVN. Cần thêm vào runner: bước `sql-seed`, `sql-assert`, `sql-cleanup` và chạy với `-AllowMutations` chỉ trên DB cô lập. Phần này cần bạn cung cấp chuỗi kết nối DB cô lập và quy ước bảng/phiếu có thể seed.

**Đã làm trong runner:** các bước `sql-seed`, `sql-assert`, `sql-cleanup` (xem `PCTP.QC.UI\PctpSql.Common.ps1`), cleanup luôn chạy trong `finally` và phải chứng minh `Remaining = 0`; mọi câu lệnh ghi bắt buộc dùng `@Prefix` và có `WHERE`; production luôn bị từ chối; `-AllowMutations` bắt buộc. `journeys\g6\g6-selftest.json` kiểm chứng cơ chế bằng bảng `#temp`. Bốn journey nghiệp vụ trong `journeys\g6\` là khung `"configured": false` — chạy Gate sẽ báo BLOCKED cho tới khi bổ sung SQL và bước UI theo `journeys\g6\README.md`.
