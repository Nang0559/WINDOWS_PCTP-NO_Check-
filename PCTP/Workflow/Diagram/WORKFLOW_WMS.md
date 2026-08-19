# Tài Liệu Kiến Trúc & Luồng Vận Hành Hệ Thống Kho (WMS / Production Management)

> **Vị trí trong bộ tài liệu:** Đây là tài liệu MẸ — mô tả bức tranh tổng thể 4 phân khu (Subsystems) và cách chúng liên kết với nhau. Chi tiết đầy đủ API/luồng nội bộ của từng phân khu nằm ở tài liệu CON riêng, được link ở mỗi mục tương ứng. Khi cần sửa danh sách API, sửa Ở TÀI LIỆU CON — không lặp lại danh sách API ở đây để tránh tình trạng có nhiều nguồn sự thật cho cùng một thông tin.

---

## 1. Tổng Quan Kiến Trúc Hệ Thống (Subsystems)

Hệ thống được chia thành 4 phân khu (Subsystems) độc lập nhưng liên kết chặt chẽ với nhau thông qua các Service và Repository:

| Subsystem | Vai trò | Tài liệu chi tiết |
| :--- | :--- | :--- |
| **KHO CORE** | Cung cấp các hàm nguyên thủy (Primitives) để quản lý không gian kho, khóa dòng chống tranh chấp và ghi nhận lịch sử giao dịch. | [`WORKFLOW_KHOCORE.md`](WORKFLOW_KHOCORE.md) |
| **NHẬP KHO (Inbound)** | Xử lý tiếp nhận hàng mới hoặc hàng Rework đạt chuẩn, kiểm tra trùng lặp và ghi nhận vào Kho Core / `STOCKTP`. | *(Sẽ tách riêng: `WORKFLOW_INBOUND.md`)* |
| **XUẤT KHO (Outbound)** | Quản lý luồng xuất hàng trực tiếp (Kho A0) hoặc xuất qua bảng trung gian chờ giao (`FVN_HangChoGiao`) cho các mục đích giao hàng, giao bù hoặc Rework. | *(Sẽ tách riêng: `WORKFLOW_OUTBOUND.md`)* |
| **XỬ LÝ HÀNG LỖI / REWORK (Exception & QTChung)** | Điều phối quy trình tiếp nhận phiếu bất thường từ khách hàng hoặc nội bộ, thực hiện QC định hướng, chạy lệnh Rework và phân tách OK/NG để nhập lại kho. | *(Sẽ tách riêng: `WORKFLOW_QTCHUNG.md`)* |

> **Nguyên tắc phụ thuộc:** NHẬP KHO, XUẤT KHO, và XỬ LÝ HÀNG LỖI đều gọi xuống KHO CORE qua interface (`ISlotService`, `IStockHistoryRepository`...) — tuyệt đối không module nghiệp vụ nào được tự viết SQL trực tiếp lên bảng `Slot`, `SlotLot`, hay `StockHistory`.

---

## 2. Các Luồng Nghiệp Vụ Chính (Main Flows)

### 2.1. Luồng Nhập Kho (Inbound Flow)
* **Quản lý lệnh:** Tiếp nhận lệnh sản xuất hoặc lệnh trả hàng.
* **Phân loại nguồn nhập:** Hàng mới từ sản xuất hoặc hàng Rework OK nhập lại.
* **Kiểm tra chống trùng:** Gọi `INhapTpReceivingService.KiemTraTruocKhiNhap` để quét mã QR/Barcode và check lịch sử `NHAP_TP_HIS`. Nếu trùng $\rightarrow$ Từ chối.
* **Phân định hình thức nhập:**
  * *Nhập hàng loạt:* Định vị tự động vào Kho ảo A0 (không cần chọn Slot).
  * *Nhập chi tiết:* Người dùng chọn cây không gian `Warehouse` $\rightarrow$ `Rack` $\rightarrow$ `Slot`.
* **Commit Transaction đồng thời:**
  * Kiểm tra/insert Case dedup (`IStockTpCaseRepository`).
  * Cộng dồn tồn kho tổng (`IStockTpRepository`).
  * Theo dõi tracking Slot-Lot (`IPhieuTrackingRepository`).
  * Cập nhật Kho Core — xem chi tiết API tại [`WORKFLOW_KHOCORE.md`](WORKFLOW_KHOCORE.md) mục *Primitive Slot*.

### 2.2. Luồng Kho Core (Core Service Provider)
Kho Core là tầng dịch vụ nền tảng, cung cấp 3 nhóm API nguyên thủy dùng chung cho mọi module khác: cấu hình không gian, thao tác Slot/SlotLot nguyên tử, và ghi lịch sử tồn kho tập trung.

> 📄 **Danh sách đầy đủ interface, tên method, và sơ đồ luồng nội bộ:** Xem tại [`WORKFLOW_KHOCORE.md`](WORKFLOW_KHOCORE.md). Không lặp lại danh sách API ở tài liệu này.

### 2.3. Luồng Xuất Kho (Outbound Flow)
* **Xác định vị trí nguồn & mục đích:**
  * *Hàng ở Kho A0 (giao thẳng):* Gọi `XuatTrucTiep` (Source = `KhoAoA0`), khóa Slot và trừ trực tiếp tồn kho tổng `STOCKTP` ngay lập tức.
  * *Hàng ở Slot thông thường (giao hàng / giao bù / rework):* Gọi `PickToChoGiao` để khóa Slot, bóc tách số lượng và **chưa** trừ `STOCKTP` ngay.
* **Đưa vào bảng chờ giao:** Dữ liệu được đẩy vào bảng trung gian `FVN_HangChoGiao` với trạng thái `ChoGiao`.
* **Xác nhận chốt xuất (Confirm):** Tùy theo mục đích để thực hiện trừ tồn kho và ghi lịch sử:
  * *Giao hàng (HVN-PGH):* `ConfirmGiaoHangTuChoGiao` (ActionType: `EXPORT`).
  * *Giao bù NG:* `XacNhanHoanTatGiaoBu` $\rightarrow$ gọi lại Confirm giao hàng (ActionType: `EXPORT_BU_NG`).
  * *Rework:* `XacNhanXuatRework` $\rightarrow$ ghi audit riêng vào `FVN_TraHangQTChung_Xuat` (ActionType: `REWORK_EXPORT`).

### 2.4. Luồng Xử Lý Hàng Lỗi / Rework & Trả Hàng (Exception & QTChung Flow)
* **Khởi tạo phiếu:** Tiếp nhận thông tin từ `IPhieuKhachTraRepository` qua `IKhachTraHangService` (khách) hoặc `ITraNoiBoService` (nội bộ) để tạo phiếu xử lý bất thường (`IQTChungService`).
* **QC Định Hướng (Gate quyết định):**
  * *Khách không lỗi thật:* Dừng quy trình / từ chối giao bù.
  * *Khách có lỗi thật (cần hàng):* Chạy quy trình giao bù qua `IGiaoBuNGService` và `IStockExportService`.
  * *Nội bộ / khách cần Rework:* Chuyển qua quy trình sửa chữa (`IReworkStockService.XuatKhoRework`).
* **Sửa chữa & kiểm tra cuối (QC Xác Nhận Cuối):**
  * Sản phẩm được đưa đi Rework tại xưởng và hoàn tất.
  * Thực hiện QC phân tách sản lượng OK và NG.
* **Nhập lại kho:**
  * *Phần OK:* Cộng lại lượng tồn qua Kho Core (`ISlotService.AddQuantity`) và `STOCKTP +`.
  * *Phần NG:* Route vào Slot hàng lỗi riêng biệt và ghi nhận bảng log `FVN_TraHangQTChung_NhapNG`.
* **Hoàn tất:** Kết thúc sự kiện `QTChungStatus.HoanTat`.

---

## 3. Sơ Đồ Tổng Thể Quy Trình (Mermaid Diagram)

```mermaid
graph TD
    %% 1. ĐẦU VÀO NHẬP KHO
    StartInbound([Yêu cầu Nhập Kho]) --> InboundProcess["INhapTpReceivingService<br/>- Check trùng QR / Lệnh SX"]
    InboundProcess --> CoreInbound["GỌI KHO CORE: Nhập dữ liệu<br/>(xem WORKFLOW_KHOCORE.md)"]

    %% 2. ĐẦU VÀO XUẤT KHO & REWORK
    StartOutbound([Yêu cầu Xuất / Rework / Bù NG]) --> OutboundProcess["IStockExportService / IQTChungService<br/>- Phân định A0 hay Slot thường"]
    OutboundProcess --> CoreOutbound["GỌI KHO CORE: Thao tác & trừ kho<br/>(xem WORKFLOW_KHOCORE.md)"]

    %% KHO CORE LÀ TRUNG TÂM (CORE LAYER)
    subgraph CoreLayer [KHO CORE — TRÁI TIM HỆ THỐNG]
        CoreCore["Cung cấp Primitive Services:<br/>- Warehouse / Rack / Slot ma trận<br/>- ISlotService (GetLots / SaveLots / ...)<br/>- IStockHistoryRepository.SaveHistory<br/><br/>Chi tiết: WORKFLOW_KHOCORE.md"]
    end

    %% LIÊN KẾT VỚI KHO CORE
    CoreInbound --> CoreCore
    CoreOutbound --> CoreCore

    %% COMMIT TRANSACTION CHUNG
    CoreCore --> CommitDB[(Commit Database Transaction)]
    CommitDB --> FinalEnd([Hoàn tất Tác vụ WMS])

    %% STYLING
    style CoreLayer fill:#e8f5e9,stroke:#388e3c,stroke-width:3px
    style InboundProcess fill:#e1f5fe,stroke:#0288d1,stroke-width:2px
    style OutboundProcess fill:#fff3e0,stroke:#f57c00,stroke-width:2px
    style CoreInbound fill:#c8e6c9,stroke:#388e3c,stroke-width:2px
    style CoreOutbound fill:#c8e6c9,stroke:#388e3c,stroke-width:2px
    style FinalEnd fill:#bfb,stroke:#333,stroke-width:2px