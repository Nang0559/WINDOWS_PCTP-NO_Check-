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
- YMVN MP uses the YMVN-specific service/procedure and configured MP table family. YMVN SP is configured but is not acceptance-ready until raw P/K/Q scan routing and deployed procedure/schema compatibility are verified; see §10.
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

## 9. Implementation notes and verification boundary

- The HVN procedure now uses a bounded exact-sum DP over eligible QR rows. It walks QR `STT` ascending to make the first exact subset deterministic. This is a technical tie-break, not an independently confirmed business preference; if multiple exact subsets exist, the first ascending-STT solution wins until the business owner specifies another preference.
- `PhieuLotRepository.GetLotNo` now executes the QR procedure and persists LOT to the exact TMP row in the same `IUnitOfWork` transaction. A failed LOT update throws and rolls back QR `DG`, STTBAN, and TMP status.
- `PhieuLotService` processes each exact `MAHANG + SOLUONG` group once, presents the existing selector while more than one STT remains, validates the returned STT against the active candidate list, and processes the final row implicitly.
- `FRM_LISTRUNGMSL` now stores selection on the form instance; cancel returns no STT and no static selection leaks between dialogs.
- YMVN MP/SP now resolves separate configured TMP/DOCQR pairs. The YMVN procedure whitelist includes its own MP/SP names and validates the SP flag/pair. It remains separate from the HVN exact-sum procedure.

### Executable evidence added

- Unit tests compile and execute the production `PhieuLotService` with repository/UI fakes for duplicate-group processing, n-1 selector calls, implicit final STT, cancellation, stale STT, different quantities, and allocation failure.
- Source-contract tests guard the production SQL transaction/selected-row update, repository transaction boundary, dialog instance state, unchanged duplicate key, and YMVN routing.
- SQL Server 2022 CI executes the actual `Usp_Qrcode_Take_Lot2405` source against disposable dbo fixtures. Cases include 60+50+40 → 100 (select 60+40), no exact subset, excluding DG rows, caller rollback, and two-session concurrent claim.
- The SQL integration fixture is deliberately minimal and isolated. It proves SQL Server execution and the stated column contract, not full compatibility with every DB57 table variation. DB57 acceptance remains a controlled, non-production step after comparing the live DOCQRCODE/TMP schemas and deployed procedure definitions.


## 10. Customer × product category × QR-format acceptance matrix (source-derived)

This matrix is derived from CustomerTableConfig and DocQRScanEngine.ProcessScan. It specifies routing; it is not proof that each route has passed on DB57. Do not substitute one generic QR string across all rows.

| Customer | Mode | Source-derived QR route | Expected scan sequence / table family | Current gate |
|---|---|---|---|---|
| HVN 100001 | MP | 4 colon fields → ScanFCC; subsequent customer-label scan → ScanHVN | FCC scan first, then HVN/customer scan; DOCQRCODE + TMPPHIEUGIAOHANG | Current single-QR G6 does not prove the two-stage flow |
| HVN 100001 | SP | 4 colon fields → ScanFCC_SP | One FCC scan populates both FCC and HVN fields; DOCQRCODE_SP + TMPPHIEUGIAOHANG_SP | No dedicated SP UI/SQL journey |
| HVN 100001 | O-type | Uses current MP/SP session route; O-type bypasses the FCC-order guard | Preserve active MP table/session and O-type category | No dedicated O-type journey |
| Honda Trading 100003 | Date-based | 6 colon fields → ScanFCC_TongTem because CoNhieuNhaMay=false and CoGear=false | One combined QR writes FCC and HVN values; DOCQRCODE_100003 + TMPPHIEUGIAOHANG_100003 | No dedicated live UI journey |
| YMVN 100002 | MP / Gear | 4 colon fields → ScanFCC_YMVN; raw P/K/Q QR → ScanYMVN | YMVN_DOCQRCODE + TMPPHIEUGIAOHANG_100002; separate YMVN take-LOT procedure | SQL characterization exists; full UI/lifecycle remains open |
| YMVN 100002 | SP / Gear | 4 colon fields → ScanFCC_YMVN; raw P/K/Q QR also routes to ScanYMVN | Expected session pair is SP_DOCQRCODE + SP_TMPPHIEUGIAOHANG | BLOCKED: raw ScanYMVN currently uses the configured MP DocQRTable, not the session-selected SP table; procedure schema compatibility is also unproven |

### 10.1 Required format-level cases

For each route, test valid and invalid samples using approved real label examples or masked DB14 read-only extraction. Never fabricate a customer barcode by rearranging fields.

- Correct field count and field meaning; empty required field; whitespace; wrong delimiter; extra/missing field.
- Positive integer quantity; zero, negative, non-numeric and overflow quantity.
- Correct item in active delivery slip; item absent; mapped FCC item; wrong customer/item mapping.
- Duplicate label / duplicate LOT / duplicate customer slip number as applicable.
- Scan order: FCC then customer label where required; reverse order must fail without partial accepted scan.
- Re-scan same QR; scan QR belonging to another STT/part/customer; preserve unrelated rows.
- MP ↔ SP switch while a QR session exists; resume after reopening form; verify DOCQR/TMP pair remains consistent.
- Gear/hour context switch during a YMVN QR session; prevent cross-hour mixing.
- LOT normalization: legacy short LOT, 13-character root, 20-character LOT, LOT-counter/quantity suffix, comma-separated multi-LOT; compare with production LotCodeHelper and active procedure.
- Quantity reconciliation: FCC quantity vs customer quantity (where both exist), selected QR total vs TMP SOLUONG, and LOT component sum vs allocated quantity.

### 10.2 Mandatory end-to-end dimensions

A customer/category QR test is not complete at parser success. For every configured route, capture and assert:

1. Resolved customer number and exact active CustomerConfig.
2. MP/SP/O-type, plant, delivery date, and Gear/hour context.
3. Input format identifier and scan sequence; mask raw QR in logs.
4. Exact DOCQR/TMP table pair and actual procedure invoked.
5. QR row identity, item, LOT, FCC/customer quantities, status and ownership/linkage.
6. Exact TMP delivery row (STT + item + quantity), LOT payload and completion status.
7. STOCKTP and SlotLot before/after quantities, FIFO selection and reconciliation.
8. Delivery slip/history/status and retry behavior.
9. Negative paths leave all business rows unchanged.
10. Cleanup covers every table written by the real path and reports zero remaining run-scoped rows.

### 10.3 Known blockers from source review

- YMVN SP raw QR table selection: ScanYMVN reads GetMaxStt and calls UpdateHVN using _cfg.Delivery.DocQRTable (MP table) instead of the session property DocQRTable. In SP mode, ProcessScan can route raw P/K/Q input into this method. Correct production routing is required before the SP acceptance case can pass.
- YMVN SP take-LOT schema: the procedure checks @TmpOk / @LegacyQrOk and throws 51213 when the configured table pair lacks required columns. Its source comments identify the SP family as a different schema. Verify actual DB57 columns before any write; table names in an allow-list do not prove support.
- HVN MP sequence: current G6 manifests submit a generated carton QR once; they do not prove the two-stage FCC → customer-label flow for CoNhieuNhaMay=true.
- Customer 100003: current G6 manifests do not exercise the six-field combined QR route or its dedicated order/table family.
- One-label and two-label routes are different: Honda Trading uses one six-field combined QR; HVN SP uses one FCC scan that fills both field sets; HVN MP uses FCC then customer label. These fixtures are not interchangeable.

### 10.4 Release rule

Do not set a route to configured:true or report E2E PASS until its exact customer/category/QR sequence has a real UI run, SQL before/after assertions, stock and delivery reconciliation, replay/negative-path coverage, and full cleanup. Source-contract PASS only proves the source still has the characterized route; it does not prove the customer flow works.
