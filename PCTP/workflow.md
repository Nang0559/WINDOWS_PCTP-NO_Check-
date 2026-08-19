```mermaid
graph TD
    A[IPhieuKhachTraRepository] --> B1[IKhachTraHangService<br/>Nguồn: Khách Hàng]
    A --> B2[ITraNoiBoService<br/>Nguồn: Nội Bộ]
    
    B1 --> C[IQTChungService<br/>Bước 1 & 2: Tiếp nhận & Phiếu Bất Thường]
    B2 --> C
    
    C --> D[Bước 3: QC Định Hướng<br/>Kiểm tra thực tế lỗi]
    
    D -->|Khách: Không lỗi| E[END<br/>Từ chối giao bù]
    D -->|Khách: Có lỗi thật| F1[Quy trình Riêng: Giao Bù Hàng NG<br/>IGiaoBuNGService / Repo]
    D -->|Nội Bộ / Khách có lỗi| F2[Quy trình Chung: Bước 4<br/>Trả sản xuất Rework]
    
    F2 --> G[Bước 5: QC Xác Nhận Cuối<br/>Phân tách OK / NG]
    G --> H[Bước 6: Nhập Kho Hàng NG<br/>ITraHangQTChungRepo & IStockTpReturnRepo]
    
    H --> I[QTChung hoàn tất<br/>QTChungHoanTatEvent]
    I --> J[🏁 KẾT THÚC]

    style A fill:#f9f,stroke:#333,stroke-width:2px
    style D fill:#bbf,stroke:#333,stroke-width:2px
    style F1 fill:#bfb,stroke:#333,stroke-width:2px
    style J fill:#fbb,stroke:#333,stroke-width:2px
