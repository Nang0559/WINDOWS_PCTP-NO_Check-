# WMS — schema DB57, độ phủ test và sổ phát hiện (tính năng → sửa SQL → sửa code)

Nguồn bằng chứng: `docs/SQL.text` (xuất từ DB57), `docs/PCTP_SQL_Runtime_Inventory_A-K.csv`, SQL trong Git, và mã nguồn C# của `PCTP/`.
Công cụ: `SQL/QC_Schema/tools/schema_coverage.py` (tĩnh, không kết nối DB). Kết quả máy sinh: `docs/SCHEMA_COVERAGE.csv` (đối tượng) và `docs/SCHEMA_COVERAGE_columns.csv` (cột).

> **Quy ước.** "DB57" = `192.168.200.57 / B7R2_FCC` (DB test). "DB14" = production `192.168.200.14\BRAVO`, không bao giờ dùng cho test/deploy.
> "Không có trong file xuất" nghĩa là **chưa có bằng chứng** đối tượng tồn tại trên DB57, chưa phải "chắc chắn không có". Xác nhận bằng cách xuất thêm đối tượng đó (mục 5).
> Mọi mục dưới đây là kết quả đọc tĩnh. Chưa có lần chạy SQL Server nào trên DB57 thật.

## 1. Test có đụng DB57/DB14 không?

| Lớp | Kết luận (có bằng chứng) |
|---|---|
| CI `unit-regression-tests` | Không có DB: chỉ đọc file nguồn được link. |
| CI `sql-engine-integration` | Chỉ có container SQL Server dùng một lần; job không có thông tin đăng nhập tới DB57/DB14. `TestEnvironmentPolicy` ở chế độ CI chỉ cho `127.0.0.1`/`localhost` + database `PCTP_QC_CI`, từ chối cả DB57. |
| Harness `PCTP.QC.SqlIntegration` | Chỉ biên dịch một tập file production chọn lọc; không link `Settings`, `SQL.cs` hay `App.config`, nên không có đường nào tự nối tới DB14. |
| `PCTP.QC.TestRunner` (chạy tay) | Cố định DB57, hiển thị "Production: blocked". Đây là đường **có chủ đích** dùng DB57. |
| Script `.sql` chạy tay trong SSMS | **Không** có bảo vệ tự động (runbook §0). Chỉ các script fixture có `THROW` khi `@@SERVERNAME LIKE '%BRAVO%'`. |
| Ứng dụng PCTP khi chạy | `PCTP/Infrastructure/Sql/SQL.cs` đang trỏ DB57 (dòng DB14 bị comment). `App.config`/`Settings.*` vẫn có chuỗi kết nối DB14. Không phải đường test, nhưng là rủi ro vận hành. |
| `WINDOWS_PCTP/DBUtils.cs` | Cố định `192.168.200.57,1433`. Dự án riêng, không thuộc bộ test. |

### Cảnh báo bảo mật (cần xử lý riêng, ngoài phạm vi test)

`PCTP/App.config`, `PCTP/Properties/Settings.settings`, `Settings.Designer.cs`, `PCTP/Infrastructure/Sql/SQL.cs` và `PCTP/SQLTABLECREATOR.cs` chứa **mật khẩu tài khoản `sa` dạng văn bản rõ**, cho cả DB57 và DB14. Tài liệu này không lặp lại giá trị.
Việc cần làm: đổi mật khẩu `sa` trên cả hai server (xem như đã lộ vì nằm trong lịch sử Git), chuyển sang tài khoản quyền tối thiểu và đưa chuỗi kết nối ra ngoài repo (biến môi trường / cấu hình người dùng). Chỉ xóa file là không đủ vì lịch sử Git vẫn giữ.

## 2. Đã xây tới đâu

| Hạng mục | Hiện trạng |
|---|---|
| Schema CI dùng một lần (`SQL/QC_Schema/01_Tables.sql`) | 15 bảng + `dbo.Split` + 3 procedure (`Usp_Qrcode_Update_Stock2405`, `_Stock_SP`, `sp_GetCurrentStockStatus`). Đủ cho G4 giao hàng và G6 stock/slot; **chưa** có các bảng Rework và nhập kho khác. |
| Bộ trích xuất DB57 (`SQL.text`) | 68 đối tượng, 16 stub (không tồn tại lúc xuất), xuất lỗi (mất IDENTITY/UNIQUE/FK, độ dài gấp đôi). Dùng làm tài liệu, chưa dùng trực tiếp làm DDL. |
| Phủ đối tượng mà code tham chiếu | Xem bảng dưới (sinh bằng công cụ). |
| Phủ cột | Công cụ so cột mà code **INSERT/UPDATE** với schema DB57: 1 chênh lệch còn lại (mục F-02) sau khi sửa F-03. SELECT chưa kiểm. |
| Test chạy trên schema DB57 cho `TraHangQTChungRepository`, `PhieuXuLyBatThuongRepository` | **Chưa**: bảng `FVN_TraHangQTChung*`, `FVN_PhieuXuLyBatThuong`, `FVN_PhieuTraHang(CT)` chưa nằm trong schema CI (mục F-07). |

Phân loại đối tượng mà code dùng (`schema_coverage.py`):

| Lớp | Số | Ý nghĩa |
|---|---|---|
| `OK-testable` | 11 | Có trên DB57, có DDL trong Git, có trong schema CI. |
| `GAP-ci-schema` | 6 | Có trên DB57 và DDL trong Git, nhưng chưa nằm trong schema CI. Làm trước: `FVN_TraHangQTChung{Xuat,Giao,QC,NhapNG}`, `FVN_HangChoGiaoPickDetail`, `Usp_Qrcode_Take_Lot2405`. |
| `GAP-no-git-ddl` | 21 | Có trên DB57 nhưng Git không có DDL; muốn test phải dựng từ file xuất (cần quy tắc IDENTITY, xem F-08). |
| `GAP-not-deployed-57` | 13 | Git có script nhưng file xuất không có đối tượng (F-04, F-05). |
| `GAP-undefined` | 19 | Code dùng nhưng cả file xuất lẫn Git đều không định nghĩa (F-06). |

## 3. Sổ phát hiện — tính năng → SQL sửa gì → code sửa ở đâu

| ID | Tính năng | Bằng chứng | Sửa SQL | Sửa code | Trạng thái |
|---|---|---|---|---|---|
| F-01 | CI bước kiểm schema tĩnh | `docs/SQL.sql.txt` đã đổi tên thành `docs/SQL.text`; workflow vẫn gọi tên cũ (tái hiện: công cụ thoát mã 2, "No such file") | — | `.github/workflows/pctp-qc-tests.yml` và các tài liệu tham chiếu | **Đã sửa trong patch** |
| F-02 | Xuất kho Rework idempotent (B0-02) | File xuất: `FVN_TraHangQTChungXuat` **không có cột `OperationId`**; code ghi/đọc cột này | Chạy `PCTP/Modules/XuLyHangLoi/Database/PHASE14_ReworkExportIdempotency.sql` trên DB57 (sau PHASE13), rồi xuất lại | Không cần: `TraHangQTChungRepository.InsertXuat` (dòng 39), `GetXuatByOperationId` (183), `QTChungService.XuatKhoRework` (298) đã đúng | **Mở — chờ SQL** (ghi trong `known_column_gaps.txt`) |
| F-03 | Tạo Rack (`WarehouseService` dòng 48) | Code `INSERT INTO Rack (…, RowCount, …)`; DB57 và `01_Tables.sql` có cột `RackRowCount` | — | `RackRepository.cs` dòng 32: `RowCount` → `RackRowCount` | **Đã sửa trong patch** + test `RackRepositoryColumnContractTests` |
| F-04 | Rework workflow: tách lô ảnh hưởng, QC ban đầu, disposition, compensation, báo cáo | Code dùng `FVN_PhieuXuLyBatThuongAffectedLot`, `…QCInitial`, `FVN_PXLB_*` (5 bảng), view `vFVN_PXLB_ProcessReport`; **file xuất không có**; Git có PHASE2/3/4_9 | Kiểm tra trên DB57 xem các object này có chưa; nếu chưa, chạy PHASE2, PHASE3, PHASE4_9, PHASE7 theo `WMS_SQL_RUNBOOK.md` §3.1 rồi xuất lại | Không | **Cần xác nhận** |
| F-05 | Giao hàng B0-01, cách ly, YMVN | Stub trên DB57: `DeliveryCommandLedger/Qr`, `PCTPQuarantineReleaseAudit`, `PCTPStockMovementGuard`; thiếu `Usp_Qrcode_Update_Stock_SP`, `Usp_Qrcode_Take_LotYMVN2405`. `Usp_Qrcode_Update_Stock2405` trên DB57 khác Git (thiếu `UPDLOCK/HOLDLOCK/ROWLOCK`, rollback tường minh — theo `G4_SCHEMA_FROM_GIT.md` §3) | Deploy từ Git theo runbook §3.2–3.3 lên DB57 (có backup); không dán tay | Không | **Mở — chờ SQL** |
| F-06 | Code gọi object không có trong file xuất lẫn Git | `usp_qrcode_gheplot` (PhieuLotRepository), `usp_qrcode_load_hangthieu`/`…view` (PhieuKhoRepository), `usp_gheplotprint` (SqlRepository), `sp_getstockhistory` (StockHistoryRepository), `SP_DOCQRCODE`/`SP_TMPPHIEUGIAOHANG` (stub), `QCDONGGOI` (TableOrderRepo), `YMVN_TMPPHIEUGIAOHANG`, `tmpLotGhep/Tach`, `B20Dept/ImageStore/ItemQuyCach`, `tbl_QR_May_DocQR` | Xuất định nghĩa từ DB57 (mục 5); nếu đối tượng không tồn tại thì code đó đang chết hoặc dùng cấu hình khác | Nếu xác nhận không tồn tại và còn đường gọi: sửa/loại code tương ứng | **Cần xác nhận** |
| F-07 | Test SQL cho `TraHangQTChungRepository`, `PhieuXuLyBatThuongRepository` | 4 bảng `FVN_TraHangQTChung*` có DDL ở Git (PHASE13) nhưng không nằm trong schema CI; `FVN_PhieuXuLyBatThuong`, `FVN_PhieuTraHang(CT)` không có DDL Git | Thêm vào schema CI (xem F-08) | Sau đó mới viết harness C# | **Bước kế tiếp** |
| F-08 | Dựng schema CI từ DB57 | File xuất mất IDENTITY/UNIQUE/FK; ví dụ `Id INT NOT NULL` không IDENTITY nhưng code `INSERT … OUTPUT INSERTED.Id` | Xuất lại bằng truy vấn đọc-chỉ (sys.columns, identity, default, PK/UQ, FK, index); hoặc quy tắc "Id là IDENTITY khi mọi INSERT của code bỏ cột Id" đã được chủ DB xác nhận | Không | **Cần chủ DB** |
| F-09 | IFS (Oracle) | `CUST_SCHED_LINE_TAB`, `CUSTOMER_ADDRESS_AV`, `CUSTOMER_ORDER_JOIN` do `IFSRepository` truy vấn qua nguồn ngoài | Không thuộc schema SQL Server | Test bằng giả lập hoặc đánh dấu ngoài phạm vi | Phân loại |
| F-10 | CI job `unit-regression-tests` đỏ (bước "Run isolated unit and regression tests") | Workflow đặt `PCTP_QC_REQUIRE_EPHEMERAL_SQL=true` ở cấp workflow, nên cả job không có DB cũng chạy ở chế độ CI. `TestEnvironmentPolicyTests` kỳ vọng chế độ dev: 3 dòng `192.168.200.57;B7R2_FCC` mong `true` nhưng chế độ CI trả `false` (đúng thiết kế). Suy ra từ mã nguồn; log chi tiết chưa xem | — | `TestEnvironmentPolicyTests`: ghim biến môi trường theo từng test (constructor chọn chế độ dev, `Dispose` khôi phục), tách collection không song song, thêm 6 ca chế độ CI + 1 ca allow-ephemeral | **Đã sửa trong patch (chưa có log CI xác nhận)** |
| F-11 | CI `sql-engine-integration`: G4 `--delivery-preflight` đỏ, bước Cleanup đỏ theo sau | Preflight đòi 6 procedure; bước Deploy chỉ cài 3. Thiếu `Usp_Qrcode_LOAD_PHIEU_DOCQR2405`, `Usp_Qrcode_Take_Lot2405`, `Usp_Qrcode_Take_LotYMVN2405`. Hai script sau bắt đầu bằng `USE [B7R2_FCC]` (công cụ tĩnh báo E004 vì rời DB CI). Job page: G3 xanh, G4 preflight đỏ | Không đổi script production: bản sao CI bỏ dòng `USE [B7R2_FCC]` | Workflow: bước chuẩn bị bản sao + thêm 3 file vào kiểm tra tĩnh và deploy; test `CiWorkflowCoversDeliveryPreflightTests` giữ danh sách deploy và preflight đồng bộ | **Đã sửa trong patch.** Nguyên nhân bước Cleanup đỏ **chưa xác định** (cần log bước đó; có thể là hệ quả của lỗi trước) |
| F-12 | Ghi `LUUPHIEUGIAOHANG` thống nhất (UQ_LUUPHIEU 8 cột, GEAR/PO/HOP) | `Usp_Qrcode_Update_Stock2405` (dòng 989, cột PO ghép động qua `@LuuPoCols`) và `Usp_Qrcode_Update_Stock_SP` (dòng 221) **đều không ghi `GearYMVN`**; chỉ đường C# YMVN và `DeliveryTmpPendingGate` ghi. `GEAR` ở họ TMP YMVN là `nvarchar(160)`, `GearYMVN` là `nvarchar(50)` (cắt/ lỗi 8152 nếu dài hơn 50) | Quyết định chủ DB: có ghi `GearYMVN` trong hai SP không; thống nhất độ dài `GearYMVN` với `GEAR` | `DeliveryTmpPendingGate` đã đọc đầy đủ cột có ở cả hai phía (HOP, STATUSDOC, Note, PO_NO, PO_ITEM, GEAR→GearYMVN), báo lỗi nghiệp vụ khi vi phạm `UQ_LUUPHIEU`, kiểm tra lưu lịch sử QR == xóa nguồn, ngày giao truyền kiểu `smalldatetime` | **Một phần**: code có test SQL; SQL SP chờ quyết định |
| F-13 | Schema CI chưa khớp DB57 ở họ `_100003` | Công cụ tĩnh: cột chỉ có ở file xuất `docqrcode_100003.gear`, `docqrcode_100003.sophieu`, `tmpphieugiaohang_100003.gear`; khác kiểu `docqrcode_100003.sltemfcc/sltemhvn` (int vs smallint), `.status` (nvarchar(20) vs smallint), `tmpphieugiaohang_100003.soluong` (int vs smallint), `.status` (nvarchar(20) vs nvarchar(50)). Độ dài NVARCHAR trong file xuất gấp đôi (W010) | Chủ DB xác nhận DB57 thật; sửa `SQL/QC_Schema/01_Tables.sql` theo xác nhận (không dùng file xuất làm DDL) | Test GEAR đang dùng bảng `##` suy ra từ `TMPPHIEUGIAOHANG` + cột `GEAR`; chuyển sang `_100003` sau khi F-13 xong | **Cần chủ DB** |

Giới hạn của công cụ: tên đối tượng ghép lúc chạy (hậu tố khách hàng `_100003`, tên bảng trong cấu hình) không giải được tĩnh — 15 file có tham chiếu động; chỉ kiểm cột cho câu `INSERT … (cột)` và `UPDATE … SET`.

## 4. Cách biến kết quả test thành việc cần sửa

Với mỗi test đỏ trên schema DB57 (hoặc dòng nào của `SCHEMA_COVERAGE*.csv`), phân loại theo nguyên nhân rồi ghi vào bảng ở mục 3:

| Triệu chứng | Nguyên nhân | Sửa SQL | Sửa code |
|---|---|---|---|
| `Invalid column name 'X'` | Code đòi cột chưa có trên DB57 | Chạy script thêm cột đã có trong Git; nếu chưa có script, viết script idempotent trong `SQL/` | — |
| `Invalid column name 'X'` mà cột đúng tên khác trên DB57 | Code sai tên cột (như F-03) | — | Sửa tên cột tại câu SQL |
| `Invalid object name 'T'` | Bảng/proc chưa deploy lên DB57 | Deploy script Git tương ứng | — |
| `Invalid object name 'T'` và không có script Git | Đối tượng chỉ tồn tại ở DB khác hoặc code chết | Xuất định nghĩa từ DB57 và đưa vào Git | Nếu code chết thì loại bỏ |
| Hành vi khác kỳ vọng nhưng không lỗi cú pháp | Proc trên DB57 khác Git (như `Usp_Qrcode_Update_Stock2405`) | Deploy bản Git, hoặc cập nhật Git theo DB57 nếu DB57 mới hơn (chủ DB quyết) | Theo kết luận |
| Vi phạm UNIQUE/PK/FK khi test | Ràng buộc có trên DB57 nhưng schema CI thiếu hoặc ngược lại | Đồng bộ ràng buộc từ lần xuất lại | — |

## 5. Danh sách cần xuất thêm từ DB57 (truy vấn chỉ đọc)

1. Mọi đối tượng ở F-04 và F-06 (nếu có): định nghĩa bảng đầy đủ (IDENTITY, default, PK, UNIQUE, FK, index) và nội dung procedure/view.
2. Xác nhận cột `OperationId` và index `UX_FVN_TraHangQTChungXuat_OperationId` của `FVN_TraHangQTChungXuat` (F-02) trước và sau khi chạy PHASE14.
3. Bản `Usp_Qrcode_Update_Stock2405`, `sp_GetCurrentStockStatus`, `Usp_Qrcode_Take_Lot2405` đang chạy trên DB57 để chủ DB quyết bản nào là chuẩn.
