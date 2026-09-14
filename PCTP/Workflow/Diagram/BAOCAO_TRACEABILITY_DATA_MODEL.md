# BAOCAO — TRACEABILITY DATA MODEL

## 1. Mục đích

Tài liệu này chốt mô hình truy xuất cho use case:

> Khách hàng yêu cầu truy vết một LOT / thùng hàng đã giao → phải tìm được lịch sử giao hàng, QRCode từng thùng, LOT + số lượng, thông tin nhập kho, sản xuất, QC và NG/Rework nếu có.

Nguồn bằng chứng giao hàng chính là `LuuPhieuGiaoHang`.

`BaoCao` chỉ đọc dữ liệu; không sở hữu transaction giao hàng.

---

## 2. Hai loại thông tin được đọc trong quy trình giao hàng

Quy trình thực tế có bước:

```text
Đọc tem FCC
      ↓
Đọc tem khách hàng
      ↓
Đối chiếu / kiểm tra
      ↓
Giao hàng
      ↓
LuuPhieuGiaoHang
```

Vì vậy BaoCao phải cho phép truy vấn cả:

- FCC QRCode;
- thông tin tem khách hàng;
- LOT;
- PartNo;
- mã/tên khách hàng;
- phiếu giao;
- khoảng thời gian giao.

Không được giả định tem khách hàng chính là LOT hoặc PartNo. Phải map đúng source field sau khi inventory schema thực tế.

---

## 3. QRCode là định danh cấp thùng

Quy ước nghiệp vụ cần giữ nguyên trong read model:

```text
1 QRCode = 1 thùng / carton hàng đã được đọc khi giao
```

Vì vậy một delivery có nhiều QR phải trả về nhiều carton riêng biệt.

Mô hình:

```text
Delivery
 ├─ QR-001 / carton 001
 │    ├─ LOT-A / qty 100
 │    └─ LOT-B / qty  50
 ├─ QR-002 / carton 002
 │    └─ LOT-A / qty  80
 └─ QR-003 / carton 003
      ├─ LOT-C / qty 120
      └─ LOT-D / qty  40
```

Không được group toàn bộ QR thành một dòng delivery nếu làm mất khả năng truy vết từng thùng.

---

## 4. `LuuPhieuGiaoHang.LotNo` là composite field

Dữ liệu hiện tại có thể lưu nhiều LOT trong cùng một chuỗi, ví dụ:

```text
LOT001-100,LOT002-80,LOT003-50
```

Hiểu là:

```text
LOT001 → 100
LOT002 → 80
LOT003 → 50
```

Đây là legacy storage format, không phải một LOT duy nhất.

BaoCao phải chuẩn hóa thành read model:

```text
DeliveryLotTrace
-----------------
DeliveryId
QRCode
LotNo
Quantity
```

Ví dụ:

```text
Source:
QRCode = QR-A001
LotNo  = LOT001-100,LOT002-80

Read model:
QR-A001 | LOT001 | 100
QR-A001 | LOT002 |  80
```

Parser phải xử lý:

- null;
- empty;
- whitespace;
- nhiều dấu phân cách theo dữ liệu thực tế;
- token không hợp lệ.

Token không parse được không được tự động biến thành `Quantity = 0`; phải giữ trạng thái lỗi để có thể audit dữ liệu giao hàng.

---

## 5. Quan hệ traceability bắt buộc

```text
Customer
   ↑
Delivery document
   ↑
QRCode / Carton
   ↑
LOT + Quantity
   ↑
Warehouse receipt / stock movement
   ↑
Production
   ↑
QC / Inspection
   ↑
NG / Rework (nếu có)
```

Đây là quan hệ read-only để truy xuất. Không có module nào trong BaoCao được phép cập nhật các node này.

---

## 6. Các hướng tra cứu

### 6.1. QRCode

```text
QRCode
 → carton
 → LOT + Quantity
 → delivery
 → customer
 → warehouse
 → production
 → QC / NG / Rework
```

### 6.2. LOT

```text
LOT
 → các QRCode chứa LOT
 → các delivery
 → customer
 → warehouse
 → production
 → QC / NG / Rework
```

### 6.3. PartNo

```text
PartNo
 → LOT
 → QRCode / carton
 → delivery
 → customer
```

### 6.4. Tên khách hàng

```text
CustomerName
 → các delivery của customer
 → QRCode / carton
 → LOT + Quantity
 → warehouse / production / QC
```

Tra cứu theo tên khách hàng là một use case chính, không phải filter phụ.

UI nên hỗ trợ autocomplete / tìm gần đúng theo tên hoặc mã khách hàng nếu nguồn customer master cho phép.

### 6.5. Tem khách hàng

```text
Customer label data
 → delivery/carton
 → FCC QRCode
 → LOT
 → customer
```

Field thực tế của tem khách hàng phải được xác định từ code/schema trước khi triển khai SQL.

---

## 7. Read model đề xuất

### DeliveryTrace

```csharp
public sealed class DeliveryTrace
{
    public string DeliveryId { get; set; }
    public string DocumentNo { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string CustomerCode { get; set; }
    public string CustomerName { get; set; }
    public string PartNo { get; set; }
    public string QRCode { get; set; }
    public string CustomerLabelData { get; set; }
    public string Status { get; set; }
}
```

`DeliveryTrace` là master cấp phiếu/thùng; không dùng nó để nhét một chuỗi composite LOT.

### DeliveryLotTrace

```csharp
public sealed class DeliveryLotTrace
{
    public string DeliveryId { get; set; }
    public string QRCode { get; set; }
    public string LotNo { get; set; }
    public decimal Quantity { get; set; }
}
```

Mỗi LOT trong một QR phải là một record riêng.

---

## 8. Query contracts mục tiêu

Không tạo một `BaoCaoRepository` God interface.

Nên tách:

```text
IQrTraceQuery
ILotTraceQuery
ICustomerDeliveryQuery
IDeliveryHistoryQuery
IReceivingHistoryQuery
IExportHistoryQuery
IProductionTraceQuery
IQualityTraceQuery
INgReworkTraceQuery
```

Các query có thể được compose bởi Application layer để tạo màn hình traceability.

---

## 9. Cardinality — yêu cầu bắt buộc

Không được dùng một SQL join phẳng kiểu:

```text
Delivery
JOIN QR
JOIN LOT
JOIN Production
JOIN QC
JOIN Stock
```

rồi trả trực tiếp toàn bộ kết quả cho grid master, vì sẽ nhân bản delivery row.

Cardinality đúng:

```text
Delivery 1 → N QRCode
QRCode   1 → N LOT
LOT      1 → N Events
```

BaoCao phải assemble theo từng cấp:

```text
Delivery master
    ↓
Carton/QRCode detail
    ↓
LOT detail
    ↓
Production / Warehouse / QC / NG-Rework timeline
```

Đây là điều kiện bắt buộc để kết quả truy xuất có thể dùng cho khiếu nại khách hàng.

---

## 10. Customer lookup

Tên khách hàng phải có thể dùng làm điểm bắt đầu truy xuất:

```text
CustomerName
 ↓
Delivery list
 ↓
QRCode/carton list
 ↓
LOT list
 ↓
Production / Warehouse / QC
```

Không copy customer master sang BaoCao nếu không cần thiết. Query adapter đọc source customer chuẩn và map thành `CustomerCode` / `CustomerName`.

Nếu một delivery lưu customer code nhưng tên nằm ở master khác, join customer chỉ ở read infrastructure; UI không biết cách join.

---

## 11. Schema inventory bắt buộc trước khi code SQL

Trước khi implement `IQrTraceQuery` / `ILotTraceQuery`, phải xác định chính xác:

1. Tên bảng `LuuPhieuGiaoHang` và schema/database.
2. Primary key của record lưu giao hàng.
3. Field QRCode.
4. Field LOT composite.
5. Field số lượng nếu có ngoài chuỗi LOT.
6. Field mã khách hàng.
7. Nguồn tên khách hàng.
8. Field ngày/giờ giao.
9. Field mã phiếu giao.
10. Field PartNo/ItemCode.
11. Field tem khách hàng.
12. Khóa liên kết từ QR → LOT / stock / production nếu tồn tại.
13. Khóa liên kết từ LOT → production.
14. Khóa liên kết LOT → receiving/stock history.
15. Khóa liên kết LOT → QC/Inspection.
16. Khóa liên kết LOT → NG/Rework.

**Không đoán tên cột.** Chỉ sau bước này mới viết SQL adapter.

---

## 12. UI BaoCao mục tiêu

Màn hình chính:

```text
+--------------------------------------------------------------+
| TRA CỨU TRUY XUẤT QR / LOT / KHÁCH HÀNG                    |
+--------------------------------------------------------------+
| QRCode       [................]                              |
| LOT          [................]                              |
| PartNo       [................]                              |
| Khách hàng   [................]                              |
| Tem KH       [................]                              |
| Từ ngày      [....]  Đến ngày [....]                        |
|                         [TÌM KIẾM]                           |
+--------------------------------------------------------------+
| Delivery / Customer / QR / LOT master                       |
+--------------------------------------------------------------+
| Chi tiết thùng (QRCode)                                     |
| LOT | Quantity | PartNo | Tem KH                            |
+--------------------------------------------------------------+
| Timeline: Production → QC → Nhập kho → Xuất → Giao          |
+--------------------------------------------------------------+
```

Khi chọn một QR:

```text
QR
 ├─ thông tin thùng
 ├─ tem khách hàng
 ├─ LOT + quantity
 ├─ delivery/customer
 └─ timeline
```

Khi chọn một LOT:

```text
LOT
 ├─ các QR/thùng đã chứa LOT
 ├─ các lần nhập kho
 ├─ sản xuất
 ├─ QC
 ├─ NG/Rework
 └─ các lần giao + khách hàng
```

Khi chọn khách hàng:

```text
Customer
 ├─ delivery history
 ├─ carton/QRCode
 ├─ LOT
 └─ production/warehouse/QC trace
```

---

## 13. CLEAN AFTER MOVE

`LuuPhieuGiaoHang` và các code transaction của `GiaoHangKhach` vẫn thuộc `GiaoHangKhach`.

BaoCao chỉ nhận quyền sở hữu **read use case**.

Không xóa operational delivery form/report chỉ vì BaoCao đã có DeliveryTrace.

Chỉ sau khi caller của một legacy history/lookup đã chuyển sang query contract mới thì mới xóa:

- UI cũ;
- designer/resource;
- compile entry;
- using cũ;
- repository/intermediary chỉ còn phục vụ read use case đó.
