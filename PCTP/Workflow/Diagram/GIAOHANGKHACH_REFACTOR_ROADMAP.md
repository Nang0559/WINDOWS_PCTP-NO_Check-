# GIAO HÀNG KHÁCH – KẾ HOẠCH REFACTOR KIẾN TRÚC

## 1. Mục tiêu

Refactor module `GiaoHangKhach` để giảm kích thước và trách nhiệm của:

- `PhieuService`
- `HVN_Presenter`
- `HVN_PGH`

nhưng **không thay đổi nghiệp vụ hiện tại**.

Nguyên tắc:

> Refactor theo từng phase nhỏ, mỗi phase compile/test/commit trước khi sang phase tiếp theo.

---

## 2. Nghiệp vụ đã chốt

### 2.1 IFS

IFS là nguồn đơn hàng gốc/kế hoạch.

```text
IFS
 └── Customer Order
      ├── Part
      ├── Quantity
      ├── Delivery time
      ├── Dock / CUA
      └── ...
```

IFS có thể được sử dụng trực tiếp để tạo phiếu giao.

### 2.2 MilkRun / LoadTuBangRieng

`LoadTuBangRieng = YES` **không có nghĩa là bỏ IFS**.

Ý nghĩa nghiệp vụ:

> Đơn hàng trên IFS là baseline, nhưng do thực tế MilkRun có thể thay đổi nên người dùng cần giao theo đơn thực tế từ bảng riêng.

```text
                 IFS
                  │
                  │ baseline
                  ▼
          ┌───────────────┐
          │   MilkRun     │
          │ actual order  │
          └───────┬───────┘
                  │
              comparison
                  │
        ┌─────────┼─────────┐
        ▼         ▼         ▼
      Thiếu      Khớp      Thừa
```

MilkRun phải có khả năng:

1. Load IFS snapshot.
2. Load actual order từ bảng riêng.
3. So sánh IFS với actual.
4. Ghi nhận thiếu/thừa/lệch.
5. Tiếp tục xử lý giao hàng.

Không được refactor thành một `TableOrder` độc lập với IFS.

### 2.3 Giao đặc biệt / GiaoDB

Giao đặc biệt là một **business scenario độc lập**.

Ví dụ:

- giao mẫu
- hàng thử
- trường hợp không có đơn IFS
- giao hàng phát sinh
- giao hàng liên quan đến xử lý tồn kho

```text
IFS
 └── Không có đơn
        │
        ▼
Upload / Manual
        │
        ▼
TMPPHIEUGIAODBHD
TMPPHIEUGIAOHANGDBCT
        │
        ▼
Build standard order
        │
        ▼
Giao hàng
        │
        ▼
LOT / QR
        │
        ▼
Cập nhật kho
```

### 2.4 GiaoDB không phải MilkRun

```text
MilkRun:
    IFS baseline
      +
    actual MilkRun
      ↓
    comparison

GiaoDB:
    không cần IFS
      ↓
    upload/manual
      ↓
    GiaoDB document
      ↓
    standard order
```

---

## 3. Phân biệt Source và Scenario

Không dùng một enum lớn để biểu diễn tất cả nghiệp vụ.

### 3.1 Source

```csharp
public enum OrderSourceKind
{
    IFS = 1,
    MilkRun = 2,
    GiaoDB = 3
}
```

- `IFS`: đơn từ IFS.
- `MilkRun`: actual order từ bảng riêng/MilkRun.
- `GiaoDB`: dữ liệu từ chứng từ GiaoDB.

`GiaoDB` là source/document data, còn `GiaoDacBiet` là business scenario.

Không tạo:

```csharp
YmvnOrderSource
```

vì YMVN vẫn có thể sử dụng nguồn MilkRun/bảng riêng.

---

## 4. MP / SP

MP và SP là **Order Category**, không phải Source.

```csharp
public enum OrderCategory
{
    MP = 1,
    SP = 2
}
```

Tất cả source đều có thể là MP hoặc SP:

```text
IFS
 ├── MP
 └── SP

MilkRun
 ├── MP
 └── SP

GiaoDB
 ├── MP
 └── SP
```

IFS hiện tại sử dụng `CUA = SUB_DOCK_CODE` để phục vụ phân loại MP/SP.

Table/MilkRun sử dụng dock code/config tương ứng.

GiaoDB sau khi chuyển thành Standard Order phải dùng common MP/SP classification.

---

## 5. QR state

Không gộp:

```csharp
_isMayBanQR
_isBanQR
```

### `_isMayBanQR`

Máy có khả năng scan/đọc QR.

### `_isBanQR`

Session hiện tại đang ở trạng thái giao bằng QR / đang đọc QR.

```text
IsMayBanQR = false
IsBanQR    = false
→ máy không hỗ trợ QR

IsMayBanQR = true
IsBanQR    = false
→ máy có QR nhưng chưa giao bằng QR

IsMayBanQR = true
IsBanQR    = true
→ máy có QR và đang giao bằng QR

IsMayBanQR = false
IsBanQR    = true
→ trạng thái không hợp lệ
```

---

# 6. Kiến trúc đích

```text
                         HVN_PGH
                            │
                       HVN_Presenter
                            │
                       PhieuService
                      (Facade mỏng)
                            │
                ┌───────────┴───────────┐
                │                       │
         PhieuLoadService          Business Services
                │                 ┌─────┼──────┐
                │                 │     │      │
                │                Kho   YMVN  GiaoDB
                │
        ┌───────┼────────┐
        │       │        │
       IFS   MilkRun   GiaoDB
        │       │        │
        │       │        └── Upload / Manual
        │       │
        │       ├── IFS baseline
        │       └── Actual MilkRun
        │                │
        │             Compare
        │
        └──────────┬──────────┘
                   │
             Standard Order
                   │
             MP / SP classify
                   │
              Enrich HOP
                   │
             QR / TMP state
                   │
               DOCQRCODE
                   │
                 LOT
```

---

# 7. PHASE 0 – Chốt behavior hiện tại

## Mục tiêu

Document nghiệp vụ hiện tại, chưa refactor logic.

| Scenario | IFS | Actual source | MP/SP | QR | Kho |
|---|---|---|---|---|---|
| Normal IFS | Có | IFS | Có | Có/không | Có |
| MilkRun | Có | Bảng riêng | Có | Có/không | Có |
| Giao đặc biệt | Không nhất thiết | GiaoDB | Có | Có/không | Có |
| YMVN | Có/bảng riêng | MilkRun | MP/SP | tùy flow | Có |

### Không code.

Commit:

```text
docs(giaohangkhach): document current order flow
```

---

# 8. PHASE 1 – Order Context

Tạo:

```text
Modules/GiaoHangKhach/Domain/Order/
    OrderCategory.cs
    OrderSourceKind.cs
    OrderLoadContext.cs
```

### OrderCategory.cs

```csharp
public enum OrderCategory
{
    MP = 1,
    SP = 2
}
```

### OrderSourceKind.cs

```csharp
public enum OrderSourceKind
{
    IFS = 1,
    MilkRun = 2,
    GiaoDB = 3
}
```

### OrderLoadContext.cs

```csharp
public class OrderLoadContext
{
    public DateTime NgayGiao { get; set; }

    public string NhaMay { get; set; }

    public int AddNm { get; set; }

    public string GioFcc { get; set; }

    public string GioFccMoTa { get; set; }

    public OrderCategory Category { get; set; }

    public OrderSourceKind Source { get; set; }

    public bool IsMayBanQR { get; set; }

    public bool IsBanQR { get; set; }

    public string DockCodeSP { get; set; }

    public IList<string> CheckedGios { get; set; }

    public CustomerConfig Config { get; set; }
}
```

### Quy tắc

Chưa xóa:

```csharp
_isMayBanQR
_isBanQR
_isLoaiSP
```

Chỉ tạo Context và build context từ state hiện tại.

Commit:

```text
refactor(giaohangkhach): add order load context
```

---

# 9. PHASE 2 – Chuẩn hóa MP/SP

Đưa việc phân loại MP/SP về concept chung:

```text
Source
   ↓
Standard Order
   ↓
OrderCategory
```

IFS:

```text
SUB_DOCK_CODE
    ↓
CUA
    ↓
MP/SP
```

MilkRun:

```text
CUA + CustomerConfig
    ↓
MP/SP
```

GiaoDB:

```text
GiaoDB
    ↓
Standard Order
    ↓
Common classifier
```

Không tạo category riêng cho từng source.

Commit:

```text
refactor(giaohangkhach): normalize order category
```

---

# 10. PHASE 3 – Source abstraction

Tạo:

```csharp
public interface IOrderSource
{
    OrderSourceKind SourceKind { get; }

    OrderSourceResult Load(OrderLoadContext context);
}
```

Implement:

```text
IfsOrderSource
MilkRunOrderSource
GiaoDbOrderSource
```

## IfsOrderSource

Chỉ load IFS.

## MilkRunOrderSource

Load:

1. IFS baseline.
2. Actual MilkRun.
3. Difference.

## GiaoDbOrderSource

Load:

1. GiaoDB header/detail.
2. Convert to Standard Order.

Source không:

- publish EventBus
- xử lý UI
- cập nhật kho
- xử lý QR workflow

Commit:

```text
refactor(giaohangkhach): introduce order sources
```

---

# 11. PHASE 4 – QR / TMP Working State

Tách:

```text
Order Source
```

khỏi:

```text
Delivery Working State
```

Tạo service quản lý:

```text
TMPPHIEUGIAOHANG
DOCQRCODE
```

Các operation:

```text
HasQr()
LoadFromQr()
SaveTmp()
DeleteTmp()
DeleteDocQr()
GetCurrentOrder()
GetTrangThaiDangBan()
```

Persistence vẫn do:

```text
PhieuTmpRepository
```

chịu trách nhiệm.

Commit:

```text
refactor(giaohangkhach): extract qr working state
```

---

# 12. PHASE 5 – PhieuLoadService

Tạo:

```csharp
public interface IPhieuLoadService
{
    OrderLoadResult Load(OrderLoadContext context);
}
```

Flow:

```text
Load(context)
     │
     ▼
Resolve Source
     │
 ┌───┼───────────┐
 ▼   ▼           ▼
IFS MilkRun     GiaoDB
 │   │           │
 │   │           └── Manual/Upload
 │   │
 │   ├── IFS baseline
 │   └── Actual
 │
 └──────┬─────────┘
        ▼
Standard Orders
        ▼
MP/SP
        ▼
Enrich HOP
        ▼
QR/TMP working state
        ▼
OrderLoadResult
```

`PhieuLoadService` là orchestration layer.

Source chỉ chịu trách nhiệm source data.

Commit:

```text
refactor(giaohangkhach): extract phieu load service
```

---

# 13. PHASE 6 – OrderLoadResult

Tạo result contract:

```csharp
public class OrderLoadResult
{
    public DataTable Orders { get; set; }

    public bool HasMaNG { get; set; }

    public bool HasDifference { get; set; }

    public OrderSourceKind Source { get; set; }

    public OrderCategory Category { get; set; }
}
```

Sau này MilkRun có thể mở rộng:

```text
IfsOrders
ActualOrders
Missing
Extra
```

Không cần tạo tất cả ngay.

Commit:

```text
refactor(giaohangkhach): introduce order load result
```

---

# 14. PHASE 7 – Split PhieuService

Đích:

```text
PhieuService
    │
    ├── LoadPhieu()
    │      ↓
    │   PhieuLoadService
    │
    ├── CapNhapKho()
    │      ↓
    │   PhieuKhoService
    │
    ├── GiaoDB()
    │      ↓
    │   PhieuGiaoDbService
    │
    └── YMVN()
           ↓
        PhieuYmvnService
```

`PhieuService` trở thành facade tương thích với UI hiện tại.

Không để `PhieuService` tiếp tục chứa:

- SQL
- source selection
- QR state
- IFS filtering
- GiaoDB business
- YMVN business

Commit:

```text
refactor(giaohangkhach): split phieu business services
```

---

# 15. PHASE 8 – Split HVN_Presenter

Tách theo use case:

```text
HVN_Presenter
    │
    ├── PhieuPresenter
    ├── DocQrPresenter
    ├── GiaoDbPresenter
    └── YmvnPresenter
```

Có thể thực hiện từng bước:

1. GiaoDB.
2. QR.
3. YMVN.
4. Phiếu thường.

Không phá event wiring hiện tại ngay lập tức.

Commit:

```text
refactor(giaohangkhach): split hvn presenter
```

---

# 16. PHASE 9 – Split IHVNView

Từ:

```csharp
IHVNView
```

thành:

```text
IPhieuView
IDocQrView
IGiaoDbView
IYmvnView
IPhieuDialogView
```

Aggregate:

```csharp
public interface IHVNView :
    IPhieuView,
    IDocQrView,
    IGiaoDbView,
    IYmvnView,
    IPhieuDialogView
{
}
```

Commit:

```text
refactor(giaohangkhach): split hvn view contracts
```

---

# 17. PHASE 10 – Split HVN_PGH

Đích:

```text
HVN_PGH
   │
   ├── PhieuGridControl
   ├── DocQrControl
   ├── GiaoDbControl
   └── YmvnControl
```

`FRM_UploadGiaoDB` tiếp tục là dialog riêng.

Flow:

```text
HVN_PGH
   ↓
GiaoDB UI
   ↓
Upload Đơn Hàng
   ↓
FRM_UploadGiaoDB
   ↓
PhieuGiaoDbService
   ↓
PhieuGiaoDBRepository
```

Không đưa business logic Upload GiaoDB vào `HVN_PGH`.

Commit:

```text
refactor(giaohangkhach): split hvn pgh
```

---

# 18. PHASE 11 – Composition Root

Đưa việc assemble dependency ra:

```text
GiaoHangKhachHvnModuleFactory
```

Flow:

```text
HVN_PGH
    ↓
GiaoHangKhachHvnModuleFactory
    ↓
Presenter
    ↓
Application Services
    ↓
Repositories
```

Form không cần biết cách tạo toàn bộ dependency.

Commit:

```text
refactor(giaohangkhach): move composition root
```

---

# 19. PhieuRepository hiện tại

Giữ facade:

```text
PhieuRepository
    ├── ValidationRepository
    ├── TmpRepository
    ├── LotRepository
    ├── KhoRepository
    ├── LuuTruRepository
    └── GiaoDBRepository
```

Không undo facade.

Không tiếp tục nhét business logic vào facade.

Service mới nên phụ thuộc repository chuyên biệt khi có thể.

---

# 20. GiaoDB responsibility

## PhieuGiaoDBRepository

Chỉ persistence/data access:

```text
LoadTmpPhieuGiaoDB()
LuuGiaoDB()
BuildDonHangTuUpload()
TaoPhieuVaChiTietGiaoDB()
...
```

## PhieuGiaoDbService

Business orchestration:

```text
Upload
Manual
Validate
Create GiaoDB document
Load standard order
Complete GiaoDB
Kho integration
```

## FRM_UploadGiaoDB

Chỉ UI:

```text
Excel
Manual entry
Preview
UI validation
Submit
```

Không chứa business SQL.

---

# 21. MilkRun responsibility

Concept nghiệp vụ:

```text
MilkRun
```

Repository hiện tại:

```text
TableOrderRepo
```

có thể tiếp tục tồn tại trong migration.

Sau này:

```text
MilkRunOrderSource
    ↓
TableOrderRepository
```

YMVN-specific methods trong `TableOrderRepo` chưa cần tách ngay.

---

# 22. YMVN

Không tạo:

```csharp
YmvnOrderSource
```

YMVN là business flow.

Có thể sử dụng:

```text
MilkRun source
    +
YMVN business rules
```

Đích:

```text
PhieuYmvnService
    ├── CapNhapKhoYMVN
    ├── HoanThanhYMVN
    ├── GetDanhSachGioYMVN
    ├── UploadMilkrunSP
    └── các business rules YMVN
```

---

# 23. Enrichment

`EnrichSttHop()` trước mắt có thể giữ đơn giản:

```text
Load
 ↓
EnrichSttHop
 ↓
Result
```

Chưa cần tạo pipeline framework.

Chỉ tạo `IOrderEnricher` nếu thực tế xuất hiện nhiều enrichment độc lập.

Nguyên tắc:

> Không tạo abstraction chỉ vì kiến trúc đẹp; chỉ tạo khi có biến thể thực tế.

---

# 24. Working state

Phân biệt:

```text
Original Source
```

với:

```text
Working Delivery State
```

## Original source

```text
IFS
MilkRun
GiaoDB
```

## Working state

```text
TMPPHIEUGIAOHANG
DOCQRCODE
```

Không coi TMP/DOCQR là OrderSource.

---

# 25. Data flow chuẩn

## Normal IFS

```text
IFS
 ↓
IfsOrderSource
 ↓
Standard Order
 ↓
MP/SP
 ↓
HOP
 ↓
QR/TMP nếu cần
 ↓
Giao
 ↓
Kho
```

## MilkRun

```text
IFS
 ↓
IFS baseline
                        → Compare
        /
MilkRun actual
 ↓
Standard Order
 ↓
MP/SP
 ↓
HOP
 ↓
QR/TMP
 ↓
Giao
 ↓
Kho
```

## GiaoDB

```text
Upload / Manual
 ↓
TMPPHIEUGIAODBHD
TMPPHIEUGIAOHANGDBCT
 ↓
GiaoDbOrderSource
 ↓
Standard Order
 ↓
MP/SP
 ↓
HOP
 ↓
QR/TMP
 ↓
Giao
 ↓
Kho
```

---

# 26. Những điều KHÔNG được làm

## Không 1

Không tạo enum kiểu:

```csharp
OrderWorkflow
{
    Normal,
    GiaoDacBiet,
    YMVN
}
```

và dùng nó cho mọi quyết định.

Vì:

- YMVN là business flow.
- GiaoDacBiet là scenario.
- MP/SP là category.
- IFS/MilkRun/GiaoDB là source/data origin.
- QR là runtime state.

Đây là các dimension khác nhau.

## Không 2

Không biến:

```text
LoadTuBangRieng
```

thành:

```text
Không dùng IFS
```

Đúng:

```text
LoadTuBangRieng
=
MilkRun actual
+
IFS baseline
```

## Không 3

Không đưa EventBus vào Source.

Source:

```text
Load data
```

Service:

```text
Business orchestration
```

Presenter:

```text
UI reaction
```

## Không 4

Không đưa SQL vào Service.

Service gọi Repository.

## Không 5

Không để `HVN_PGH` tạo toàn bộ dependency.

Composition Root chịu trách nhiệm.

## Không 6

Không xóa code cũ hàng loạt.

Ưu tiên:

```text
new service
    ↓
wrap old repository
    ↓
redirect caller
    ↓
test
    ↓
remove old code
```

---

# 27. Strategy hiện tại

Hiện tại đã có:

```text
OrderLoadStrategyFactory
IfsOrderLoadStrategy
OrderTableLoadStrategy
```

Không cần xóa ngay.

Migration:

```text
Old Strategy
    ↓
New IOrderSource
```

Sau khi flow mới ổn định mới xóa Strategy cũ.

Mục tiêu:

> Tách source selection khỏi business orchestration.

---

# 28. Test matrix

Sau mỗi phase, tối thiểu test:

## IFS

```text
QR machine
non-QR machine
MP
SP
```

## MilkRun

```text
IFS = actual
IFS > actual
IFS < actual
MP
SP
QR
non-QR
```

## GiaoDB

```text
Không có IFS
Upload Excel
Manual
MP
SP
QR
non-QR
Có LOT
Không LOT
Cập nhật kho
```

## QR state

```text
false / false
true  / false
true  / true
false / true -> invalid
```

---

# 29. Commit strategy

```text
01 docs: document current order flow
02 refactor: add order load context
03 refactor: normalize order category
04 refactor: introduce order sources
05 refactor: extract qr working state
06 refactor: extract phieu load service
07 refactor: introduce order load result
08 refactor: split phieu business services
09 refactor: split hvn presenter
10 refactor: split hvn view contracts
11 refactor: split hvn pgh
12 refactor: move composition root
```

Mỗi commit:

```text
Build
 ↓
Run
 ↓
Test
 ↓
Commit
```

---

# 30. Definition of Done

Refactor hoàn thành khi:

```text
PhieuService
    ↓
Facade mỏng

HVN_Presenter
    ↓
Không chứa business logic lớn

HVN_PGH
    ↓
Không assemble toàn bộ dependency

IFS
    ↓
Source rõ ràng

MilkRun
    ↓
IFS baseline + actual + comparison

GiaoDB
    ↓
Manual/Upload + GiaoDB document

MP/SP
    ↓
Common category

TMP/DOCQR
    ↓
Working state

Kho
    ↓
Business service riêng

YMVN
    ↓
Business service riêng
```

Behavior của hệ thống hiện tại phải được giữ nguyên.

---

# 31. Thứ tự bắt tay vào code

Không bắt đầu bằng việc sửa `HVN_Presenter`.

Không bắt đầu bằng việc chia `HVN_PGH`.

Không bắt đầu bằng việc viết hàng loạt Strategy.

Bắt đầu:

```text
PHASE 0
   ↓
PHASE 1
   ↓
compile
   ↓
commit
   ↓
PHASE 2
   ↓
compile
   ↓
commit
   ↓
PHASE 3
```

Bước code đầu tiên chỉ là:

```text
OrderCategory.cs
OrderSourceKind.cs
OrderLoadContext.cs
```

sau đó build Context từ state hiện tại.

---

# 32. Nguyên tắc kiến trúc cốt lõi

Có 5 dimension phải giữ độc lập:

```text
                 ┌──────────────────┐
                 │      SOURCE      │
                 │ IFS/MilkRun/GDB  │
                 └────────┬─────────┘
                          │
                 ┌────────▼─────────┐
                 │     CATEGORY     │
                 │      MP / SP     │
                 └────────┬─────────┘
                          │
                 ┌────────▼─────────┐
                 │ WORKING STATE    │
                 │ TMP / DOCQR /QR  │
                 └────────┬─────────┘
                          │
                 ┌────────▼─────────┐
                 │ BUSINESS FLOW    │
                 │ Kho/YMVN/GiaoDB  │
                 └────────┬─────────┘
                          │
                 ┌────────▼─────────┐
                 │       UI         │
                 │ Presenter / Form │
                 └──────────────────┘
```

> **Không trộn Source + Category + Runtime State + Business Flow + UI vào một enum/class/service duy nhất.**
