# G4 — Schema baseline evidence

> **Cập nhật phạm vi 2026-10-27:** Các số liệu coverage cũ trong tài liệu này là snapshot lịch sử,
> trước khi chuyển sang SQL2 metadata và trước khi giới hạn schema theo PCTP runtime inventory.
> Không dùng các số liệu cũ để kết luận phạm vi hoặc số bảng CI hiện tại.
>
> Baseline hiện tại phải được sinh từ `docs/PCTP_SQL_Runtime_Inventory_A-K.csv` + SQL literal
> references trong PCTP + FK closure từ SQL2 metadata DB57. Không dựng toàn bộ 550 bảng B7R2_FCC.
> `docs/SQL.text` chỉ là tư liệu lịch sử bị mất thông tin, không phải nguồn DDL/absence proof.
> Xem `SQL/QC_Schema/README.md` để biết quy tắc scope hiện hành.


> **Cập nhật 2026-10-27:** Các số liệu coverage bên dưới là snapshot lịch sử của lần rà soát
> trước, khi `schema_coverage.py` còn đọc `docs/SQL.text`. Không dùng các số liệu đó để
> kết luận schema DB57 hiện tại. Coverage hiện tại đã chuyển sang `docs/SQL2_ColumnInventory.csv`
> và `SQL/Result_Export_SQL2_Complete_DDL_Metadata.txt`; absence trong snapshot được phân loại
> là `GAP-db57-unverified`, không phải bằng chứng object không tồn tại. CI tạo coverage tươi,
> sinh table/constraint DDL từ cùng snapshot, kiểm tra exact column/constraint metadata và
> đưa transitive dbo FK dependencies vào schema disposable. Xem `SQL/QC_Schema/README.md`.

Phạm vi: đầu vào của G4 (`SQL/QC_Schema/*`, các proc trong `SQL/`, fixture) và hai file bạn bổ sung
`docs/SQL.text` + `docs/PCTP_SQL_Runtime_Inventory_A-K.csv`.  
Công cụ: `SQL/QC_Schema/tools/sql_batch_check.py` (mô phỏng cách `sqlcmd -b -i` tách batch và nạp file; không kết nối DB).

> **Trạng thái G4 không đổi: 🔴 BLOCKED.** Tài liệu này chỉ chứng minh tĩnh rằng đầu vào nạp được và ghi các chênh lệch với DB57.
> Chưa có lần chạy SQL Server nào trên SHA này; bằng chứng G4 vẫn phải là run CI (hoặc `Invoke-PctpQcGates.ps1 -Gate G4`) ghi SHA đầy đủ.

## 1. Bộ nạp batch — mô phỏng

| Đầu vào | Kết quả |
|---|---|
| 7 file deploy của CI (`01_Tables`, `05_Split`, `Usp_Qrcode_Update_Stock2405`, `Usp_Qrcode_Update_Stock_SP`, `sp_GetCurrentStockStatus`, seed, cleanup) | **0 lỗi**: không `\n` literal, không dòng nhiễu SSMS, không `USE`, không biến sqlcmd, mọi `CREATE PROC/FUNCTION` là lệnh đầu batch, không dấu phẩy thừa, không mã định danh server production |
| `docs/SQL.text` | 69 batch, 68 object, **16 object là stub** (không tồn tại trên DB lúc xuất). **Không nạp được nguyên trạng** (mục 2) |
| Bản chuẩn hóa sinh bởi `--emit-loadable --halve-unicode-lengths` | 52 object, 0 lỗi nạp; 45 câu `CREATE TABLE` parse được bằng `sqlglot` (T-SQL) |

Công cụ có 11 unit test (`python3 -m unittest discover -s SQL/QC_Schema/tools`), đã thêm vào job `sql-engine-integration` trước bước deploy.

## 2. `docs/SQL.text` — lỗi của file xuất

| Lỗi | Số liệu |
|---|---|
| Chuỗi `\n` là hai ký tự `\` + `n` thay cho xuống dòng trong DDL | 45/45 câu `CREATE TABLE` |
| Dấu phẩy thừa trước `)` | 21 batch |
| Dòng nhiễu SSMS `(68 rows affected)` ở đầu file | 1 |
| Độ dài `NVARCHAR/NCHAR` gấp đôi thật (xuất `max_length` tính bằng byte như số ký tự) | trên 15 bảng trùng với `01_Tables.sql`: 119 cột đúng gấp 2×; sau khi chia đôi có 216 cột khớp, 0 cột gấp 2× |
| Mất `IDENTITY`, `UNIQUE` và khóa ngoại so với `01_Tables.sql` | `IDENTITY` mất ở `Warehouse`, `Rack`, `Slot`, `SlotLot`; `UNIQUE` `UQ_LUUPHIEU` mất ở `LUUPHIEUGIAOHANG`; các FK của `Slot`/`SlotLot` không có trong file xuất; PK vẫn còn |

Hệ quả: **không dùng `docs/SQL.text` làm nguồn DDL cho G4.** Nó đúng khi dùng làm tài liệu (tên bảng, cột, nội dung proc).
Nếu dùng DDL thì chỉ qua `--emit-loadable --halve-unicode-lengths`, và chỉ sau khi chủ DB xác nhận quy tắc chia đôi (đã khớp 216/216 cột trên 15 bảng đối chiếu).
Nên sửa bộ xuất: dùng `max_length/2` cho `nvarchar/nchar`, xuống dòng thật, không dấu phẩy thừa, bỏ dòng kết quả SSMS.

## 3. Chênh lệch giữa DB57 (theo file xuất) và Git

**Proc.** So sánh sau khi chuẩn hóa khoảng trắng/comment:

| Proc | DB57 vs Git |
|---|---|
| `Usp_Qrcode_Update_Stock2405` | **Khác.** DB57 có `PCTP_DELIVERY:`, `sp_getapplock`, savepoint, `@LockOwner = N'Transaction'`, `XACT_STATE()` (đủ marker preflight) nhưng theo so sánh văn bản **thiếu** `WITH (UPDLOCK, HOLDLOCK, ROWLOCK)` khi đọc LOT vật lý, biến `@StockRowsUpdated`, và đoạn rollback tường minh ở các nhánh trả lỗi rồi `RETURN` |
| `Usp_Qrcode_Take_Lot2405`, `sp_GetCurrentStockStatus` | Khác (ví dụ `sp_GetCurrentStockStatus` ở Git dùng CTE `StockLotKeys`; chưa xác định bản nào mới hơn) |
| `Usp_Qrcode_Update_Stock_SP`, `Usp_Qrcode_Take_LotYMVN2405` | **Không có trong file xuất** (G4 preflight kiểm `Usp_Qrcode_Update_Stock_SP`) |
| `Usp_Qrcode_LOAD_PHIEU_DOCQR2405` | Có trên DB57; Git có `SQL/Usp_Qrcode_LOAD_PHIEU_DOCQR2405.sql`, chưa so sánh |

Kết luận: chạy G4 trên DB57 thật cần deploy các proc từ Git trước (runbook §3.3). G4 trên CI dùng proc từ Git nên không bị ảnh hưởng.

**Object không tồn tại trên DB57 lúc xuất (16 stub):** bảng `PCTPStockMovementGuard`, `PCTPQuarantineReleaseAudit`, `DeliveryCommandLedger`, `DeliveryCommandQr` (script có trong `SQL/` nhưng chưa deploy lên DB57), `CustomerConfig`, `CustomerTableConfig`, `GiaoHangKhachCustomerOptions`, `GridViewSettings`, `QCDONGGOI`, `Purchase_Order_HVNDB`; view `IFSPHIEUGIAOHANG*View` (4); proc `SP_TMPPHIEUGIAOHANG`, `SP_DOCQRCODE`.
`DeliveryCommandSqlScenario` tự nạp `PCTP_DeliveryCommandLedger.sql` nên không phụ thuộc DB57.

**`dbo.Split`.** File xuất chứa định nghĩa chính thức (hàm bảng nhiều lệnh, cột `strval`, giữ thứ tự token). `05_Split.sql` thay shim `STRING_SPLIT` (cột `value`, không bảo đảm thứ tự). Đã sửa README và workflow. Proc đọc `SELECT *` nên không phụ thuộc tên cột.

## 4. Việc cần chủ DB quyết định (chưa sửa)

1. **Hai bảng khách `_100003`** khác `01_Tables.sql`: `DOCQRCODE_100003` có thêm `GEAR`, `SOPHIEU`; `TMPPHIEUGIAOHANG_100003` có thêm `GEAR`; `SLTEMHVN`/`SLTEMFCC`/`SOLUONG` là `int` (Git: `smallint`); `STATUS` là `nvarchar` (Git: `smallint` hoặc `nvarchar(50)`). Cần biết DB57 hiện tại hay `SQL_sql.sql` cũ là đúng trước khi sửa `01_Tables.sql`; G4 mặc định (STT 32001, khách mặc định) không dùng hai bảng này.
2. Các bảng stub như `CustomerConfig`, `CustomerTableConfig`: do code tạo lúc chạy hay chưa từng có trên DB57? Cột "Có trên DB57?" trong CSV hiện ghi "Chưa đối chiếu"; nên cập nhật từ danh sách stub ở trên.
3. Dòng `dbo.Split` trong CSV vẫn nhắc shim; cập nhật thành `SQL/QC_Schema/05_Split.sql`.

## 5. Giới hạn của kiểm tra này

- Môi trường soạn patch không có SQL Server lẫn .NET SDK: chưa nạp thật, chưa biên dịch. Patch không đổi file `.cs` hay `.csproj`.
- Mô phỏng chỉ bắt lỗi nạp tĩnh (tách batch, cú pháp bề mặt). Lỗi tên cột, kiểu dữ liệu, phụ thuộc bảng chỉ lộ khi CI chạy thật.
