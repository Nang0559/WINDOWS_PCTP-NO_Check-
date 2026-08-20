# Tài Liệu Quy Trình Nghiệp Vụ: Xử Lý Hàng Lỗi / QTChung (Exception & Rework Workflow)

Tài liệu này mô tả chi tiết luồng xử lý các phiếu bất thường từ khách hàng hoặc nội bộ, thực hiện QC định hướng, điều phối qua bảng chờ giao, tiến hành sửa chữa (Rework) tại xưởng và phân tách sản lượng OK/NG để nhập lại kho.

---

## 1. Mô Tả Chi Tiết Các Bước Trong Luồng Xử Lý Hàng Lỗi

1. **Khởi Tạo & Tiếp Nhận Ban Đầu:**
     - Nguồn Khách hàng:
  - Tiếp nhận thông tin từ `IPhieuKhachTraRepository` thông qua `IKhachTraHangService`.
  - Gọi `IQTChungService.TaoPhieuXuLyBatThuong`.
  - Trạng thái: `Moi` → `DaTaoPhieuBatThuong`.

- Nguồn Nội bộ:
  - Tiếp nhận thông tin thông qua `ITraNoiBoService`.
  - Gọi `IQTChungService.TaoPhieuXuLyBatThuong`.

- **Nhánh tạo trực tiếp từ Slot — Nội bộ:**
  - Người dùng chọn Slot/LOT đang tồn trên `FormChonSlotNoiBo`.
  - Hệ thống tạo phiếu qua `IPhieuLoiRepository.InsertPhieuXuLyBatThuongNoiBo`.
  - Phiếu được tạo với:
    - `Nguon = NguonPhieuBatThuong.NoiBo`
    - `TrangThai = ChoQC`
  - Phiếu bỏ qua bước tạo từ chứng từ khách trả và đi thẳng vào **QC Định Hướng**.
  - Từ đây sử dụng chung toàn bộ workflow QTChung phía sau.

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
    %% KHOI TAO VA TIEP NHAN
    StartRepo["IPhieuKhachTraRepository"] --> B1["IKhachTraHangService<br/>Nguon: Khach Hang"]
    StartRepo --> B2["ITraNoiBoService<br/>Nguon: Noi Bo"]

    B1 --> Step1["IQTChungService.TaoPhieuXuLyBatThuong<br/>Status: Moi -> DaTaoPhieuBatThuong"]
    B2 --> Step1

    %% NHANH 1c - TAO TRUC TIEP TU SLOT NOI BO
    SlotForm["FormChonSlotNoiBo<br/>Tao phieu truc tiep tu Slot LOT"]

    SlotService["ISlotService<br/>Kho Core"]

    SlotForm -->|Doc Slot LOT| SlotService
    SlotService -->|Tra danh sach ton| SlotForm

    SlotForm -->|Tao phieu Noi Bo| Step2

    %% GHI CHU DOC DU LIEU
    SlotReadNote["READ ONLY<br/>Chi doc de chon Slot LOT<br/>Khong ghi hoac tru ton"]

    SlotForm -.-> SlotReadNote
    SlotReadNote -.-> SlotService

    %% QC DINH HUONG
    Step1 --> Step2["IQTChungService.QCDinhHuongRework<br/>Gate quyet dinh<br/>Status: DaDinhHuongRework"]

    %% NHANH 1 - KHACH KHONG LOI THAT
    Step2 -->|Khach khong loi that| EndNoErr["END<br/>Tu choi giao bu"]

    %% NHANH 2 - GIAO BU
    Step2 -->|Khach loi that chi can giao bu| GiaoBu1["IGiaoBuNGService.GiaoBuTheoQR<br/>IStockExportService.PickToChoGiao<br/>Loai: GiaoBuNG"]

    GiaoBu1 --> GiaoBu2["IGiaoBuNGService.XacNhanHoanTatGiaoBu<br/>ConfirmGiaoHangTuChoGiao"]

    GiaoBu2 --> EndGiaoBu["END"]

    %% NHANH 3 - REWORK
    Step2 -->|Noi bo hoac Khach can Rework| Rework1["IQTChungService.XuatKhoRework<br/>IReworkStockService.XuatKhoRework<br/>IStockExportService.PickToChoGiao<br/>Loai: Rework<br/>Status: DaXuatKhoRework"]

    Rework1 --> Rework2["IReworkStockService.XacNhanXuatRework<br/>ConfirmGiaoHangTuChoGiao<br/>InsertXuat"]

    Rework2 --> Step5["IQTChungService.GhiNhanGiaoSanXuat<br/>InsertGiao<br/>Khong dung Slot STPCKTP<br/>Status: DaGiaoSanXuat"]

    %% REWORK TAI XUONG
    Step5 --> Step6["Rework tai xuong<br/>Moc trang thai ngoai he thong"]

    %% QC CUOI
    Step6 --> Step7["IQTChungService.QCXacNhanCuoi<br/>InsertQC<br/>SoLuongOK va SoLuongNG<br/>Status: DaQCXacNhanCuoi"]

    %% KIEM TRA TEM
    Step7 -->|NeedsInspection true| Inspection["FormInspection<br/>Kiem tra tem phan hang OK"]
    Step7 -->|NeedsInspection false| QtyCheck["Kiem tra SoLuongNG"]

    Inspection --> QtyCheck

    %% PHAN TACH OK NG
    QtyCheck -->|SoLuongNG bang 0| StatusHoanTat1["Status: HoanTat"]

    QtyCheck -->|SoLuongNG lon hon 0| Step8["IReworkStockService.NhapLaiHangNG<br/>OK: ISlotService.AddQuantity<br/>OK: STOCKTP tang<br/>NG: Route vao Slot NG<br/>InsertNhapNG"]

    Step8 --> StatusHoanTat2["Status: HoanTat"]

    %% KET THUC
    StatusHoanTat1 --> FinalEnd["KET THUC"]
    StatusHoanTat2 --> FinalEnd
    EndNoErr --> FinalEnd
    EndGiaoBu --> FinalEnd

    %% STYLE
    style StartRepo fill:#f9f,stroke:#333,stroke-width:2px
    style Step2 fill:#bbf,stroke:#333,stroke-width:2px
    style GiaoBu1 fill:#bfb,stroke:#333,stroke-width:2px
    style Rework1 fill:#fbb,stroke:#333,stroke-width:2px
    style SlotForm fill:#ffeb99,stroke:#333,stroke-width:2px
    style SlotService fill:#d9ead3,stroke:#333,stroke-width:2px
    style SlotReadNote fill:#fff2cc,stroke:#333,stroke-width:1px
    style FinalEnd fill:#fbb,stroke:#333,stroke-width:2px
