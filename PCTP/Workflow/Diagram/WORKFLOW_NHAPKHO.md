```mermaid
graph TD
    Start([BẮT ĐẦU NHẬP KHO]) --> ManageOrder[Quản lý Lệnh Nhập<br/>- Lệnh sản xuất / Lệnh trả hàng<br/>- Danh sách chờ nhập]
    
    ManageOrder --> SelectType{Phân loại nguồn nhập}

    %% CÁC NHÁNH NGUỒN NHẬP
    SelectType -->|1. Hàng mới từ sản xuất| NewGoods[Hàng mới]
    SelectType -->|2. Rework đạt chuẩn| ReworkOK[Rework OK]
    SelectType -->|3. Hàng lỗi / Bất thường| ReworkNG[Rework NG]

    %% XỬ LÝ NHẬN HÀNG & KIỂM TRA TRÙNG
    NewGoods --> CheckDup[Kiểm tra Nhập Trùng<br/>- Check Barcode / QR Code<br/>- Check Lô / Serial đã tồn tại?]
    ReworkOK --> CheckDup

    CheckDup -->|Đã tồn tại / Trùng| ErrorDuplicate[Cảnh báo: Lỗi nhập trùng!<br/>Từ chối / Yêu cầu xác thực lại]
    
    %% PHÂN RẼ HÌNH THỨC NHẬP (CHI TIẾT VS HÀNG LOẠT)
    CheckDup -->|Hợp lệ / Chưa có| SelectMode{Chọn hình thức nhập}

    SelectMode -->|Nhập hàng loạt| DirectA0[Định vị mặc định: Khu vực A0<br/>Không cần chọn Slot]
    SelectMode -->|Nhập chi tiết| Location_Block

    subgraph Location_Block [Định Vị Không Gian Kho Chi Tiết]
        SelectWH[Chọn Kho / Warehouse] --> SelectRack[Chọn Kệ / Rack]
        SelectRack --> SelectSlot[Chọn Ô chứa / Slot]
    end

    %% Rework NG RẼ HƯỚNG RIÊNG
    ReworkNG --> AbnormalFlow[Chuyển về khối Hàng Lỗi / Bất thường<br/>Xử lý phế phẩm / hủy]

    %% CẬP NHẬT KHO CORE & TỒN KHO
    DirectA0 --> UpdateCore[Cập nhật vào KHO CORE<br/>- Gán vị trí A0 / Slot tương ứng<br/>- Cộng dồn tồn kho StockTP]
    SelectSlot --> UpdateCore
    
    UpdateCore --> EndImport([🏁 HOÀN TẤT NHẬP KHO])
    ErrorDuplicate --> EndImport
    AbnormalFlow --> EndImport

    %% STYLING
    style Start fill:#f9f,stroke:#333,stroke-width:2px
    style CheckDup fill:#ff9,stroke:#333,stroke-width:2px
    style SelectMode fill:#bbf,stroke:#333,stroke-width:2px
    style DirectA0 fill:#bfb,stroke:#333,stroke-width:2px
    style UpdateCore fill:#fbb,stroke:#333,stroke-width:2px