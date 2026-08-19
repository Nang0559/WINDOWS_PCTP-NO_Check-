# Tài Liệu Quy Trình Nghiệp Vụ: Nhập Kho (Inbound Flow)

Tài liệu này mô tả chi tiết luồng vận hành và kiến trúc xử lý của module **Nhập Kho**, đảm bảo tính toàn vẹn dữ liệu thông qua cơ chế Transaction duy nhất và kiểm tra chống trùng lặp mã QR/Case.

---

## 1. Mô Tả Chi Tiết Các Bước Trong Luồng Nhập Kho

1. **Quản lý Lệnh Nhập:** 
   * Tiếp nhận thông tin từ lệnh sản xuất hoặc lệnh trả hàng để chuẩn bị cho quá trình nhập kho.
2. **Phân Loại Nguồn Nhập:**
   * **Hàng mới từ sản xuất:** Đi trực tiếp vào chuỗi kiểm tra.
   * **Rework OK nhập lại:** Thuộc luồng xử lý hàng lỗi (chuyển hướng từ mục xử lý hàng lỗi qua `NhapLaiHangNG`).
3. **Kiểm Tra Trùng Lặp (Validation Service):**
   * Sử dụng `INhapTpReceivingService.KiemTraTruocKhiNhap` để kiểm tra xem mã QR đã được nhập chưa và kiểm tra trường hợp trùng Case dựa trên lịch sử `NHAP_TP_HIS`.
   * *Nếu trùng:* Trả về kết quả `ScanResult.Trung` $\rightarrow$ Từ chối giao dịch, không thực hiện transaction.
   * *Nếu hợp lệ:* Chuyển sang bước phân định hình thức nhập.
4. **Phân Định Hình Thức Nhập:**
   * **Nhập hàng loạt (Bulk Mode):** Sử dụng `ISlotService.GetOrCreateVirtualSlotText` để định vị vào Kho Áo A0 mà không cần chọn Slot thủ công.
   * **Nhập chi tiết (Detailed Mode):** Người dùng thực hiện chọn không gian theo thứ tự `Warehouse` $\rightarrow$ `Rack` $\rightarrow$ chọn 1 Slot cụ thể thông qua `ISlotService.GetEmptySlots`.
5. **Thực Hiện Nhập Vào Slot (Unit of Work):**
   * Tất cả các luồng sau khi định vị vị trí đều gọi chung `INhapTpReceivingService.NhapTpVaoSlot` chạy trong **một `IUnitOfWork` duy nhất**.
6. **Bốn Nhánh Transaction Song Song:**
   * *Nhánh 1:* `IStockTpCaseRepository` — Kiểm tra và insert Case dedup.
   * *Nhánh 2:* `IStockTpRepository` — Insert hoặc update cộng dồn tồn kho tổng `STOCKTP`.
   * *Nhánh 3:* `IPhieuTrackingRepository` — Insert phiếu mới theo dõi Slot-Lot tracking.
   * *Nhánh 4:* `ISlotService` — Lưu chi tiết `SaveLots` và cộng số lượng `AddQuantity` vào Kho Core.
7. **Commit Transaction & Ghi Lịch Sử:**
   * Sau khi commit thành công vào Database, hệ thống thực hiện gọi `SaveHistory` với `ActionType` tương ứng (ví dụ: `IMPORT`, `BULK_IMPORT`, hoặc `NHAP_LAI_SAU_REWORK`).
   * Kết thúc quy trình nhập kho.

---

## 2. Sơ Đồ Quy Trình Nhập Kho (Mermaid Diagram)

```mermaid
graph TD
    Start([BẮT ĐẦU NHẬP KHO]) --> ManageOrder[Quản lý lệnh nhập<br/>Lệnh sản xuất / Lệnh trả hàng]
    
    ManageOrder --> SelectType{Phân loại nguồn nhập}

    %% CÁC NHÁNH NGUỒN NHẬP
    SelectType -->|1. Hàng mới từ SX| NewGoods[Hàng mới]
    SelectType -->|2. Rework OK nhập lại| ReworkOK["Thuộc luồng Xử lý Hàng Lỗi<br/>— xem mục 4.4<br/>NhapLaiHangNG"]

    %% SERVICE KIỂM TRA TRÙNG TRƯỚC KHI NHẬP
    NewGoods --> CheckService["INhapTpReceivingService.Ki<br/>emTraTruocKhiNhap<br/>- Check QR đã nhập chưa<br/>- Check Case trùng<br/>(NHAP_TP_HIS)"]
    ReworkOK --> CheckService

    CheckService -->|Trùng| ScanTrung["ScanResult.Trung<br/>— Từ chối, không<br/>transaction"]

    %% HÌNH THỨC NHẬP KHI HỢP LỆ
    CheckService -->|Hợp lệ| FormMode{Hình thức nhập}

    %% Nhập hàng loạt (Kho A0)
    FormMode -->|Nhập hàng loạt| BulkMode["ISlotService.GetOrCreateVirt<br/>ualSlotText<br/>(Kho Áo A0, không chọn<br/>Slot)"]

    %% Nhập chi tiết (Chọn Warehouse -> Rack -> Slot)
    FormMode -->|Nhập chi tiết| LocationBlock

    subgraph LocationBlock [Định vị chi tiết]
        SelectWH[Chọn Warehouse] --> SelectRack[Chọn Rack]
        SelectRack --> SelectSlot["ISlotService.GetEmptySlots<br/>-> chọn 1 Slot cụ thể"]
    end

    %% GỌI SERVICE NHẬP VÀO SLOT CHUNG
    BulkMode --> NhapVaoSlot["INhapTpReceivingService.N<br/>hapTpVaoSlot<br/>— MỘT IUnitOfWork DUY<br/>NHẤT"]
    SelectSlot --> NhapVaoSlot

    %% 4 NHÁNH TRANSACTION SONG SONG
    NhapVaoSlot --> Tr1["1. IStockTpCaseRepository<br/>— check/insert Case dedup"]
    NhapVaoSlot --> Tr2["2. IStockTpRepository —<br/>insert/update STOCKTP<br/>(cộng)"]
    NhapVaoSlot --> Tr3["3. IPhieuTrackingRepository<br/>— insertPhieuMoi (SlotLot<br/>tracking)"]
    NhapVaoSlot --> Tr4["4. ISlotService — SaveLots +<br/>AddQuantity (Kho Core)"]

    %% HỘI TỤ COMMIT TRANSACTION
    Tr1 --> CommitDB[(Commit transaction)]
    Tr2 --> CommitDB
    Tr3 --> CommitDB
    Tr4 --> CommitDB

    %% LỊCH SỬ SAU COMMIT
    CommitDB --> History["Sau khi commit: SaveHistory<br/>ActionType = IMPORT /<br/>BULK_IMPORT / NHAP_LAI_SAU_REWORK"]

    %% HOÀN TẤT
    History --> End([HOÀN TẤT NHẬP KHO])
    ScanTrung --> End

    %% STYLING
    style Start fill:#f9f,stroke:#333,stroke-width:2px
    style CheckService fill:#ff9,stroke:#333,stroke-width:2px
    style NhapVaoSlot fill:#fbb,stroke:#333,stroke-width:2px
    style Tr2 fill:#fbb,stroke:#333,stroke-width:2px
    style End fill:#bfb,stroke:#333,stroke-width:2px
