```mermaid
graph TD
    Start([BẮT ĐẦU: Xử lý kho]) --> CoreSetup[1. Tầng Kho Core & Không gian<br/>- Khai báo Warehouse, Rack, Slot<br/>- Cấu hình ma trận, Slot ảo A0<br/>(IWarehouse, IRack, ISlot Repository)]

    CoreSetup --> ActionType{Phân loại tác vụ}

    %% ==========================================
    %% LUỒNG 1: NHẬP KHO CHUẨN / NHẬP LẠI OK
    %% ==========================================
    ActionType -->|Nhập kho / Nhập lại OK| Inbound[Nhập kho Core<br/>- Tra cứu Slot trống: GetEmptySlots<br/>- Cộng dồn tồn kho StockTP<br/>- Gán Lot vào Slot cụ thể]

    %% ==========================================
    %% LUỒNG 2: XUẤT KHO / VẬN HÀNH
    %% ==========================================
    ActionType -->|Xuất kho| CheckSource{Kiểm tra vị trí hàng xuất}

    %% 2.1. Hàng ở Rack A0 (Xuất thẳng)
    CheckSource -->|Hàng nằm sẵn ở Rack A0| DirectA0[Mở form HVN-PGH<br/>- Không cần pick hàng rườm rà<br/>- Trừ trực tiếp StockTP tại A0 và Export ngay]

    %% 2.2. Hàng ở Slot thông thường (Phải Pick)
    CheckSource -->|Hàng ở Slot khác A0| PickSlot[Click Slot trên MainStockSV<br/>- Nhổ hàng khỏi Slot vật lý<br/>- Đẩy vào bảng FVN_HANGCHOGIAO<br/>- Đánh dấu mục đích rõ ràng]

    PickSlot --> PurposeBranch{Xác định mục đích tại FVN_HANGCHOGIAO}

    %% --- Mục đích: Giao khách ---
    PurposeBranch -->|Mục đích: Giao khách| FlowGiaoKhach[Trạng thái: chờ giao<br/>(Chưa trừ StockTP tại Kho Core)]
    FlowGiaoKhach --> OpenPGH[Mở form HVN-PGH<br/>Thực hiện quy trình giao hàng]
    OpenPGH --> FinalExport[Bước cuối xuất kho<br/>- Tự động quét nguồn từ FVN_HANGCHOGIAO<br/>- Trừ tồn kho StockTP & Đánh dấu Export]

    %% --- Mục đích: Trả sản xuất Rework ---
    PurposeBranch -->|Mục đích: Rework| FlowRework[Trạng thái: waitrewwork<br/>(ĐÃ TRỪ tồn kho tại StockTP)]

    FlowRework --> RepoSource[IPhieuKhachTraRepository]
    RepoSource --> ServiceInternal[ITraNoiBoService / IKhachTraHangService]
    ServiceInternal --> StepQC[IQTChungService & QC Định hướng]

    StepQC -->|Có lỗi thực tế / Nội bộ| StepRework[Trả sản xuất Rework<br/>- Cập nhật trạng thái sang: rewwork]
    StepRework --> DoRework[Xưởng tiến hành Rework]

    DoRework --> FormNhapLai[Mở form frm_NhaplaiNG<br/>- Lọc FVN_HANGCHOGIAO status: rewwork<br/>- QC Xác nhận cuối phân tách OK / NG]

    FormNhapLai --> SplitResult{Phân tách OK / NG}
    SplitResult -->|Phần OK| InboundOK[Nhập trở lại Kho Core<br/>Cộng dồn StockTP]
    SplitResult -->|Phần NG| InboundNG[Xử lý kho NG / In phiếu giao QC]

    %% HOÀN TẤT
    Inbound --> End([HOÀN TẤT])
    DirectA0 --> End
    FinalExport --> End
    InboundOK --> End
    InboundNG --> End

    %% STYLING
    style Start fill:#f9f,stroke:#333,stroke-width:2px
    style CoreSetup fill:#bbf,stroke:#333,stroke-width:2px
    style CheckSource fill:#ff9,stroke:#333,stroke-width:2px
    style PickSlot fill:#bbf,stroke:#333,stroke-width:2px
    style FormNhapLai fill:#bfb,stroke:#333,stroke-width:2px
    style End fill:#fbb,stroke:#333,stroke-width:2px
