# GIAOHANGKHACH – REFACTOR ROADMAP

> Source of truth cho refactor `PCTP/Modules/GiaoHangKhach/HVN`.
>
> Nguyên tắc bắt buộc: **Move → Redirect caller → Verify → Delete legacy**.
> Không duy trì implementation mới + legacy song song khi phase đã đánh dấu DONE.

---

# 1. Mục tiêu

Tách rõ:

```text
UI
 ↓
Presenter / Orchestration
 ↓
Service / Business
 ↓
Repository / Persistence
```

`HVN_PGH` chỉ còn lifecycle, composition root, event forwarding và migration boundary cần thiết.

---

# 2. Kiến trúc chuẩn

```text
HVN_PGH
   ↓
HVN_Presenter
   ├── PhieuPresenter
   ├── DocQrPresenter
   ├── GiaoDbPresenter
   └── YmvnPresenter
   ↓
PhieuService
   ├── PhieuLoadService
   ├── PhieuKhoService
   ├── PhieuGiaoDbService
   └── PhieuYmvnService
```

Source:

```text
IOrderSource
├── IfsOrderSource
├── TableOrderSource
└── GiaoDbOrderSource
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

# 3. Phase 0 → 11

| Phase | Nội dung | Trạng thái |
|---|---|---|
| 0 | Chốt behavior | ✅ Hoàn thành |
| 1 | Order Context | ✅ Hoàn thành |
| 2 | Chuẩn hóa MP/SP | ✅ Hoàn thành |
| 3 | Source abstraction | ✅ Hoàn thành |
| 4 | QR/TMP Working State | ✅ Hoàn thành |
| 5 | PhieuLoadService | ✅ Hoàn thành |
| 6 | OrderLoadResult + QR | ✅ Hoàn thành |
| 7 | PhieuService facade | ✅ Hoàn thành |
| 8 | Presenter split | ✅ Hoàn thành |
| 9 | View contracts | ✅ Hoàn thành |
| 10 | Header ownership | ✅ Hoàn thành |
| 11 | Final ownership cleanup | ✅ Hoàn thành |

---

# 4. Phase 12 – UI ownership finalization

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

## Tổng trạng thái Phase 12

| Phase | Nội dung | Trạng thái |
|---|---|---|
| 12A | ActionBar | ✅ Hoàn thành |
| 12B | Grid presentation/state | ✅ Hoàn thành |
| 12C | DOC QR UI/actions | ✅ Hoàn thành |
| 12D | GiaoDB UI/dialog | ✅ Hoàn thành |
| 12E | Dialog / Report UI | 🟡 Boundary đã tạo – còn redirect legacy |
| 12F | QR input / scan UI | 🟡 Boundary đã tạo – còn redirect legacy |
| 12G | Remaining UI helpers | 🟡 Đang xử lý |
| 12H | Final UI slimming | ⬜ Chưa hoàn thành |

---

# 5. Phase 12A – ActionBar

**Trạng thái: ✅ Hoàn thành**

`PhieuActionBarControl` sở hữu ActionBar presentation, button configuration, stable action identifier và `ActionClicked`.

Legacy đã xóa khỏi `HVN_PGH.cs`:

```text
SetupYMVNButtons()
UIButton_ButtonClick()
HandleGhepLotToggle()
UIButton.Buttons.Clear/Add/Insert trong form
```

Designer không còn `UIButton.ButtonClick += ... UIButton_ButtonClick`.

---

# 6. Phase 12B – Grid presentation/state

**Trạng thái: ✅ Hoàn thành**

`PhieuGridControl` sở hữu order grid, column/view configuration và selected-row UI state.

Runtime access đã route qua boundary; legacy aliases chỉ còn cho Designer/migration compatibility khi cần.

---

# 7. Phase 12C – DOC QR UI/actions

**Trạng thái: ✅ Hoàn thành**

`DocQrControl` sở hữu:

```text
GetFocusedStt
GetFocusedTemInfo
DeleteFocusedRow
ClearRows
```

Scan/business logic vẫn nằm ở `DocQRScanEngine`, `DocQRSessionState`, `DocQRService`.

---

# 8. Phase 12D – GiaoDB UI/dialog

**Trạng thái: ✅ Hoàn thành**

`GiaoDbControl` sở hữu boundary UI/lifetime của upload/manual dialog.

```text
GiaoDbControl
    ↓
FRM_UploadGiaoDB lifetime
```

---

# 9. Phase 12E – Dialog / Report UI

**Trạng thái: 🟡 Boundary đã tạo – chưa DONE**

Đã tạo:

```text
PCTP/Modules/GiaoHangKhach/HVN/Controls/PhieuDialogControl.cs
```

Control đã nhận ownership presentation cho:

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

Migration boundary đã khởi tạo `PhieuDialogControl`.

**Còn thiếu để DONE:** redirect toàn bộ caller trong `HVN_PGH` sang control và xóa implementation dialog/report cũ khỏi form.

Business validation/action vẫn phải ở Presenter/Service.

---

# 10. Phase 12F – QR input / scan UI

**Trạng thái: 🟡 Boundary đã tạo – chưa DONE**

Đã tạo:

```text
PCTP/Modules/GiaoHangKhach/HVN/Controls/DocQrInputControl.cs
```

Control sở hữu:

```text
Text
Clear()
FocusInput()
Submitted
```

`txt_DOCQRCODE` được adopt qua migration boundary; scan/business processing không được đưa vào control.

**Còn thiếu để DONE:**

```text
QRCodeInput → DocQrInputControl.Text
ClearQRInput → DocQrInputControl.Clear()
txt_DOCQRCODE.Focus() → DocQrInputControl.FocusInput()
Designer/legacy KeyPress → Submitted
```

Sau redirect phải xóa handler/key-input implementation cũ.

---

# 11. Phase 12G – Remaining UI helpers

**Trạng thái: 🟡 Đang xử lý**

Đã mở rộng `HangThieuControl` với:

```text
Bind(DataTable)
ShowAndBringToFront()
```

Mục tiêu audit tiếp:

- direct GridView access
- direct child-control configuration
- helper chỉ phục vụ một UserControl
- facade không còn caller
- legacy event handler
- using chỉ còn do legacy code

Mỗi responsibility phải đi theo:

```text
Move → Redirect → Verify → Delete legacy
```

**Chưa được đánh dấu DONE** cho tới khi `HVN_PGH` không còn implementation song song.

---

# 12. Phase 12H – Final UI slimming

**Trạng thái: ⬜ Chưa hoàn thành**

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
Dialog/report presentation implementation
QR input implementation
```

12H chỉ được đánh dấu DONE sau khi 12E → 12G đã DONE và code search xác nhận không còn legacy implementation tương ứng.

---

# 13. UserControl ownership

| Control | Ownership |
|---|---|
| `PhieuHeaderControl` | Date, tab, GioXuat, radio, CheckGX, header UI state |
| `PhieuGridControl` | Order grid, columns/views, selected-row UI state |
| `PhieuBottomStateControl` | Lệch / Ghép LOT / Sửa số lượng grids |
| `HangThieuControl` | Grid hàng thiếu + binding/presentation |
| `DocQrControl` | DOC QR grid + UI state |
| `DocQrInputControl` | DOC QR input + submit event |
| `PhieuActionBarControl` | ActionBar presentation + action event |
| `PhieuDialogControl` | Dialog/report presentation + dialog lifetime |
| `GiaoDbControl` | GiaoDB dialog UI/lifetime boundary |
| `HVN_PGH` | Form lifecycle + composition + forwarding |

---

# 14. Repository / Service boundaries

`PhieuRepository` vẫn là facade compatibility với các repository chuyên biệt.

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

# 15. YMVN / MilkRun

Không tạo `YmvnOrderSource`. YMVN là business flow.

`PhieuYmvnService` sở hữu các operation YMVN như CapNhapKhoYMVN, HoanThanhYMVN, GetDanhSachGioYMVN, UploadMilkrunSP và business rules liên quan.

---

# 16. Data flow chuẩn

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

---

# 17. Definition of Done – toàn Phase 12

Phase 12 chỉ được đánh dấu **DONE** khi:

```text
12A DONE
12B DONE
12C DONE
12D DONE
12E DONE
12F DONE
12G DONE
12H DONE
```

và code search xác nhận không còn implementation UI legacy tương ứng trong `HVN_PGH`.
