# Tài Liệu Kiến Trúc & Luồng Vận Hành Hệ Thống Kho (WMS / Production Management)

Tài liệu này mô tả tổng quan bức tranh kiến trúc và các luồng nghiệp vụ chính (Main Flow) của hệ thống quản lý kho, sản xuất và xử lý hàng lỗi.

---

## 1. Tổng Quan Kiến Trúc Hệ Thống (Subsystems)

Hệ thống được chia thành 4 phân khu (Subsystems) độc lập nhưng liên kết chặt chẽ với nhau thông qua các Service và Repository:

*   **KHO CORE (Warehouse / Rack / Slot / SlotLot / StockHistory):** Cung cấp các hàm nguyên thủy (Primitives) để quản lý không gian kho, khóa dòng chống tranh chấp và ghi nhận lịch sử giao dịch.
*   **NHẬP KHO (Inbound):** Xử lý tiếp nhận hàng mới hoặc hàng Rework đạt chuẩn, kiểm tra trùng lặp và ghi nhận vào Kho Core / `STOCKTP`.
*   **XUẤT KHO (Outbound):** Quản lý luồng xuất hàng trực tiếp (Kho A0) hoặc xuất qua bảng trung gian chờ giao (`FVN_HangChoGiao`) cho các mục đích giao hàng, giao bù hoặc Rework.
*   **XỬ LÝ HÀNG LỖI / REWORK (Exception & QTChung):** Điều phối quy trình tiếp nhận phiếu bất thường từ khách hàng hoặc nội bộ, thực hiện QC định hướng, chạy lệnh Rework và phân tách OK/NG để nhập lại kho.

---

## 2. Các Luồng Nghiệp Vụ Chính (Main Flows)

### 2.1. Luồng Nhập Kho (Inbound Flow)
1. **Quản lý lệnh:** Tiếp nhận lệnh sản xuất hoặc lệnh trả hàng.
2. **Phân loại nguồn nhập:** Hàng mới từ sản xuất hoặc Rework OK nhập lại.
3. **Kiểm tra chống trùng:** Gọi `INhapTpReceivingService.KiemTraTruocKhiNhap` để quét mã QR/Barcode và check lịch sử `NHAP_TP_HIS`. Nếu trùng $\rightarrow$ Từ chối.
4. **Phân định hình thức nhập:**
   * **Nhập hàng loạt:** Định vị tự động vào Kho ảo A0 (không cần chọn Slot).
   * **Nhập chi tiết:** Người dùng chọn cây không gian `Warehouse -> Rack -> Slot`.
5. **Commit Transaction Đồng thời:** 
   * Kiểm tra/insert Case dedup (`IStockTpCaseRepository`).
   * Cộng dồn tồn kho tổng (`IStockTpRepository`).
   * Theo dõi tracking Slot-Lot (`IPhieuTrackingRepository`).
   * Cập nhật Kho Core (`ISlotService.SaveLots + AddQuantity`).

### 2.2. Luồng Kho Core (Core Service Provider)
Kho Core đóng vai trò là tầng dịch vụ nền tảng cung cấp các API cốt lõi cho các module khác:
*   **Cấu hình không gian:** Khởi tạo và cập nhật ma trận không gian qua `IRackService.UpdateLayout`.
*   **Thao tác Primitive Slot:** Cung cấp các hàm `GetLots`, `SaveLots`, `LockSlotForUpdate`, `UpdateSlotHeaderFromLots`, `FindSlotsContainingLot`, và `GetOrCreateVirtualSlotText`.
*   **Ghi lịch sử:** Tập trung ghi nhật ký qua `IStockHistoryRepository.SaveHistory`.

### 2.3. Luồng Xuất Kho (Outbound Flow)
1. **Xác định vị trí nguồn & Mục đích:**
   * **Hàng ở Kho A0 (Giao thẳng):** Gọi `XuatTrucTiep` (Source = KhoAoA0), khóa Slot và trừ trực tiếp tồn kho tổng `STOCKTP` ngay lập tức.
   * **Hàng ở Slot thông thường (Giao hàng / Giao bù / Rework):** Gọi `PickToChoGiao` để khóa Slot, bóc tách số lượng và **chưa trừ STOCKTP ngay**.
2. **Đưa vào bảng chờ giao:** Dữ liệu được đẩy vào bảng trung gian `FVN_HangChoGiao` với trạng thái `ChoGiao`.
3. **Xác nhận chốt xuất (Confirm):** Tùy theo mục đích để thực hiện trừ tồn kho và ghi lịch sử:
   * *Giao hàng (HVN-PGH):* `ConfirmGiaoHangTuChoGiao` (`ActionType: EXPORT`).
   * *Giao bù NG:* `XacNhanHoanTatGiaoBu` $\rightarrow$ gọi lại Confirm giao hàng (`ActionType: EXPORT_BU_NG`).
   * *Rework:* `XacNhanXuatRework` $\rightarrow$ ghi audit riêng vào `FVN_TraHangQTChung_Xuat` (`ActionType: REWORK_EXPORT`).

### 2.4. Luồng Xử Lý Hàng Lỗi / Rework & Trả Hàng (Exception & QTChung Flow)
1. **Khởi tạo phiếu:** Tiếp nhận thông tin từ `IPhieuKhachTraRepository` qua `IKhachTraHangService` (Khách) hoặc `ITraNoiBoService` (Nội bộ) để tạo phiếu xử lý bất thường (`IQTChungService`).
2. **QC Định Hướng (Gate Quyết định):**
   * *Khách không lỗi thật:* Dừng quy trình / Từ chối giao bù.
   * *Khách có lỗi thật (Cần hàng):* Chạy quy trình giao bù qua `IGiaoBuNGService` và `IStockExportService`.
   * *Nội bộ / Khách cần Rework:* Chuyển qua quy trình sửa chữa (`IReworkStockService.XuatKhoRework`).
3. **Sửa chữa & Kiểm tra cuối (QC Xác Nhận Cuối):**
   * Sản phẩm được đưa đi Rework tại xưởng và hoàn tất.
   * Thực hiện QC phân tách sản lượng OK và NG.
4. **Nhập lại kho:**
   * *Phần OK:* Cộng lại lượng tồn (`SlotService.AddQuantity` và `STOCKTP +`).
   * *Phần NG:* Route vào Slot hàng lỗi riêng biệt và ghi nhận bảng log `FVN_TraHangQTChung_NhapNG`.
5. **Hoàn tất:** Kết thúc sự kiện `QTChungStatus.HoanTat`.

---

## 3. Sơ Đồ Tổng Thể Quy Trình (Mermaid Diagram)


```mermaid
graph TD
    %% 1. ĐẦU VÀO NHẬP KHO
    StartInbound([Yêu cầu Nhập Kho]) --> InboundProcess["INhapTpReceivingService<br/>- Check trùng QR / Lệnh SX"]
    InboundProcess --> CoreInbound["GỌI KHO CORE: Nhập dữ liệu<br/>- ISlotService.GetOrCreateVirtualSlotText / GetEmptySlots<br/>- ISlotService.LockSlotForUpdate<br/>- ISlotService.SaveLots + AddQuantity<br/>- IStockTpRepository (Cộng STOCKTP)"]

    %% 2. ĐẦU VÀO XUẤT KHO & REWORK
    StartOutbound([Yêu cầu Xuất / Rework / Bù NG]) --> OutboundProcess["IStockExportService / QTChungService<br/>- Phân định A0 hay Slot thường"]
    OutboundProcess --> CoreOutbound["GỌI KHO CORE: Thao tác & Trừ kho<br/>- Xuất thẳng A0: Trừ trực tiếp STOCKTP<br/>- Pick Slot thường: LockSlotForUpdate<br/>- Bóc tách Lot & Đẩy vào FVN_HangChoGiao"]

    %% KHO CORE LÀ TRUNG TÂM (CORE LAYER)
    subgraph CoreLayer [KHO CORE — TRÁI TIM HỆ THỐNG]
        CoreCore["Cung cấp Primitive Services:<br/>- Warehouse / Rack / Slot ma trận<br/>- GetLots / SaveLots<br/>- UpdateSlotHeaderFromLots<br/>- IStockHistoryRepository.SaveHistory"]
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
