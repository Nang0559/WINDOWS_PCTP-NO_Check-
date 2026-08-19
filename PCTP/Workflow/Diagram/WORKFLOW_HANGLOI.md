```mermaid
graph TD
    %% ----------------------------------------------------
    %% KHỞI TẠO & TIẾP NHẬN BAN ĐẦU
    %% ----------------------------------------------------
    StartRepo[IPhieuKhachTraRepository] --> B1[IKhachTraHangService<br/>Nguồn: Khách Hàng]
    StartRepo --> B2[ITraNoiBoService<br/>Nguồn: Nội Bộ]
    
    B1 --> Step1[IQTChungService<br/>Bước 1: TaoPhieuXuLyBatThuong]
    B2 --> Step1
    
    Step1 --> Step2["Bước 2: QCDinhHuongRework<br/>(gate quyết định —<br/>QTChungStatus.DaDinhHuo<br/>ngRework)"]
    
    %% ----------------------------------------------------
    %% PHÂN NHÁNH 1: KHÁCH KHÔNG LỖI THẬT
    %% ----------------------------------------------------
    Step2 -->|Khách: Không lỗi thật| EndNoErr[END — Từ chối giao bù]

    %% ----------------------------------------------------
    %% PHÂN NHÁNH 2: KHÁCH CÓ LỖI THẬT (GIAO BÙ)
    %% ----------------------------------------------------
    Step2 -->|Khách: Có lỗi thật, chỉ cần<br/>đến hàng| GiaoBu1["IGiaoBuNGService.GiaoBuTh<br/>eoQR<br/>-><br/>IStockExportService.PickToC<br/>hoGiao<br/>Purpose=XuatGiaoBuNG"]
    
    GiaoBu1 --> GiaoBu2["IGiaoBuNGService.XacNhan<br/>HoanTatGiaoBu<br/>-><br/>IStockExportService.Confirm<br/>GiaoHangTuChoGiao"]
    GiaoBu2 --> EndGiaoBu([END])

    %% ----------------------------------------------------
    %% PHÂN NHÁNH 3: NỘI BỘ / KHÁCH CẦN REWORK
    %% ----------------------------------------------------
    Step2 -->|Nội bộ / Khách cần Rework| Rework1["IQTChungService.XuatKhoR<br/>ework<br/>-><br/>IReworkStockService.XuatKh<br/>oRework<br/>-><br/>IStockExportService.PickToC<br/>hoGiao<br/>(Purpose=XuatRework)"]
    
    Rework1 --> Rework2["Xác nhận thực xuất:<br/>IReworkStockService.XacNh<br/>anXuatRework<br/>-><br/>ConfirmGiaoHangTuChoGiao<br/>+ InsertXuat"]
    
    Rework2 --> Step5["Bước 5: GiaoHangRework<br/>ITraHangQTChungRepositor<br/>y.InsertGiao<br/>(KHÔNG dùng<br/>Slot/STOCKTP)"]
    
    Step5 --> Step6["Bước 6: SanXuatBaoReworkXong<br/>(mốc trạng thái — không<br/>dụng kho)"]
    
    Step6 --> Step7["Bước 7: QCXacNhanCuoi<br/>InsertQC — phân tách<br/>OK/NG"]
    
    %% Phân tách sau QC cuối
    Step7 -->|NG = 0| StatusHoanTat1[QTChungStatus.HoanTat]
    Step7 -->|NG > 0| Step8["Bước 8: NhapLaiHangNG<br/>IReworkStockService.NhapL<br/>aiHangNG<br/>-> ISlotService.AddQuantity<br/>(Kho Core)<br/>-><br/>IStockExportRepository.Adju<br/>stSlConLai (STOCKTP +)<br/>+ InsertNhapNG (audit)"]
    
    Step8 --> StatusHoanTat2[QTChungStatus.HoanTat]

    %% ----------------------------------------------------
    %% KẾT THÚC CHUNG
    %% ----------------------------------------------------
    StatusHoanTat1 --> FinalEnd([🏁 KẾT THÚC])
    StatusHoanTat2 --> FinalEnd
    EndNoErr --> FinalEnd

    %% STYLING
    style StartRepo fill:#f9f,stroke:#333,stroke-width:2px
    style Step2 fill:#bbf,stroke:#333,stroke-width:2px
    style GiaoBu1 fill:#bfb,stroke:#333,stroke-width:2px
    style Rework1 fill:#fbb,stroke:#333,stroke-width:2px
    style FinalEnd fill:#fbb,stroke:#333,stroke-width:2px