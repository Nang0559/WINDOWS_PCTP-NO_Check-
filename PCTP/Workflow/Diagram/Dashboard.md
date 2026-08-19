Tài Liệu Thiết Kế: Màn Hình Chính & Trung Tâm Điều Hành Kho (Dashboard)Vị trí trong bộ tài liệu: Đây là bản trực quan hoá giao diện (UI) dựa trên luồng nghiệp vụ tổng thể tại WORKFLOW_WMS.md và ranh giới 4 phân khu tại ARCHITECTURE_DEPENDENCIES.md (hoặc WORKFLOW_DEPEND.md). Tài liệu này không định nghĩa lại luồng hay phụ thuộc mà chỉ mô tả cách hiển thị trạng thái vận hành lên màn hình chính.1. Cấu Trúc Màn Hình Chính (Shell Dashboard Layout)Màn hình TrungTamDieuHanhKho được thiết kế theo nguyên tắc ánh xạ 1-1 với 4 Subsystems chính của hệ thống. Ngay khi mở ứng dụng, người dùng có thể nắm bắt toàn bộ tình trạng công việc cần xử lý thông qua các con số thống kê (badge) được truy vấn thực tế từ cơ sở dữ liệu.Plaintext┌─────────────────────────────────────────────────────────────────┐
│  TRUNG TÂM ĐIỀU HÀNH KHO          [Người dùng: ...]  [Ngày: ...] │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌───────┐│
│  │ 📥 NHẬP KHO   │  │ 📤 XUẤT KHO   │  │ ⚠️ XỬ LÝ LỖI  │  │ 🏭 KHO ││
│  │              │  │              │  │              │  │ CORE  ││
│  │ Chờ nhập: 12 │  │ Chờ giao: 8  │  │ Chờ QC ĐH: 3 │  │       ││
│  │ [Vào module] │  │ Chờ xác      │  │ Đang Rework: 5│  │Bản đồ ││
│  └──────────────┘  │ nhận: 4      │  │ Chờ NC cuối: 2│  │kho vật││
│                     │ [Vào module] │  │ [Vào module] │  │lý     ││
│                     └──────────────┘  └──────────────┘  └───────┘│
│                                                                   │
│  ─────────────────── BẠN ĐANG Ở ĐÂU? ─────────────────────────  │
│  [Sơ đồ luồng trực quan — highlight đúng bước hiện tại]         │
│                                                                   │
│  ─────────────────── TRA CỨU & BÁO CÁO NHANH ──────────────────  │
│  [🔍 Tra cứu QR / Lệnh / Mã Lot]     [📊 Báo cáo tồn kho / Xuất nhập]│
└─────────────────────────────────────────────────────────────────┘
⚠️ Lưu ý kiến trúc: Khu vực "Tra cứu & Báo cáo" không phải là phân khu (subsystem) thứ 5. Đây là tính năng xuyên suốt (cross-cutting), đọc dữ liệu đồng thời từ cả 4 phân khu (ví dụ: tra cứu 1 mã LOT cần xem cả tồn kho core, lịch sử xuất kho, và phiếu bất thường liên quan). Việc đặt cùng hàng giao diện với 4 ô module chỉ nhằm tối ưu hóa bố cục màn hình, hoàn toàn không đại diện cho một subsystem độc lập.2. Sơ Đồ Điều Hướng (Mermaid Diagram)Sơ đồ dưới đây minh họa cấu trúc điều hướng từ màn hình Trung tâm Điều hành Kho đến các module nghiệp vụ tương ứng và tính năng tra cứu xuyên suốt:Code-Snippetgraph TD
    Dashboard([Trung Tâm Điều Hành Kho - Dashboard]) --> Sub1[📥 Nhập Kho]
    Dashboard --> Sub2[📤 Xuất Kho]
    Dashboard --> Sub3[⚠️ Xử Lý Hàng Lỗi]
    Dashboard --> Sub4[🏭 Kho Core / Bản Đồ]
    Dashboard -.-> Sub5[🔍 Tra Cứu & Báo Cáo Nhanh<br/><i>tính năng xuyên suốt, không phải subsystem</i>]

    Sub1 --> Click1[Click] --> Screen1[Màn hình Quản lý Nhập TP]
    Sub2 --> Click2[Click] --> Screen2[Màn hình Quản lý Xuất & Chờ Giao]
    Sub3 --> Click3[Click] --> Screen3[Màn hình QTChung & Quản lý Phiếu Bất Thường]
    Sub4 --> Click4[Click] --> Screen4[Bản đồ Không gian Kho 2D]
    Sub5 --> Scan5[Quét QR / Click] --> Screen5[Hiển thị kết quả tra cứu tức thời]

    style Sub5 stroke-dasharray: 5 5
3. Nguồn Dữ Liệu Cho Badge (Tránh Shell chứa logic nghiệp vụ)Theo nguyên tắc kiến trúc, Shell (TrungTamDieuHanhKho) chỉ đóng vai trò điều hướng và hiển thị UI, tuyệt đối không chứa logic nghiệp vụ hoặc tự gọi trực tiếp nhiều repository khác nhau để tính toán badge. Hệ thống chuẩn hóa thông qua một tầng tổng hợp riêng (IDashboardQueryService):C#public interface IDashboardQueryService {
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
Bảng Ánh Xạ Nguồn Dữ Liệu Badge Thực TếPhân KhuTên Badge / Chỉ SốNguồn Số Liệu (Service / Repository)Điều Kiện Lọc / Trạng TháiNhập KhoChờ nhậpINhapTpReceivingService / NHAP_TP_HISLọc theo các bản ghi có trạng thái chưa xử lý / chưa hoàn tất.Xuất KhoChờ giaoIHangChoGiaoRepositoryGetByReference với trạng thái ChoGiao, thực hiện COUNT theo loại reference.Xuất KhoChờ xác nhậnIHangChoGiaoRepository / IStockExportServiceCác lệnh đã pick vào chờ giao nhưng chưa thực hiện Confirm chốt xuất (TrangThai = DangGiao).Xử Lý LỗiChờ QC định hướngIPhieuXuLyBatThuongRepositoryLọc bản ghi có trạng thái QTChungStatus.ChoQCDinhHuong.Xử Lý LỗiĐang ReworkBảng phiếu xử lý / QTChungLọc bản ghi có trạng thái QTChungStatus.DangRework đang thực hiện sửa chữa tại xưởng.Xử Lý LỗiChờ QC xác nhận cuốiBảng phiếu xử lý / QTChungLọc bản ghi chờ QC kiểm tra sau khi hoàn tất Rework (QTChungStatus.ChoQCXacNhanCuoi).Lưu ý: DashboardCounters map trực tiếp theo đúng tên state trong enum QTChungStatus đã được định nghĩa tại luồng Xử lý hàng lỗi — không tự đặt tên trạng thái mới ở tầng UI.3.1. Cập Nhật Badge Theo Thời Gian Thực (Realtime Event Bus)Hệ thống không sử dụng cơ chế polling liên tục gây quá tải cơ sở dữ liệu, mà tái sử dụng cơ chế sự kiện sẵn có trong kiến trúc (ví dụ: StockChangedNotifier, AppEventBus hoặc LotStatusResetEvent). Khi một module con ghi nhận thay đổi dữ liệu (ví dụ: PhieuXuLyBatThuongRepository.UpdateTrangThai), hệ thống sẽ phát sự kiện; Dashboard sẽ đăng ký (subscribe) lắng nghe và tự động làm mới đúng badge liên quan mà không cần refresh toàn bộ màn hình.4. Khu Vực Tra Cứu & Báo Cáo Xuyên Suốt (Cross-Cutting Features)4.1. Ô Tra Cứu Nhanh Đa Năng (Global Quick Search)Chức năng: Cho phép nhập hoặc quét mã QR/Barcode trực tiếp từ thiết bị (mã lệnh sản xuất, mã pallet, mã lô hàng Lot, hoặc mã phiếu bất thường).Cơ chế điều hướng thông minh: Hệ thống tự động nhận diện định dạng mã (Pattern Matching) và điều hướng người dùng đến đúng màn hình chi tiết tương ứng:Nếu là mã Nhập kho $\rightarrow$ Mở màn hình chi tiết Inbound.Nếu là mã Phiếu bất thường / QTChung $\rightarrow$ Mở màn hình xử lý lỗi tương ứng.Nếu là mã Slot / Lot $\rightarrow$ Mở bản đồ Kho Core (ISlotService) để tra cứu vị trí tồn kho không gian thực tế.4.2. Báo Cáo Tồn Kho & Lịch Sử Giao Dịch Nhanh (Quick Reports Widget)Báo cáo Tồn kho theo thời gian thực: Hiển thị tổng tồn kho STOCKTP kết hợp phân bổ chi tiết theo từng khu vực kho (Kho A0, Kho thường, Kho lỗi/NG).Báo cáo Lịch sử giao dịch (StockHistory): Tích hợp lối tắt xem nhanh các biến động xuất/nhập/chuyển vị trí gần nhất dựa trên IStockHistoryRepository, hỗ trợ xuất dữ liệu ra file Excel phục vụ kiểm kê ca sản xuất.5. Các Việc Còn Treo (Action Items / Open Questions)Xác nhận nguồn số liệu thực tế cho badge "Nhập kho — Chờ nhập" (do chưa có repository cụ thể được định nghĩa tường minh trong các tài liệu phân khu trước).Quyết định kiến trúc dịch vụ: IDashboardQueryService sẽ là service viết mới hoàn toàn hay được gom nhóm từ các service hiện có của các subsystem.Cấu hình sự kiện EventBus: Thống nhất rõ sự kiện nào (StockChangedNotifier hay AppEventBus) kích hoạt việc refresh badge nào để tránh việc Dashboard subscribe tràn lan không cần thiết.
