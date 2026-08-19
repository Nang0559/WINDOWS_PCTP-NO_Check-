# Tài Liệu Quy Trình Nghiệp Vụ: Xuất Kho & Xử Lý Hàng Lỗi (Outbound & Rework Flow)

Tài liệu này mô tả chi tiết luồng vận hành xuất kho, phân định rõ ràng giữa xuất trực tiếp (Kho A0) và cơ chế 2 pha qua bảng trung gian chờ giao (`FVN_HangChoGiao`) cho các mục đích Giao hàng, Giao bù NG, và Rework.

---

## 1. Mô Tả Chi Tiết Các Nhánh Nghiệp Vụ Xuất Kho

1. **Xác Định Vị Trí & Mục Đích Xuất:** Hệ thống tiếp nhận yêu cầu xuất kho và phân loại nguồn hàng cần xử lý.
2. **Nhánh 1: Hàng Ở Kho A0 (Xuất Trực Tiếp):**
   * Mở form HVN-PGH, gọi `IStockExportService.XuatTrucTiep` với nguồn từ Bulk (không qua bảng chờ giao).
   * Trừ tồn kho tổng `STOCKTP` ngay lập tức (`SLXUAT` tăng, `SLCONLAI` giảm).
3. **Nhánh 2: Khởi Tạo Từ Phiếu Bất Thường (Exception & QTChung):**
   * Tiếp nhận thông tin từ `IPhieuKhachTraRepository` thông qua `ITraNoiBoService` (Nội bộ) hoặc `IKhachTraHangService` (Khách hàng).
   * Chuyển đến `IQTChungService` để tiếp nhận, tạo phiếu bất thường và thực hiện **QC Định Hướng**.
4. **Nhánh 3: Hàng Ở Slot (Giao Hàng / Giao Bù / Rework qua Bảng Chờ Giao):**
   * Thực hiện click Slot trên giao diện `MainStockSV`, gọi `IStockExportService.PickToChoGiao` với mục đích tương ứng (`GiaoHang`, `GiaoBuNG`, hoặc `XuatRework`). **Lưu ý: Chưa trừ tồn kho `STOCKTP` ở pha này.**
   * Dữ liệu được đẩy vào bảng trung gian `FVN_HangChoGiao` với trạng thái `ChoGiao`.
   * **Pha Xác Nhận (Confirm):** Tùy thuộc vào loại mục đích để thực hiện hành động chốt xuất:
     * *Giao hàng:* Gọi `ConfirmGiaoHangTuChoGiao` $\rightarrow$ Trừ tồn kho tổng.
     * *Giao bù NG:* Gọi `IGiaoBuNGService.XacNhanHoanTatGiaoBu` $\rightarrow$ Gọi lại Confirm trừ tồn kho.
     * *Rework:* Gọi `IReworkStockService.XacNhanXuatRework` $\rightarrow$ Ghi log audit vào bảng `FVN_TraHangQTChung_Xuat`.
5. **Nhánh 4: Tiến Hành Rework & Nhập Lại Kho:**
   * Sau khi xuất Rework hoàn tất, hàng được đưa đi sửa chữa tại xưởng.
   * Mở form `frm_NhapLaiNG` để QC xác nhận, phân tách sản lượng **OK** và **NG**.
   * Gọi `IReworkStockService.NhapLaiNG`:
     * *Phần OK:* Cộng lại lượng tồn `SLCONLAI` và dịch chuyển Slot.
     * *Phần NG:* Định tuyến vào Slot hàng lỗi riêng biệt.
     * Ghi nhận log vào bảng `FVN_TraHangQTChung_NhapNG`.
   * Kết thúc sự kiện hoàn tất QTChung (`QTC-HungHoanTatEvent`).

---

## 2. Sơ Đồ Quy Trình Xuất Kho (Mermaid Diagram)

```mermaid
graph TD
    Start([BẮT ĐẦU XUẤT KHO]) --> CheckSource{Xác định vị trí & mục đích}

    %% NHÁNH 1: HÀNG KHO A0
    CheckSource -->|Hàng Kho A0| DirectA0["IStockExportService.XuatTrucTiep<br/>(Source = Bulk, không qua chờ giao)"]
    DirectA0 --> ExportA0["Trừ STOCKTP ngay<br/>(SLXUAT +, SLCONLAI -)"]
    ExportA0 --> SaveHistA0[SaveHistory: ActionType = EXPORT]

    %% NHÁNH 2: KHỞI TẠO TỪ PHIẾU KHÁCH TRẢ / NỘI BỘ
    CheckSource -->|Khởi tạo qua phiếu| RepoSource[IPhieuKhachTraRepository]
    RepoSource --> ServiceInternal[ITraNoiBoService<br/>Nguồn: Nội Bộ]
    RepoSource --> ServiceCustomer[IKhachTraHangService<br/>Nguồn: Khách Hàng]
    ServiceInternal --> StepC[IQTChungService: Tạo Phiếu Bất Thường]
    ServiceCustomer --> StepC
    StepC --> StepD[QC Định Hướng]

    %% NHÁNH 3: HÀNG KHO THƯỜNG (DÙNG CHUNG PHA PICK CHO GIAO)
    CheckSource -->|Hàng Slot thường| MainStock["IStockExportService.PickToChoGiao<br/>(Purpose = GiaoHang / GiaoBuNG / XuatRework)<br/>- CHƯA trừ STOCKTP"]
    MainStock --> LockSlot["SlotService.LockSlotForUpdate<br/>(Kho Core)"]
    LockSlot --> SaveHistoryPick[SaveHistory: ActionType = CHO_GIAO]
    SaveHistoryPick --> TableChoGiao[(FVN_HangChoGiao<br/>Trạng Thái: ChoGiao)]

    %% PHÂN NHÓM XÁC NHẬN (CONFIRM) TỪ BẢNG CHỜ GIAO
    TableChoGiao --> TypeConfirm{Loại Chờ Giao}

    TypeConfirm -->|Giao hàng| ConfGiaoHang["IStockExportService.Confirm<br/>GiaoHangTuChoGiao"]
    ConfGiaoHang --> TrừStock1["Trừ STOCKTP<br/>ActionType = EXPORT"]

    TypeConfirm -->|Giao bù NG| ConfGiaoBu["IGiaoBuNGService.XacNhan<br/>HoanTatGiaoBu"]
    ConfGiaoBu --> TrừStock2["Trừ STOCKTP<br/>ActionType = EXPORT_BU_NG"]

    TypeConfirm -->|Rework| ConfRework["IReworkStockService.XacNhan<br/>XuatRework"]
    ConfRework --> InsertReworkLog["ITraHangQTChungRepository.InsertXuat<br/>ActionType = REWORK_EXPORT"]

    TypeConfirm -->|Hủy lệnh chờ giao| CancelChoGiao["IStockExportService.HuyChoGiao<br/>Trả lại tồn Slot / Xóa bản ghi"]

    %% XỬ LÝ KẾT QUẢ QC ĐỊNH HƯỚNG & REWORK
    StepD -->|Khách: Không lỗi| EndNoErr[END — Từ chối giao bù]
    StepD -->|Khách: Có lỗi thật| StepGiaoBu["IGiaoBuNGService.GiaoBuTheoQR<br/>-> Đẩy vào luồng Chờ Giao (GiaoBuNG)"]
    StepD -->|Nội bộ / Rework| StepReworkAction["Đẩy vào luồng Chờ Giao (XuatRework)"]

    %% TIẾN HÀNH REWORK & NHẬP LẠI KHO
    InsertReworkLog --> DoRework[Tiến hành Rework tại xưởng]
    DoRework --> ReworkDone[Rework hoàn tất]
    ReworkDone --> FormNhapLai["frm_NhapLaiNG<br/>QC xác nhận phân tách OK / NG"]
    FormNhapLai --> ImportAction["IReworkStockService.NhapLaiNG<br/>- OK: Cộng lại SLCONLAI + Slot Lot<br/>- NG: Route vào Slot NG riêng<br/>- Ghi FVN_TraHangQTChung_NhapNG"]
    ImportAction --> EventEnd[QTChung hoàn tất]

    %% HỘI TỤ KẾT THÚC CHUNG
    SaveHistA0 --> End([🏁 HOÀN TẤT])
    TrừStock1 --> End
    TrừStock2 --> End
    InsertReworkLog --> End
    CancelChoGiao --> End
    EndNoErr --> End
    EventEnd --> End

    %% STYLING
    style Start fill:#f9f,stroke:#333,stroke-width:2px
    style CheckSource fill:#ff9,stroke:#333,stroke-width:2px
    style DirectA0 fill:#bbf,stroke:#333,stroke-width:2px
    style MainStock fill:#bbf,stroke:#333,stroke-width:2px
    style TableChoGiao fill:#ffccbc,stroke:#333,stroke-width:2px
    style ConfRework fill:#fbb,stroke:#333,stroke-width:2px
    style FormNhapLai fill:#bfb,stroke:#333,stroke-width:2px
    style End fill:#bfb,stroke:#333,stroke-width:2px