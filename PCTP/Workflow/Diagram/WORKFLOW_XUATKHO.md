```mermaid
graph TD
    Start([BẮT ĐẦU XUẤT KHO]) --> CheckSource{Xác định vị trí hàng xuất}

    %% ========================================================
    %% NHÁNH 1: HÀNG Ở KHO A0 (XUẤT TRỰC TIẾP)
    %% ========================================================
    CheckSource -->|Hàng ở Kho A0| DirectA0["Mở form HVN-PGH<br/>- IStockExportService.XuatTrucTiep<br/>(Source = Bulk)<br/>- Không qua FVN_HangChoGiao"]
    DirectA0 --> ExportA0["Trừ STOCKTP ngay<br/>(SLXUAT = SLCONLAI -)"]


    %% ========================================================
    %% NHÁNH 2: KHÔNG PICK TỪ ĐO — KHỞI TẠO QUA PHIẾU
    %% ========================================================
    CheckSource -->|Không pick từ do — chỉ khởi tạo qua| RepoSource[IPhieuKhachTraRepository]
    RepoSource --> ServiceInternal[ITraNoiBoService<br/>Nguồn: Nội Bộ]
    RepoSource --> ServiceCustomer[IKhachTraHangService<br/>Nguồn: Khách Hàng]

    ServiceInternal --> StepC[IQTChungService<br/>Bước 1&2: Tiếp nhận & Tạo Phiếu Bất Thường]
    ServiceCustomer --> StepC

    StepC --> StepD[Bước 3: QC Định Hướng]


    %% ========================================================
    %% NHÁNH 3: HÀNG Ở SLOT, MỤC ĐÍCH GIAO HÀNG
    %% ========================================================
    CheckSource -->|Hàng ở Slot, mục đích GIAO HÀNG| MainStock["Click Slot trên MainStockSV<br/>(IStockExportService.PickToChoGiao)<br/>- Purpose = GiaoHang / GiaoBuNG<br/>- CHƯA trừ STOCKTP"]
    
    MainStock --> TableChoGiao[FVN_HangChoGiao<br/>TrangThai = ChoGiao]
    TableChoGiao --> OpenPGH["Mở form HVN-PGH<br/>Thực hiện giao hàng cho khách"]
    OpenPGH --> ConfirmPGH["ConfirmGiaoHangTuChoGiao<br/>- Trừ STOCKTP (SLXUAT +, SLCONLAI -)<br/>- TrangThai = DaGiao"]


    %% ========================================================
    %% NHÁNH 4: XỬ LÝ KẾT QUẢ QC ĐỊNH HƯỚNG
    %% ========================================================
    StepD -->|Khách: Không lỗi| EndNoErr[END — Từ chối giao bù]
    
    StepD -->|Khách: Có lỗi thật| StepGiaoBu["IGiaoBuNGService.GiaoBuTheoQR<br/>-> IStockExportService.PickToChoGiao<br/>(Purpose=GiaoBuNG)<br/>-> FVN_HangChoGiao (Loại = GiaoBuNG)"]
    StepGiaoBu --> ConfirmGiaoBu["IGiaoBuNGService.XacNhanHoanTatGiaoBu<br/>-> ConfirmGiaoHangTuChoGiao (trừ STOCKTP)"]

    StepD -->|Nội bộ / Khách có lỗi cần Rework| StepReworkAction["IQTChungService điều phối<br/>IReworkStockService.XuatKhoRework(phieuxu.xy.id, slot.lotid, ...)<br/>- Trừ Slot.Lot (qua SlotService)<br/>- Chỉ trừ SL.CON LAI (KHÔNG dùng SL.XUẤT)<br/>- Ghi FVN_TraHangQITChung_Xuat<br/>(qua ITraHangQITChungRepository)<br/>(1 bước duy nhất — không qua FVN_HangChoGiao)"]


    %% ========================================================
    %% NHÁNH 5: TIẾN HÀNH REWORK & NHẬP LẠI
    %% ========================================================
    StepReworkAction --> DoRework[Tiến hành Rework tại xưởng]
    DoRework --> ReworkDone[Rework hoàn tất]

    ReworkDone --> FormNhapLai["frm_NhapLaiNG<br/>QC xác nhận, phân tách OK / NG"]
    
    FormNhapLai --> ImportAction["IReworkStockService.NhapLaiNG<br/>- OK: cộng lại SL.CON LAI + Slot.lot dịch<br/>- NG: route vào Slot NG riêng<br/>- Ghi FVN_TraHangQITChung_NhapNG"]
    
    ImportAction --> EventEnd[QTChung hoàn tất<br/>QTC-HungHoanTatEvent]


    %% ========================================================
    %% HỘI TỤ KẾT THÚC CHUNG
    %% ========================================================
    ExportA0 --> End([HOÀN TẤT])
    ConfirmPGH --> End
    EndNoErr --> End
    ConfirmGiaoBu --> End
    EventEnd --> End


    %% STYLING
    style Start fill:#f9f,stroke:#333,stroke-width:2px
    style CheckSource fill:#ff9,stroke:#333,stroke-width:2px
    style DirectA0 fill:#bbf,stroke:#333,stroke-width:2px
    style MainStock fill:#bbf,stroke:#333,stroke-width:2px
    style StepReworkAction fill:#fbb,stroke:#333,stroke-width:2px
    style FormNhapLai fill:#bfb,stroke:#333,stroke-width:2px
    style End fill:#fbb,stroke:#333,stroke-width:2px
