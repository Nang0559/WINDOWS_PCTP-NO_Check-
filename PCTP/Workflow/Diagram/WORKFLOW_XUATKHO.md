```mermaid
graph TD
    Start([BẮT ĐẦU XUẤT KHO]) --> CheckSource{Xác định vị trí hàng xuất}

    %% ----------------------------------------------------
    %% LUỒNG 1: HÀNG ĐANG Ở KHO TẠM A0 (GIAO THẲNG, KHÔNG PICK)
    %% ----------------------------------------------------
    CheckSource -->|Hàng ở Kho A0| DirectA0["Mở form HVN-PGH<br/>- Không cần qua MainStockSV<br/>- Không cần đưa vào FVN_HANGCHOGIAO"]
    DirectA0 --> ExportA0["Bước cuối: Cập nhật xuất kho hệ thống<br/>- Tự động lấy nguồn từ A0<br/>- Trừ StockTP ngay lập tức"]

    %% ----------------------------------------------------
    %% LUỒNG 2: HÀNG ĐANG Ở SLOT KỆ KHÁC A0 (PHẢI PICK HÀNG)
    %% ----------------------------------------------------
    CheckSource -->|Hàng ở Slot thông thường| MainStock["Click Slot trên MainStockSV<br/>- Nhổ hàng ra khỏi slot thực tế<br/>- Đưa vào bảng FVN_HANGCHOGIAO<br/>- Đánh dấu mục đích rõ ràng"]

    MainStock --> MarkPurpose{Phân loại mục đích tại FVN_HANGCHOGIAO}

    %% --- 2.1. Mục đích: Giao khách ---
    MarkPurpose -->|Mục đích: giao khách| FlowGiaoKhach["Trạng thái: chờ giao<br/>[Chưa trừ StockTP]"]
    FlowGiaoKhach --> OpenPGH["Mở form HVN-PGH<br/>Thực hiện quy trình giao hàng cho khách"]
    OpenPGH --> StepCuoiPGH["Bước cuối: Cập nhật xuất kho hệ thống<br/>- Hệ thống tự động tìm kiếm nguồn từ FVN_HANGCHOGIAO<br/>- Trừ StockTP và đánh dấu Export"]

    %% --- 2.2. Mục đích: Trả sản xuất Rework ---
    MarkPurpose -->|Mục đích: rewwork| FlowRework["Trạng thái: waitrewwork<br/>[ĐÃ TRỪ tồn kho tại StockTP]"]

    FlowRework --> RepoSource[IPhieuKhachTraRepository]
    RepoSource --> ServiceInternal[ITraNoiBoService<br/>Nguồn: Nội Bộ]
    RepoSource --> ServiceCustomer[IKhachTraHangService<br/>Nguồn: Khách Hàng]

    ServiceInternal --> StepC[IQTChungService<br/>Bước 1 & 2: Tiếp nhận & Phiếu Bất Thường]
    ServiceCustomer --> StepC

    StepC --> StepD[Bước 3: QC Định Hướng<br/>Kiểm tra thực tế lỗi]

    StepD -->|Khách: Không lỗi| EndNoErr[END<br/>Từ chối giao bù]
    StepD -->|Khách: Có lỗi thật| StepF1[Quy trình Riêng: Giao Bù Hàng NG<br/>IGiaoBuNGService / Repo]
    
    StepD -->|Nội Bộ / Khách có lỗi| StepF2["Đến phần Trả sản xuất Rework<br/>- Tự động lấy dữ liệu từ FVN_HANGCHOGIAO<br/>- Dựa vào mục đích đã đánh dấu là: rewwork<br/>- Cập nhật trạng thái sang: rewwork"]

    StepF2 --> DoRework[Tiến hành sản xuất Rework tại xưởng]
    DoRework --> ReworkDone[Rework hoàn tất]

    ReworkDone --> StepG["Bước 5: Mở form frm_NhaplaiNG<br/>- Lọc FVN_HANGCHOGIAO theo trạng thái rewwork<br/>- QC Xác nhận cuối: Phân tách OK / NG"]

    StepG --> StepH["Bước 6: Nhập lại kho qua Repository<br/>- ITraHangQTChungRepo & IStockTpReturnRepo<br/>- Lượng OK: Nhập lại Kho Core / StockTP<br/>- Lượng NG: Nhập kho NG / In phiếu giao QC"]

    StepH --> StepI[QTChung hoàn tất<br/>QTChungHoanTatEvent]

    %% KẾT THÚC
    ExportA0 --> End([HOÀN TẤT])
    StepCuoiPGH --> End
    EndNoErr --> End
    StepF1 --> End
    StepI --> End

    %% STYLING
    style Start fill:#f9f,stroke:#333,stroke-width:2px
    style CheckSource fill:#ff9,stroke:#333,stroke-width:2px
    style DirectA0 fill:#bbf,stroke:#333,stroke-width:2px
    style MainStock fill:#bbf,stroke:#333,stroke-width:2px
    style StepF2 fill:#fbb,stroke:#333,stroke-width:2px
    style StepG fill:#bfb,stroke:#333,stroke-width:2px
    style End fill:#fbb,stroke:#333,stroke-width:2px