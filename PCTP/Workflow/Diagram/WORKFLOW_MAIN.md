```mermaid
graph TD
    Start([BẮT ĐẦU]) --> NhapKho[NHẬP KHO]
    Start --> XuatKho[XUẤT KHO]
    Start --> HangLoi[HÀNG LỖI / BẤT THƯỜNG]

    %% NHÁNH XUẤT KHO
    XuatKho --> GiaoHang[GIAO HÀNG]
    XuatKho --> ReworkXuat[REWORK TRẢ VỀ]
    ReworkXuat --> ReworkXong[REWORK XONG]
    ReworkXong --> NhapLaiNG[NHẬP LẠI NG<br/>FRM_NHAPLAING]
    NhapLaiNG -->|OK| OKBranch[Phần OK]
    NhapLaiNG -->|NG| NGBreak[Phần NG / Xử lý bất thường]
    NGBreak --> HangLoi

    %% NHÁNH HÀNG LỖI / BẤT THƯỜNG & 6 BƯỚC QC / GIAO BÙ
    HangLoi --> RepoSource[IPhieuKhachTraRepository]
    RepoSource --> BranchSource{Nguồn phát sinh?}
    
    BranchSource -->|Khách hàng| IKTra[IKhachTraHangService]
    BranchSource -->|Nội bộ| ITNoiBo[ITraNoiBoService]

    IKTra --> QTChung[IQTChungService<br/>Bước 1 & 2: Tiếp nhận & Phiếu Bất Thường]
    ITNoiBo --> QTChung

    QTChung --> QCDinhHuong[Bước 3: QC Định Hướng<br/>Kiểm tra thực tế lỗi]

    QCDinhHuong -->|Khách: Không lỗi| EndKH[END / Từ chối giao bù]
    QCDinhHuong -->|Khách: Có lỗi thật| GiaoBu[Quy trình Riêng: Giao Bù Hàng NG<br/>IGiaoBuNGService / Repo / PhieuGiao]
    QCDinhHuong -->|Có lỗi thật / Nội bộ| Step4[Bước 4: Trả sản xuất Rework]

    Step4 --> Step5[Bước 5: QC Xác Nhận Cuối<br/>Phân tách OK / NG]
    Step5 --> Step6[Bước 6: Nhập Kho Hàng NG<br/>ITraHangQTChungRepo & IStockTpReturnRepo]

    %% HỘI TỤ VỀ KHO CORE & STOCKTP
    NhapKho --> KhoCore[KHO CORE]
    OKBranch --> KhoCore
    Step6 -->|Nhập lại OK qua kho core| KhoCore

    KhoCore --> StockTP[STOCKTP]
    GiaoBu --> EventEnd[QTChung hoàn tất & EventHandler]
    Step6 --> EventEnd
    EventEnd --> FinalEnd([🏁 KẾT THÚC])

    %% STYLE ĐỂ DỄ QUAN SÁT
    style Start fill:#f9f,stroke:#333,stroke-width:2px
    style KhoCore fill:#bbf,stroke:#333,stroke-width:2px
    style StockTP fill:#bfb,stroke:#333,stroke-width:2px
    style GiaoBu fill:#ff9,stroke:#333,stroke-width:2px