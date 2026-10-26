# G1 — Phân loại gap (triage) trên ma trận truy vết

- Nguồn: `docs/TRACEABILITY_MATRIX.md` + `PCTP.QC.UI/G1-gaps.csv`, sinh bằng `python PCTP.QC.UI/Build-TraceabilityMatrix.py --docs docs --out <dir> --allow-gaps` trên commit `53af05dcde9d8c0da9942dfca3369098c7a30701` cộng patch này.
- Kết quả: **88 dòng code (66 GAP), 17 SQL script, 22 dòng TestMatrix chưa `implemented`, 105 gap**. G1 vẫn **FAIL** (chỉ PASS khi 0 gap). Tài liệu này phân loại gap để đóng có thứ tự; **không** làm giảm tiêu chí và **không** sửa generator.
- Cột Owner để trống có chủ đích: gate yêu cầu mỗi ngoại lệ có owner và lý do, owner do người sở hữu repo chỉ định.

## 1. Ba snapshot G1 đang tồn tại — chỉ snapshot này là hiện hành

| Snapshot | Commit sinh | Số liệu | Ghi chú |
|---|---|---|---|
| `PCTP.QC.UI/G1-gaps.csv` (trước patch) | `ab064ed…` | 122 dòng | Có trong cây nhánh; `WMS_RELEASE_GATE.md` từng ghi là vắng mặt |
| `docs/TRACEABILITY_MATRIX.md` (trước patch) | `8f00ebe…` | 87 dòng code, 104 gap | Lỗi thời |
| Snapshot này | HEAD + patch | 88 dòng code, 105 gap | Hiện hành; dùng cho mọi tham chiếu G1 |

Ba số liệu khác nhau vì code và test thay đổi giữa các commit, không phải vì tiêu chí đổi.

## 2. Giới hạn của ma trận (đọc trước khi tin dòng "OK")

1. **Test được nối theo tên.** Một dòng có test khi tên class/file xuất hiện (`\bTên\b`) trong bất kỳ file test nào. Tên xuất hiện trong chuỗi đường dẫn hoặc comment cũng tính. "OK" nghĩa là *có nhắc tới*, không nghĩa là *có kiểm chứng hành vi*.
2. **`PhieuKhoRepository` (G1-015) được tính COMPLETE** dù là blocker P0 B0-01 (còn ghi `STOCKTP` ngoài KhoCore). Dòng này không được dùng làm bằng chứng đóng B0-01.
3. **Service không chứa literal SQL ghi** (`QTChungService`, `ReworkStockService`, `StockExportService`, `NhapTpReceivingService`) bị xếp mức READ, dù chúng sở hữu transaction và khóa `sp_getapplock`. Số gap WRITE đếm thiếu đường điều phối; các đường này được bảo vệ bằng test riêng (contract test, kịch bản SQL hai phiên) chứ không thể suy ra từ ma trận.
4. **Cột "SQL script → code" chỉ quét C#.** Script được tham chiếu từ PowerShell, tài liệu hoặc test (`QC_G4_DeliveryFixture_*`, `PCTPStockMovementGuard`) vẫn bị báo `no-code-reference`.
5. SQL ghép động hoặc tên bảng nằm trong biến không bị bắt (giới hạn đã nêu trong chính ma trận).

## 3. Gap code mức WRITE (27 dòng) — thứ tự đóng đề xuất

| Tier | Mục tiêu | Dòng | Thiếu | Hành động đóng |
|---|---|---|---|---|
| T0 | B0-02/B0-03 thật (harness gọi method thật) | G1-072 `TraHangQTChungRepository` | sql-test | SQL test cho `InsertXuat/InsertGiao/InsertQC/GetGiao/GetXuatByOperationId` trên fixture cô lập; điều kiện tiên quyết cho harness gọi `XuatKhoRework`/`GiaoHangRework`/`QCXacNhanCuoi` |
| T0 | B0-02/B0-03 | G1-071 `PhieuXuLyBatThuongRepository` | sql-test | SQL test đọc/ghi trạng thái phiếu và `UpdateStatusIfCurrentIs` |
| T0 | B0-01 Delivery | G1-014, G1-016, G1-017 (`PhieuGiaoDB`, `PhieuLot`, `PhieuLuuTru`), G1-021 `TableOrderRepo` | sql-test | Đi cùng các kịch bản parity P1–P13; không đóng riêng lẻ |
| T0 | B0-01 Delivery | G1-015 `PhieuKhoRepository` | (tính OK) | Theo dõi bằng ratchet `DeliveryLegacyWriterInventoryTests` và parity P1–P13, không bằng ma trận |
| T1 | Single Writer KhoCore/XuatKho | G1-047, G1-048, G1-050 (`QuarantineReleaseAudit`, `Rack`, `StockHistory`), G1-084 `HangChoGiaoRepository`, G1-086 `StockExportRepository` | sql-test / test | Mở rộng harness SQL hiện có (`ProductionBulkStockSlot*`) sang các repository này |
| T1 | Nền tảng KhoCore | G1-049 `SlotRepository`, G1-051 `WarehouseRepository`, G1-045/046 `Inspection*Repository` | test | Test repository trên fixture cô lập |
| T1 | Nhập kho / hàng lỗi | G1-064 `StockTpStatusRepository`, G1-069 `PhieuGiaoRepository`, G1-070 `PhieuTraHangRepository`, G1-073 `TransactionalPhieuTraHangRepository`, G1-074 `AffectedLotTraceService` | test | Test repository trên fixture cô lập |
| T2 | Báo cáo / cấu hình | G1-006, G1-010 (`BaoCao`), G1-012 `ItemFifoConfigRepository`, G1-019 `PhieuTmpRepository`, G1-033 `MachinePermissionService`, G1-036/038 (`PhieuLoadService`, `PhieuService`) | test / sql-test | Test đọc/ghi nhỏ; riêng báo cáo gắn `REP-001` |

Đã đóng trong patch này: G1-061 `StockTPRepository` (khoảng trống chỉ là tên file khác chữ hoa so với tên class trong tài liệu transaction; xem `docs/TRANSACTION_BOUNDARIES.md`).

## 4. Gap code mức READ (39 dòng chưa có test)

Gồm các service `DELEGATES` và repository đọc (`BaoCao`, `GiaoHangKhach` phần FIFO/DocQR/in phiếu, `KhoCore` Inspection/Slot/Rack/Warehouse service, `NhapKho` dashboard/lookup, `XuLyHangLoi` phụ trợ, `XuatKho` validation). Không có đường ghi stock. Đề xuất:

- Nếu đọc thuần: có thể được chấp nhận là **ngoại lệ có chủ đích** với lý do "read-only, không đổi dữ liệu", **chỉ khi owner ký** từng dòng. Không tự động coi là đóng.
- `StockExportService` (G1-087) và `StockTpLookupService` (G1-066) thuộc đường xuất/nhập stock: không nên chấp nhận ngoại lệ; cần test hành vi.

## 5. SQL script (17 file)

Phân loại theo `WMS_RELEASE_GATE.md` §8: (a) triển khai/migration, (b) fixture test, (c) chẩn đoán, (d) lỗi thời. Phân loại dưới đây dựa trên tiêu đề script và nơi được tham chiếu; **owner/DBA cần xác nhận**. Không file nào được đề xuất xóa.

| Script | Loại đề xuất | Bằng chứng | Gap ma trận | Hành động |
|---|---|---|---|---|
| `A0_SlotLot_Diagnose_Repair.sql` | (c) chẩn đoán/khôi phục thủ công | Header: bước 1–4 chỉ SELECT, bước 5 mặc định ROLLBACK | no-code, no-test | Ngoại lệ có chủ đích; ghi vào runbook, không có test tự động |
| `NHAP_TP_HIS_Retire.sql` | (a) migration ngừng dùng bảng | Header: code không còn đọc/ghi `NHAP_TP_HIS`, chạy từng bước | no-code, no-test | Ngoại lệ; xác nhận không có tham chiếu trước khi DBA chạy |
| `PCTPQuarantineRelease.sql` | (a) triển khai | Tham chiếu bởi `QuarantineReleaseAuditRepository` và `docs/TRANSACTION_BOUNDARIES.md` | no-test | Gắn tên bảng/index vào kịch bản SQL quarantine release |
| `PCTPStockMovementGuard.sql` | (a) triển khai | Tham chiếu bởi `BulkStockSlotRepository`, `LegacyStockSlotRepositoryAdapter`, `SqlEngineWorkflowScenario` | no-code (generator chỉ quét literal SQL) | Gap giả do giới hạn #4 |
| `PCTP_EnforceUniqueStockTpLot.sql`, `PCTP_StockTpIdentityAudit.sql` | (a) triển khai / (c) audit | Chỉ có `AtomicityFindingsSourceContractTests` (đọc nguồn) và `WMS_STOCK_STATIC_AUDIT.md` | no-code, no-test | Cần chạy trên schema đại diện (G4) |
| `PHASE15_HangChoGiaoPickDetails.sql` | (a) triển khai | Code dùng `FVN_HangChoGiaoPickDetail` | no-test | Thêm kịch bản tạo bảng từ script |
| `PHASE16_QC_Final_ExecutionTimeout_Fix.sql` | (a) migration liên quan `QCXacNhanCuoi` | Header nêu `QTChungService.QCXacNhanCuoi` | no-code, no-test | Gắn với harness gọi method thật |
| `QC_G4_DeliveryFixture_Seed.sql`, `QC_G4_DeliveryFixture_Cleanup.sql` | (b) fixture | Seed được `DeliveryProductionAcceptanceScenario.cs` tham chiếu | no-code, no-test | Gap giả: fixture không phải đối tượng code |

## 6. Dòng TestMatrix chưa `implemented` (22 dòng)

| Nhóm | ID | Gắn với |
|---|---|---|
| UI journey (G6) | UI-001, UI-003, UI-004, UI-005, UI-006, UI-007, WMS-E2E-001 | G6 |
| Giao khách (B0-01) | DEL-003, DEL-004, DEL-007 | parity P1–P13 |
| Hàng lỗi / cách ly | RWK-002, RWK-008, QC-RELEASE-001, RWK-IDEM-001 | G5, harness method thật |
| Nhập kho / khóa / định danh | IN-TXN-001, STK-LOCK-001, STK-IDENTITY-001, QR-004 | G3/G4 |
| Báo cáo / hiệu năng / YMVN | REP-001, PERF-002, YMVN-SPEC-001, YMVN-SPEC-002 | G4 / G6 |

## 7. Quy tắc cập nhật

Mỗi lần đóng một gap: sửa code/test/tài liệu thật, chạy lại generator, commit lại cả `docs/TRACEABILITY_MATRIX.md` và `PCTP.QC.UI/G1-gaps.csv`. Không chỉnh tay hai file này; không nới regex để làm gap biến mất.
