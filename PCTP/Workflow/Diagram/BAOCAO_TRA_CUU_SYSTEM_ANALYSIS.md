# PHÂN TÍCH THIẾT KẾ HỆ THỐNG — BAO CÁO & TRA CỨU

## 1. Mục tiêu

`BaoCao` là bounded module dành cho **read-only reporting, lookup và traceability** của PCTP.

Mục tiêu của module không phải gom tất cả file có tên `Report`, mà là gom **các use case đọc dữ liệu xuyên nhiều bounded module** và cung cấp read model ổn định cho UI.

Baseline bắt buộc:

- .NET Framework 4.7.2.
- C# 7.3.
- Không làm thay đổi `master`.
- Không để BaoCao ghi dữ liệu nghiệp vụ.
- Không để BaoCao phụ thuộc vào WinForms của module khác.
- Không di chuyển transaction/business rule chỉ vì nó đang được dùng cho báo cáo.

---

## 2. Ranh giới bounded context

```text
+-------------------+---------------------------------------------+
| Module            | Quyền sở hữu                                |
+-------------------+---------------------------------------------+
| NhapKho           | nghiệp vụ nhập kho                          |
| XuatKho           | nghiệp vụ xuất kho                          |
| GiaoHangKhach     | nghiệp vụ giao hàng                         |
| XuLyHangLoi       | NG / Rework / Giao bù / trả hàng            |
| KhoCore           | stock, slot, inspection và stock movement   |
| KhoVatLy          | UI/legacy shell của warehouse                |
| BaoCao            | query/read model/report/traceability        |
+-------------------+---------------------------------------------+
```

Nguyên tắc: **write ownership ở module nghiệp vụ; read composition ở BaoCao**.

---

## 3. Kết quả rà soát thực tế hiện tại

### 3.1. `FormStockHistory` là legacy reporting UI thật sự

File hiện tại:

`Modules/KhoVatLy/FormStockHistory.cs`

Namespace hiện tại:

`PCTP.VIEWSTOCK.ViewForm`

Chức năng:

1. Tra lịch sử nhập/xuất bằng stored procedure `sp_GetStockHistory`.
2. Lọc theo FromDate, ToDate, ItemCode.
3. Lấy danh sách ItemCode trực tiếp từ `StockHistory`.
4. Xem tồn hiện tại bằng `sp_GetCurrentStockStatus`.
5. Export Excel.
6. Print preview.

Đây là **ứng viên MOVE** sang `BaoCao.UI`, nhưng chỉ sau khi dựng query port/read model tương đương.

Không được copy nguyên form vào BaoCao vì form đang chứa SQL, connection và mapping UI.

### 3.2. `StockHistoryRepository` là write-side, không phải repository báo cáo

`Modules/KhoCore/Repositories/StockHistoryRepository.cs` hiện ghi vào `StockHistory` với các trường:

- ActionType
- ItemCode
- TemCode
- LotNo
- Quantity
- Date
- FromSlotId
- ToSlotId
- QrData
- MaPhieu
- PerformedBy

Repository này phải tiếp tục thuộc write-side của KhoCore. BaoCao chỉ đọc bảng/history thông qua query infrastructure.

### 3.3. `StockExportHistoryRepository` đang dùng history của KhoCore

`Modules/XuatKho/Repositories/StockExportHistoryRepository.cs` gọi `IStockHistoryRepository.SaveHistory(...)` và có kiểm tra idempotency theo `ActionType + MaPhieu`.

Do đó **không tạo thêm bảng StockHistory thứ hai trong BaoCao**.

BaoCao đọc source hiện có.

### 3.4. `IPhieuTrackingRepository` đang trộn read và write

File:

`Infrastructure/Repositories/IPhieuTrackingRepository.cs`

Đang chứa cả:

- `GetPhieuTheoLot`
- `GetTongSlActiveTheoLot`
- `ExistsQrData`
- `InsertPhieuMoi`
- `CapNhatTrangThai`
- `CapNhatQuantity`

Đây là một điểm kiến trúc cần xử lý.

Kế hoạch:

```text
IPhieuTrackingRepository
        |
        +-- write methods  -> owner nghiệp vụ tương ứng
        |
        +-- read methods   -> query port của BaoCao nếu dùng cho traceability
```

Không đổi ngay trong cùng commit với UI để tránh phá dependency đang chạy.

### 3.5. `PhieuTrackingRepository` thực tế đang thuộc namespace legacy

`Infrastructure/Repositories/PhieuTrackingRepository.cs` có namespace:

`PCTP.VIEWSTOCK.Repository`

Nhưng nó truy vấn dữ liệu core:

- `SlotLot`
- `Slot`
- `Rack`
- `Warehouse`

Use case `GetPhieuTheoLot` là query/traceability, phù hợp với BaoCao về mặt responsibility. Tuy nhiên repository này còn chứa write operation nên cần tách từng phần trước khi xóa namespace legacy.

### 3.6. `StockTpLookupService` không phải báo cáo tổng hợp

`Modules/NhapKho/Services/StockTpLookupService.cs` cung cấp:

- GetByLot
- GetTonKhoHienTai
- GetTonKhoTheoLot

Nó đang phục vụ nghiệp vụ/lookup nội bộ của NhapKho và repository `StockTP`.

Phân loại hiện tại: **KEEP**.

Không di chuyển service này chỉ vì có chữ `Lookup`.

BaoCao sẽ có read model riêng cho màn hình báo cáo/tồn kho, tránh coupling vào `StockItem` của legacy.

### 3.7. `StockTPRepository` đang chứa cả command và query

Repository hiện vừa:

- insert/update STOCKTP;
- đọc theo LOT;
- đọc tồn hiện tại;
- đọc tồn batch.

Đây là repository nghiệp vụ NhapKho/stock transition. BaoCao không được gọi trực tiếp repository này ở tầng UI.

Sau này nếu cần, BaoCao có query repository riêng đọc `STOCKTP` với DTO read-only.

### 3.8. `InspectionLogRepository` đã có query history có giá trị

`Modules/KhoCore/Repositories/InspectionLogRepository.cs` có:

- `GetByInspectionCode`
- `GetHistoryMaster`
- `GetHistoryDetail`
- `SaveLog`

`SaveLog` thuộc KhoCore.

`GetHistoryMaster/Detail` có thể được adapter thành read query của BaoCao.

Đặc biệt `GetHistoryMaster` đã có filter:

- CheckedAt range
- ItemCode
- FinalResult

và tổng hợp Pass/Fail/TotalBox.

Kết luận: **write giữ KhoCore; history UI/query composition có thể MOVE sang BaoCao**.

---

## 4. Báo cáo hiện hữu: phân loại không được di chuyển máy móc

### GiaoHangKhach — KEEP

Các report hiện hữu trong `Modules/GiaoHangKhach/Reports`:

- `GHEPLOT_YMVN`
- `GHEPLOT`
- `IN_GHP_LOT_YMVN`
- `IN_GHP_LOT`
- `PHIEUGIAOHANG_EXP`
- `Report`
- `rpPhieuGiaoHang`
- `rpPhieuGiaoHangYAM`
- `temphieugh`
- `YMVN`
- `YMVN_PGH`

Đây chủ yếu là **operational document/report của giao hàng**, phải tiếp tục nằm cùng module sở hữu nghiệp vụ.

### XuLyHangLoi — KEEP

- `NG_DETAIL`
- `RpPhieuXacNhanPhuTungLoi`
- `RpPhieuXuLyBatThuong`

Đây là tài liệu/phiếu phục vụ xử lý bất thường. Giữ trong XuLyHangLoi.

### KhoVatLy / NhapKho — KEEP transaction document, MOVE history UI

- `Report/RpInNhapKho` và `Modules/KhoVatLy/Report/RpInNhapKho`: phiếu nghiệp vụ nhập kho -> KEEP.
- `FormStockHistory`: lịch sử/tồn -> MOVE sang BaoCao sau khi thay query.
- `FormInspectionHistory`: history inspection -> UI có thể MOVE sang BaoCao; write/query ownership của InspectionLog vẫn ở KhoCore.

---

## 5. Read model chuẩn của BaoCao

Không dùng domain entity của các module làm output của BaoCao.

### 5.1. ItemHistoryRow

Read model đã tạo cần đại diện cho timeline xuyên module:

```text
QR / Tem
  -> LOT
  -> QC / Inspection
  -> Nhập kho
  -> Slot
  -> Xuất kho
  -> Phiếu giao
  -> Khách hàng
```

Các trường hiện tại là baseline, nhưng cần mở rộng sau inventory DB:

- EventId / SourceId
- EventTime
- EventType
- QRCode
- TemCode
- PartNo / ItemCode
- LotNo
- DocumentNo
- SourceModule
- Quantity
- UserName
- LocationCode
- CustomerNo
- Status

Không được ép mọi source có cùng schema nghiệp vụ; adapter mapping mới là nơi chuẩn hóa.

### 5.2. Các read model nên tách theo use case

```text
ItemHistoryRow
StockHistoryRow
CurrentStockRow
ReceivingHistoryRow
ExportHistoryRow
DeliveryHistoryRow
QualityHistoryRow
```

`ItemHistoryRow` chỉ là timeline projection; không nên biến nó thành một God DTO chứa mọi cột của hệ thống.

---

## 6. Query architecture mục tiêu

```text
WinForms / BaoCao UI
        |
        v
BaoCao Application
        |
        +--> IItemHistoryQuery
        +--> IStockHistoryQuery
        +--> ICurrentStockQuery
        +--> IReceivingHistoryQuery
        +--> IExportHistoryQuery
        +--> IDeliveryHistoryQuery
        +--> IQualityHistoryQuery
        |
        v
BaoCao Infrastructure.Query
        |
        +--> SQL / read repository / adapter
        |
        +--> StockHistory
        +--> STOCKTP
        +--> SlotLot / Slot / Rack / Warehouse
        +--> InspectionLog
        +--> delivery/receiving sources
```

UI không được biết:

- connection string;
- SQL text;
- stored procedure name;
- domain entity của module khác.

---

## 7. Main_APP hiện tại: chưa được xóa legacy ngay

`Shell/Main_APP.cs` hiện có legacy using:

```csharp
using PCTP.VIEWSTOCK;
using PCTP.VIEWSTOCK.Repository;
```

File cũng đang làm nhiều dashboard/query công việc khác.

Quy tắc migration:

```text
Main_APP
  |
  +-- dashboard operational -> giữ phần dashboard cần thiết
  |
  +-- legacy history/lookup -> redirect sang BaoCao
  |
  +-- SQL trực tiếp          -> loại bỏ từng use case sau parity check
```

Không xóa hai using trên chỉ dựa vào tên. Phải xác định từng symbol/caller trước để tránh làm hỏng các module hiện đang dùng namespace legacy.

---

## 8. Migration matrix ban đầu

| Thành phần | Owner hiện tại | Desired | Action |
|---|---|---|---|
| FormStockHistory | VIEWSTOCK/KhoVatLy UI | BaoCao UI | MOVE |
| FormStockHistory.Designer/resx | VIEWSTOCK | BaoCao UI | MOVE/REBUILD |
| sp_GetStockHistory query | DB legacy | BaoCao Infrastructure | ADAPTER/REIMPLEMENT |
| sp_GetCurrentStockStatus | DB legacy | BaoCao Infrastructure | ADAPTER/REIMPLEMENT |
| StockHistoryRepository.SaveHistory | KhoCore | KhoCore | KEEP |
| StockExportHistoryRepository | XuatKho | XuatKho | KEEP |
| PhieuTrackingRepository read | VIEWSTOCK | BaoCao query | EXTRACT |
| PhieuTrackingRepository write | VIEWSTOCK | nghiệp vụ owner | EXTRACT/KEEP TRANSITIONAL |
| StockTpLookupService | NhapKho | NhapKho | KEEP |
| StockTPRepository command | NhapKho | NhapKho | KEEP |
| StockTPRepository reporting query | NhapKho | BaoCao query | FUTURE EXTRACT |
| InspectionLogRepository.SaveLog | KhoCore | KhoCore | KEEP |
| InspectionLogRepository history query | KhoCore | BaoCao query port | ADAPTER |
| GiaoHangKhach Reports | GiaoHangKhach | GiaoHangKhach | KEEP |
| XuLyHangLoi Reports | XuLyHangLoi | XuLyHangLoi | KEEP |
| RpInNhapKho | NhapKho/KhoVatLy | NhapKho | KEEP |
| FormInspectionHistory | KhoVatLy | BaoCao UI | MOVE AFTER PARITY |
| legacy VIEWSTOCK namespace | legacy | remove gradually | DECOMPOSE |
| backup presenter | Presentation/_backup | none | DELETE |

---

## 9. Không được làm

1. Không tạo `BaoCaoRepository` rồi gọi toàn bộ repository của các module khác.
2. Không đưa `LotInfo`, `StockItem`, `PhieuXuLyBatThuong`, `PhieuGiao...` vào read model của BaoCao.
3. Không chuyển transaction write sang BaoCao.
4. Không xóa report chỉ vì nằm trong thư mục `Reports`.
5. Không xóa `VIEWSTOCK` namespace một lần khi chưa tách read/write.
6. Không đưa SQL của `Main_APP` vào `BaoCao` một cách nguyên xi.
7. Không tạo một `ItemHistoryRow` chứa tất cả nghiệp vụ của hệ thống.

---

## 10. Thứ tự triển khai bắt buộc

### Phase A — Inventory / boundary

- hoàn tất danh sách source/caller;
- xác định từng query và write method;
- xác định bảng/stored procedure thực tế;
- khóa ownership.

### Phase B — Query ports

Tách contract theo use case, không dùng một repository God interface.

### Phase C — Query infrastructure

Implement read-only SQL/adapters và mapping sang read model.

### Phase D — BaoCao UI

Dựng:

- Tra cứu QR/LOT/Part/Document;
- Timeline;
- Lịch sử kho;
- Tồn hiện tại;
- Lịch sử kiểm tra/QC;
- Lịch sử xuất/giao;
- Export/print.

### Phase E — Redirect Main_APP

Main_APP chỉ điều hướng tới BaoCao; không giữ query implementation.

### Phase F — Remove legacy

Chỉ xóa implementation sau khi:

```text
old query == new query
old UI == new UI behavior
build OK
runtime OK
regression OK
```

---

## 11. Tiêu chí hoàn thành module

BaoCao chỉ được xem là hoàn thành khi:

- [ ] Không có write transaction.
- [ ] Không phụ thuộc UI module khác.
- [ ] Không trả domain entity của module khác.
- [ ] Query contracts được tách theo use case.
- [ ] QR/LOT/Part/Document lookup hoạt động.
- [ ] Timeline xuyên module hoạt động.
- [ ] Stock history/current stock parity với legacy.
- [ ] Inspection history parity với legacy.
- [ ] Delivery/export/receiving history parity.
- [ ] Main_APP không còn SQL lookup/report legacy cho các use case đã migrate.
- [ ] `VIEWSTOCK` chỉ còn phần chưa được phân rã; cuối cùng namespace này có thể loại bỏ.
- [ ] Build net472/C#7.3 thành công trên môi trường phát triển thực tế.
- [ ] Không thay đổi `master` cho tới khi branch ổn định.
