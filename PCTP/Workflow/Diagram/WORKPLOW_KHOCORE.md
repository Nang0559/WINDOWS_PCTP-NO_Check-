```mermaid
graph TD
    Start([Yêu cầu tác động Kho Core]) --> ActionCheck{Phân loại tác động}

    %% ==========================================
    %% NHÁNH 1: CẤU HÌNH KHÔNG GIAN
    %% ==========================================
    ActionCheck -->|1. Cấu hình không gian| ConfigSpace["Khởi tạo Warehouse -> Rack<br/>-> Slot"]
    ConfigSpace --> UpdateLayout["IRackService.UpdateLayout<br/>(qua IRackRepository)"]
    UpdateLayout --> EndConfig([Hoàn tất cấu hình])

    %% ==========================================
    %% NHÁNH 2: PRIMITIVE SLOT
    %% ==========================================
    ActionCheck -->|2. Primitive Slot| PrimitiveSlot["ISlotService cung cấp:<br/>- GetLots / SaveLots<br/>- LockSlotForUpdate<br/>-<br/>UpdateSlotHeaderFromLots<br/>- FindSlotsContainingLot<br/>- GetOrCreateVirtualSlotText"]
    PrimitiveSlot --> EndPrimitive([Trả kết quả cho module gọi])

    %% ==========================================
    %% NHÁNH 3: GHI LỊCH SỬ
    %% ==========================================
    ActionCheck -->|3. Ghi lịch sử| History["IStockHistoryRepository.Sav<br/>eHistory<br/>(ActionType do module gọi<br/>truyền vào)"]
    History --> EndHistory([Ghi 1 dòng StockHistory])

    %% STYLING
    style Start fill:#f9f,stroke:#333,stroke-width:2px
    style PrimitiveSlot fill:#bdfcc9,stroke:#333,stroke-width:2px
    style History fill:#bdfcc9,stroke:#333,stroke-width:2px
