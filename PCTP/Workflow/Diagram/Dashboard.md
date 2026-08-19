# Tài Liệu Thiết Kế: Màn Hình Chính & Trung Tâm Điều Hành Kho (Dashboard)

> **Vị trí trong bộ tài liệu:** Đây là bản trực quan hoá (UI) của `WORKFLOW_WMS.md` (luồng nghiệp vụ) và tài liệu kiến trúc phân chia 4 phân khu (`WORKFLOW_DEPEND.md`). Tài liệu này không định nghĩa lại luồng hay phụ thuộc mà chỉ mô tả cách hiển thị trực quan những gì các tài liệu trước đã định nghĩa.

---

## 1. Cấu Trúc Màn Hình Chính (Shell Dashboard Layout)

Giao diện `TrungTamDieuHanhKho` được thiết kế theo nguyên tắc 1-1 với 4 Subsystems chính, giúp người dùng ngay khi mở ứng dụng có thể nắm bắt nhanh tình trạng công việc thông qua các con số thống kê (badge) thực tế từ cơ sở dữ liệu.

```text
┌─────────────────────────────────────────────────────────────────┐
│  TRUNG TÂM ĐIỀU HÀNH KHO          [Người dùng: ...]  [Ngày: ...] │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌───────┐│
│  │ 📥 NHẬP KHO   │  │ 📤 XUẤT KHO   │  │ ⚠️ XỬ LÝ LỖI  │  │ 🏭 KHO ││
│  │              │  │              │  │              │  │ CORE  ││
│  │ Chờ nhập: 12 │  │ Chờ giao: 8  │  │ Chờ QC ĐH: 3 │  │       ││
│  │ [Vào module] │  │ Chờ xác      │  │ Đang Rework: 5│  │Bản đồ ││
│  └──────────────┘  │ nhận: 4      │  │ Chờ NC cuối: 2│  │kho vật││
│                     │ [Vào module] │  │ [Vào module] │  │lý     ││
│                     └──────────────┘  └──────────────┘  └───────┘│
│                                                                   │
│  ─────────────────── BẠN ĐANG Ở ĐÂU? ─────────────────────────  │
│  [Sơ đồ luồng trực quan — highlight đúng bước hiện tại]         │
│                                                                   │
│  ─────────────────── TRA CỨU & BÁO CÁO NHANH ──────────────────  │
│  [🔍 Tra cứu QR / Lệnh / Mã Lot]     [📊 Báo cáo tồn kho / Xuất nhập]│
└─────────────────────────────────────────────────────────────────┘

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