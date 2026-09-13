# GIAO HÀNG KHÁCH – KẾ HOẠCH VÀ TIẾN ĐỘ REFACTOR KIẾN TRÚC

> Roadmap sống của module `GiaoHangKhach`. Trạng thái phải phản ánh code thực tế sau từng phase.
>
> Nguyên tắc bắt buộc từ Phase 12: responsibility chuyển sang owner mới thì caller phải chuyển theo và implementation legacy phải được xóa ngay khi không còn là migration source thật sự.

---

## 1. Mục tiêu

Giảm trách nhiệm của `PhieuService`, `HVN_Presenter`, `HVN_PGH` nhưng không thay đổi nghiệp vụ.

```text
HVN_PGH
   ↓
Presenter mỏng
   ↓
Application / Business Services
   ↓
Source + Repository chuyên biệt
```

UI ownership:

```text
UserControl = UI state/presentation
Presenter   = orchestration
Service     = business logic
Repository  = persistence
```

Không giữ song song new implementation + legacy implementation sau migration, trừ Designer migration source thật sự cần `Adopt()`.

---

## 2. Trạng thái tổng thể

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
| 10 | UI ownership / header | ✅ Hoàn thành |
| 11 | Final header/grid ownership cleanup | ✅ Hoàn thành |
| 12A | ActionBar | 🟡 Đang hoàn thiện cleanup legacy source |
| 12B | Grid presentation/state | ✅ Hoàn thành |
| 12C | DOC QR UI/actions | ✅ Hoàn thành |
| 12D | GiaoDB UI/dialog | ✅ Hoàn thành |
| 12E | Dialog/Report UI | ⬜ Chưa bắt đầu |
| 12F | QR input / scan UI | ⬜ Chưa bắt đầu |
| 12G | Remaining UI helpers | ⬜ Chưa bắt đầu |
| 12H | Final UI slimming | ⬜ Chưa bắt đầu |

**12A chưa được đánh dấu DONE** vì `HVN_PGH.cs` vẫn còn source legacy `UIButton_ButtonClick()` và `SetupYMVNButtons()` trực tiếp thao tác `UIButton`. Runtime đã được chặn khỏi legacy path, nhưng Definition of Done yêu cầu xóa source legacy.

---

# 3. Nghiệp vụ và dimension đã chốt

## 3.1 Source

```csharp
public enum OrderSourceKind
{
    IFS = 1,
    TableOrder = 2,
    GiaoDB = 3
}
```

- IFS: nguồn đơn hàng gốc/kế hoạch.
- TableOrder: actual order từ bảng riêng/MilkRun.
- GiaoDB: dữ liệu chứng từ GiaoDB.

`GiaoDB` là source/document data; `YMVN` là business flow, không phải source.

## 3.2 Category

```csharp
public enum OrderCategory
{
    MP = 1,
    SP = 2
}
```

Tất cả source có thể là MP/SP. Bảng riêng/MilkRun có thể lọc từng dòng bằng `DockCodeRowCategoryFilter`; IFS gốc được scope theo giờ xuất.

## 3.3 LoadTuBangRieng

`LoadTuBangRieng = YES` nghĩa là:

```text
IFS baseline
   +
actual TableOrder/MilkRun
   ↓
comparison
```

Không được hiểu là bỏ IFS.

## 3.4 QR state

Phân biệt:

```text
_isMayBanQR = máy có khả năng QR
_isBanQR    = session hiện tại đang giao/đọc QR
```

Không gộp hai state này.

## 3.5 Working state

Original source:

```text
IFS / TableOrder / GiaoDB
```

Working delivery state:

```text
TMPPHIEUGIAOHANG / DOCQRCODE
```

TMP/DOCQR không phải OrderSource.

---

# 4. Kiến trúc hiện tại

```text
HVN_PGH
   ↓
HVN_Presenter
   ├── PhieuPresenter
   ├── DocQrPresenter
   ├── GiaoDbPresenter
   └── YmvnPresenter
   ↓
PhieuService (facade)
   ├── PhieuLoadService
   ├── PhieuKhoService
   ├── PhieuGiaoDbService
   └── PhieuYmvnService
```

Source layer:

```text
IOrderSource
├── IfsOrderSource
├── TableOrderSource
└── GiaoDbOrderSource
```

Factory:

```text
OrderSourceFactory
```

Working state:

```text
IDeliveryWorkingState
PhieuTmpRepository
```

QR:

```text
DocQRTableResolver
DocQRSessionState
DocQRScanEngine
DocQRService
```

---

# 5. Phase 0 → 6

## Phase 0 – Chốt behavior

**✅ Hoàn thành**

Đã xác định độc lập các dimension Source / Category / QR runtime state / Business scenario / UI state.

## Phase 1 – Order Context

**✅ Hoàn thành**

Đã có:

```text
OrderCategory
OrderSourceKind
OrderLoadContext
```

Context chứa customer/date/machine/addNM/giờ/category/source/machine role/QR state và checked giờ.

## Phase 2 – Chuẩn hóa MP/SP

**✅ Hoàn thành**

Đã tách session category khỏi row filtering; `DockCodeRowCategoryFilter` xử lý filtering thực tế của bảng riêng.

## Phase 3 – Source abstraction

**✅ Hoàn thành**

Đã có:

```text
IOrderSource
OrderSourceResult
OrderSourceFactory
IfsOrderSource
TableOrderSource
GiaoDbOrderSource
```

Source chỉ load/normalize source data, không chứa business orchestration.

## Phase 4 – QR/TMP Working State

**✅ Hoàn thành**

`IDeliveryWorkingState` + `PhieuTmpRepository`; TMP/DOCQR được xác định là working state.

## Phase 5 – PhieuLoadService

**✅ Hoàn thành**

Flow chuẩn:

```text
Load(context)
 → Resolve Source
 → Standard Orders
 → MP/SP
 → Enrich HOP
 → QR/TMP working state
 → OrderLoadResult
```

## Phase 6 – OrderLoadResult + QR

**✅ Hoàn thành**

`OrderLoadResult` chứa orders/hàng thiếu/NG/difference/source/category/caption/warning/QR state.

QR được tách thành resolver/session/engine/service.

---

# 6. Phase 7 → 11

## Phase 7 – Split PhieuService

**✅ Hoàn thành**

```text
PhieuService
├── PhieuLoadService
├── PhieuKhoService
├── PhieuGiaoDbService
└── PhieuYmvnService
```

`PhieuService` giữ facade compatibility.

## Phase 8 – Split HVN_Presenter

**✅ Hoàn thành**

Có `HVNPresenterContext` và presenter chuyên biệt:

```text
PhieuPresenter
DocQrPresenter
GiaoDbPresenter
YmvnPresenter
```

## Phase 9 – View contracts + UserControls

**✅ Hoàn thành**

Contracts:

```text
IPhieuView
IDocQrView
IGiaoDbView
IYmvnView
IViewFeedback
IHVNView
```

UserControls:

```text
PhieuHeaderControl
PhieuGridControl
DocQrControl
PhieuBottomStateControl
HangThieuControl
PhieuActionBarControl
```

## Phase 10 – Header ownership

**✅ Hoàn thành**

`PhieuHeaderControl` sở hữu date/tab/GioXuat/radio/CheckGX và header UI state. Legacy header handlers đã được loại bỏ.

## Phase 11 – Final ownership cleanup

**✅ Hoàn thành**

Đã xác định `HVN_PGH` là lifecycle/composition root, sử dụng `ReplaceControl()` cho migration và không còn compatibility bridge cũ của header/bottom-state.

---

# 7. Phase 12 – UI ownership finalization

Nguyên tắc:

```text
Move
 ↓
Redirect caller
 ↓
Verify behavior
 ↓
Delete legacy implementation
```

Không chấp nhận:

```text
New Control
   +
HVN_PGH legacy implementation song song
```

---

# 8. Phase 12A – ActionBar

**Trạng thái: 🟡 Đang hoàn thiện cleanup legacy source**

## 8.1 Owner

`PhieuActionBarControl` sở hữu:

```text
ActionBar presentation
button configuration
stable action identifier
ActionClicked event
```

Designer control được tiếp quản qua:

```text
UIButton
  ↓
PhieuActionBarControl.Adopt()
```

Các cấu hình đã chuyển:

```text
ConfigureNormal()
ConfigurePhieuView()
ConfigureDocQr()
ConfigureGiaoDb()
ConfigureYmvN()
UpdateLoaiPhieuCaption()
```

## 8.2 Action contract

Action được xác định bằng enum/tag ổn định, không dùng caption làm business identifier.

```text
PhieuActionBarAction
PhieuActionBarEventArgs
ActionClicked
```

## 8.3 Runtime boundary đã hoàn tất

`HVN_PGH.PhieuGridMigration.cs` đã được sửa để migrate ActionBar **trước `base.OnLoad()`**. Điều này bảo đảm `HVN_PGH_Load`/Presenter không nhìn thấy một ActionBar owner cũ trong runtime.

Legacy `UIButton.ButtonClick` cũng được detach tại migration boundary trước `base.OnLoad()`.

Commit:

```text
98c5a134a19c349e8691f3c611ebc0a578f1dd2f
refactor(giaohangkhach): finalize phase 12A actionbar migration boundary
```

## 8.4 Cleanup còn bắt buộc

Cần xóa khỏi `HVN_PGH.cs`:

```text
SetupYMVNButtons()
UIButton_ButtonClick()
HandleGhepLotToggle()
UIButton.Buttons.Clear/Add/Insert trong form
legacy ActionBar using nếu không còn dùng
```

Sau cleanup, `UIButton` chỉ được tồn tại như Designer migration source cho `Adopt()`.

## 8.5 Definition of Done

```text
PhieuActionBarControl
    ↓
ActionBar presentation + action event

HVN_PGH
    ↓
composition + event forwarding

Không còn ActionBar implementation song song
```

---

# 9. Phase 12B – Grid presentation/state

**Trạng thái: ✅ Hoàn thành**

`PhieuGridControl` là owner của order grid, column/view configuration và selected-row UI state.

API chính:

```text
GetFocusedStt
GetFocusedMaHang
GetFocusedLot
GetFocusedStatus
GetFocusedQuantity
HasLotToSave
HasUnconfirmedRows
RefreshLotRow
```

`PhieuBottomStateControl` sở hữu:

```text
LechGrid
GhepLotGrid
SuaSlGrid
```

Runtime access đã được route qua boundary; legacy aliases chỉ còn cho Designer/migration compatibility khi cần.

Không đưa SQL/Kho/LOT transaction/business flow vào GridControl.

---

# 10. Phase 12C – DOC QR UI/actions

**Trạng thái: ✅ Hoàn thành**

`DocQrControl` sở hữu:

```text
GetFocusedStt
GetFocusedTemInfo
DeleteFocusedRow
ClearRows
```

`HVN_PGH` chỉ giữ facade/forwarding cần cho contract.

Migration source `gridCtrDOCQrCODE` / `gridVDOCQRCODE` được giữ để `Adopt()`.

Scan/business logic vẫn nằm ở:

```text
DocQRScanEngine
DocQRSessionState
DocQRService
```

---

# 11. Phase 12D – GiaoDB UI/dialog

**Trạng thái: ✅ Hoàn thành**

`GiaoDbControl` sở hữu boundary UI/lifetime của upload/manual dialog.

```text
GiaoDbControl
    ↓
FRM_UploadGiaoDB lifetime
```

`GiaoDbPresenter` giữ orchestration; `PhieuGiaoDbService` giữ business; `PhieuGiaoDBRepository` giữ persistence.

Không đưa Presenter/business/SQL vào UserControl.

---

# 12. Phase 12E – Dialog / Report UI

**Trạng thái: ⬜ Chưa bắt đầu**

Các UI helper còn lại:

```text
ShowChonSttTrungMa
ShowKiemTraMaNG
ShowTachLot
ShowLoiCapNhapKho
ShowReport
ShowReportWithGioHeader
```

Business validation/action vẫn ở service/presenter.

---

# 13. Phase 12F – QR input / scan UI

**Trạng thái: ⬜ Chưa bắt đầu**

Cần audit/chuyển:

```text
QRCodeInput
ClearQRInput
txt_DOCQRCODE
scan input UI
```

Không trộn input UI với `DocQRScanEngine`, `DocQRSessionState`, `DocQRService`.

---

# 14. Phase 12G – Remaining UI helpers

**Trạng thái: ⬜ Chưa bắt đầu**

Audit `HVN_PGH` cho:

- direct GridView access
- direct child-control configuration
- helper chỉ phục vụ một UserControl
- facade không còn caller
- legacy event handler
- using chỉ còn do legacy code

Mỗi responsibility:

```text
Move → Redirect → Verify → Delete legacy
```

---

# 15. Phase 12H – Final UI slimming

**Trạng thái: ⬜ Chưa bắt đầu**

Đích cuối:

```text
HVN_PGH
├── WinForms lifecycle
├── composition root
├── UserControl composition
├── presenter event forwarding
└── migration boundary cần thiết
```

Không còn:

```text
Business orchestration lớn
SQL
Grid presentation chi tiết
ActionBar presentation
DOC QR manipulation
Header state implementation
GiaoDB business logic
YMVN business logic
```

---

# 16. UserControl ownership

| Control | Ownership |
|---|---|
| `PhieuHeaderControl` | Date, tab, GioXuat, radio, CheckGX, header UI state |
| `PhieuGridControl` | Order grid, columns/views, selected-row UI state |
| `PhieuBottomStateControl` | Lệch / Ghép LOT / Sửa số lượng grids |
| `HangThieuControl` | Grid hàng thiếu |
| `DocQrControl` | DOC QR grid + UI state |
| `PhieuActionBarControl` | ActionBar presentation + action event |
| `GiaoDbControl` | GiaoDB dialog UI/lifetime boundary |
| `HVN_PGH` | Form lifecycle + composition + forwarding |

---

# 17. Repository / Service boundaries

`PhieuRepository` vẫn là facade compatibility với các repository chuyên biệt.

```text
PhieuRepository
├── ValidationRepository
├── TmpRepository
├── LotRepository
├── KhoRepository
├── LuuTruRepository
└── GiaoDBRepository
```

Business services:

```text
PhieuLoadService
PhieuKhoService
PhieuGiaoDbService
PhieuYmvnService
```

GiaoDB:

```text
FRM_UploadGiaoDB = UI
GiaoDbControl    = UI boundary
GiaoDbPresenter  = orchestration
PhieuGiaoDbService = business
PhieuGiaoDBRepository = persistence
```

---

# 18. YMVN / MilkRun

Không tạo `YmvnOrderSource`. YMVN là business flow.

Có thể dùng:

```text
TableOrder/MilkRun source
+
YMVN business rules
```

`PhieuYmvnService` sở hữu các operation YMVN như CapNhapKhoYMVN, HoanThanhYMVN, GetDanhSachGioYMVN, UploadMilkrunSP và business rules liên quan.

Không tạo abstraction chỉ để làm kiến trúc đẹp nếu chưa có biến thể thực tế.

---

# 19. Data flow chuẩn

## IFS

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

## TableOrder/MilkRun

```text
IFS baseline
      ↘
       Compare ← actual TableOrder
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
GiaoDB header/detail
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

# 20. Những điều không được làm

1. Không trộn Source + Category + QR state + Business flow + UI state vào một enum/class/service.
2. Không biến `LoadTuBangRieng` thành “không dùng IFS”.
3. Không đưa EventBus vào Source.
4. Không đưa SQL vào Service.
5. Không để `HVN_PGH` tùy tiện assemble business dependency ngoài composition boundary.
6. Không giữ implementation legacy sau migration nếu không còn là migration source.
7. Không đưa business logic vào UserControl.

---

# 21. Test matrix

Sau phase có thay đổi boundary cần kiểm tra tối thiểu:

```text
IFS: QR / non-QR / MP / SP
TableOrder: equal / less / greater / MP / SP / QR / non-QR
GiaoDB: upload / manual / MP / SP / QR / non-QR / LOT / no LOT / kho
QR: false/false, true/false, true/true, false/true-invalid
UI: Date, Tab, GioXuat, MP/SP, CheckGX, ActionBar, order selection, DOC QR, bottom-state
```

---

# 22. Commit / phase strategy

Mỗi phase nên có đường đi:

```text
Move responsibility
 ↓
Redirect caller
 ↓
Build / test nếu môi trường cho phép
 ↓
Delete legacy
 ↓
Update roadmap
 ↓
Commit
```

Roadmap là source of truth về **responsibility và trạng thái**, không phải commit message duy nhất.

---

# 23. Definition of Done cuối cùng

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

IFS / TableOrder / GiaoDB
    ↓
Source rõ ràng

MP / SP
    ↓
Common category

TMP / DOCQR
    ↓
Working state

Kho / YMVN / GiaoDB
    ↓
Business services riêng

Header
    ↓
PhieuHeaderControl

Order Grid
    ↓
PhieuGridControl

DOC QR Grid
    ↓
DocQrControl

Bottom State
    ↓
PhieuBottomStateControl

ActionBar
    ↓
PhieuActionBarControl
```

Behavior hệ thống phải giữ nguyên.

---

# 24. Nguyên tắc sau Phase 12

Mọi thay đổi phải trả lời:

1. Ai là owner mới?
2. Caller đã chuyển chưa?
3. Legacy implementation đã xóa chưa?
4. Có compatibility boundary thật sự cần giữ không?

Nếu caller đã chuyển và không còn compatibility requirement thì legacy implementation phải được xóa trong cùng phase.

Mục tiêu:

```text
Một responsibility
       ↓
Một owner rõ ràng
       ↓
Một đường xử lý duy nhất
       ↓
Không có legacy path song song
```
