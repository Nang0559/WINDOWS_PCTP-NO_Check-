# WMS — Runbook chạy SQL và thu bằng chứng gate

> Mục đích: một tài liệu duy nhất cho người chạy SQL: chạy ở đâu, theo thứ tự nào, lệnh nào, ghi bằng chứng ra sao.
> Định nghĩa gate **chuẩn** nằm ở `PCTP/Workflow/Diagram/WMS_RELEASE_GATE.md`. Tài liệu này không đổi định nghĩa đó.

## 0. An toàn — đọc trước khi chạy bất cứ thứ gì

| Môi trường | Server / database | Được chạy? |
|---|---|---|
| DB test cô lập ("DB57") | `192.168.200.57` / `B7R2_FCC` | **Có** (sau backup / xin phép người quản DB) |
| Production | `192.168.200.14\BRAVO` (cùng tên DB `B7R2_FCC`) | **KHÔNG BAO GIỜ** cho script test/deploy trong tài liệu này |

- Hai server dùng **cùng tên database**. Luôn kiểm tra `SELECT @@SERVERNAME, DB_NAME();` trước khi chạy script trong SSMS.
- Harness `PCTP.QC.SqlIntegration` và các script PowerShell tự từ chối production. Script `.sql` chạy tay trong SSMS thì **không** có bảo vệ này.
- Mật khẩu SQL: biến môi trường `PCTP_QC_SQL_PASSWORD` hoặc nhập khi được hỏi. Không ghi vào file, không đưa lên dòng lệnh.

## 1. Gate trong tài liệu vs gate trong script chạy

Hai nơi đang đánh số G3–G6 **khác nhau**. Khi ghi bằng chứng phải ghi rõ theo nơi nào.

| Nội dung cần chứng minh | `WMS_RELEASE_GATE.md` | `Invoke-PctpQcGates.ps1` |
|---|---|---|
| Traceability | G1 | `-Gate G1` |
| Unit / regression | G2 | `-Gate G2` |
| SQL engine integration (tempdb fixture) | G3 | `-Gate G3` |
| Hai session đồng thời, B0-02/B0-03 (R1–R5) | G5 | **chạy bên trong `-Gate G3`** (xem §4) |
| Procedure thật, schema thật, rollback-only | G4 | `-Gate G4` (cần G3 PASS cùng commit) |
| UI chỉ điều hướng (không đổi dữ liệu) | — | `-Gate G5` |
| UI nghiệp vụ end-to-end có assert DB | G6 | `-Gate G6` |

Quy ước ghi bằng chứng: dùng nhãn của `WMS_RELEASE_GATE.md`, kèm tên lệnh chạy, ví dụ
"G5 (R1–R5) — chạy trong `Invoke-PctpQcGates.ps1 -Gate G3`".

## 2. Chuẩn bị

1. Checkout đúng commit cần chứng minh và **commit hết thay đổi** (script ghi cờ `dirty` vào kết quả; kết quả "dirty" không dùng làm bằng chứng).
2. Backup DB57 (hoặc xác nhận có bản backup gần nhất).
3. Chạy ở thư mục gốc repo, Windows PowerShell 5.1:
   ```powershell
   git status --porcelain      # phải rỗng
   git rev-parse HEAD          # ghi lại SHA này
   ```

## 3. Deploy script SQL lên DB57 (theo thứ tự)

Thứ tự bên dưới được **suy ra từ phần đầu mỗi file và từ bảng mà các file tham chiếu** — repo chưa có tài liệu thứ tự chính thức. Người quản DB cần xác nhận một lần rồi sửa bảng này cho đúng.

Mỗi script đều viết để chạy lại được (idempotent) trừ khi ghi chú khác. Chạy từng file, đọc kết quả, rồi mới chạy file kế.

### 3.1 Nhóm XuLyHangLoi — `PCTP/Modules/XuLyHangLoi/Database/`

| # | File | Ghi chú |
|---|---|---|
| 1 | `PHASE2_AffectedLotSnapshot.sql` | tạo `FVN_PhieuXuLyBatThuongAffectedLot` |
| 2 | `PHASE3_InitialQC.sql` | tạo `FVN_PhieuXuLyBatThuongQCInitial` |
| 3 | `PHASE4_9_ReworkDispositionCompensationWorkflowReport.sql` | tạo các bảng `FVN_PXLB_*` |
| 4 | `PHASE7_WorkflowSeed.sql` | seed dữ liệu workflow |
| 5 | `PHASE10_PhieuTraHang_CustomerNo.sql` | sửa `FVN_PhieuTraHang` (bảng gốc phải có sẵn) |
| 6 | `PHASE11_PhieuTraHangCT_LienKetPhieuXuLy.sql` | sửa `FVN_PhieuTraHangCT` |
| 7 | `PHASE12_PhieuXuLyBatThuong_PhieuTraHangCTId.sql` | sửa `FVN_PhieuXuLyBatThuong` |
| 8 | `PHASE13_QTChung_ReworkTables.sql` | tạo các bảng `FVN_TraHangQTChung*` |
| 9 | `PHASE14_ReworkExportIdempotency.sql` | index/ràng buộc trên `FVN_TraHangQTChungXuat` → **phải sau #8** |

### 3.2 Nhóm kho / giao hàng — `SQL/`

| # | File | Ghi chú |
|---|---|---|
| 10 | `PCTP_StockTpIdentityAudit.sql` | **CHỈ ĐỌC.** Đọc kết quả trước khi chạy #11 |
| 11 | `PCTP_EnforceUniqueStockTpLot.sql` | chỉ chạy **sau khi** #10 sạch (không trùng LOT) |
| 12 | `PCTPStockMovementGuard.sql` | khóa idempotency cho stock movement |
| 13 | `PCTP_DeliveryCommandLedger.sql` | ledger cho delivery command (B0-01) |
| 14 | `PCTPQuarantineRelease.sql` | bảng audit release cách ly |
| 15 | `PHASE14_SlotLot_NgQuarantine.sql` | `SlotLot.PhieuStatus = 3` cho hàng NG |
| 16 | `PHASE15_HangChoGiaoPickDetails.sql` | chạy trước khi deploy code hỗ trợ hủy chờ giao |
| 17 | `PHASE16_QC_Final_ExecutionTimeout_Fix.sql` | sửa timeout QC xác nhận cuối |
| 18 | `PCTP_LuuPhieuGiaoHang_CustomerIsolation.sql` | preflight chỉ đọc + 1 index |
| 19 | `Usp_Qrcode_Update_Stock2405.sql` | deploy bằng script ở §3.3, **không** dán tay |

Không chạy: `A0_SlotLot_Diagnose_Repair.sql` (chẩn đoán/sửa dữ liệu A0 — chỉ khi có yêu cầu riêng), `NHAP_TP_HIS_Retire.sql` (tự nhắc "chạy từng bước, không tự động DROP"), `QC_G4_DeliveryFixture_*.sql` (do G4 tự dùng).

### 3.3 Deploy hai proc giao hàng (`Usp_Qrcode_Update_Stock2405` và `Usp_Qrcode_Update_Stock_SP`)

Script deploy cả hai proc (mặc định `-Procedure All`; có thể chọn `Stock2405` hoặc `Stock_SP`). G4 preflight kiểm cả hai, nên nếu chỉ deploy một proc thì G4 vẫn FAIL.

```powershell
# 1) so sánh, không ghi
.\PCTP.QC.UI\Deploy-PctpDeliveryProcedure.ps1 -SqlUser <user> -WhatIfOnly
# 2) deploy thật lên DB test (lưu bản cũ vào artifacts\deploy\<timestamp>\ trước khi đổi)
.\PCTP.QC.UI\Deploy-PctpDeliveryProcedure.ps1 -SqlUser <user> -ConfirmIsolatedDb
```

### 3.4 Procedure legacy `Usp_Qrcode_LUU_PHIEU` — retire có kiểm soát

`SQL/Usp_Qrcode_LUU_PHIEU_RetireUnsafeLegacy.sql` xử lý procedure cũ có `UPDATE LUUPHIEUGIAOHANG` thiếu `WHERE` và `DELETE DOCQRCODE` theo `KETQUA` trên toàn bảng.

- Mặc định `@ConfirmRetire = 0`: chỉ preflight, không thay đổi procedure.
- Script chỉ cho phép database `B7R2_FCC` khi `CONNECTIONPROPERTY('local_net_address') = '192.168.200.57'`; ngoài ra vẫn từ chối server có tên chứa `BRAVO`. Tên instance/alias một mình không chứng minh môi trường là test.
- Trước khi đổi `@ConfirmRetire = 1`, backup DB test và xác minh cả source code, cấu hình, module SQL và SQL Agent không còn caller hợp lệ.
- Nếu phát hiện caller SQL hoặc Agent, script dừng. Kiểm tra module SQL/Agent không phát hiện được mọi lời gọi C# hoặc SQL động; phải tìm trong source/config và kiểm thử luồng ứng dụng.
- Khi xác nhận retire, procedure giữ nguyên chữ ký nhưng trả lỗi `51015` thay vì chạy các lệnh ghi dữ liệu không an toàn. Đây là fail-closed, không phải bản thay thế nghiệp vụ cho luồng lưu phiếu chuẩn.

Kết quả precheck người dùng cung cấp:
- `ACCFAST\\BRAVO / B7R2_FCC`: procedure tồn tại; không dùng để chạy retire.
- `QC-CNC\\BRV / B7R2_FCC`: procedure tồn tại; truy vấn không tìm thấy SQL module caller hoặc SQL Agent job step. Lần chạy là `PRECHECK ONLY`, không có DDL.
- Chưa có bằng chứng rằng `QC-CNC\\BRV` chính là DB57. Chạy truy vấn chỉ đọc sau trên đúng kết nối:

```sql
SELECT
    @@SERVERNAME AS ServerName,
    DB_NAME() AS DatabaseName,
    CONVERT(nvarchar(48), CONNECTIONPROPERTY('local_net_address')) AS SqlServerIp;
```

Chỉ tiếp tục nếu `SqlServerIp = 192.168.200.57`. Nếu giá trị khác hoặc `NULL`, dừng và không đặt `@ConfirmRetire = 1`. Precheck SQL không loại trừ caller từ C#, cấu hình, SQL động hoặc hệ thống bên ngoài.

## 4. Chạy gate và thu bằng chứng

```powershell
.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G1
.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G2
.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G3 -SqlUser <user>
.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G4 -SqlUser <user>      # cần G3 PASS cùng commit
.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G5 -ExePath <PCTP.exe>
.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G6 -ExePath <PCTP.exe> -SqlUser <user> -AllowMutations -ConfirmAppUsesIsolatedDb
```

Kết quả nằm ở `artifacts\gates\<7 ký tự đầu của SHA>\<Gate>\` (log + summary).

### Điều kiện riêng của G5/G6 (UI)

- `PCTP.exe.config` cạnh file exe được truyền vào `-ExePath` **phải trỏ DB57**. Runner đọc config này và từ chối chạy (kể cả G6 ghi dữ liệu) nếu thấy server production `192.168.200.14\BRAVO`. Đây là chốt an toàn, không phải lỗi: đừng sửa runner để bỏ qua.
- Không sửa `PCTP\bin\Debug\PCTP.exe.config` đang dùng hằng ngày. Sao chép toàn bộ thư mục build sang thư mục riêng (ví dụ `artifacts\ui-app\`), sửa connection string trong bản sao đó sang DB57 rồi truyền `-ExePath artifacts\ui-app\PCTP.exe`.
- Log của runner chỉ ghi lỗi chung (`Argument types do not match`); lý do thật (ví dụ "references the production server") nằm ở ảnh `99-runner-error.png` trong thư mục journey và dòng đỏ trên cùng ảnh đó.
- G5 không có `gate-summary.json` nghĩa là script bị dừng giữa chừng; chạy lại và đừng đóng cửa sổ ứng dụng khi runner còn chạy.

### Đọc kết quả: khi nào là bằng chứng

- `WorkingTreeDirty: true` trong `gate-summary.json` nghĩa là kết quả **không dùng làm bằng chứng**, kể cả khi PASS. Commit hết rồi chạy lại.
- G4 cần G3 PASS **cùng SHA**. Đổi commit thì chạy lại từ G2.
Chỉ coi là PASS khi chính script báo PASS trên lần chạy đó.

### Những gì R1–R5 (B0-02/B0-03) chứng minh và không chứng minh

`ReworkTwoSessionScenario.cs` chạy khi harness chạy đầy đủ (kể cả trong `-Gate G3`):
- **Có**: hai kết nối SQL độc lập tranh nhau hạn mức xuất/giao Rework; dùng đúng câu lệnh khóa (app lock, khóa dòng) lấy từ source production; mỗi kịch bản có "đối chứng không khóa" bắt buộc bán lố — chứng minh test sẽ bắt được nếu thiếu khóa.
- **Không**: không gọi `QTChungService.GiaoHangRework` / `ReworkStockService.XuatKhoRework` thật (cần cả đồ thị WinForms); không đụng bảng `dbo` nghiệp vụ (dùng bảng tempdb). Thứ tự bước trong code được giữ bằng source-contract test.

## 5. Mẫu ghi bằng chứng (dán vào `WMS_RELEASE_GATE.md` §7 sau mỗi lần chạy)

```
Gate (nhãn WMS_RELEASE_GATE): G_
Lệnh chạy:                    Invoke-PctpQcGates.ps1 -Gate G_
Commit SHA đầy đủ:            <40 ký tự>   (cây làm việc sạch: có / không)
Ngày giờ, người chạy:         
Server / database:            192.168.200.57 / B7R2_FCC
Nhận diện schema:             <ngày backup hoặc hash schema>
Kết quả script:               PASS / FAIL
Thư mục bằng chứng:           artifacts\gates\<short>\G_\
Ghi chú / ngoại lệ:           
```

Theo `WMS_RELEASE_GATE.md` §5, thiếu SHA, định danh test, định danh schema hoặc kết quả chạy thật thì chỉ được ghi `IMPLEMENTED / NOT VERIFIED`.

## 6. Khi có lỗi

Gửi: tên gate, thư mục `artifacts\gates\<short>\<Gate>\` (log), và 30 dòng cuối của log lỗi. Không gửi mật khẩu hoặc connection string.
