# GIAOHANGKHACH – REFACTOR ROADMAP

> Source of truth cho refactor `PCTP/Modules/GiaoHangKhach/HVN`.
>
> Nguyên tắc bắt buộc: **Move → Redirect caller → Verify → Delete legacy**.
> Không duy trì implementation mới + legacy song song khi phase đã đánh dấu DONE.

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
5f9fb79  refactor(HVN): complete DocQrControl presentation boundary for 12G
<current> docs(HVN): record final 12G verification commit
```

> `4b578db` là commit sửa an toàn sau khi phát hiện duplicate `OnFormClosed` trong partial migration file. Không tính là phase riêng.

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

**Commit:** `d9f80be`

---

# 10. Phase 12F – QR input / scan UI

**Trạng thái: ✅ Hoàn thành**

`DocQrInputControl` sở hữu `Text`, `Clear()`, `FocusInput()` và `Submitted`.

Đã redirect `QRCodeInput`, `ClearQRInput`, focus và Enter/Submit về boundary. Migration detach handler legacy để tránh double-submit.

**Commit:** `6158e56`, `d9f80be`

---

# 11. Phase 12G – Remaining UI helpers

**Trạng thái: ✅ Hoàn thành**

Đã redirect:

```text
BindHangThieu → HangThieuControl.Bind
ShowHangThieuCaNgay → HangThieuControl.Bind + ShowAndBringToFront
DOC QR datasource → DocQrControl.Bind
DOC QR presentation → DocQrControl
```

`DocQrControl` đã bổ sung `Bind()` và `ShowAndBringToFront()` để `HVN_PGH` không còn bind trực tiếp `gridCtrDOCQrCODE`.

**Commit:** `d9f80be`, `5f9fb79`

---

# 12. Phase 12H – Final UI slimming

**Trạng thái: ✅ Hoàn thành**

`HVN_PGH` sau 12E–12G chỉ giữ lifecycle, composition root, forwarding và migration boundary cần thiết. Dialog/report instantiation, QR submit implementation, Hàng thiếu direct binding và DOC QR direct datasource binding đã được loại khỏi form.

**Commit:** `d9f80be`, `5f9fb79`

---

# Definition of Done – Phase 12E → 12H

```text
12E DONE  ✅
12F DONE  ✅
12G DONE  ✅
12H DONE  ✅
```

Các UserControl tương ứng:

```text
PhieuDialogControl
DocQrInputControl
HangThieuControl
DocQrControl
```

là presentation boundary; business logic vẫn nằm ngoài UI controls.
