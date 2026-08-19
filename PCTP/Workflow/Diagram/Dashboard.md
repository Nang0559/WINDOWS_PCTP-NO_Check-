# Tài Liệu Thiết Kế: Màn Hình Chính & Trung Tâm Điều Hành Kho (Dashboard)

> **Vị trí trong bộ tài liệu:** Đây là bản trực quan hoá giao diện (UI) dựa trên luồng nghiệp vụ tổng thể tại `WORKFLOW_WMS.md` và ranh giới 4 phân khu tại `ARCHITECTURE_DEPENDENCIES.md`. Tài liệu này mô tả cách hiển thị trạng thái vận hành lên màn hình chính.

---

## 1. Cấu Trúc Màn Hình Chính (Shell Dashboard Layout)

Màn hình `TrungTamDieuHanhKho` được chia thành các khu vực chính ánh xạ 1-1 với 4 Subsystems, giúp người dùng nắm bắt công việc qua các badge số liệu thực tế:

* **Khu vực module nghiệp vụ (4 Phân khu chính):**
  * **📥 Nhập Kho:** Hiển thị badge *Chờ nhập*. Bấm vào để vào module Nhập TP.
  * **📤 Xuất Kho:** Hiển thị badge *Chờ giao* và *Chờ xác nhận*. Bấm vào để vào module Quản lý Xuất.
  * **⚠️ Xử Lý Lỗi:** Hiển thị badge *Chờ QC định hướng*, *Đang Rework*, *Chờ QC xác nhận cuối*. Bấm vào để mở QTChung / Phiếu bất thường.
  * **🏭 Kho Core:** Hiển thị bản đồ kho vật lý 2D / Không gian kho.
* **Khu vực tra cứu & báo cáo xuyên suốt (Cross-cutting):**
  * Ô tra cứu nhanh đa năng (Quét QR / Lệnh / Mã Lot).
  * Widget báo cáo tồn kho & lịch sử giao dịch nhanh.

> ⚠️ **Lưu ý kiến trúc:** Khu vực *"Tra cứu & Báo cáo"* **không** phải là phân khu thứ 5, mà là tính năng xuyên suốt đọc dữ liệu từ cả 4 phân khu.

---

## 2. Quy Định Điều Hướng Module

Khi người dùng thao tác trên màn hình chính, hệ thống điều hướng đến các màn hình chi tiết tương ứng:
* **Click Nhập Kho** $\rightarrow$ Mở màn hình Quản lý Nhập TP.
* **Click Xuất Kho** $\rightarrow$ Mở màn hình Quản lý Xuất & Chờ Giao.
* **Click Xử Lý Lỗi** $\rightarrow$ Mở màn hình QTChung & Quản lý Phiếu Bất Thường.
* **Click Kho Core** $\rightarrow$ Mở bản đồ không gian kho 2D.
* **Quét mã QR / Tra cứu** $\rightarrow$ Tự động nhận diện pattern và hiển thị kết quả tức thời.

---

## 3. Sơ Đồ Điều Hướng (Mermaid Diagram)

Sơ đồ dưới đây minh họa cấu trúc điều hướng từ màn hình Trung tâm Điều hành Kho đến các module nghiệp vụ tương ứng (sử dụng hướng hiển thị ngang để tránh tràn dòng):

```mermaid
graph LR
    Dashboard([Dashboard]) --> Sub1[📥 Nhập Kho]
    Dashboard --> Sub2[📤 Xuất Kho]
    Dashboard --> Sub3[⚠️ Xử Lý Lỗi]
    Dashboard --> Sub4[🏭 Kho Core]
    Dashboard -.-> Sub5[🔍 Tra Cứu & Báo Cáo]

    Sub1 --> Screen1[Quản lý Nhập TP]
    Sub2 --> Screen2[Quản lý Xuất & Chờ Giao]
    Sub3 --> Screen3[QTChung & Phiếu Bất Thường]
    Sub4 --> Screen4[Bản đồ Không gian Kho 2D]
    Sub5 --> Screen5[Kết quả tra cứu tức thời]

    style Sub5 stroke-dasharray: 5 5
4. Nguồn Dữ Liệu Cho Badge (Tránh Shell chứa logic nghiệp vụ)Theo nguyên tắc kiến trúc, Shell (TrungTamDieuHanhKho) không chứa logic nghiệp vụ mà thông qua tầng tổng hợp riêng (IDashboardQueryService):C#public interface IDashboardQueryService {
    DashboardCounters GetCounters();
}

public sealed class DashboardCounters {
    public int NhapKho_ChoNhap { get; set; }
    public int XuatKho_ChoGiao { get; set; }
    public int XuatKho_ChoXacNhan { get; set; }
    public int XuLyLoi_ChoQCDinhHuong { get; set; }
    public int XuLyLoi_DangRework { get; set; }
    public int XuLyLoi_ChoQCXacNhanCuoi { get; set; }
}
Bảng Ánh Xạ Nguồn Dữ Liệu Badge Thực TếPhân KhuTên Badge / Chỉ SốNguồn Số Liệu (Service / Repository)Điều Kiện Lọc / Trạng TháiNhập KhoChờ nhậpINhapTpReceivingService / NHAP_TP_HISLọc theo các bản ghi có trạng thái chưa xử lý / chưa hoàn tất.Xuất KhoChờ giaoIHangChoGiaoRepositoryGetByReference với trạng thái ChoGiao, thực hiện COUNT theo loại reference.Xuất KhoChờ xác nhậnIHangChoGiaoRepository / IStockExportServiceCác lệnh đã pick vào chờ giao nhưng chưa thực hiện Confirm chốt xuất (TrangThai = DangGiao).Xử Lý LỗiChờ QC định hướngIPhieuXuLyBatThuongRepositoryLọc bản ghi có trạng thái QTChungStatus.ChoQCDinhHuong.Xử Lý LỗiĐang ReworkBảng phiếu xử lý / QTChungLọc bản ghi có trạng thái QTChungStatus.DangRework đang thực hiện sửa chữa tại xưởng.Xử Lý LỗiChờ QC xác nhận cuốiBảng phiếu xử lý / QTChungLọc bản ghi chờ QC kiểm tra sau khi hoàn tất Rework (QTChungStatus.ChoQCXacNhanCuoi).Lưu ý: DashboardCounters map trực tiếp theo đúng tên state trong enum QTChungStatus đã được định nghĩa tại luồng Xử lý hàng lỗi — không tự đặt tên trạng thái mới ở tầng UI.5. Cập Nhật Badge Theo Thời Gian Thực (Realtime Event Bus)Hệ thống không sử dụng cơ chế polling liên tục gây quá tải cơ sở dữ liệu, mà tái sử dụng cơ chế sự kiện sẵn có trong kiến trúc (ví dụ: StockChangedNotifier, AppEventBus hoặc LotStatusResetEvent). Khi một module con ghi nhận thay đổi dữ liệu (ví dụ: PhieuXuLyBatThuongRepository.UpdateTrangThai), hệ thống sẽ phát sự kiện; Dashboard sẽ đăng ký (subscribe) lắng nghe và tự động làm mới đúng badge liên quan mà không cần refresh toàn bộ màn hình.6. Khu Vực Tra Cứu & Báo Cáo Xuyên Suốt (Cross-Cutting Features)6.1. Ô Tra Cứu Nhanh Đa Năng (Global Quick Search)Chức năng: Cho phép nhập hoặc quét mã QR/Barcode trực tiếp từ thiết bị (mã lệnh sản xuất, mã pallet, mã lô hàng Lot, hoặc mã phiếu bất thường).Cơ chế điều hướng thông minh: Hệ thống tự động nhận diện định dạng mã (Pattern Matching) và điều hướng người dùng đến đúng màn hình chi tiết tương ứng.6.2. Báo Cáo Tồn Kho & Lịch Sử Giao Dịch Nhanh (Quick Reports Widget)Báo cáo Tồn kho theo thời gian thực: Hiển thị tổng tồn kho STOCKTP kết hợp phân bổ chi tiết theo từng khu vực kho (Kho A0, Kho thường, Kho lỗi/NG).Báo cáo Lịch sử giao dịch (StockHistory): Tích hợp lối tắt xem nhanh các biến động xuất/nhập/chuyển vị trí gần nhất dựa trên IStockHistoryRepository.
    
