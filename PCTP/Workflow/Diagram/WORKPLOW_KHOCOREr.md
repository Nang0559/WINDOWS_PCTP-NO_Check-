```mermaid
graph TD
    A[Bắt đầu: Nhập / Xuất / Xử lý] --> B[Kho Core Manager]
    
    B --> C1[Nhập kho chuẩn / Nhập lại OK]
    B --> C2[Xuất kho - HVN-PGH]
    
    C1 --> D1[Cộng dồn StockTP]
    C1 --> D2[Gán vị trí Slot lưu trữ]
    
    C2 --> E1[Nhánh 1: Giao hàng thông thường]
    C2 --> E2[Nhánh 2: Trả Rework]
    
    E1 -->|Nằm ở Slot| F1[Nhổ khỏi Slot<br/>Chuyển FVN_HANGCHOGIAO<br/>Trạng thái: chờ giao<br/>Chưa trừ StockTP]
    E1 -->|Nằm sẵn Rack A0| F2[Trừ trực tiếp StockTP<br/>Xuất ngay tại Kho Core]
    
    F1 -->|Xuất thực tế| F3[Nhổ khỏi hàng chờ giao<br/>Đánh dấu: Export<br/>Trừ tồn kho StockTP]
    
    E2 --> G1[Nhổ khỏi Slot<br/>Đẩy vào FVN_HANGCHOGIAO<br/>Trạng thái: waitrewwork<br/>Có trừ StockTP]
    G1 --> G2[Lập phiếu bàn giao<br/>Đổi trạng thái: rewwork]
    
    G2 --> H[FRM_NHAPLAING<br/>Lọc danh sách rewwork]
    
    H -->|Phần OK| I1[Nhập trở lại Kho Core<br/>Cộng dồn StockTP]
    H -->|Phần NG| I2[Xác nhận xử lý bất thường<br/>In phiếu giao QC / Phế phẩm]

    style A fill:#f9f,stroke:#333,stroke-width:2px
    style B fill:#bbf,stroke:#333,stroke-width:2px
    style C1 fill:#bfb,stroke:#333,stroke-width:2px
    style C2 fill:#fbb,stroke:#333,stroke-width:2px
