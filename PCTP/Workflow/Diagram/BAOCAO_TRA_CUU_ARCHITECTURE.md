# BAOCAO / TRA CỨU — ARCHITECTURE

## 1. Mục tiêu

`BaoCao` là module **read-only** chuyên:

- Báo cáo nghiệp vụ.
- Tra cứu mã hàng / QR / LOT.
- Tra cứu lịch sử nhập kho, xuất kho, giao hàng.
- Truy vết vòng đời của một mã hàng.
- Xuất dữ liệu/report phục vụ vận hành và audit.

Module này **không sở hữu nghiệp vụ ghi** của Nhập Kho, Xuất Kho, Giao Hàng Khách hoặc Xử Lý Hàng Lỗi.

## 2. Nguyên tắc ownership

```text
NhapKho       ── owns ──> receiving transaction
XuatKho       ── owns ──> stock export transaction
GiaoHangKhach ── owns ──> delivery transaction
XuLyHangLoi   ── owns ──> NG / Rework / GiaoBu transaction
KhoCore       ── owns ──> stock / slot / stock movement
BaoCao        ── reads ──> reporting projections / query contracts
```

`BaoCao` không được:

- gọi trực tiếp Form của module khác;
- cập nhật `STOCKTP`, Slot hoặc chứng từ nghiệp vụ;
- chứa rule Pick/FIFO/nhập/xuất/giao hàng;
- trở thành nơi tập trung lại repository transaction của các module.

## 3. Dependency rule

```text
                    ┌─────────────────────┐
                    │      BaoCao         │
                    │ Query / Report /    │
                    │ Traceability        │
                    └──────────┬──────────┘
                               │ READ ONLY
              ┌────────────────┼────────────────┐
              ▼                ▼                ▼
          KhoCore          NhapKho          XuatKho
          history/stock    receiving        export
                               │                │
                               └──────┬─────────┘
                                      ▼
                                GiaoHangKhach
                                      │
                                      ▼
                                XuLyHangLoi
```

Ở mức code, ưu tiên query contract/read model thay vì phụ thuộc vào UI hoặc domain entity của module khác.

## 4. Query model

Query chính:

- `SearchItem` — tìm QR/LOT/Part/Document.
- `GetItemHistory` — timeline của một item.
- `GetStockHistory` — biến động tồn.
- `GetReceivingHistory` — lịch sử nhập.
- `GetExportHistory` — lịch sử xuất.
- `GetDeliveryHistory` — lịch sử giao.
- `GetQualityHistory` — NG/Rework/QC.

Read model chuẩn:

```text
ItemHistoryRow
  - QrCode
  - PartNo
  - LotNo
  - EventTime
  - EventType
  - DocumentNo
  - SourceModule
  - Quantity
  - UserName
  - LocationCode
  - CustomerNo
  - Status
```

## 5. Traceability timeline

```text
QR
 ↓
LOT
 ↓
QC / NG / Rework (nếu có)
 ↓
Nhập kho
 ↓
Vị trí / Slot
 ↓
Xuất kho
 ↓
Phiếu giao
 ↓
Khách hàng
```

Một item có thể đi qua nhiều nhánh. Query layer phải hợp nhất thành timeline theo `EventTime`, không làm thay đổi dữ liệu nguồn.

## 6. UI ownership

UI của module đặt dưới:

```text
PCTP/Modules/BaoCao/UI
```

Shell `Main_APP` chỉ có trách nhiệm **navigation** tới module. Không đặt SQL/query nghiệp vụ tra cứu trong `Main_APP`.

## 7. Legacy migration rule

Mọi chức năng tra cứu legacy xử lý theo thứ tự:

```text
1. Identify legacy owner
2. Define BaoCao query contract
3. Implement new read model/query
4. Redirect Main_APP caller
5. Verify result parity
6. Remove legacy implementation
7. Remove obsolete using/reference
```

Không xóa code legacy nếu chưa xác định caller và chưa có replacement tương đương.

## 8. Report ownership

Các report dùng để **in chứng từ của một nghiệp vụ đang sở hữu** có thể tiếp tục nằm trong module nghiệp vụ.

Ví dụ:

```text
GiaoHangKhach/Reports  -> report chứng từ giao hàng
XuLyHangLoi/Reports    -> report chứng từ xử lý hàng lỗi
KhoVatLy/Report        -> report vận hành kho
```

`BaoCao` chỉ nhận ownership của:

- báo cáo tổng hợp;
- báo cáo lịch sử;
- báo cáo truy vết;
- báo cáo tìm kiếm/đối soát;
- báo cáo cross-module.

Không gom tất cả report in chứng từ vào `BaoCao` một cách máy móc.

## 9. Security / consistency

`BaoCao` phải tôn trọng quyền truy cập hiện tại của ứng dụng. Query chỉ đọc dữ liệu mà user được phép xem.

Các query lớn phải có:

- giới hạn khoảng thời gian;
- giới hạn số dòng;
- filter theo khóa tìm kiếm khi có thể;
- không load toàn bộ lịch sử vào memory nếu không cần.

## 10. Kết luận

`BaoCao` là **Reporting & Traceability bounded module**, không phải một business transaction module mới.

Mục tiêu cuối cùng:

```text
Main_APP
   │
   └── Navigation ──> BaoCao
                         │
                         ├── Search
                         ├── History
                         ├── Stock report
                         ├── Delivery report
                         └── Traceability
```
