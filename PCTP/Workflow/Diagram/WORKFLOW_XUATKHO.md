```mermaid
graph TD
    Start([BẮT ĐẦU XUẤT KHO]) --> CheckSource{Xác định vị trí + mục đích xuất}

    %% ========================================================
    %% NHÁNH 1: HÀNG Ở KHO A0 (GIAO THẲNG)
    %% ========================================================
    CheckSource -->|Hàng ở Kho A0 — giao thẳng| DirectA0["IStockExportService.XuatTrucTiep<br/>(Source = KhoAoA0)"]
    
    DirectA0 --> LockA0["SlotService.LockSlotForUpdate<br/>(Kho Core)"]
    LockA0 --> ExportRepoA0["IStockExportRepository.DeductStockTp<br/>(Lưu ngay)"]
    ExportRepoA0 --> SaveSlotA0["SlotService.SaveLots<br/>(Kho Core)"]
    SaveSlotA0 --> HistoryA0["SaveHistory<br/>ActionType: EXPORT"]


    %% ========================================================
    %% NHÁNH 2: HÀNG Ở SLOT (GIAO HÀNG / GIAO BÙ NG / REWORK)
    %% ========================================================
    CheckSource -->|Hàng ở Slot — GiaoHang /<br/>GiaoBuNG / Rework| PickChoGiao["IStockExportService.PickToChoGiao<br/>(Purpose = GiaoHang /<br/>GiaoBuNG / XuatRework)"]
    
    PickChoGiao --> LockSlot["SlotService.LockSlotForUpdate<br/>(Kho Core)"]
    LockSlot --> DeductCore["LotNoHelper.SubtractLots +<br/>SlotService.SaveSlots/UpdateSlotHeader<br/>(Kho Core)"]
    
    DeductCore --> InsertChoGiao["HangChoGiaoRepository.Insert<br/>FVN_HangChoGiao —<br/>TrangThai: ChoGiao<br/>LoạiYeuCauChoGiao :<br/>GiaoHang | GiaoBuNG | Rework"]
    
    InsertChoGiao --> NotTrừStock["CHƯA trừ STOCKTP<br/>SaveHistory<br/>ActionType: CHO_GIAO"]
    
    NotTrừStock --> WaitConfirm[Chờ bước Confirm riêng]
    
    WaitConfirm --> CheckPurpose{Aggpy Confirm?}


    %% --- Phân nhánh Confirm 1: Giao hàng HVN-PGH ---
    CheckPurpose -->|Giao hàng: HVN-PGH xác<br/>nhận đăng giao| ConfirmPGH["IStockExportService.Confirm<br/>GiaoHangTuChoGiao"]
    ConfirmPGH --> TrừStockPGH["Trừ STOCKTP (ngay)<br/>TrangThai: DaGiao<br/>SaveHistory<br/>ActionType: EXPORT"]


    %% --- Phân nhánh Confirm 2: Giao bù NG ---
    CheckPurpose -->|Giao bù NG:<br/>IGiaoBuNGService xác nhận| GiaoBuConfirm["IGiaoBuNGService.XacNhan<br/>HoanTatGiaoBu<br/>-> gọi lại<br/>ConfirmGiaoHangTuChoGiao<br/>ở chợ ứng dụng"]
    GiaoBuConfirm --> TrừStockGiaoBu["Trừ STOCKTP (ngay)<br/>TrangThai: DaGiao<br/>SaveHistory<br/>ActionType: EXPORT_BU_N<br/>G"]


    %% --- Phân nhánh Confirm 3: Rework ---
    CheckPurpose -->|Rework:<br/>ReworkStockService xác<br/>nhận đã thực xuất| ReworkConfirm["ReworkStockService.XacNh<br/>anXuatRework<br/>-> gọi<br/>ConfirmGiaoHangTuChoGiao<br/>+ RỒI ghi<br/>FVN_TraHangQTChung_Xuat<br/>(audit riêng)"]
    ReworkConfirm --> ReworkInsert["ITraHangQTChungRepository<br/>.InsertXuat<br/>(vừa Xuất/Hàng Loai: NG-Ngoại<br/>lượng này)"]


    %% ========================================================
    %% HỘI TỤ KẾT THÚC CHUNG
    %% ========================================================
    HistoryA0 --> End([HOÀN TẤT])
    TrừStockPGH --> End
    TrừStockGiaoBu --> End
    ReworkInsert --> End


    %% STYLING
    style Start fill:#f9f,stroke:#333,stroke-width:2px
    style CheckSource fill:#ff9,stroke:#333,stroke-width:2px
    style DirectA0 fill:#bbf,stroke:#333,stroke-width:2px
    style PickChoGiao fill:#bbf,stroke:#333,stroke-width:2px
    style InsertChoGiao fill:#ffc000,stroke:#333,stroke-width:2px
    style ReworkConfirm fill:#fbb,stroke:#333,stroke-width:2px
    style ReworkInsert fill:#fbb,stroke:#333,stroke-width:2px
    style End fill:#bfb,stroke:#333,stroke-width:2px