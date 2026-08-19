```mermaid
graph TD
    Start([BẮT ĐẦU]) --> NhapKho[NHẬP KHO]
    Start --> XuatKho[XUẤT KHO]
    Start --> HangLoi[HÀNG LỖI / BẤT THƯỜNG<br/>Nội bộ / Khách trả]

    %% ----------------------------------------------------
    %% 1. NHÁNH NHẬP KHO
    %% ----------------------------------------------------
    NhapKho --> KhoCore[KHO CORE & STOCKTP]


    %% ----------------------------------------------------
    %% 2. NHÁNH XUẤT KHO
    %% ----------------------------------------------------
    XuatKho --> GiaoHang[GIAO HÀNG CHO KHÁCH<br/>- Kho A0 hoặc Pick từ Slot]
    
    XuatKho --> ChuyenRework[ĐƯA ĐI REWORK SẢN XUẤT<br/>- Nhổ từ Slot / FVN_HANGCHOGIAO<br/>- Trạng thái: waitrewwork]
    
    ChuyenRework --> X XuongRework[Xưởng tiến hành Rework]
    XuongRework --> NhapLaiNG[FRM_NHAPLAING<br/>Lọc trạng thái rewwork]

    NhapLaiNG -->|Phần OK| OKBranch[Nhập lại OK]
    NhapLaiNG -->|Phần NG| NGBreak[Phần NG / Chuyển xử lý phế phẩm]

    OKBranch --> KhoCore


    %% ----------------------------------------------------
    %% 3. NHÁNH HÀNG LỖI / BẤT THƯỜNG (QUY TRÌNH 6 BƯỚC)
    %% ----------------------------------------------------
    HangLoi --> RepoSource[IPhieuKhachTraRepository]
    RepoSource --> BranchSource{Nguồn phát sinh?}
    
    BranchSource -->|Khách hàng| IKTra[IKhachTraHangService]
    BranchSource -->|Nội bộ| ITNoiBo[ITraNoiBoService]

    IKTra --> QTChung[IQTChungService<br/>Bước 1 & 2: Tiếp nhận & Phiếu Bất Thường]
    ITNoiBo --> QTChung

    QTChung --> QCDinhHuong[Bước 3: QC Định Hướng<br/>Kiểm tra thực tế lỗi]

    QCDinhHuong -->|Khách: Không lỗi| EndKH[END / Từ chối giao bù]
    QCDinhHuong -->|Khách: Có lỗi thật| GiaoBu[Quy trình Riêng: Giao Bù Hàng NG<br/>IGiaoBuNGService]
    
    QCDinhHuong -->|Có lỗi thật / Nội bộ| Step4[Bước 4: Chuyển qua Xưởng Rework / Xử lý]

    Step4 --> Step5[Bước 5: QC Xác Nhận Cuối<br/>Phân tách OK / NG]
    Step5 --> Step6[Bước 6: Nhập Kho Hàng NG<br/>ITraHangQTChungRepo & IStockTpReturnRepo]


    %% ----------------------------------------------------
    %% HỘI TỤ KẾT THÚC
    %% ----------------------------------------------------
    NGBreak --> Step6
    GiaoBu --> EventEnd[QTChung hoàn tất & EventHandler]
    Step6 --> EventEnd
    GiaoHang --> EventEnd
    EventEnd --> FinalEnd([🏁 KẾT THÚC])


    %% STYLE
    style Start fill:#f9f,stroke:#333,stroke-width:2px
    style KhoCore fill:#bbf,stroke:#333,stroke-width:2px
    style NhapLaiNG fill:#bfb,stroke:#333,stroke-width:2px
    style GiaoBu fill:#ff9,stroke:#333,stroke-width:2px
