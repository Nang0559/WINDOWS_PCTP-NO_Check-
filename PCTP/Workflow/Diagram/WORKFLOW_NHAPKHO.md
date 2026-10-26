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
   * Sử dụng `INhapTpReceivingService.KiemTraTruocKhiNhap` để kiểm tra xem mã QR đã được nhập chưa: đối chiếu `SlotLot.QrData` (tồn hiện tại) và nhật ký `StockHistory` (`ActionType = IMPORT`, còn nguyên khi hàng đã xuất hết). Bảng `NHAP_TP_HIS` không còn được sử dụng.
   * *Nếu trùng:* Trả về kết quả `ScanResult.Trung` $\rightarrow$ Từ chối giao dịch, không thực hiện transaction.
   * *Nếu hợp lệ:* Chuyển sang bước phân định hình thức nhập.
     **3b. Kiểm Tra Tem Thùng (nếu có InspectionConfig):**
    - Gọi `IInspectionConfigService.NeedsInspection(itemCode)`
    - Nếu `true` → mở `FormInspection(temTong, config, inspSvc)`
    - Nếu `InspectionPassed = false` → từ chối nhập kho
    - Nếu `InspectionPassed = true` → tiếp tục bước 4 (NhapTpVaoSlot)
4. **Phân Định Hình Thức Nhập:**
   * **Nhập hàng loạt (Bulk Mode):** Sử dụng `ISlotService.GetOrCreateVirtualSlotText` để định vị vào Kho Áo A0 mà không cần chọn Slot thủ công.
   * **Nhập chi tiết (Detailed Mode):** Người dùng thực hiện chọn không gian theo thứ tự `Warehouse` $\rightarrow$ `Rack` $\rightarrow$ chọn 1 Slot cụ thể thông qua `ISlotService.GetEmptySlots`.
5. **Thực Hiện Nhập Vào Slot (Unit of Work):**
   * Tất cả các luồng sau khi định vị vị trí đều gọi chung `INhapTpReceivingService.NhapTpVaoSlot` chạy trong **một `IUnitOfWork` duy nhất**.
6. **Thực hiện atomic receiving qua KhoCore trong transaction:**
   * Lấy application lock theo thứ tự QR → LOT bằng `IStockTpImportGuardRepository`. Khóa LOT tuần tự hóa các tem khác nhau cùng cập nhật một LOT sản xuất.
   * Kiểm tra QR đã nhập trong `SlotLot.QrData` hoặc `StockHistory(IMPORT)` sau khi đã lấy khóa.
   * `StockMovementService.Receive` gọi `IStockReceivingRepository` do `Infrastructure.Stock.StockReceivingRepository` triển khai trực tiếp (không còn adapter `NhapKho` trong module factory) để insert/update `STOCKTP`, sau đó gọi `BulkStockSlotRepository.AddLot` để ghi một dòng SlotLot với TemCode, QrData, NgaySX, SoPhieuTong, MaPhieu, ParentSoPhieu, ImportDate và PhieuStatus; đồng thời tính lại Slot header.
   * `AddLot` từ chối chạy nếu không có transaction. Workflow owner chịu trách nhiệm rollback khi movement trả Fail/Duplicate.
   * `IPhieuTrackingRepository.InsertPhieuMoi` không còn được gọi trong luồng nhập thành phẩm chuẩn; repository vẫn có thể tồn tại cho các caller legacy khác.
7. **Ghi Lịch Sử & Commit:**
   * Sau khi movement thành công, workflow gọi `SaveHistory(ActionType = IMPORT)` trước `Commit`; history, STOCKTP, SlotLot và Slot header cùng commit/rollback trong một UoW.
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
    NewGoods --> CheckService["INhapTpReceivingService.Ki<br/>emTraTruocKhiNhap<br/>- Check QR đã nhập chưa<br/>(SlotLot.QrData +<br/>StockHistory IMPORT)"]
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

    %% CHUỖI ATOMIC RECEIVING TUẦN TỰ TRONG MỘT UOW
    NhapVaoSlot --> Tr1["AcquireImportLock(QR)<br/>AcquireLotImportLock(LOT)"]
    Tr1 --> Tr2["Kiểm tra QR trùng<br/>SlotLot + StockHistory"]
    Tr2 --> Tr3["StockMovementService.Receive<br/>IStockReceivingRepository<br/>insert/update STOCKTP"]
    Tr3 --> Tr4["BulkStockSlotRepository.AddLot<br/>insert SlotLot metadata/QR<br/>refresh Slot header"]
    Tr4 --> History["SaveHistory(IMPORT)<br/>trước Commit"]
    History --> CommitDB[(Commit transaction)]

    %% HOÀN TẤT
    History --> End([HOÀN TẤT NHẬP KHO])
    ScanTrung --> End

    %% STYLING
    style Start fill:#f9f,stroke:#333,stroke-width:2px
    style CheckService fill:#ff9,stroke:#333,stroke-width:2px
    style NhapVaoSlot fill:#fbb,stroke:#333,stroke-width:2px
    style Tr2 fill:#fbb,stroke:#333,stroke-width:2px
    style End fill:#bfb,stroke:#333,stroke-width:2px
