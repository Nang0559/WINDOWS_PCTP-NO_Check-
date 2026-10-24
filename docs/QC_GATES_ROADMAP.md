# Lộ trình hoàn thành 6 Gate (PCTP QC)

Nguyên tắc: một Gate chỉ PASS khi có **log chạy thật gắn với commit** (thư mục `artifacts\gates\<commit>\`). Không đánh dấu PASS từ việc đọc code.

| Gate | Trạng thái | Cách chạy / làm | Điều kiện PASS |
|---|---|---|---|
| G1 | Chưa làm | Xem mục G1 bên dưới | Mỗi dòng ma trận có file code, SP/SQL, transaction boundary và test tương ứng |
| G2 | Chưa chạy | `.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G2` | Failed = 0, Total > 0, có file `.trx` |
| G3 | Chưa chạy | `.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G3 -SqlUser <user>` | Preflight **và** chạy đầy đủ repository scenario đều exit 0 |
| G4 | Một phần có sẵn | Scenario có sẵn trong `PCTP.QC.SqlIntegration` (rollback, duplicate, concurrency) + SP `Usp_Qrcode_Update_Stock2405` | SQL hậu điều kiện đúng sau cả thành công lẫn thất bại |
| G5 | Đang sửa | `Invoke-PctpUiSuite.ps1 -OutputDirectory .\artifacts\ui-<commit>` | 5 journey PASS, ảnh + tree JSON mới |
| G6 | Chưa có | Xem mục G6 bên dưới | UI + SQL + cleanup cùng PASS trên fixture cô lập |

## Thứ tự làm
G2 → G3 → G5 (làm song song được) → G1 → G4 → G6. G2/G3 rẻ nhất và cho biết nền test có đáng tin không.

## G1 – Kiểm kê và truy vết
1. Lấy danh sách repository/service từ code: `PCTP\Modules\*\Repositories`, `*\Services`.
2. Lấy danh sách SP/SQL: thư mục `SQL\` và chuỗi SQL trong repository.
3. Đối chiếu với `PCTP.QC.Tests\TestMatrix.json` (đã có `TestMatrixIntegrityTests`). Bổ sung cột: file nguồn, SP, bảng bị ghi, transaction (`TRANSACTION_BOUNDARIES.md`).
4. Mọi dòng không có test → ghi rõ "GAP", không bỏ qua.

## G4 – Rollback / duplicate / concurrency
Chạy trên DB cô lập (đã có `TestEnvironmentPolicy` chặn production `192.168.200.14\BRAVO`). Mỗi case phải có truy vấn hậu điều kiện: tồn kho, SlotLot, history, bảng guard `PCTPStockMovementGuard`. Case thất bại phải chứng minh **không** thay đổi dữ liệu.

## G5 – 5 journey QC.UI
- Dùng cầu nối `Main_APP.QcUiBridge.cs` (đặt `PCTP_QC_UI_BRIDGE=1`, runner tự đặt).
- `qc-direction` và `qc-final` trước đây kỳ vọng form `FormQCDinhHuong`/`FormQCXacNhanCuoi`, nhưng menu thực tế mở màn hình quy trình dùng chung (`FormQuanLyTienTrinhHangLoi`, tiêu đề "Quản lý tiến trình Xử lý hàng lỗi"). Hai manifest đã được sửa cho đúng nguồn.
- Màn hình chi tiết QC chỉ mở (ShowDialog) từ một dòng phiếu đã seed → chuyển sang G6.

## G6 – Journey nghiệp vụ có seed/reset
Mỗi journey cần 4 phần, theo thứ tự: **seed** (SQL tạo dữ liệu duy nhất, ví dụ tiền tố `QCUI_<runId>`) → **thao tác UI** → **SQL hậu điều kiện** → **cleanup** (xoá đúng dữ liệu đã seed, kiểm tra không còn dòng nào).
Thứ tự đề xuất: (1) QC định hướng trên phiếu seed, (2) QC xác nhận cuối, (3) nhập kho một phiếu, (4) giao hàng HVN. Cần thêm vào runner: bước `sql-seed`, `sql-assert`, `sql-cleanup` và chạy với `-AllowMutations` chỉ trên DB cô lập. Phần này cần bạn cung cấp chuỗi kết nối DB cô lập và quy ước bảng/phiếu có thể seed.
