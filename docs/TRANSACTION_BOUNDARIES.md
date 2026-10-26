# Stock mutation transaction-boundary audit

Audit target: `feature/pctp-qc-test-platform`

This document records a source-level review of the current stock-mutation composition. It is not a SQL integration-test result.

## Verified composition

| Workflow | UnitOfWork composition | Stock mutation participants |
|---|---|---|
| Main stock / physical warehouse | `MainStockModuleFactory.Build()` creates one `UnitOfWork` | `StockExportRepository`, `SlotRepository` through `SlotService`, `StockTpRepository`, history and delivery repositories receive that same instance |
| Finished-goods receiving | `NhapTpModuleFactory.Build()` creates one `UnitOfWork` | `StockTpRepository`, `SlotRepository` through `SlotService`, tracking, import guard, status and history repositories receive that same instance |
| Rework / QC | `XuLyHangLoiModuleFactory.Build()` creates one `UnitOfWork`; the overload also accepts the workflow-owned instance | Slot, STOCKTP, history, rework/QT and abnormal-ticket repositories are composed with that instance |
| Customer delivery | `GiaoHangKhachModuleFactory.Build()` creates `phieuUow` | `BulkStockSlotRepository`, `StockHistoryRepository`, `HangChoGiaoRepository`, and `LegacyStockBalanceRepositoryAdapter` share the same executor/UoW |

Naming note for the G1 generator: the class `StockTpRepository` lives in the file `StockTPRepository.cs` (note the capital P). The generator matches the file stem, so both spellings are written here. `StockTPRepository.cs` does not own a transaction: it takes the workflow `IUnitOfWork` in its constructor and executes through `PhieuSqlExecutor`, so it participates in the transaction begun by `NhapTpReceivingService` (receiving) or by the stock export workflow (main stock). Its SQL behaviour is exercised by `ProductionStockTpRepositoryScenario` against an isolated `#STOCKTP` fixture.

The stock-slot adapter delegates to the workflow's `SlotService`; the balance adapters delegate to repositories constructed with the same workflow UoW. This is the intended path for SQL mutations to participate in one transaction. This row verifies factory composition only; it is not evidence that the complete customer-delivery operation has been executed or rolled back in a SQL integration test.

## Verified workflow ownership

- `NhapTpReceivingService.NhapTpVaoSlot` begins the UoW before the in-transaction duplicate-QR check and stock movement; it commits on success and rolls back on failure.
- `StockExportService` begins a transaction around pick/export, pending-delivery records, history, and delivery confirmation. A failed stock movement causes rollback; exception paths use `SafeRollback`.
- `ReworkStockService` begins a transaction around rework stock movements and associated QT/history writes; failed movements use `FailAndRollback` or the exception rollback path.
- `StockMovementService` intentionally does not begin/commit a transaction for ordinary Receive/Export/Move/Pick operations. Its class contract assigns transaction ownership to the calling workflow. `Correct` is an exception: it requires an injected UoW and owns its local transaction.

## Customer-delivery transaction findings

The production path is composed through `GiaoHangKhachModuleFactory.Build()`: it creates one `phieuUow`, passes that same instance to `BulkStockSlotRepository`, `StockHistoryRepository`, `HangChoGiaoRepository`, and `PhieuRepository`; `PhieuRepository` delegates stock confirmation to `PhieuKhoRepository`. The changes below are in the repository actually reached by `PhieuKhoService`.

- **Stored-procedure paths (`CapNhapKho` / `CapNhapKhoSP`)**: begin `phieuUow` before executing the stock stored procedure and call the transaction-aware `SqlRepositoryBase.ExecuteStoredProcedureDataSet` helper. SlotLot deductions use `manageTransaction: false`, so the SP, every LOT adjustment, pending-delivery closure, and history writes share the same connection/transaction. The stock-changed notification is raised only after commit.
- **YMVN direct/manual path (`CapNhapKhoYMVN`)**: the available-stock check is performed inside the transaction with `UPDLOCK, HOLDLOCK`; FIFO deduction uses guarded updates (`SLCONLAI >= @sl`) and verifies exactly one affected row. SlotLot deduction, QR/history persistence, TMP status update, pending-delivery closure/history, and the configured order-table update execute before the same transaction commits. The stock-changed notification is raised only after commit.
- **Pending-delivery finalization (`HoanTatSauKhiTruKho`)**: no longer opens a nested transaction or suppresses exceptions. It requires the caller's active UoW and a configured `IHangChoGiaoRepository`; closure and associated history are executed through the active connection/transaction. Failure now propagates to the caller and triggers rollback.
- **Stored-procedure result validation**: when a stored procedure returns rows, the result must include valid `LOT` and positive `SOLUONG` values. Missing columns or malformed rows throw rather than silently skipping SlotLot adjustment.
- **YMVN LOT parsing**: malformed LOT/quantity tokens and an empty parsed list are rejected before any stock mutation; invalid tokens are no longer silently ignored.

These are source-level changes and transaction-composition checks. They are not yet proof of SQL rollback under injected failures, concurrent delivery, stored-procedure behavior, or duplicate/replayed delivery confirmation.

## Important limitations and follow-up

1. Unit tests with fake repositories prove validation and call ordering, not SQL rollback or locking behavior.
2. The current default CI does not provision a disposable SQL Server database. No scenario should be labelled SQL integration, deadlock, timeout, retry, or concurrent-write coverage until it runs against that isolated database.
3. `StockMovementService.Move` performs source decrease followed by target insertion. Its atomicity therefore depends on callers using the documented shared transaction. Before adding a new direct caller, verify that it begins the same UoW and rolls back when the movement result is unsuccessful.
4. `LegacyStockSlotRepositoryAdapter` delegates through `ISlotService`; transaction sharing depends on constructing that service with a repository bound to the same UoW, as the factories above currently do.
5. Delivery tests now additionally cover the production `BulkStockAdjustService` with fakes (`DEL-006`); this proves branch/ordering/transaction-callback behavior only. It does not execute the actual SQL repository graph. The required delivery integration scenarios are `DEL-003` (cross-repository rollback), `DEL-004` (duplicate/replayed confirmation), `DEL-005` (insufficient stock/shortfall), and `DEL-007` (stored-procedure/finalization transaction boundary). All remain pending; do not treat the factory's shared-UoW wiring as proof of workflow atomicity.
6. Source review found a concrete delivery consistency gap: stored-procedure routes mutate STOCKTP before the later independently managed SlotLot transaction; YMVN keeps STOCKTP and SlotLot together but performs pending-delivery closure/history finalization after commit. See the findings section above. These are production behavior risks, not merely missing tests.
5. A future SQL integration suite should deliberately fail after the first mutation (for example, after source decrease but before target insert) and assert that both source and target state are unchanged after rollback. It must use only a local database accepted by `TestEnvironmentPolicy.EnsureSafeSqlTestTarget`.

## G1 repository transaction contracts

Source-level audit at commit `9bcf9c9`, written for the G1 traceability matrix. It records what each file does
with `UnitOfWork` as read from the code. It is not SQL evidence: rollback/atomicity is proven only by G3/G4 for the
paths those gates execute.

### Contract vocabulary

Most repositories derive from `SqlRepositoryBase`. Its `ExecuteNonQuery` / `ExecuteScalar` / `ExecuteStoredProcedure`
use the `UnitOfWork` connection and transaction when `Uow.IsInTransaction` is true, and otherwise delegate to
`PhieuSqlExecutor`, which opens its own connection and auto-commits each statement.
`UnitOfWork.Begin()` is nesting-safe (depth counter), so a service may call it inside a caller's transaction.

| Contract | Meaning |
|---|---|
| JOIN-OR-AUTOCOMMIT | Joins the caller's transaction if one is active; otherwise each statement auto-commits. Atomicity of a multi-statement method depends on the caller. |
| OWNS-IF-NO-AMBIENT | `ownTransaction = !HasTransaction`: begins/commits/rolls back its own transaction only when the caller has none. |
| REQUIRES-TX | Throws when no transaction is active. |
| OWNS | Always begins its own transaction (or nests through the depth counter) and commits/rolls back. |
| DELEGATES | Contains no SQL execution call; transaction ownership lives in the repository it calls. |

### Per-file contracts

| File | Writes | Contract | Verified detail |
|---|---|---|---|
| `CurrentStockRepository` | none in C# | READ via SP | Calls `sp_GetCurrentStockStatus` only. The SP body is in `SQL\sp_GetCurrentStockStatus.sql`; `AtomicityFindingsSourceContractTests` asserts the script has no INSERT/UPDATE/DELETE/MERGE/EXEC and keeps the column aliases this repository reads. That checks the script in the repo, not the deployed procedure: confirm the deployed body with `sp_helptext sp_GetCurrentStockStatus` on the test DB. |
| `ItemFifoConfigRepository` | `FVN_ItemFifoConfig`, `FVN_ItemFifoConfig_History` | OWNS-IF-NO-AMBIENT | `Upsert` and `Delete` write config + history in one transaction; commit on success, rollback on exception. |
| `PhieuGiaoDBRepository` | `TMPPHIEUGIAODBHD`, `TMPPHIEUGIAOHANGDBCT` | OWNS-IF-NO-AMBIENT (`TaoPhieuVaChiTietGiaoDB`, `LuuGiaoDB` via `InTransaction`) | See finding F4. |
| `PhieuLotRepository` | `[tenBan]`, `[docQRTable]` via `Usp_Qrcode_Take_Lot*` / `Usp_Qrcode_gheplot` | JOIN-OR-AUTOCOMMIT for the SP and `CapNhapLotTmpPhieu`; `LayLaiLotNo` OWNS-IF-NO-AMBIENT via `InTransaction` | See finding F2. |
| `PhieuLuuTruRepository` | `LUUPHIEUGIAOHANG` | `LuuPhieuSP` OWNS-IF-NO-AMBIENT via `InTransaction` | See finding F3. |
| `PhieuTmpRepository` | TMP/DOCQR/IFS tables | OWNS-IF-NO-AMBIENT (`PushIfsSnapshot`, `LuuVaLoad`: drop/create, bulk insert, guard DELETE and load SP in one transaction); JOIN-OR-AUTOCOMMIT for `InsertTmpRow`, `XoaTmpPhieu`, `XoaDocQRCode` | Single-statement writes. |
| `TableOrderRepo` | `Purchase_Order_YMVN` | OWNS (`UploadMilkrunSP`: own connection + transaction; no `UnitOfWork`) | See finding F1. |
| `MachinePermissionService` | `tbl_QR_MAY_DOCQR` | OWNS | `ChuyenMayBan` opens `_sql.BeginTransaction`, resets TT, then updates or inserts the target machine; commit, rollback on exception. |
| `PhieuLoadService` | none directly | DELEGATES | No SQL execution call; passes the load SP name to `_workingState.SaveFromSource` / `_phieuRepo.LuuVaLoad`, which own the transaction. |
| `PhieuService` | none directly | DELEGATES | No SQL execution call; forwards to `_phieuRepo`, `_khoService`, `_ymvnService`, `_giaoDbService`. |
| `InspectionConfigRepository` | `InspectionConfig` | JOIN-OR-AUTOCOMMIT | Single-statement `Insert` / `Update` / `Delete`. |
| `InspectionLogRepository` | `InspectionLog` | JOIN-OR-AUTOCOMMIT | Single INSERT in `SaveLog`. |
| `QuarantineReleaseAuditRepository` | `PCTPQuarantineReleaseAudit` | REQUIRES-TX | `RecordRelease` throws if `!HasTransaction`. Referenced by `QuarantineReleaseService` and by unit tests with fakes (`QuarantineReleaseServiceTests`); the G3 `QuarantineReleaseScenario` does not reference this repository, so there is no SQL test of the audit insert yet. |
| `RackRepository` | `Rack`, `Slot` | JOIN-OR-AUTOCOMMIT | `DeleteCascade` OWNS-IF-NO-AMBIENT via `InTransaction`; see finding F5. Other writes are single statements. |
| `WarehouseRepository` | `Warehouse` | JOIN-OR-AUTOCOMMIT | Single INSERT in `Insert`. |
| `StockTpStatusRepository` | `STOCKTP` | JOIN-OR-AUTOCOMMIT | `MoLaiLot` and `DongBoSLSXVaMoLaiNeuThayDoi` are single UPDATEs, each checking the affected-row count. |
| `PhieuGiaoRepository` | `LUUPHIEUGIAOHANG` | JOIN-OR-AUTOCOMMIT | Single UPDATE in `CapNhatNotePhieuGiao`. |
| `PhieuTraHangRepository` | `FVN_PhieuTraHang`, `FVN_PhieuTraHangCT` | JOIN-OR-AUTOCOMMIT | Header and items are written by separate calls (`Insert`, `InsertItems`); `XuLyHangLoiServiceBase` (the create-phieu method that calls `Repo.Insert` then `Repo.InsertItems`) wraps both in `Uow.Begin()` ... `Uow.Commit()` with `SafeRollback`. Other callers were not audited. |
| `PhieuXuLyBatThuongRepository` | `FVN_PhieuXuLyBatThuong`, `FVN_PhieuTraHangCT` | JOIN-OR-AUTOCOMMIT | `Insert` writes the header then links `FVN_PhieuTraHangCT`; see finding F6. |
| `TraHangQTChungRepository` | `FVN_TraHangQTChung*` | JOIN-OR-AUTOCOMMIT | Each `Insert*` / `Update*` is one statement. |
| `TransactionalPhieuTraHangRepository` | `FVN_PhieuTraHang` | JOIN-OR-AUTOCOMMIT | Facade: `UpdateStatus` runs in the active transaction when present, otherwise delegates to `PhieuTraHangRepository.UpdateStatus`. |
| `AffectedLotTraceService` | `FVN_PhieuXuLyBatThuongAffectedLot` | OWNS | `TruyVetLOT` begins, deletes the old snapshot, inserts the new one, commits; rollback on exception. |

### Findings F1-F6 (code changed; closure pending evidence)

These are production-behavior risks found while reading the code. A finding is closed only by a code change plus a test, not by this text.
Each fix below uses `SqlRepositoryBase.InTransaction`: it joins the caller's `UnitOfWork` transaction when one is active, otherwise it begins, commits and rolls back its own.

Evidence for every finding: source-contract tests in `PCTP.QC.Tests\AtomicityFindingsSourceContractTests` (G2: code shape only) and `PCTP.QC.SqlIntegration\InTransactionScenario` (G3: runtime commit/rollback/join-caller behavior of the shared scope, in tempdb). Neither runs the F2-F6 repositories or `TableOrderRepo` against SQL, and neither has been run on the Windows machine yet. Re-run G1-G3 on the new commit before calling any finding closed.

- **F1 `TableOrderRepo.UploadMilkrunSP`**: was check-then-INSERT per row through `_db` with no transaction. Now one connection + transaction for the whole upload, and each row is a single `IF NOT EXISTS (... WITH (UPDLOCK, HOLDLOCK)) INSERT`, so a failure rolls back the whole upload and two concurrent uploads cannot both insert the same `(Oder_no, Part_no)`. `TableOrderRepo` has no `UnitOfWork`, so it owns this transaction and cannot join a caller's. Still open: no unique constraint on `Purchase_Order_YMVN` is evidenced in the repo (check for existing duplicates before adding one), and no SQL test exists for this method because it opens its own connection to `dbo.Purchase_Order_YMVN`.
- **F2 `PhieuLotRepository.LayLaiLotNo`**: was two UPDATEs through `Db.*`, ignoring the `UnitOfWork`. Now both go through the base class inside `InTransaction`.
- **F3 `PhieuLuuTruRepository.LuuPhieuSP`**: was UPDATE + COUNT through `Db.*`. Now both go through the base class inside `InTransaction`, so they join a caller's transaction.
- **F4 `PhieuGiaoDBRepository.LuuGiaoDB`**: was `DropCreate`, `BulkInsertDataTable` and `CallPhieuSP` on their own connections (auto-commit) with only the last UPDATE joinable. Now all four steps use the `UnitOfWork` connection/transaction (`Db.DropCreate/BulkInsert/CallPhieuSP(Connection, Transaction, ...)`), as `PhieuTmpRepository.LuuVaLoad` already does. The IFS/TMP tables stay locked until commit.
- **F5 `RackRepository.DeleteCascade`** (called by `RackService.Delete`, which begins no transaction): was `DELETE Slot` then `DELETE Rack`, auto-committed separately. Now both DELETEs run inside `InTransaction`. Not checked: whether other tables reference `Slot`/`Rack` (foreign keys).
- **F6 `PhieuXuLyBatThuongRepository.Insert`**: header INSERT plus `UPDATE FVN_PhieuTraHangCT`. Callers checked: both `QTChungService` paths that reach `TaoPhieuXuLyCore` call `_uow.Begin()` ... `_uow.Commit()` with `SafeRollback`, so the service path was already atomic. `Insert` now also owns a transaction when called without one.

## Safety

This audit reads source code and records production source changes on the test branch. It does not open a database, execute SQL, or alter production data.
