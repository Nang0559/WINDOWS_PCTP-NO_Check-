```mermaid
graph TD
    Start([BẮT ĐẦU: Yêu cầu tác động Kho Core]) --> ActionCheck{Phân loại loại tác động}

    %% ==========================================
    %% 1. NHÓM 1: KHỞI TẠO & CẤU TRÚC KHÔNG GIAN
    %% ==========================================
    ActionCheck -->|1. Cấu hình không gian| SetupSpace[Khởi tạo Warehouse -> Rack -> Slot]
    SetupSpace --> UpdateMatrix[Cập nhật ma trận hàng/cột cho Rack<br/>- IRackRepository.UpdateLayout]
    UpdateMatrix --> EndSetup([Hoàn tất cấu hình không gian])


    %% ==========================================
    %% 2. NHÓM 2: LUỒNG NHẬP KHO (INBOUND FLOW)
    %% ==========================================
    ActionCheck -->|2. Nhập kho / Nhập lại OK| InboundStart[Nhận thông tin Item, LotNo, Số lượng]
    
    InboundStart --> FindSlot{Kiểm tra loại vị trí nhập}
    FindSlot -->|Nhập kho thường| FindEmpty[Tra cứu Slot trống tối ưu<br/>- ISlotRepository.GetEmptySlots]
    FindSlot -->|Nhập kho ảo A0| FindA0[Lấy hoặc tạo Slot ảo A0<br/>- IBulkStockSlotRepository.GetOrCreateVirtualSlotId]

    FindEmpty --> CommitInbound
    FindA0 --> CommitInbound

    CommitInbound[Thực hiện Transaction Nhập Kho Core] --> LockSlot[Khóa dòng Slot chống tranh chấp<br/>- ISlotRepository.LockSlotForUpdate]
    
    LockSlot --> SaveData[Ghi nhận dữ liệu vào Kho Core]
    SaveData --> SaveLot[1. Lưu chi tiết vào bảng SlotLot / SaveLots]
    SaveData --> UpdateHeader[2. Cập nhật tổng lượng & Header Slot]
    SaveData --> AddStockTP[3. Cộng dồn số lượng vào Tồn kho tổng StockTP]
    
    AddStockTP --> CommitDB[(Commit Transaction Database)]
    CommitDB --> EndInbound([Hoàn tất Nhập Kho Core])


    %% ==========================================
    %% 3. NHÓM 3: LUỒNG XUẤT KHO / NHỔ HÀNG (OUTBOUND FLOW)
    %% ==========================================
    ActionCheck -->|3. Xuất kho / Nhổ hàng| OutboundStart[Nhận yêu cầu xuất SlotId, Qty, ItemCode]

    OutboundStart --> CheckExportType{Kiểm tra vị trí nguồn hàng}

    %% 3.1. Xuất trực tiếp từ Rack A0 (Không qua pick)
    CheckExportType -->|Nằm ở Rack A0| DirectExport[Xuất thẳng từ A0]
    DirectExport --> LockA0[Khóa Slot A0]
    LockA0 --> SubStockTP_A0[Trừ trực tiếp số lượng khỏi StockTP]
    SubStockTP_A0 --> ClearSlotA0[Cập nhật lại lượng Slot A0]

    %% 3.2. Nhổ hàng từ Slot thông thường (Pick kệ)
    CheckExportType -->|Nằm ở Slot thường| PickExport[Nhổ hàng từ Slot vật lý]
    PickExport --> LockSlotPick[Khóa Slot cần nhổ<br/>- LockSlotForUpdate]
    LockSlotPick --> SplitLot[Bóc tách Lot / Trừ SlotLot quantity]
    SplitLot --> PushMiddle[Đẩy dữ liệu vào bảng trung gian FVN_HANGCHOGIAO<br/>- Kèm trạng thái: chờ giao hoặc waitrewwork]
    
    %% Quyết định thời điểm trừ StockTP theo mục đích
    PushMiddle --> CheckPurpose{Kiểm tra mục đích xuất}
    CheckPurpose -->|Mục đích: Chờ giao khách| DelayStock[Chưa trừ StockTP ngay<br/>(Sẽ trừ ở bước chốt xuất kho cuối cùng)]
    CheckPurpose -->|Mục đích: Trả Rework| SubStockTP_Core[ĐÃ TRỪ ngay StockTP và cập nhật Slot]

    ClearSlotA0 --> CommitExportDB[(Commit Transaction Database)]
    DelayStock --> CommitExportDB
    SubStockTP_Core --> CommitExportDB

    CommitExportDB --> EndOutbound([Hoàn tất Tác vụ Kho Core])

    %% STYLING
    style Start fill:#f9f,stroke:#333,stroke-width:2px
    style CommitInbound fill:#bfb,stroke:#333,stroke-width:2px
    style AddStockTP fill:#bfb,stroke:#333,stroke-width:2px
    style SubStockTP_Core fill:#ff9,stroke:#333,stroke-width:2px
    style PushMiddle fill:#bbf,stroke:#333,stroke-width:2px