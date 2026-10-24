# Delivery QR allocation — business acceptance specification

Branch: `feature/pctp-qc-test-platform`

This specification is the acceptance baseline for duplicate delivery-slip rows and QR-to-STT allocation. It is not evidence that the current implementation passes. Tests must execute the production C# service/repository and the active SQL procedure against an isolated SQL Server fixture.

## 1. Confirmed business rules

1. `TinhTong()` is performed per delivery-slip row.
2. QR rows already aggregated successfully are marked `DG` and must not be reused.
3. Duplicate candidates are grouped by exact `MAHANG + SOLUONG`. Different quantities for the same item are not duplicates in this flow.
4. The existing `FRM_LISTRUNGMSL` form is reused. The operator chooses a delivery-slip `STT`; the operator does not choose individual QR rows.
5. For the selected STT, the system automatically allocates eligible QR rows for the selected line's required quantity. Allocation must find a set whose total equals the target quantity; it must not simply take QR rows in physical/read order and stop at the first overshoot.
6. Only an exact quantity match may create/update LOT on the selected TMP delivery row and mark the allocated QR rows `DG`.
7. If no exact allocation exists, do not create LOT, do not mark any candidate QR `DG`, and do not consume them.
8. After a successful selected STT, continue with the remaining duplicate STTs. A previously completed STT and its QR rows must not be offered/consumed again. For n matching rows, the selection form is shown for the remaining choices as required by the existing UX; the final unambiguous row may be processed without another choice.
9. YMVN remains a separate business process and must branch by `CustomerTableConfig`; do not force HVN allocation semantics into YMVN.

## 2. Exact-allocation acceptance examples

| Case | Required | Eligible QR quantities | Expected |
|---|---:|---|---|
| QR-001 | 100 | 60, 50, 40 | Select 60 + 40; exact 100; LOT created; only those QR become DG |
| QR-002 | 100 | 60, 50 | No exact subset; no LOT; neither QR becomes DG |
| QR-003 | 100 | 100, 40 | Select 100; 40 remains eligible |
| QR-004 | 100 | 30, 30, 40, 70 | Exact subset exists; total selected must be 100 |
| QR-005 | 100 | 60(DG), 40, 60 | DG row excluded; allocate the two eligible rows only if they sum exactly to 100 |
| QR-006 | 100 | 60, 40 | After success, replay must not allocate the same QR again |
| QR-007 | 100 | 60, 50, 40 | If allocation fails after tentative selection, rollback STTBAN/other QR linkage and leave all candidates reusable |
| QR-008 | 100 | 60, 40 | Two concurrent attempts for same STT must not both claim the same QR rows |

When more than one exact subset exists, selection must be deterministic and documented. Do not invent a tie-breaker from cursor/read order. The business owner must confirm the tie-break policy if it changes which QR/LOT trace is consumed.

## 3. Duplicate STT workflow tests

| Case | Setup/action | Expected |
|---|---|---|
| DUP-001 | Same MAHANG and SOLUONG on STT 1 and 3; choose STT 3 | Only STT 3 receives LOT; STT 1 remains pending |
| DUP-002 | After DUP-001, process remaining duplicate | STT 3 and its QR are excluded; remaining STT can be selected/processed |
| DUP-003 | Three identical rows | No STT is processed twice; selection is not repeated for an already completed STT |
| DUP-004 | Same MAHANG but different SOLUONG | They are not grouped as duplicates by this flow |
| DUP-005 | User cancels duplicate selection | No QR status/linkage or TMP LOT mutation |
| DUP-006 | Selected STT is not a member of the displayed candidate set | Reject selection; no SQL mutation |
| DUP-007 | STT already has LOT/processed state when submitted | Reject or skip idempotently; never overwrite or consume QR again |

## 4. SQL and persistence invariants

Every successful allocation must satisfy all of the following in one transaction:

- The sum of allocated QR quantity equals the selected TMP row's `SOLUONG`.
- Every allocated QR belongs to the requested `MAHANGFCC`, is eligible, and was not already `DG`.
- Each allocated QR is linked to the selected delivery `STTBAN` only for that allocation.
- Exactly the allocated QR rows are marked `KETQUA='DG'`; unselected QR rows remain unchanged.
- The selected TMP row alone is updated with LOT and completion status.
- The returned LOT components reconcile to the allocated QR rows and their quantities.
- Any exception, cancellation, insufficient exact quantity, or stale/concurrent claim leaves no partial QR linkage, DG status, LOT, or completion state.
- A repeated request is idempotent and cannot consume the same QR a second time.

Required SQL assertions: before/after snapshots of QR primary identity, `MAHANGFCC`, QR quantity fields, `KETQUA`, `STTBAN`, `GIO`; TMP `STT`, `MAHANG`, `SOLUONG`, `LOT`, `STATUSDOC`, `TTPHIEU`.

## 5. Customer routing tests

- HVN MP uses its configured TMP/DOCQR table pair and the HVN allocation procedure.
- HVN SP uses its configured SP TMP/DOCQR table pair.
- YMVN MP and SP continue through the YMVN-specific service/procedure and configured YMVN table family.
- Customer 100003 uses its configured table family; it must not silently fall back to HVN tables.
- Unknown/missing customer configuration fails closed before SQL mutation.

## 6. Test layers and evidence requirements

| Layer | What it proves | Required evidence |
|---|---|---|
| Pure allocator unit tests | Exact-sum allocation, exclusion of DG, deterministic tie-break, shortage/no-solution | Unit test results |
| C# service tests | Selected STT isolation, duplicate loop, cancel/stale selection, no repeated STT | Fakes recording repository calls and mutations |
| SQL Server integration | Active `Usp_Qrcode_Take_Lot2405` behavior, transaction rollback, exact QR status/linkage, concurrency | SQL Server 2022 disposable CI database; execute procedure, inspect persisted rows |
| Customer routing tests | Correct config-specific procedure and table names | Fake repository/procedure invocation assertions |
| Windows UI journey | Existing duplicate form displays candidate STTs and returns chosen STT; cancel works | UI Automation report + screenshots |
| DB57 acceptance | Verify schema/procedure compatibility using approved non-production DB57 only | Explicit approved test window and before/after cleanup report |

## 7. Safety boundaries

- Never connect tests to production `192.168.200.14\\BRAVO`.
- CI SQL tests must use a disposable SQL Server 2022 Developer instance/container.
- DB57 `192.168.200.57 / B7R2_FCC` is for controlled acceptance only, not automated CI.
- Do not infer primary keys or QR identity columns. Discover and use the actual DB57 schema before writing integration fixtures.
- Source-text assertions are supplementary only. They cannot replace executing the stored procedure and asserting database postconditions.

## 8. Current code-review gaps to close

The branch review at commit `9e787c06c4f8be8480a72f7e9fff435ed54985d0` found:

1. `PhieuLotService.TinhTongLot` iterates input rows and re-queries duplicate candidates without an explicit processed-STT/group state. It can revisit a duplicate group and does not prove that the chosen STT belongs to the active candidate set.
2. `Usp_Qrcode_Take_Lot2405` adds QR quantities sequentially; it does not search an exact-sum subset.
3. The procedure inserts rows into `#QRData` with `ORDER BY STT`, but the cursor SELECT has no `ORDER BY`; SQL does not guarantee that insertion order is preserved.
4. `@_DeM` is passed by `PhieuLotRepository.GetLotNo` but is not used by the procedure. Its intended meaning must be clarified from the legacy caller/schema before changing its contract.
5. The procedure temporarily updates `STTBAN` while accumulating QR and later marks rows by `STTBAN + MAHANG`. This needs exact row-identity scoping and transaction/locking tests to prove no unrelated QR row can be marked DG.
6. `FRM_LISTRUNGMSL` exposes selection through static `MAHANG`, `STTPHIEU`, and `SL` fields, creating stale cross-dialog state risk.
7. The C# path shown by `PhieuLotRepository.GetLotNo` invokes `Usp_Qrcode_Take_Lot2405`; the YMVN path must be verified separately against `CustomerTableConfig` and its active call sites before any shared refactor.

These are failing acceptance areas until fixed and verified with the evidence above.
