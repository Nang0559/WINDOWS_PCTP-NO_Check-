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
    CommitDB --> History["Sau khi commit: SaveHistory<br/>ActionType = IMPORT /<br/>BULK_IMPORT"]

    %% HOÀN TẤT
    History --> End([HOÀN TẤT NHẬP KHO])
    ScanTrung --> End

    %% STYLING
    style Start fill:#f9f,stroke:#333,stroke-width:2px
    style CheckService fill:#ff9,stroke:#333,stroke-width:2px
    style NhapVaoSlot fill:#fbb,stroke:#333,stroke-width:2px
    style Tr2 fill:#fbb,stroke:#333,stroke-width:2px
    style End fill:#bfb,stroke:#333,stroke-width:2px