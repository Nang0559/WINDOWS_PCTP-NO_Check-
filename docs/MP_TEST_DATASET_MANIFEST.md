# MP realistic test dataset — phase 2

## Purpose and direction

This is a **source-to-fixture plan**, not a schema parity report.

- SQL14: `192.168.200.14\\BRAVO / B7R2_FCC` is the legacy operational source. It is read-only and is used only to select a small, relevant set of MP item/order/lot/stock examples.
- SQL57: `192.168.200.57 / B7R2_FCC` currently contains the newer PCTP development schema, views, procedures, functions, warehouse locations and application-specific extensions. It is the schema/logic target during development.
- Later production deployment is a separate migration/release activity. This harness must not synchronize SQL14 from SQL57 or make production changes.

The successful metadata inventory is run [40688306068](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/actions/runs/40688306068), commit `bd23bdc9e9758136716614aef6ad699d0c21336f`. It queried metadata on both databases and exported no business rows. The 139 column findings are **compatibility/mapping findings**, not 139 defects that should be fixed by making SQL57 match SQL14.

## What the inventory actually tells us

Approximate counts from the inventory (not an assertion that every row is valid/current):

| Object | SQL14 | SQL57 | Phase 2 interpretation |
|---|---:|---:|---|
| `B20Item` | 3,450 | 3,325 | Source master reference; select only items needed by chosen MP lots |
| `B20ItemQuyCach` | 1,205 | 562 | Select only rows belonging to selected items |
| `STOCKTP` | 70,379 | 66,248 | Candidate source for positive-stock lots; validate quantity semantics |
| `Purchase_Order_YMVN` | 34,770 | 4 | Candidate order reference; select only a few matching lines |
| `TMPPHIEUGIAOHANG` | 23 | 9 | Optional transaction evidence; inspect status/linkage before using |
| `TMPPHIEUGIAOHANGDBCT` | 18 | 1 | Optional child transaction evidence |
| `LUUDOCQRCODE` | 4,326,610 | 4,169,928 | Historical trace only; exact LOT/QR filtering is mandatory |
| `Warehouse / Rack / Slot` | 1 / 1 / 1 | 5 / 14 / 375 | Do not copy source location IDs into the target fixture |
| `SlotLot` | 8,653 | 147 | Generate target allocations for selected lots; do not bulk-copy legacy allocation rows |
| `FVN_ItemFifoConfig` | 0 | 2 | Target-owned PCTP policy/configuration |
| `InspectionConfig` | 0 | 2 | Target-owned PCTP policy/configuration |

Counts are approximate metadata counts from SQL Server partitions. They do not establish that a row is MP, current, or suitable for a test. No business-row selection has been performed yet.

The full first-pass object classification is in [MP_TEST_DATASET_OBJECTS.csv](MP_TEST_DATASET_OBJECTS.csv). Entries marked `CANDIDATE_ONLY` are not permission to extract until the source keys, business filters, target mapping and quantity invariants are verified.

## Initial dataset shape

Dataset ID: `MP-REALISTIC-001`

Target scope:
- Start with 2–5 MP items.
- Start with 3–10 LOTs with positive `STOCKTP.SLCONLAI`, if source validation confirms that field's meaning.
- Select 1–3 representative order lines where a reliable order/part/date relationship exists.
- Include one partial-issue/remaining-stock example if source rows support it.
- Do not export full tables, whole order histories, employee/user data, or all `LUUDOCQRCODE` history.
- If the source cannot prove a relationship, omit that optional table rather than guessing the join.

The extraction query must be deterministic and bounded: explicit selected item/LOT/order identifiers, a small maximum row count, query timeout, and a manifest of counts/hashes. SQL14 remains SELECT-only. Business row contents must not be placed in ordinary CI logs or committed to Git.

## Mapping and target fixture rules

1. Build a dependency/key map from the selected rows and repository SQL; verify uniqueness before choosing natural keys. Do not assume table names alone prove relationships.
2. Map legacy item and lot data to SQL57's current target schema. In particular, SQL14 `B20Item.LineCode` and SQL57 `B20Item.LinesCode` differ; source `Purchase_Order_YMVN.IsDelivered/DeliveredDate` are absent from the target table in this inventory. These require explicit mapping decisions, not blind inserts.
3. Treat `Warehouse`, `Rack`, `Slot`, and `SlotLot` as target-generated fixture data. Create fresh test-prefixed location rows and map new target IDs.
4. Quantity invariants are mandatory:
   - sum of active `SlotLot.Quantity` for a slot equals the slot header `Slot.Quantity`;
   - aggregate slot allocations for a LOT must reconcile with the intended fixture quantity;
   - `STOCKTP` quantities must be reconciled against allocations and movement history according to the application's confirmed business rules;
   - do not set a header quantity to an arbitrary total just to make one assertion pass.
5. Every write goes only to a dedicated SQL57 catalog matching `B7R2_FCC_QC*`, never SQL14 and never the shared SQL57 `B7R2_FCC`. The current live inventory workflow is metadata-only and does not authorize writes.
6. Fixture creation must be repeatable/idempotent, scoped by a unique run ID, transactional where supported, and paired with pre-checks, post-checks, and cleanup verification. Do not execute business procedures as part of extraction.
7. Preserve target-owned views, stored procedures, functions, extensions and policy tables. Do not attempt SQL14 → SQL57 schema synchronization.

## Work sequence

- [x] Phase 1: metadata-only inventory on SQL14 and SQL57; source permission probe; artifact uploaded.
- [x] Phase 2A: first-pass source/target object manifest created from metadata and code coverage.
- [ ] Phase 2B: inspect relevant repository SQL and PK/FK/index metadata; confirm exact joins and candidate natural keys.
- [ ] Phase 2C: run a read-only, bounded candidate preview on SQL14 (counts and selected identifiers only initially).
- [ ] Phase 3: define an explicit, reviewed extraction contract and transformation mapping.
- [ ] Phase 4: seed only an isolated SQL57 `B7R2_FCC_QC*` catalog; create target Rack/Slot/SlotLot rows; validate invariants.
- [ ] Phase 5: run `Usp_Qrcode_Update_Stock2405`, `PhieuKhoRepository`, FIFO and bulk stock-slot scenarios with rollback/cleanup checks.
- [ ] Phase 6: produce a separate, reviewed SQL57 → SQL14 deployment/migration plan after PCTP is accepted.

## Current blockers (intentionally fail closed)

- The metadata artifact contains no business values, so actual MP identity and the exact join path have not been proven.
- Need to confirm which `STOCKTP` rows represent MP/HVN/YMVN and the meaning of `SLCONLAI`, `SLNHAP`, `SLXUAT`, and status fields.
- Need to review target-specific views/functions and the live SQL dependencies used by the production repository code before writing the fixture loader.
- The current workflow secret for SQL57 inventory reports login `sa`; replace it with a least-privilege metadata account before continuing to broader access. This is not a failure of run #3, but should be corrected before the write phase.

## Safety boundary

No production writes, no SQL14 DDL/DML, no broad business-row export, no copying source location IDs, and no writes to shared SQL57 `B7R2_FCC`. Any next workflow for preview/extraction must be a separately dispatched, explicitly confirmed workflow and must retain the isolated CI guards.
