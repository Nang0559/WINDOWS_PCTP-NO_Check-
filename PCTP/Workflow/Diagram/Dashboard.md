```mermaid
graph TD
    Dashboard([Trung Tâm Điều Hành Kho - Dashboard]) --> Sub1[📥 Nhập Kho]
    Dashboard --> Sub2[📤 Xuất Kho]
    Dashboard --> Sub3[⚠️ Xử Lý Hàng Lỗi]
    Dashboard --> Sub4[🏭 Kho Core / Bản Đồ]
    Dashboard -.-> Sub5[🔍 Tra Cứu & Báo Cáo Nhanh<br/><i>tính năng xuyên suốt, không phải subsystem</i>]

    Sub1 --> Click1[Click] --> Screen1[Màn hình Quản lý Nhập TP]
    Sub2 --> Click2[Click] --> Screen2[Màn hình Quản lý Xuất & Chờ Giao]
    Sub3 --> Click3[Click] --> Screen3[Màn hình QTChung & Quản lý Phiếu Bất Thường]
    Sub4 --> Click4[Click] --> Screen4[Bản đồ Không gian Kho 2D]
    Sub5 --> Scan5[Quét QR / Click] --> Screen5[Hiển thị kết quả tra cứu tức thời]

    style Sub5 stroke-dasharray: 5 5