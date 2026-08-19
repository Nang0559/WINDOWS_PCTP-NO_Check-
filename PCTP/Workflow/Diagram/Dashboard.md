# Tài Liệu Thiết Kế: Màn Hình Chính & Trung Tâm Điều Hành Kho (Dashboard)

> **Vị trí trong bộ tài liệu:** Đây là bản trực quan hoá giao diện (UI) dựa trên luồng nghiệp vụ tổng thể tại `WORKFLOW_WMS.md` và ranh giới 4 phân khu tại `ARCHITECTURE_DEPENDENCIES.md`. Tài liệu này mô tả cách hiển thị trạng thái vận hành lên màn hình chính.

---

## 1. Cấu Trúc Màn Hình Chính (Shell Dashboard Layout)

Màn hình `TrungTamDieuHanhKho` được chia thành các khu vực chính ánh xạ 1-1 với 4 Subsystems, giúp người dùng nắm bắt công việc qua các badge số liệu thực tế:

* **Khu vực module nghiệp vụ (4 Phân khu chính):**
  * **📥 Nhập Kho:** Hiển thị badge *Chờ nhập*. Bấm vào để vào module Nhập TP.
  * **📤 Xuất Kho:** Hiển thị badge *Chờ giao* và *Chờ xác nhận*. Bấm vào để vào module Quản lý Xuất.
  * **⚠️ Xử Lý Lỗi:** Hiển thị badge *Chờ QC định hướng*, *Đang Rework*, *Chờ QC xác nhận cuối*. Bấm vào để mở QTChung / Phiếu bất thường.
  * **🏭 Kho Core:** Hiển thị bản đồ kho vật lý 2D / Không gian kho.
* **Khu vực tra cứu & báo cáo xuyên suốt (Cross-cutting):**
  * Ô tra cứu nhanh đa năng (Quét QR / Lệnh / Mã Lot).
  * Widget báo cáo tồn kho & lịch sử giao dịch nhanh.

> ⚠️ **Lưu ý kiến trúc:** Khu vực *"Tra cứu & Báo cáo"* **không** phải là phân khu thứ 5, mà là tính năng xuyên suốt đọc dữ liệu từ cả 4 phân khu.

---

## 2. Quy Định Điều Hướng Module

Khi người dùng thao tác trên màn hình chính, hệ thống điều hướng đến các màn hình chi tiết tương ứng:
* **Click Nhập Kho** $\rightarrow$ Mở màn hình Quản lý Nhập TP.
* **Click Xuất Kho** $\rightarrow$ Mở màn hình Quản lý Xuất & Chờ Giao.
* **Click Xử Lý Lỗi** $\rightarrow$ Mở màn hình QTChung & Quản lý Phiếu Bất Thường.
* **Click Kho Core** $\rightarrow$ Mở bản đồ không gian kho 2D.
* **Quét mã QR / Tra cứu** $\rightarrow$ Tự động nhận diện pattern và hiển thị kết quả tức thời.

---

## 3. Sơ Đồ Điều Hướng (Mermaid Diagram)

Sơ đồ dưới đây minh họa cấu trúc điều hướng từ màn hình Trung tâm Điều hành Kho đến các module nghiệp vụ tương ứng:

```mermaid
graph LR
    Dashboard([Dashboard]) --> Sub1["📥 Nhập Kho"]
    Dashboard --> Sub2["📤 Xuất Kho"]
    Dashboard --> Sub3["⚠️ Xử Lý Lỗi"]
    Dashboard --> Sub4["🏭 Kho Core"]
    Dashboard -.-> Sub5["🔍 Tra Cứu & Báo Cáo"]

    Sub1 --> Screen1["Quản lý Nhập TP"]
    Sub2 --> Screen2["Quản lý Xuất & Chờ Giao"]
    Sub3 --> Screen3["QTChung & Phiếu Bất Thường"]
    Sub4 --> Screen4["Bản đồ Không gian Kho 2D"]
    Sub5 --> Screen5["Kết quả tra cứu tức thời"]

    style Sub5 stroke-dasharray: 5 5
