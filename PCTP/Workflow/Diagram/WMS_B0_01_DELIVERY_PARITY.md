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
