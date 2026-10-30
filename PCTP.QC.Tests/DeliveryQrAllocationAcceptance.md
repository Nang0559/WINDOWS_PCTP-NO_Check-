# Delivery QR allocation — business acceptance specification

Branch: `feature/wms-live-data-test-harness`

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
- YMVN is treated as a separate, operator-confirmed working flow for this review. Keep its dedicated regression coverage; this pass does not raise a new YMVN defect.
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
| YMVN 100002 | MP / SP / Gear | Keep existing dedicated YMVN route and stored-procedure regression tests | YMVN-specific tables and procedure family | Operator reports YMVN is working; no new YMVN defect is asserted by this HVN-focused review |

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

- HVN MP sequence: current G6 manifests submit only generated FCC carton QR(s), omit the corresponding customer-label scan, and run final stock/delivery assertions without an explicit completion/CNK UI action. Both manifests are now BLOCKED until the real label format and completion controls are captured.
- Customer 100003: current G6 manifests do not exercise the six-field combined QR route or its dedicated order/table family.
- The operator-provided HVN customer-label sample is now recorded below. The current G6 manifests still submit FCC-only scans, so they do not yet exercise the real two-stage MP scan (FCC first, then customer label), completion/CNK, or final SQL state.
- HVN SP remains a distinct configured route: one FCC scan populates FCC and HVN fields in the SP DOCQR table. It needs its own UI/SQL journey; it must not be represented by the MP fixture.

### 10.4 Release rule

Do not set a route to configured:true or report E2E PASS until its exact customer/category/QR sequence has a real UI run, SQL before/after assertions, stock and delivery reconciliation, replay/negative-path coverage, and full cleanup. Source-contract PASS only proves the source still has the characterized route; it does not prove the customer flow works.

## Real HVN/FCC QR format characterization (operator-provided samples)

The source-contract suite now uses the following exact sample payloads (not synthetic QR values):

- FCC: `231115010090-25,231116010090-50:22660-K90-VM10-M1:16/00/2023:75`
- HVN customer label: `24061106007225:23010K1Y J510M1:23010K1Y J510M1ZZZ:5:4R62:`

With the current .NET `rawQr.Split(':')` call, the FCC sample has 4 elements and routes to `ScanFCC`; the HVN sample has 6 elements, including the trailing empty element, and routes to `ScanHVN` for MP. The confirmed HVN handler contract uses only: element 0 as the HVN label/LOT for duplicate checking, element 1 as item code (spaces removed for comparison), and element 3 as quantity. Elements 2 and 4, plus the trailing empty element 5, are deliberately ignored; no validation or persistence is required for them.

This is parser/routing characterization only. It does not establish that a full live HVN delivery succeeds. G6 remains BLOCKED until the journey pairs the actual FCC and customer-label formats in the required order and exercises the real completion/CNK action plus SQL postconditions.



## 11. Defect/rework handoff — configurable FIFO policy

This section extends delivery acceptance to the stock handoff used when goods are routed to defect/rework processing. It is a required configuration and test contract; it does not authorize changing production PCTP business code or deployed SQL in this QC-only pass.

### 11.1 Configuration contract

FIFO must be an explicit policy, not an accidental consequence of query ordering:

- `FIFO`: require stock allocation in the configured FIFO order for the selected item/warehouse/stock scope. The allocation must identify the actual source LOT(s), quantities, and ordering evidence.
- `NON_FIFO`: do not enforce FIFO ordering. The selected eligible stock/LOT may be used according to the existing authorized selection rule, but the allocation must still be deterministic, quantity-safe, and traceable.
- The existing approved switch is item-level: `FVN_ItemFifoConfig.EnforceFifo` keyed by `ItemCode` (`1` = enforce, `0` = bypass), audited by `UpdatedAt`/`UpdatedBy` and `FVN_ItemFifoConfig_History`. Rework should resolve this same item-level setting for the product on the defect ticket. Do not create a competing customer-level default unless the business owner explicitly approves a new configuration model.
- **Observed current behavior:** `ItemFifoConfigRepository.GetEnforceFifo` returns `false` when no row exists, and the item-config UI initializes newly added items with `EnforceFifo = false`. Therefore, an absent row currently behaves as NON_FIFO rather than a distinct “not configured” state. This must be an explicit business decision and a tested contract; do not describe it as fail-closed.
- The selected policy and resolved item code must be visible in the QC execution report. The policy must not be inferred from customer name, UI screen, or whichever LOT happens to be returned first.
- Changing FIFO policy during an active allocation/session must not change the policy halfway through that transaction.

### 11.2 Shared invariants — both FIFO and NON_FIFO

Regardless of policy:

1. Only eligible stock for the requested item, warehouse, and source status may be allocated.
2. Allocated quantity must equal the requested defect/rework quantity; partial allocation is allowed only where the existing approved business rule explicitly allows it.
3. No LOT/stock row may be consumed twice by concurrent requests; use the production transaction/locking boundary.
4. Source LOT and allocated quantity must remain traceable through the rework request, export, receipt/return, and stock ledger.
5. Stock balances must not become negative; zero/negative requested quantities fail before writes.
6. A failed validation, cancellation, exception, or partial write must roll back all stock, LOT, request, and ledger changes.
7. Replay with the same business key must be idempotent; a different request must not inherit an earlier allocation.
8. FIFO policy affects ordering only. It must not bypass item, warehouse, status, ownership, quantity, or authorization checks.
9. Delivery completion and rework handoff are distinct state transitions; completing one must not implicitly complete the other.
10. Cleanup and reports must include every table/ledger touched by the real production path.

### 11.3 FIFO-specific acceptance

- Select the oldest eligible LOT according to the actual production FIFO key; document that key from the production query/schema rather than assuming date or LOT lexical order.
- When the oldest LOT is insufficient, continue to the next eligible LOT(s) in FIFO order until the requested quantity is met, subject to the approved partial-allocation rule.
- Exclude blocked, quarantined, already-consumed, wrong-warehouse, wrong-item, and otherwise ineligible stock before ordering.
- Persist the exact LOT-to-quantity breakdown; the total must reconcile to the rework request and stock ledger.
- Concurrent handoffs must not allocate the same FIFO quantity twice.

### 11.4 NON_FIFO-specific acceptance

- Do not assert that the oldest LOT is selected.
- Assert instead that the selected LOT(s) are eligible, belong to the requested item/warehouse, and cover the requested quantity exactly.
- The selection mechanism must be explicit and reproducible (for example, operator-selected LOTs if that is the existing approved UX); do not silently substitute unordered SQL result order for an approved selection rule.
- Persist the same LOT-to-quantity trace and ledger reconciliation required by FIFO mode.
- Test that toggling NON_FIFO does not disable stock validation, concurrency protection, rollback, idempotency, or audit history.

### 11.5 Required cross-product test matrix

Run each applicable customer/category route under both FIFO policies. A route is not covered merely because another customer uses the same table names or QR parser.

| Customer / route | Delivery type to cover | FIFO | NON_FIFO | Additional route-specific proof |
|---|---|---|---|---|
| HVN 100001 | MP | Required | Required | FCC then customer label; duplicate check on HVN element 0; item element 1; quantity element 3; exact TMP/DOCQR pair; completion/CNK |
| HVN 100001 | SP | Required | Required | Single FCC scan populates FCC/HVN fields; SP table pair |
| HVN 100001 | O-type | Required | Required | O-type session/category isolation and its actual MP/SP table family |
| Honda Trading 100003 | Date-based combined QR | Required | Required | Six-field parser route, dedicated order/table family, date and quantity reconciliation |
| YMVN 100002 | MP / Gear | Required | Required | Dedicated YMVN QR parser, Gear/hour context, YMVN take-LOT procedure |
| YMVN 100002 | SP / Gear | Required | Required | Dedicated SP table family and YMVN procedure contract; retain operator-confirmed working behavior |
| Any additional configured customer | Every configured category/type | Required | Required | Discover from live CustomerTableConfig; unknown config must fail closed |

For each cell, cover: success, insufficient stock, no eligible LOT, wrong item/warehouse, duplicate/replay, concurrent allocation, exception after first write, cancellation, policy change during active session, and cleanup. Where a mode is not supported by an existing approved route, record it as NOT APPLICABLE with the exact configuration/source evidence; do not mark it PASS by omission.

### 11.6 Evidence and release gate

- Unit tests: policy resolution, FIFO ordering, NON_FIFO selection contract, exact quantity, invalid/missing config.
- Production C# integration: execute the actual delivery/rework services and repositories with real SQL repositories; mocks alone are not acceptance evidence.
- SQL integration: run the actual deployed-equivalent procedures on isolated SQL Server 2022; assert before/after stock, LOT, ledger, request, and status rows.
- UI: exercise the actual customer/category route, policy selection/config display, delivery completion, and defect/rework handoff.
- DB57: controlled non-production acceptance only, with approved window, schema/procedure comparison, run-scoped seed, and zero-row cleanup report.
- A source-contract PASS or isolated procedure PASS cannot establish customer E2E PASS. Each matrix cell needs its own evidence and report.

### 11.7 Source-review finding and decisions that must not be guessed

The branch currently has an item-level switch in `FVN_ItemFifoConfig.EnforceFifo`. `FifoSessionService` uses it for customer delivery: false skips RAM FIFO initialization; true loads stock and builds FIFO-ranked reservations. The existing functional design documents a second DB final validation before CNK.

The current `ReworkStockService.GetLotsCanRework` returns positive STOCKTP/active SlotLot matches ordered by warehouse/rack/slot/LOT, and `XuatKhoRework` accepts a selected `slotLotId`/LOT. The inspected rework service does not resolve `FVN_ItemFifoConfig` or enforce the delivery FIFO rank. Therefore, the existing item switch is **not yet proven to govern rework export**. This is a real integration gap to cover; it is not permission to modify production code in this QC-only pass.

**Business decision confirmed by the operator (2026-10-30): Policy A — preserve the defect ticket's exact `SoLoLoi`.** FIFO applies only to eligible stock rows whose LOT equals that affected LOT. The rework export must never widen the candidate set to older/different LOTs of the same item to satisfy a shortage.

Consequences:
- Resolve the defect ticket's item code and exact affected `SoLoLoi` before querying stock. Candidate stock must match both values, the authorized warehouse, active SlotLot, and eligible stock status.
- In FIFO mode, rank/consume only the eligible rows within that exact affected LOT using the actual production FIFO key. Do not compare or fall back to another LOT.
- In NON_FIFO mode, an authorized explicit SlotLot selection may choose among eligible rows within that same affected LOT; it may not choose another LOT.
- If the affected LOT has insufficient eligible quantity, fail with a shortage result and make no stock/ledger/request mutation. Do not auto-substitute another LOT. Any business-approved split across physical stock rows must remain within the same exact LOT.
- A missing/blank affected LOT is not permission to search all LOTs. Reject or route to an explicitly approved exception workflow; do not infer a replacement LOT.
- The execution evidence must record requested `SoLoLoi`, resolved item, selected SlotLot row(s), per-row quantity, FIFO policy, FIFO rank/key where applicable, and shortage/exception reason.

#### 11.8 Affected-LOT constrained acceptance cases

| Case | Setup / action | Expected |
|---|---|---|
| RL-LOT-001 | Ticket item A, `SoLoLoi=L1`; L1 and older L0 both have stock; FIFO enabled | Select only eligible L1 rows; L0 is never a candidate |
| RL-LOT-002 | Same ticket; L1 has enough quantity across multiple eligible stock rows | Consume rows in FIFO order within L1 only; exact quantity and trace |
| RL-LOT-003 | Same ticket; L1 is short but L0 has enough | Fail shortage; no fallback to L0; no writes |
| RL-LOT-004 | FIFO disabled; L1 has multiple eligible SlotLot rows | Explicit selection may choose any eligible L1 row(s); never L0 |
| RL-LOT-005 | FIFO disabled; operator selects a SlotLot belonging to L0 | Reject before mutation |
| RL-LOT-006 | Ticket has blank/missing `SoLoLoi` | Fail closed or invoke separately approved exception flow; never query all LOTs |
| RL-LOT-007 | Selected SlotLot has wrong item, warehouse, inactive status, or insufficient available quantity | Reject; no stock/ledger mutation |
| RL-LOT-008 | Concurrent requests target the same ticket/LOT stock | No double allocation; transaction/locking and idempotency hold |
| RL-LOT-009 | Failure after first stock/ledger write | Full rollback; all L1 and unrelated LOT rows unchanged |
| RL-LOT-010 | Replay same business key after success | Return idempotent result; do not consume L1 twice |

**Current implementation boundary:** the inspected `ReworkStockService` still does not resolve `FVN_ItemFifoConfig` or enforce this affected-LOT-constrained FIFO policy. This decision closes the business-rule ambiguity, not the implementation gap. The production service/SQL integration remains BLOCKED until the approved item-level switch is applied inside the same transaction/locking boundary and the above cases execute against real repositories on isolated SQL Server. This QC-only pass still does not authorize changes to centralized production C# or deployed business procedures.


## 12. Operator-confirmed production QR LOT contract (2026-10-30)

The authoritative field layout is documented in `docs/PCTP_QR_LOT_FORMAT_CONTRACT.md`. This rule is separate from the customer delivery QR formats in section 10.

- Total label: six colon-separated fields; warehouse receiving uses this label.
- Box label: four colon-separated fields; warehouse issue/delivery uses this label.
- LOTNO token layout: 6 date + 5 product + 1 shift + 1 Gear + 3 line + 4 department + 4 daily sequence + 4 embedded quantity = 28 characters.
- Canonical stock LOT identity is the first 20 characters (date/product/shift/Gear/line/department). The 4-character daily sequence and 4-character embedded quantity are label-level suffixes, not separate stock LOT identity.
- Box issue quantity is read from the box QR quantity field. It is not inherited from the total label.
- HVN Gear is `0`; YMVN Gear is `1=A, 2=B, 3=C, 4=D, 5=E`.
- FIFO is constrained to the resolved stock LOT. It must not substitute a different LOT.

### QR contract test coverage

`QrLotFormatContractTests` verifies the confirmed offsets, 20-character identity, total-label production parser fields, customer Gear mapping, a synthetic 28-character box-token vector, and rejection of the earlier 22-character box token as a valid fixture.

### Known production integration blocker

The production `QRCodeParser.ParseTachLotQr` still documents a legacy 27-character LOT assumption. The new 28-character / 20-character stock identity contract is not yet proven end-to-end through receiving, box issue, stock persistence, and ledger. The contract tests are not a substitute for that integration. Keep the production parser/SQL unchanged in this QC-only commit; report this as BLOCKED until the business owner confirms the corrected real box QR and authorizes any necessary production-code change.
