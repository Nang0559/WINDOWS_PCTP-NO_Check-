# GIAO HÀNG KHÁCH – KẾ HOẠCH VÀ TIẾN ĐỘ REFACTOR KIẾN TRÚC

> Tài liệu này là **roadmap sống** của module `GiaoHangKhach`.
> Nội dung được phục hồi từ lịch sử Git trước khi tài liệu bị rút gọn, sau đó cập nhật trạng thái Phase 12 theo code hiện tại.
> Mục đích của tài liệu là giữ lại **context, quyết định kiến trúc, nghiệp vụ và checklist build/runtime** để có thể truy ngược khi phát sinh lỗi.

---

## 1. Mục tiêu

Refactor module `GiaoHangKhach` để giảm kích thước và trách nhiệm của:

- `PhieuService`
- `HVN_Presenter`
- `HVN_PGH`

nhưng **không thay đổi nghiệp vụ hiện tại**.

Mục tiêu cuối cùng:

```text
HVN_PGH
   ↓
Presenter mỏng
   ↓
Application / Business Services
   ↓
Source + Repository chuyên biệt
```

Đồng thời UI được chia thành các `UserControl` có ownership rõ ràng.

### Nguyên tắc refactor

Không giữ song song hai implementation sau khi một responsibility đã được chuyển xong.

```text
Legacy responsibility
       ↓
New owner
       ↓
Redirect caller
       ↓
Kiểm tra behavior
       ↓
XÓA legacy implementation
```

Bridge chỉ được giữ khi nó là **migration boundary thật sự**, ví dụ Designer vẫn tạo control cũ để `UserControl.Adopt()` nhận lại control đó.

---

# 2. Trạng thái tổng thể

| Phase | Nội dung | Trạng thái |
|---|---|---|
| 0 | Chốt behavior hiện tại | ✅ Hoàn thành |
| 1 | Order Context | ✅ Hoàn thành |
| 2 | Chuẩn hóa MP/SP | ✅ Hoàn thành |
| 3 | Source abstraction | ✅ Hoàn thành |
| 4 | QR/TMP Working State | ✅ Hoàn thành |
| 5 | `PhieuLoadService` | ✅ Hoàn thành |
| 6 | `OrderLoadResult` + QR refactor | ✅ Hoàn thành |
| 7 | Split business services | ✅ Hoàn thành |
| 8 | Split `HVN_Presenter` | ✅ Hoàn thành |
| 9 | Split View contracts + UserControls | ✅ Hoàn thành |
| 10 | UI ownership / remove legacy header bridge | ✅ Hoàn thành |
| 11 | Final header/grid ownership cleanup | ✅ Hoàn thành |
| 12A | ActionBar | ✅ Hoàn thành |
| 12B | Grid presentation/state | ✅ Hoàn thành |
| 12C | DOC QR UI/actions | ✅ Hoàn thành |
| 12D | GiaoDB UI/dialog | ✅ Hoàn thành |
| 12E | Dialog/Report UI | ✅ Hoàn thành |
| 12F | QR input / scan UI | ✅ Hoàn thành |
| 12G | Remaining UI helpers | ✅ Hoàn thành |
| 12H | Final UI slimming | ✅ Hoàn thành |

> **Lưu ý:** trạng thái DONE ở Phase 12 nghĩa là responsibility đã được redirect về owner mới theo roadmap. Build/runtime thực tế vẫn phải được kiểm tra trong môi trường Visual Studio của dự án.

---

# 3. Nghiệp vụ đã chốt

## 3.1 IFS

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

## 3.2 OrderTable / LoadTuBangRieng

`LoadTuBangRieng = YES` **không có nghĩa là bỏ IFS**.

Ý nghĩa nghiệp vụ:

> IFS là baseline; bảng riêng/MilkRun là actual order để giao thực tế và có thể cần so sánh với baseline.

```text
                 IFS
                  │
                  │ baseline
                  ▼
          ┌───────────────┐
          │   OrderTable  │
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

Không refactor thành một `TableOrder` độc lập với IFS.

## 3.3 Giao đặc biệt / GiaoDB

GiaoDB là một **business scenario độc lập**.

Ví dụ:

- giao mẫu
- hàng thử
- trường hợp không có đơn IFS
- giao hàng phát sinh
- giao hàng liên quan đến xử lý tồn kho

```text
Upload / Manual
      ↓
GiaoDB header/detail
      ↓
Standard Order
      ↓
Giao hàng
      ↓
LOT / QR
      ↓
Kho
```

## 3.4 GiaoDB không phải OrderTable

```text
OrderTable:
    IFS baseline
      +
    actual OrderTable
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

# 4. Phân biệt Source và Scenario

Không dùng một enum lớn để biểu diễn tất cả nghiệp vụ.

## 4.1 Source

```csharp
public enum OrderSourceKind
{
    IFS = 1,
    TableOrder = 2,
    GiaoDB = 3
}
```

- `IFS`: đơn từ IFS.
- `TableOrder`: actual order từ bảng riêng/OrderTable.
- `GiaoDB`: dữ liệu từ chứng từ GiaoDB.

`GiaoDB` là source/document data, còn `GiaoDacBiet` là business scenario.

Không tạo:

```csharp
YmvnOrderSource
```

vì YMVN vẫn có thể sử dụng nguồn MilkRun/bảng riêng.

---

# 5. MP / SP

MP và SP là **Order Category**, không phải Source.

```csharp
public enum OrderCategory
{
    MP = 1,
    SP = 2
}
```

Tất cả source đều có thể là MP hoặc SP.

## 5.1 Ba cơ chế xác định Category hiện tại

Có **3 cơ chế khác nhau**, không phải một quy tắc `CUA = SUB_DOCK_CODE` áp dụng chung:

| Luồng | Cấp độ quyết định | Tín hiệu | Nơi xử lý |
|---|---|---|---|
| IFS gốc (`100001`) | Toàn phiên load | Nhãn giờ xuất có `SP6`/`SP#` | `GioXuatRepository.MapMaGio` + `PhieuService.IsLoaiSP()` |
| Bảng riêng / MilkRun (`100003`, `CoLoaiSP=true`) | Từng dòng | `CUA` so với `CustomerConfig.DockCodeSP` | SQL `TableOrderRepo` + `DockCodeRowCategoryFilter` |
| Toggle thủ công | Toàn phiên | Nút `Xem: MP/SP` | `_isLoaiSP`, `HVN_PGH.BtnToggleLoaiPhieu_Click` |

Toggle thủ công là **input** cho flow, không phải một cơ chế phân loại thứ ba độc lập.

### 5.2 Đích thiết kế

```csharp
public interface IOrderCategoryResolver
{
    OrderCategory Resolve(OrderLoadContext ctx);
}

public interface IRowCategoryFilter
{
    DataTable Filter(DataTable data, OrderCategory wanted, CustomerConfig cfg);
}
```

`IRowCategoryFilter` chỉ áp dụng khi dữ liệu có thể chứa đồng thời MP và SP, chủ yếu ở Bảng riêng/MilkRun.

IFS gốc không cần lọc lại bằng interface này vì SQL đã scope theo giờ xuất.

Không tạo abstraction riêng cho GiaoDB/YMVN nếu chưa có biến thể thực tế cần nó.

---

# 6. QR state

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

# 7. Kiến trúc đích

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

# 8. PHASE 0 – Chốt behavior hiện tại

**Trạng thái: ✅ Hoàn thành**

Đã document behavior trước refactor và xác định các dimension độc lập:

```text
Source
Category
QR runtime state
Business scenario
UI state
```

Không thay đổi nghiệp vụ trong phase này.

---

# 9. PHASE 1 – Order Context

**Trạng thái: ✅ Hoàn thành**

Đã tạo:

```text
Modules/GiaoHangKhach/Domain/Order/
    OrderCategory.cs
    OrderSourceKind.cs
    OrderLoadContext.cs
```

Context chứa các thông tin cần cho một lần load:

```csharp
CustomerConfig Config
DateTime NgayGiao
string NhaMay
int AddNm
string GioFcc
string GioFccMoTa
OrderCategory Category
OrderSourceKind Source
MachineRole MachineRole
bool IsBanQR
IList<string> CheckedGios
```

Không xóa ngay `_isMayBanQR`, `_isBanQR`, `_isLoaiSP`; context được build từ state hiện tại.

---

# 10. PHASE 2 – Chuẩn hóa MP/SP

**Trạng thái: ✅ Hoàn thành**

Đã thống nhất `OrderCategory` là concept chung và tách:

```text
Session category
        ≠
Row category filtering
```

Đối với Bảng riêng/MilkRun, logic lọc theo `DockCodeSP` được tách thành responsibility riêng (`DockCodeRowCategoryFilter`) thay cho việc để `PhieuService` trực tiếp gánh logic.

SQL filtering trong `TableOrderRepo` vẫn giữ tại DB vì lý do hiệu năng.

---

# 11. PHASE 3 – Source abstraction

**Trạng thái: ✅ Hoàn thành**

Đã có:

```text
IOrderSource
OrderSourceResult
OrderSourceFactory
IfsOrderSource
TableOrderSource
GiaoDbOrderSource
```

Contract:

```csharp
public interface IOrderSource
{
    OrderSourceKind SourceKind { get; }
    OrderSourceResult Load(OrderLoadContext context);
}
```

Source chỉ load/chuẩn hóa source data.

Không để Source:

- publish EventBus
- xử lý UI
- cập nhật kho
- điều khiển QR workflow

---

# 12. PHASE 4 – QR / TMP Working State

**Trạng thái: ✅ Hoàn thành**

Đã tách source data khỏi working delivery state.

Có:

```text
IDeliveryWorkingState
PhieuTmpRepository
```

Các operation chính:

```text
HasQr()
LoadFromQr()
SaveTmp()
ClearTmp()
ClearDocQr()
GetCurrentOrder()
GetTrangThaiDangBan()
```

TMP/DOCQR là **working state**, không phải OrderSource.

---

# 13. PHASE 5 – PhieuLoadService

**Trạng thái: ✅ Hoàn thành**

Đã tạo `PhieuLoadService` để làm orchestration layer cho load phiếu.

Flow:

```text
Load(context)
     ↓
Resolve Source
     ↓
IFS / TableOrder / GiaoDB
     ↓
Standard Orders
     ↓
MP/SP
     ↓
Enrich HOP
     ↓
QR/TMP working state
     ↓
OrderLoadResult
```

`PhieuService` không còn là nơi điều phối toàn bộ source loading.

---

# 14. PHASE 6 – OrderLoadResult + QR refactor

**Trạng thái: ✅ Hoàn thành**

Đã tạo `OrderLoadResult` chứa:

```text
Orders
HangThieu
HasMaNG
HasDifference
Source
Category
Caption
Warning
IsQr
```

`PhieuService.LoadPhieu()` lấy kết quả từ `PhieuLoadService`, cache các thông tin cần cho compatibility và publish `PhieuLoadedEvent`.

### QR

Đã tách:

```text
DocQRTableResolver
DocQRSessionState
DocQRScanEngine
DocQRService
```

`DocQRService` giữ vai trò facade mỏng; scan logic nằm ở engine/state phù hợp.

---

# 15. PHASE 7 – Split PhieuService

**Trạng thái: ✅ Hoàn thành**

Đã tách các business responsibility chính:

```text
PhieuService
    │
    ├── LoadPhieu()
    │      ↓
    │   PhieuLoadService
    │
    ├── Kho
    │      ↓
    │   PhieuKhoService
    │
    ├── GiaoDB
    │      ↓
    │   PhieuGiaoDbService
    │
    └── YMVN
           ↓
        PhieuYmvnService
```

`PhieuService` giữ vai trò facade tương thích với UI.

Đã chuyển:

- Kho → `PhieuKhoService`
- GiaoDB → `PhieuGiaoDbService`
- YMVN → `PhieuYmvnService`

Không tiếp tục đưa SQL/source selection/QR state/business flow lớn vào `PhieuService`.

---

# 16. PHASE 8 – Split HVN_Presenter

**Trạng thái: ✅ Hoàn thành**

Đã tách:

```text
HVN_Presenter
    │
    ├── PhieuPresenter
    ├── DocQrPresenter
    ├── GiaoDbPresenter
    └── YmvnPresenter
```

Có:

```text
HVNPresenterContext
```

để giữ context chung và compatibility boundary.

`HVN_Presenter` hiện là facade điều phối presenter chuyên biệt thay vì chứa toàn bộ use case.

---

# 17. PHASE 9 – Split View contracts + UserControls

**Trạng thái: ✅ Hoàn thành**

## 17.1 View contracts

Đã tách:

```text
IPhieuView
IDocQrView
IGiaoDbView
IYmvnView
IViewFeedback
```

`IHVNView` kế thừa các contract chuyên biệt để giữ compatibility:

```csharp
public interface IHVNView :
    IPhieuView,
    IDocQrView,
    IGiaoDbView,
    IYmvnView
{
    // feedback/loading compatibility
}
```

Presenter chuyên biệt chỉ phụ thuộc interface chuyên biệt:

```text
PhieuPresenter  → IPhieuView
DocQrPresenter  → IDocQrView
GiaoDbPresenter → IGiaoDbView
YmvnPresenter   → IYmvnView
```

## 17.2 UserControls

Đã tạo và đưa vào migration:

```text
PhieuHeaderControl
PhieuGridControl
DocQrControl
PhieuBottomStateControl
HangThieuControl
PhieuActionBarControl
```

---

# 18. PHASE 10 – UI ownership / Header

**Trạng thái: ✅ Hoàn thành**

Phase này đã được triển khai theo các bước 10A → 10N.

## 18.1 Header ownership

`PhieuHeaderControl` hiện sở hữu:

```text
SelectedDate
SelectedTabAddNM
CurrentGioXuat
SetDate
SetTab
LockDatePicker
UnlockDatePicker
BindGioXuatVP
BindGioXuatHN
LockRadioExcept
UnlockAllRadio
UpdateGioXuatFromDB
CheckGX APIs
```

Events:

```text
DateChanged
GioXuatChanged
TabChanged
CheckGXChanged
```

Flow:

```text
DatePicker / Radio / Tab
        ↓
PhieuHeaderControl
        ↓
Header event
        ↓
HVN_PGH facade/event
        ↓
Presenter
```

## 18.2 Legacy header event handlers

Đã loại bỏ các handler cũ:

```text
dateNX_EditValueChanged
tabPaneHVN_Click
RDO_GXHN_SelectedIndexChanged
radioGroup2_SelectedIndexChanged
```

và các subscription tương ứng.

## 18.3 Migration boundary

Designer vẫn có thể tạo control cũ trong `InitializeComponent()`.

Đây **không được coi là legacy ownership** nếu control đó chỉ đóng vai trò migration source:

```text
Designer
   ↓
legacy control instance
   ↓
Adopt()
   ↓
new UserControl owns behavior
```

Không xóa field Designer hàng loạt chỉ để làm code nhìn sạch hơn; việc đó có thể khiến WinForms Designer rewrite layout ngoài ý muốn.

---

# 19. PHASE 11 – Final UI ownership cleanup

**Trạng thái: ✅ Hoàn thành**

Phase 11 gồm các bước 11A → 11G.

## 19.1 11A – UI ownership audit

Audit toàn bộ direct UI references trong `HVN_PGH` và xác định owner phù hợp.

## 19.2 11B – Radio UI ownership

Chuyển vào `PhieuHeaderControl`:

```text
BindGioXuatVP
BindGioXuatHN
LockRadioExcept
UnlockAllRadio
```

## 19.3 11C – GiaoDB GioXuat interpretation

`UpdateGioXuatFromDB` interpretation được chuyển vào `PhieuHeaderControl`.

Presenter vẫn nhận event `GioXuatChanged`; UI control không gọi trực tiếp Presenter.

## 19.4 11D – Date/header state

Date state được tập trung vào `PhieuHeaderControl`:

```text
dateNX
SelectedDate
SetDate
LockDatePicker
UnlockDatePicker
DateChanged
```

`SetDate()` có cơ chế suppress event khi programmatically update DatePicker để tránh phát sinh flow không mong muốn.

## 19.5 11E – Remove UI compatibility bridge

Đã xóa:

```text
HVN_PGH.UiCompatibility.cs
```

Không còn dùng compatibility alias để giả lập ownership cũ của bottom-state grids.

## 19.6 11F – Simplify grid migration

Migration sử dụng helper chung:

```csharp
ReplaceControl(existing, replacement, parent)
```

Ownership sau migration:

```text
panelPhieu
    → PhieuHeaderControl

GCT_HT
    → HangThieuControl

gridCtrDONHANG
    → PhieuGridControl

gridCtrDOCQrCODE
    → DocQrControl
```

## 19.7 11G – Final audit

`HVN_PGH` được xác định rõ là:

```text
WinForms lifecycle
Dependency composition
UserControl composition
Presenter event forwarding
Migration/composition root
```

Không phải owner của business UI details đã được chuyển sang UserControl.

---

# 20. PHASE 12 – UI ownership finalization

Phase 12 áp dụng nguyên tắc mạnh hơn các phase trước:

> **Mỗi responsibility chuyển sang UserControl xong phải xóa implementation cũ khỏi `HVN_PGH` ngay trong phase đó, nếu không còn lý do migration compatibility.**

Không để tình trạng:

```text
New Control xử lý
       +
HVN_PGH vẫn giữ implementation cũ
```

trừ khi code cũ thực sự còn là Designer migration source.

---

# 21. PHASE 12A – ActionBar

**Trạng thái: ✅ Hoàn thành**

## Owner mới

`PhieuActionBarControl` nhận lại `WindowsUIButtonPanel` được Designer tạo:

```text
Designer UIButton
       ↓
PhieuActionBarControl.Adopt()
       ↓
PhieuActionBarControl owns ActionBar presentation
```

Đã chuyển các nhóm cấu hình ActionBar vào control:

```text
ConfigureNormal(...)
ConfigurePhieuView(...)
ConfigureDocQr()
ConfigureGiaoDb()
ConfigureYmvN(...)
UpdateLoaiPhieuCaption(...)
```

ActionBar phát event bằng action identifier ổn định, không dùng caption text làm business command.

Ví dụ:

```text
ACTION:DOC_QR
ACTION:LAY_LAI_LOT
ACTION:GHEP_LOT_TOGGLE
ACTION:CAP_NHAT_KHO
ACTION:KIEM_TRA_MA_NG
ACTION:GHI_CHU_STOP
ACTION:XOA_GHI_CHU_STOP
```

Event:

```csharp
ActionClicked
```

`HVN_PGH` chỉ forward action tới presenter/business flow.

Definition of Done:

```text
PhieuActionBarControl
       ↓
ActionBar presentation + action event

HVN_PGH
       ↓
composition + business forwarding

Không còn ActionBar implementation song song
```

---

# 22. PHASE 12B – Grid presentation/state

**Trạng thái: ✅ Hoàn thành**

`PhieuGridControl` sở hữu order-grid presentation và UI state liên quan đến grid.

Các API đã chuyển/chuẩn hóa:

```text
GetFocusedStt()
GetFocusedMaHang()
HasLotToSave()
HasUnconfirmedRows()
```

`PhieuBottomStateControl` tiếp tục sở hữu:

```text
LechGrid
GhepLotGrid
SuaSlGrid
```

và:

```text
ShowLech()
ShowGhepLot()
ShowSuaSoLuong()
```

Các action cần dòng đang chọn đi theo flow:

```text
ActionBar
    ↓
Action event
    ↓
HVN_PGH / Presenter
    ↓
PhieuGridControl
    ↓
selected-row state
```

Không đưa SQL, Kho, LOT transaction, GiaoDB business hoặc YMVN business vào Grid Control.

---

# 23. PHASE 12C – DOC QR UI/actions

**Trạng thái: ✅ Hoàn thành**

`DocQrControl` là owner của DOC QR grid presentation/state.

Các thao tác UI thuộc boundary:

```text
GetFocusedDocQRStt
GetFocusedDocQRTemInfo
DeleteFocusedDocQRRow
ClearDocQRRows
Bind(DataTable)
ShowAndBringToFront()
```

Scan/business engine vẫn thuộc:

```text
DocQRScanEngine
DocQRSessionState
DocQRService
```

Control chỉ sở hữu:

```text
Grid
Selection
Input binding
UI action
```

---

# 24. PHASE 12D – GiaoDB UI/dialog

**Trạng thái: ✅ Hoàn thành**

UI/dialog GiaoDB được giữ ở presentation boundary; business vẫn thuộc:

```text
PhieuGiaoDbService
PhieuGiaoDBRepository
```

Nguyên tắc:

```text
FRM_UploadGiaoDB
    ↓
Excel / Manual / Preview / UI validation
    ↓
PhieuGiaoDbService
    ↓
Repository
```

Không đưa business SQL vào UserControl/form presentation.

---

# 25. PHASE 12E – Dialog / Report UI

**Trạng thái: ✅ Hoàn thành**

`PhieuDialogControl` là owner duy nhất của dialog/report presentation.

Các API được redirect:

```text
ShowChonSttTrungMa
ShowKiemTraMaNG
ShowTachLot
ShowLoiCapNhapKho
ShowChonHinhThucIn
ShowChonLotTuKho
ShowReport
ShowReportWithGioHeader
ShowReportYMVN
```

`HVN_PGH` chỉ forward tới `_phieuDialogControl`.

Các `new FRM_*` và `ReportPrintTool` legacy tương ứng đã được loại khỏi form trong các responsibility đã migrate.

Business validation/action vẫn nằm ngoài control.

**Commit chính:** `d9f80be`

---

# 26. PHASE 12F – QR input / scan UI

**Trạng thái: ✅ Hoàn thành**

`DocQrInputControl` sở hữu:

```text
Text
Clear()
FocusInput()
Submitted
```

Đã redirect:

```text
QRCodeInput
ClearQRInput
focus
Enter / Submit
```

về UI boundary.

Migration detach handler legacy để tránh double-submit.

Scan/business logic vẫn thuộc:

```text
DocQRScanEngine
DocQRSessionState
DocQRService
```

**Commit chính:** `6158e56`, `d9f80be`

---

# 27. PHASE 12G – Remaining UI helpers

**Trạng thái: ✅ Hoàn thành**

Đã redirect:

```text
BindHangThieu → HangThieuControl.Bind
ShowHangThieuCaNgay → HangThieuControl.Bind + ShowAndBringToFront
DOC QR datasource → DocQrControl.Bind
DOC QR presentation → DocQrControl
```

`DocQrControl` đã bổ sung:

```csharp
Bind(DataTable)
ShowAndBringToFront()
```

để `HVN_PGH` không còn bind trực tiếp `gridCtrDOCQrCODE` cho responsibility này.

**Commit chính:** `d9f80be`, `5f9fb79`

---

# 28. PHASE 12H – Final UI slimming

**Trạng thái: ✅ Hoàn thành**

`HVN_PGH` sau 12E–12G chỉ giữ:

```text
WinForms lifecycle
Composition root
UserControl composition
Presenter event forwarding
Migration boundary cần thiết
```

Đã loại khỏi form các responsibility đã chuyển:

```text
Dialog/report instantiation
QR submit implementation
Hàng thiếu direct binding
DOC QR direct datasource binding
```

**Commit chính:** `d9f80be`, `5f9fb79`

---

# 29. Definition of Done – Phase 12A → 12H

```text
12A DONE  ✅
12B DONE  ✅
12C DONE  ✅
12D DONE  ✅
12E DONE  ✅
12F DONE  ✅
12G DONE  ✅
12H DONE  ✅
```

Các UserControl tương ứng:

```text
PhieuHeaderControl
PhieuGridControl
PhieuBottomStateControl
HangThieuControl
DocQrControl
DocQrInputControl
PhieuActionBarControl
PhieuDialogControl
```

là presentation boundary; business logic vẫn nằm ngoài UI controls.

---

# 30. Các UserControl và ownership hiện tại

| Control | Ownership |
|---|---|
| `PhieuHeaderControl` | Date, tab, GioXuat, radio, CheckGX, header UI state |
| `PhieuGridControl` | Order grid, columns/views, selected-row UI state |
| `PhieuBottomStateControl` | Lệch / Ghép LOT / Sửa số lượng grids |
| `HangThieuControl` | Grid hàng thiếu |
| `DocQrControl` | DOC QR grid và UI state |
| `DocQrInputControl` | QR input text, focus, clear, submit event |
| `PhieuActionBarControl` | ActionBar presentation + action event |
| `PhieuDialogControl` | Dialog/report presentation |
| `HVN_PGH` | Form lifecycle + composition + forwarding |

Nguyên tắc:

> UserControl sở hữu UI state/presentation của chính nó; Presenter sở hữu orchestration; Service sở hữu business logic.

---

# 31. PhieuRepository hiện tại

Giữ facade repository hiện tại để tránh phá compatibility:

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

Service mới nên phụ thuộc repository chuyên biệt khi có thể.

---

# 32. GiaoDB responsibility

## PhieuGiaoDBRepository

Persistence/data access:

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

UI:

```text
Excel
Manual entry
Preview
UI validation
Submit
```

Không chứa business SQL.

---

# 33. MilkRun responsibility

Repository hiện tại:

```text
TableOrderRepo
```

vẫn có thể tồn tại trong migration.

Đích:

```text
TableOrderSource
      ↓
TableOrderRepository
```

MilkRun phải giữ semantics:

```text
IFS baseline
     +
Actual TableOrder
     ↓
Comparison
```

Không biến MilkRun thành source độc lập với IFS baseline nếu nghiệp vụ hiện tại vẫn cần comparison.

---

# 34. YMVN

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
    └── business rules YMVN
```

---

# 35. Enrichment

`EnrichSttHop()` trước mắt giữ đơn giản:

```text
Load
 ↓
EnrichSttHop
 ↓
Result
```

Chỉ tạo `IOrderEnricher` khi thực tế xuất hiện nhiều enrichment độc lập.

Nguyên tắc:

> Không tạo abstraction chỉ vì kiến trúc đẹp; chỉ tạo khi có biến thể thực tế.

---

# 36. Working state

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
TableOrder
GiaoDB
```

## Working state

```text
TMPPHIEUGIAOHANG
DOCQRCODE
```

Không coi TMP/DOCQR là OrderSource.

---

# 37. Data flow chuẩn

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

## TableOrder

```text
IFS
 ↓
IFS baseline
      ↘
       Compare ← TableOrder actual
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
QR/TMP nếu cần
 ↓
Giao
 ↓
Kho
```

---

# 38. Những điều KHÔNG được làm

## Không 1 – Không trộn các dimension

Không tạo enum kiểu:

```csharp
OrderWorkflow
{
    Normal,
    GiaoDacBiet,
    YMVN
}
```

để quyết định mọi thứ.

Các dimension phải độc lập:

```text
Source       = IFS / TableOrder / GiaoDB
Category     = MP / SP
QR state     = machine/session runtime state
Business     = Kho / YMVN / GiaoDB
UI state     = Control ownership
```

## Không 2 – Không biến LoadTuBangRieng thành “không dùng IFS”

Đúng:

```text
LoadTuBangRieng
=
IFS baseline
+
MilkRun actual
+
comparison
```

## Không 3 – Không đưa EventBus vào Source

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

## Không 4 – Không đưa SQL vào Service

Service gọi Repository.

## Không 5 – Không để `HVN_PGH` assemble business dependency tùy tiện

Composition boundary phải rõ ràng; về sau có thể tiếp tục đưa assembly vào module factory nếu cần.

## Không 6 – Không giữ implementation legacy sau migration

Trừ migration source thực sự cần thiết.

```text
New owner
    ↓
Caller migrated
    ↓
Legacy implementation deleted
```

Đây là nguyên tắc bắt buộc từ Phase 12 trở đi.

---

# 39. Strategy hiện tại

Hiện tại đã có:

```text
OrderLoadStrategyFactory
IfsOrderLoadStrategy
OrderTableLoadStrategy
```

Không xóa hàng loạt chỉ vì đã có `IOrderSource`.

Migration chỉ kết thúc khi caller đã chuyển hoàn toàn và behavior được xác nhận.

Mục tiêu cuối:

```text
Source selection
       ↓
IOrderSource
       ↓
PhieuLoadService
```

Sau khi không còn caller của Strategy cũ mới xóa Strategy cũ.

---

# 40. Test matrix

Sau mỗi phase có thay đổi behavior boundary, tối thiểu kiểm tra:

## IFS

```text
QR machine
non-QR machine
MP
SP
```

## TableOrder

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

## UI migration

```text
Date change
Tab change
GioXuat change
MP/SP toggle
CheckGX
ActionBar actions
Order row selection
DOC QR selection
Bottom-state tabs
Dialog/report
QR submit
Hàng thiếu
```

---

# 41. Commit / phase strategy

Không dùng một commit lớn cho nhiều responsibility không liên quan.

Mẫu:

```text
Move responsibility
 ↓
Redirect caller
 ↓
Build / run / test nếu môi trường cho phép
 ↓
Commit
 ↓
Delete temporary workflow/bridge nếu không còn cần
```

Các phase cũ đã được triển khai thành nhiều commit nhỏ; roadmap này mô tả **trách nhiệm và trạng thái**, không coi commit message cũ là source of truth duy nhất.

---

# 42. Chuỗi commit phục hồi / Phase 12

Các commit lịch sử được dùng để phục hồi context:

```text
7f01693  docs(giaohangkhach): update refactor roadmap through phase 12B
70e4b69  Update GIAOHANGKHACH_REFACTOR_ROADMAP.md
33aa5d7  Update GIAOHANGKHACH_REFACTOR_ROADMAP.md
5e05d6a  Update GIAOHANGKHACH_REFACTOR_ROADMAP.md
```

Các commit Phase 12 hiện tại:

```text
6158e56  refactor(HVN): complete 12F QR input boundary and migration cleanup
4b578db  fix(HVN): remove duplicate partial OnFormClosed override
d9f80be  refactor(HVN): complete dialog, hang-thieu and QR input boundaries 12E-12G
5f9fb79  refactor(HVN): complete DocQrControl presentation boundary for 12G
```

`4b578db` là commit sửa an toàn sau khi phát hiện duplicate `OnFormClosed` trong partial migration file. Không tính là phase riêng.

> Lưu ý: roadmap được phục hồi từ commit lịch sử; commit hiện tại của chính tài liệu sẽ được ghi ở Git history sau thao tác này.

---

# 43. Definition of Done cuối cùng

Refactor hoàn thành khi:

```text
PhieuService
    ↓
Facade mỏng

HVN_Presenter
    ↓
Presenter facade + presenters chuyên biệt

HVN_PGH
    ↓
WinForms lifecycle + composition + forwarding

IFS
    ↓
Source rõ ràng

TableOrder
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

Header
    ↓
PhieuHeaderControl

Order Grid
    ↓
PhieuGridControl

DOC QR Grid
    ↓
DocQrControl

Bottom State Grids
    ↓
PhieuBottomStateControl

ActionBar
    ↓
PhieuActionBarControl

Dialog / Report
    ↓
PhieuDialogControl

QR Input
    ↓
DocQrInputControl
```

Behavior của hệ thống hiện tại phải được giữ nguyên.

---

# 44. Nguyên tắc kiến trúc cốt lõi

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
                 │ TMP / DOCQR / QR │
                 └────────┬─────────┘
                          │
                 ┌────────▼─────────┐
                 │ BUSINESS FLOW    │
                 │ Kho/YMVN/GiaoDB  │
                 └────────┬─────────┘
                          │
                 ┌────────▼─────────┐
                 │       UI         │
                 │ Control/Presenter│
                 └──────────────────┘
```

> **Không trộn Source + Category + Runtime State + Business Flow + UI vào một enum/class/service duy nhất.**

---

# 45. Nguyên tắc refactor sau Phase 12

Từ đây về sau, mọi thay đổi phải trả lời được 4 câu hỏi:

1. **Ai là owner mới của responsibility này?**
2. **Caller đã chuyển sang owner mới chưa?**
3. **Legacy implementation đã xóa chưa?**
4. **Có còn compatibility boundary thực sự cần giữ không?**

Nếu câu 2 đã `YES` và câu 4 là `NO` thì phải thực hiện câu 3 ngay trong cùng phase.

Mục tiêu không phải là có thật nhiều class/control, mà là:

```text
Mỗi responsibility
       ↓
Một owner rõ ràng
       ↓
Một đường xử lý duy nhất
       ↓
Không có legacy path song song
```

---

# 46. Ghi chú build/runtime sau phục hồi

Tài liệu này được dùng làm **historical reference** khi build thực tế.

Khi Visual Studio báo lỗi sau refactor, kiểm tra theo thứ tự:

```text
1. Compile error
   ↓
2. Namespace / reference / constructor
   ↓
3. Designer InitializeComponent
   ↓
4. Event wiring
   ↓
5. Migration boundary / Adopt()
   ↓
6. Runtime behavior
   ↓
7. SQL / Repository behavior
```

Không sửa ngược kiến trúc chỉ để che compile error. Trước tiên xác định error thuộc phase nào và responsibility nào đã được move.

### Phase 12 verification note

Các thay đổi Phase 12 đã được commit trực tiếp trên `master`. Việc GitHub connector không có Visual Studio/.NET build environment đồng nghĩa rằng **không được coi roadmap DONE là bằng chứng build/runtime đã pass**. Build thực tế vẫn phải chạy trên máy phát triển của dự án.
