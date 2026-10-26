# B0-01 Delivery Command — parity and verification contract

Status: **contract scaffold only; no production cutover**.

## Business decisions

- **F-Y1 = PART-scoped rows.** Each PART owns its own list of LOT allocation slices. A PART quantity equals the sum of its LOT slices. A PART may span multiple LOTs; the same LOT may appear under different PARTs when the source allocation proves that split.
- **F-Y4 = atomic business command.** Every STT/part in one command commits or rolls back as one unit. Do not make a single STT independently successful when another STT in the same command fails. A day's work may be split into multiple independently retryable commands.

## Command identity and allocation contract

- `BusinessKey` is mandatory, stable across retries, and scoped to the customer/delivery document plus its business revision/window. Never generate a new GUID for each retry.
- `PendingStt` lists the `TMPPHIEUGIAOHANG.STT` rows closed by this command (needed for the TMP guard, pending-delivery close and F-Y4 atomicity). At least one, distinct, positive.
- `CustomerCode`, `DeliveryReference`, `PerformedBy`, and occurrence time are required.
- Every PART has one or more LOT allocations, QR identities, optional Gear/Model metadata, and a quantity derived from its LOT slices.
- FIFO sequence is unique within a PART. Each LOT allocation carries the displayed LOT key, the exact STOCKTP LOT key, optional physical `SlotLotId`, positive quantity, and FIFO sequence.
- Allocation is prepared and validated upstream. The eventual handler must revalidate identities, remaining quantities, FIFO order and source-document status inside its transaction before mutation; the command object is not a substitute for DB validation.
- Duplicate PART rows are rejected; combine their LOT slices under one PART before constructing the command.

## Required parity scenarios

IDs are `P1..P13` (command parity). They are distinct from the `Y1..Y15` characterization cases in `docs/acceptance/YMVN_DELIVERY_SPEC_ACCEPTANCE.md`, which freeze the legacy behaviour these parity cases must match.

| ID | Scenario | Required assertions |
|---|---|---|
| P1 | One PART, one LOT | STOCKTP and SlotLot each decrease by the same quantity; one PART ledger detail |
| P2 | One PART spans multiple LOTs | PART total equals sum of LOT slices; FIFO sequence respected |
| P3 | Multiple PARTs, same LOT | PART traceability preserved; aggregate LOT deduction equals sum of PART allocations |
| P4 | QR/PART association | Every delivered QR is linked to the correct PART and LOT; QR cannot be replayed |
| P5 | Partial LOT | Deduct exact partial quantity; remaining STOCKTP and SlotLot reconcile |
| P6 | Multiple STT in one command | All commit or all roll back (F-Y4) |
| P7 | Same BusinessKey retry | Return prior outcome; no second stock, ledger, history or pending-delivery mutation |
| P8 | Same QR with another BusinessKey | Reject duplicate QR; no mutation |
| P9 | Failure after STOCKTP update | Entire transaction rolls back, including SlotLot, ledger, history and TMP/pending status |
| P10 | Failure after first PART/STT | All PART/STT changes in this command roll back |
| P11 | YMVN Gear/Model | Gear/Model and CoGear semantics match current `Cfg.Delivery` behavior |
| P12 | MP/SP variants | Correct configured TMP/DocQR tables and `Usp_Qrcode_Take_LotYMVN2405` result parity |
| P13 | F-Y1 = part: scan rows | Delivering one STT of a PART archives/deletes the `DG` rows of that PART, including other STT of the same PART; non-DG rows stay (legacy Y15) |

## Migration constraints

1. Keep the existing stock and YMVN stored procedures in place and callable during parity work.
2. Do not execute legacy stored procedure stock mutation and the new movement command in the same business operation.
3. Do not wire the new handler to production until Windows build, SQL integration, two-session idempotency/concurrency, rollback and all applicable parity cases pass.
4. Remove `IStockMovementService = null` from the production construction path only after every real `PhieuKhoRepository` construction path is identified and supplied with a non-null command dependency. Keep any compatibility constructor internal/test-only and impossible to select from production composition.
5. The handler must own one UoW for STOCKTP, SlotLot, QR/part detail, delivery ledger, pending status and history. A successful response must report the committed quantity; a duplicate response must report the prior committed quantity.
6. Keep SQL artifacts under the repository's root `SQL/` location. The current `PCTP/Workflow/Diagram/` tree contains no `.sql` files; do not move or delete root SQL scripts as part of documentation cleanup.

## Evidence still required

- Full `PCTP.csproj` MSBuild on Windows with DevExpress, NuGet and WebView2 prerequisites.
- SQL fixture with representative `STOCKTP`, SlotLot, QR, TMP, ledger and history schema.
- Real method execution against isolated SQL Server; two sessions for same BusinessKey and same QR.
- Failure injection after each mutation boundary and exact before/after quantity reconciliation.
- Full UI journey and cleanup on an isolated database.

## Current-state inventory (source review of `53af05d` plus the ratchet test patch)

Verified from source; none of this is a SQL result.

**Legacy STOCKTP writers still reachable from customer delivery** (`PhieuKhoRepository.cs`):

| Writer | Entry points | Notes |
|---|---|---|
| `Usp_Qrcode_Update_Stock2405` | `CapNhapKho` (and `CapNhapKhoHTN` through it) | stored procedure deducts STOCKTP; SlotLot then deducted by `BulkStockAdjustService` |
| `Usp_Qrcode_Update_Stock_SP` | `CapNhapKhoSP` | same pattern for the SP variant |
| `UPDATE STOCKTP ... SLCONLAI = SLCONLAI - @sl` in `TruStockTpFifo` | `CapNhapKhoYMVN` | repository-owned FIFO deduction guarded by `SLCONLAI >= @sl` and a one-row check |

`DeliveryLegacyWriterInventoryTests` pins exactly this set (two procedures, one `UPDATE STOCKTP`, one caller of `TruStockTpFifo`). Adding a writer fails the test. It is a ratchet, not closure: B0-01 stays OPEN.

**Construction paths (migration constraint 4).** `PhieuRepository` is constructed once in production, in `GiaoHangKhachModuleFactory.Build` (file `GiaoHangKhachFactory.cs`), which creates `StockMovementService` and passes it as `stockMovement:`. `PhieuRepository` constructs `PhieuKhoRepository` once and forwards `stockMovement`. So the production path is wired with a non-null movement service, and no other production construction site exists in `PCTP/`. The three `= null` defaults (`PhieuRepository`, `PhieuKhoRepository`, `BulkStockAdjustService`) are therefore unused by production composition but are still selectable by any future caller. Removing them is a separate change after the delivery command exists; `BulkStockAdjustService.TruKhoAoTheoLot` already fails closed (throws) when the service is null.

**Parity status (P1–P13): SLICE 1 STARTED, NOT WIRED.** `DeliveryStockCommandHandler` (P1, P2, P3, P5–P10) exists behind `IDeliveryStockCommandHandler` and is NOT referenced by `GiaoHangKhachModuleFactory` (migration constraint 3). It mutates stock only through `IStockMovementService.Export`, never calls the legacy procedures, rejects allocations without `SlotLotId` (A0/virtual path stays legacy), and uses `IDeliveryCommandLedger` (BusinessKey + QR uniqueness) and `IDeliveryPendingGate` (all-or-nothing TMP close; SQL implementation still needs the DBA-confirmed TMP schema). `DeliveryStockCommandHandlerTests` proves behaviour only against in-memory transactional fakes (G2), not SQL (G3/G5). `SQL/PCTP_DeliveryCommandLedger.sql` and `DeliveryCommandLedgerRepository` are written but unexecuted. Still open: P4, P11, P12, P13, the pending gate SQL implementation, and the cutover. B0-01 stays OPEN; `DeliveryLegacyWriterInventoryTests` is intentionally unchanged.

**(superseded text kept for history) Parity status (P1–P13): NOT STARTED.** `DeliveryStockCommand` and `IDeliveryStockCommandHandler` exist as a contract (validated by `DeliveryStockCommandContractTests`); there is no handler implementation and no parity test. `DEL-003`, `DEL-004` and `DEL-007` remain partial in `TestMatrix.json`.

**Not provided by the current code (inputs the handler design must decide):**

1. No persisted idempotency key on any delivery path. Replay protection today depends on TMP/pending state; `DEL-004` is therefore partial. P7/P8 need a durable `BusinessKey` and a unique index, delivered as an idempotent migration under the root `SQL/` folder.
2. The YMVN path takes LOT lists as parsed strings (`lotSl`) rather than PART-scoped allocations; the command's PART/LOT model has no producer yet.
3. The stored-procedure paths return LOT/SOLUONG rows after deducting STOCKTP; a handler that deducts itself must not also call these procedures (migration constraint 2).

## Suggested build order for the handler (no code in this patch)

1. SQL fixture with representative `STOCKTP`, `SlotLot`, QR, TMP, ledger and history shapes (G4 prerequisite; DBA-confirmed schema, no invented procedure definitions).
2. Handler for the simplest slice (P1, P5) behind an internal switch that is off in `GiaoHangKhachModuleFactory`.
3. Add P7/P8 (idempotency) and P9/P10 (rollback injection) before widening to multi-LOT/multi-PART (P2–P4, P6).
4. YMVN (P11, P13) and MP/SP variants (P12) last, against the existing `YMVN_DELIVERY_SPEC_ACCEPTANCE.md` characterization cases.
5. Replace the stored-procedure source-contract assertions (`DeliveryTransactionContractTests`, `DeliveryProductionBoundaryTests`) deliberately and only after P1–P13 pass; keep rollback/idempotency regressions.
