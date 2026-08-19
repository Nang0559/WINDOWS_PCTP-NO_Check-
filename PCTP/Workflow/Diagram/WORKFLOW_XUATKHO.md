```mermaid
graph TD
    StartXuan([BẮT ĐẦU XUẤT KHO]) --> ClickSlot[Click Slot trên MainStockSV<br/>- Pick hàng khỏi kệ thực tế<br/>- Nhập vào FVN_HANGCHOGIAO]

    ClickSlot --> SelectBranch{Phân loại mục đích xuất}

    %% ----------------------------------------------------
    %% NHÁNH 1: GIAO HÀNG CHO KHÁCH
    %% ----------------------------------------------------
    SelectBranch -->|1. Giao hàng cho khách| CheckLotLocate{Kiểm tra vị trí LotNo / Nguồn gốc}

    CheckLotLocate -->|1.1. Hàng đang nằm ở Slot cụ thể| SlotSpecific[Trạng thái tại FVN_HANGCHOGIAO: chờ giao<br/>- KHÔNG trừ StockTP]
    SlotSpecific --> RealExport1[Thực tế xuất hàng giao khách<br/>- Nhổ khỏi danh sách hàng chờ giao<br/>- Trừ tồn kho StockTP<br/>- Đánh dấu trạng thái Slot: Export]

    CheckLotLocate -->|1.2. Hàng đang ở Rack A0| RackA0[Thao tác thẳng tại Kho Core<br/>- Đánh dấu trạng thái: Export<br/>- Trừ tồn kho StockTP ngay lập tức]


    %% ----------------------------------------------------
    %% NHÁNH 2: TRẢ HÀNG VỀ SẢN XUẤT REWORK (LIÊN KẾT QUY TRÌNH HÀNG LỖI)
    %% ----------------------------------------------------
    SelectBranch -->|2. Trả hàng về sản xuất Rework| ReturnRework[Nhổ khỏi Slot / Vị trí<br/>- Đưa vào FVN_HANGCHOGIAO<br/>- Trạng thái: waitrewwork<br/>- ĐÃ TRỪ tồn kho tại StockTP]

    ReturnRework --> RepoSource[IPhieuKhachTraRepository]
    
    RepoSource --> ServiceInternal[ITraNoiBoService<br/>Nguồn: Nội Bộ]
    RepoSource --> ServiceCustomer[IKhachTraHangService<br/>Nguồn: Khách Hàng]

    ServiceInternal --> StepC[IQTChungService<br/>Bước 1 & 2: Tiếp nhận & Phiếu Bất Thường]
    ServiceCustomer --> StepC

    StepC --> StepD[Bước 3: QC Định Hướng<br/>Kiểm tra thực tế lỗi]

    StepD -->|Khách: Không lỗi| EndNoErr[END<br/>Từ chối giao bù]
    StepD -->|Khách: Có lỗi thật| StepF1[Quy trình Riêng: Giao Bù Hàng NG<br/>IGiaoBuNGService / Repo]
    StepD -->|Nội Bộ / Khách có lỗi| StepF2[Quy trình Chung: Bước 4<br/>Trả sản xuất Rework<br/>- Cập nhật FVN_HANGCHOGIAO sang status: rewwork]

    StepF2 --> DoRework[Tiến hành sản xuất Rework]
    DoRework --> ReworkDone[Rework hoàn tất]

    ReworkDone --> StepG[Bước 5: Mở form frm_NhaplaiNG<br/>- Lọc FVN_HANGCHOGIAO status: rewwork<br/>- QC Xác nhận cuối: Phân tách OK / NG]

    StepG --> StepH[Bước 6: Nhập lại kho qua Repository<br/>- ITraHangQTChungRepo & IStockTpReturnRepo<br/>- Lượng OK: Nhập lại Kho Core / StockTP<br/>- Lượng NG: Nhập kho NG / In phiếu giao QC]

    StepH --> StepI[QTChung hoàn tất<br/>QTChungHoanTatEvent]


    %% KẾT THÚC CÁC NHÁNH
    RealExport1 --> EndXuat([🏁 HOÀN TẤT XUẤT GIAO HÀNG])
    RackA0 --> EndXuat
    EndNoErr --> EndXuat
    StepF1 --> EndXuat
    StepI --> EndXuat

    %% STYLING
    style StartXuan fill:#f9f,stroke:#333,stroke-width:2px
    style ClickSlot fill:#bbf,stroke:#333,stroke-width:2px
    style SlotSpecific fill:#ff9,stroke:#333,stroke-width:2px
    style ReturnRework fill:#fbb,stroke:#333,stroke-width:2px
    style StepD fill:#bbf,stroke:#333,stroke-width:2px
    style StepG fill:#bfb,stroke:#333,stroke-width:2px
    style StepI fill:#fbb,stroke:#333,stroke-width:2px