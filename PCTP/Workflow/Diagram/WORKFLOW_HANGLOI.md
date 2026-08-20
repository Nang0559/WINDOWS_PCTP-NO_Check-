# Tài Liệu Quy Trình Nghiệp Vụ: Xử Lý Hàng Lỗi / QTChung (Exception & Rework Workflow)

Tài liệu này mô tả chi tiết luồng xử lý các phiếu bất thường từ khách hàng hoặc nội bộ, thực hiện QC định hướng, điều phối qua bảng chờ giao, tiến hành sửa chữa (Rework) tại xưởng và phân tách sản lượng OK/NG để nhập lại kho.

---

## 1. Mô Tả Chi Tiết Các Bước Trong Luồng Xử Lý Hàng Lỗi

1. **Khởi Tạo & Tiếp Nhận Ban Đầu:**
   * Tiếp nhận thông tin từ `IPhieuKhachTraRepository` thông qua `IKhachTraHangService` (Nguồn: Khách hàng) hoặc `ITraNoiBoService` (Nguồn: Nội bộ).
   * Gọi `IQTChungService.TaoPhieuXuLyBatThuong` để khởi tạo phiếu với trạng thái ban đầu (`Moi` $\rightarrow$ `DaTaoPhieuBatThuong`).
   * **Nhánh 1c (mới) — Tạo trực tiếp từ Slot, nguồn Nội Bộ, không qua chứng từ khách trả:**
     `FormChonSlotNoiBo` đọc danh sách Slot/LOT đang tồn qua `ISlotService.GetAllActiveSlotLots()`
     (Kho Core), sau đó gọi `IPhieuLoiRepository.InsertPhieuXuLyBatThuongNoiBo` để tạo phiếu với
     `Nguon = NguonPhieuBatThuong.NoiBo`, `TrangThai = ChoQC` — rơi thẳng vào bước 2 (QC Định Hướng),
     dùng chung toàn bộ luồng phía sau với phiếu sinh từ khách trả.
2. **QC Định Hướng (Gate Quyết Định):**
   * Thực hiện qua `IQTChungService.QCDinhHuongRework` để chuyển trạng thái sang `DaDinhHuongRework` và phân tách thành 3 nhánh xử lý chính:
   * **Nhánh 1 (Khách không lỗi thật):** Dừng quy trình, từ chối giao bù ($\rightarrow$ `END`).
   * **Nhánh 2 (Khách có lỗi thật, cần giao bù):** Gọi `IGiaoBuNGService.GiaoBuTheoQR` $\rightarrow$ `IStockExportService.PickToChoGiao` (Loại: `GiaoBuNG`), sau đó xác nhận hoàn tất qua `IGiaoBuNGService.XacNhanHoanTatGiaoBu` để trừ tồn kho.
   * **Nhánh 3 (Nội bộ / Khách cần Rework):** Chuyển sang luồng xuất kho Rework.
3. **Xuất Kho Rework & Giao Sản Xuất:**
   * Gọi `IQTChungService.XuatKhoRework` $\rightarrow$ `IReworkStockService.XuatKhoRework` $\rightarrow$ `IStockExportService.PickToChoGiao` (Loại: `Rework`).
   * Thực hiện xác nhận xuất qua `IReworkStockService.XacNhanXuatRework` kết hợp `ConfirmGiaoHangTuChoGiao` và ghi log vào `ITraHangQTChungRepository.InsertXuat` (Trạng thái: `DaXuatKhoRework`).
   * Ghi nhận giao sản xuất qua `IQTChungService.GhiNhanGiaoSanXuat` (`ITraHangQTChungRepository.InsertGiao`, không dùng Slot/STOCKTP, trạng thái: `DaGiaoSanXuat`).
4. **Tiến Hành Rework & QC Xác Nhận Cuối:**
   * Sản phẩm được tiến hành sửa chữa tại xưởng (mốc trạng thái ngoài hệ thống).
   * Sau khi hoàn tất, thực hiện `IQTChungService.QCXacNhanCuoi` qua `ITraHangQTChungRepository.InsertQC` để ghi nhận số lượng OK/NG (Trạng thái: `DaQCXacNhanCuoi`).
  **4b. Kiểm Tra Tem khi Nhập Lại sau Rework:**
  - Sau QC xác nhận cuối (`QCXacNhanCuoi`)
  - Nếu mã hàng có `NeedsInspection = true`
  → chạy `FormInspection` cho phần hàng OK trước khi `NhapLaiHangNG`
5. **Nhập Lại Kho & Hoàn Tất:**
   * *Trường hợp sản phẩm đạt chuẩn hoàn toàn (Số lượng NG = 0):* Chuyển thẳng trạng thái `HoanTat`.
   * *Trường hợp phát sinh phế phẩm (Số lượng NG > 0):* Gọi `IReworkStockService.NhapLaiHangNG` để:
     * Phần **OK**: Cộng lại lượng tồn (`ISlotService.AddQuantity` tại Kho Core và tăng tồn kho tổng `STOCKTP +`).
     * Phần **NG**: Định tuyến vào Slot hàng lỗi riêng biệt.
     * Ghi nhận audit vào `ITraHangQTChungRepository.InsertNhapNG`.
     * Chuyển trạng thái sang `HoanTat`.
   * Kết thúc toàn bộ quy trình sự kiện QTChung.

---

## 2. Sơ Đồ Quy Trình Xử Lý Hàng Lỗi / QTChung (Mermaid Diagram)

```mermaid
graph TD
    %% KHỞI TẠO & TIẾP NHẬN BAN ĐẦU
    StartRepo[IPhieuKhachTraRepository] --> B1[IKhachTraHangService<br/>Nguồn: Khách Hàng]
    StartRepo --> B2[ITraNoiBoService<br/>Nguồn: Nội Bộ]
    
    B1 --> Step1["IQTChungService.TaoPhieuXuLyBatThuong<br/>Status: Moi -> DaTaoPhieuBatThuong"]
    B2 --> Step1
    
    Step1 --> Step2["IQTChungService.QCDinhHuongRework<br/>(gate quyết định)<br/>Status: DaDinhHuongRework"]
    
    %% PHÂN NHÁNH 1: KHÁCH KHÔNG LỖI THẬT
    Step2 -->|Khách: Không lỗi thật| EndNoErr[END — Từ chối giao bù]

    %% PHÂN NHÁNH 2: KHÁCH CÓ LỖI THẬT (GIAO BÙ)
    Step2 -->|Khách: Có lỗi thật, chỉ cần giao bù| GiaoBu1["IGiaoBuNGService.GiaoBuTheoQR<br/>-> IStockExportService.PickToChoGiao<br/>(Loại = GiaoBuNG)"]
    
    GiaoBu1 --> GiaoBu2["IGiaoBuNGService.XacNhanHoanTatGiaoBu<br/>-> ConfirmGiaoHangTuChoGiao"]
    GiaoBu2 --> EndGiaoBu([END])

    %% PHÂN NHÁNH 3: NỘI BỘ / KHÁCH CẦN REWORK
    Step2 -->|Nội bộ / Khách cần Rework| Rework1["IQTChungService.XuatKhoRework<br/>-> IReworkStockService.XuatKhoRework<br/>-> IStockExportService.PickToChoGiao<br/>(Loại = Rework)<br/>Status: DaXuatKhoRework"]
    
    Rework1 --> Rework2["IReworkStockService.XacNhanXuatRework<br/>-> ConfirmGiaoHangTuChoGiao<br/>+ ITraHangQTChungRepository.InsertXuat"]
    
    Rework2 --> Step5["IQTChungService.GhiNhanGiaoSanXuat<br/>ITraHangQTChungRepository.InsertGiao<br/>(KHÔNG dùng Slot/STOCKTP)<br/>Status: DaGiaoSanXuat"]
    
    Step5 --> Step6[Rework tại xưởng<br/>mốc trạng thái ngoài hệ thống]
    
    Step6 --> Step7["IQTChungService.QCXacNhanCuoi<br/>ITraHangQTChungRepository.InsertQC<br/>(SoLuongOK / SoLuongNG)<br/>Status: DaQCXacNhanCuoi"]
    
    %% Phân tách sau QC cuối
    Step7 -->|SoLuongNG = 0| StatusHoanTat1[Status: HoanTat]
    Step7 -->|SoLuongNG > 0| Step8["IReworkStockService.NhapLaiHangNG<br/>- OK: ISlotService.AddQuantity + STOCKTP +<br/>- NG: Route Slot NG riêng<br/>- ITraHangQTChungRepository.InsertNhapNG"]
    
    Step8 --> StatusHoanTat2[Status: HoanTat]

    %% KẾT THÚC CHUNG
    StatusHoanTat1 --> FinalEnd([🏁 KẾT THÚC])
    StatusHoanTat2 --> FinalEnd
    EndNoErr --> FinalEnd

    %% STYLING
    style StartRepo fill:#f9f,stroke:#333,stroke-width:2px
    style Step2 fill:#bbf,stroke:#333,stroke-width:2px
    style GiaoBu1 fill:#bfb,stroke:#333,stroke-width:2px
    style Rework1 fill:#fbb,stroke:#333,stroke-width:2px
    style FinalEnd fill:#fbb,stroke:#333,stroke-width:2px
