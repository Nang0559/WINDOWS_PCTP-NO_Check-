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

## Tổng trạng thái Phase 12

| Phase | Nội dung | Trạng thái |
|---|---|---|
| 12A | ActionBar | ✅ Hoàn thành |
| 12B | Grid presentation/state | ✅ Hoàn thành |
| 12C | DOC QR UI/actions | ✅ Hoàn thành |
| 12D | GiaoDB UI/dialog | ✅ Hoàn thành |
| 12E | Dialog / Report UI | ✅ Hoàn thành |
| 12F | QR input / scan UI | ✅ Hoàn thành |
| 12G | Remaining UI helpers | ✅ Hoàn thành |
| 12H | Final UI slimming | ✅ Hoàn thành |

**Chuỗi commit Phase 12E → 12H:**

```text
6158e56  refactor(HVN): complete 12F QR input boundary and migration cleanup
4b578db  fix(HVN): remove duplicate partial OnFormClosed override
 d9f80be  refactor(HVN): complete dialog, hang-thieu and QR input boundaries 12E-12G
<roadmap commit>  docs(HVN): mark Phase 12E-12H completed
```

> `4b578db` là commit sửa an toàn sau khi phát hiện duplicate `OnFormClosed` trong partial migration file. Không tính là phase riêng.

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

**Trạng thái: ✅ Hoàn thành**

`PhieuDialogControl` là owner duy nhất của dialog/report presentation:

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

`HVN_PGH` chỉ forward tới `_phieuDialogControl`; các `new FRM_*` và `ReportPrintTool` legacy tương ứng đã được loại khỏi form.

Business validation/action vẫn ở Presenter/Service.

**Commit:** `d9f80be`

---

# 10. Phase 12F – QR input / scan UI

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
QRCodeInput → DocQrInputControl.Text
ClearQRInput → DocQrInputControl.Clear()
txt_DOCQRCODE.Focus() → DocQrInputControl.FocusInput()
legacy KeyPress → DocQrInputControl.Submitted
```

Migration chủ động detach `txt_DOCQRCODE_KeyPress` để không phát sinh double-submit.

**Commit:** `6158e56` và `d9f80be`

---

# 11. Phase 12G – Remaining UI helpers

**Trạng thái: ✅ Hoàn thành**

Đã redirect:

```text
BindHangThieu → HangThieuControl.Bind
ShowHangThieuCaNgay → HangThieuControl.Bind + ShowAndBringToFront
DOC QR datasource → DocQrControl.Bind
DOC QR view → DocQrControl boundary
```

Các thao tác view chuyển màn hình trong `HVN_PGH` chỉ điều phối UserControl, không còn bind trực tiếp `GCT_HT`/`gridCtrDOCQrCODE`.

**Commit:** `d9f80be`

---

# 12. Phase 12H – Final UI slimming

**Trạng thái: ✅ Hoàn thành**

`HVN_PGH` sau Phase 12E–12G chỉ giữ:

```text
WinForms lifecycle
composition root
UserControl composition
presenter event forwarding
migration boundary
form-level state bắt buộc cho compatibility
```

Đã loại khỏi form:

```text
dialog/report instantiation
QR input submit implementation
HangThieu direct binding
DOC QR direct datasource binding
```

Không đưa business logic mới vào UserControl.

**Commit:** `d9f80be`

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
