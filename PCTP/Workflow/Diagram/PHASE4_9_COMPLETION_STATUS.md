# Phase 4 -> 9 — XuLyHangLoi

## Business chain

`Initial QC` -> `Rework Plan` -> `Rework Export` -> `Production Delivery` -> `Rework QC` -> `Disposition` -> `Compensation` -> `Workflow` -> `Report`

## Phase 4 — Rework

- Rework quantity is sourced only from `FVN_PhieuXuLyBatThuongQCInitial.SoLuongRework`.
- It is not calculated from total NG.
- `ReworkPhase4Service` exposes the remaining export/delivery quantities.
- SQL plan table prevents exported/delivered quantities from exceeding the QC-approved plan.

## Phase 5 — Rework QC

- QC is allowed only after the approved Rework quantity has been fully exported and delivered to production.
- `OK + NG = Rework quantity` is enforced in both service validation and SQL CHECK constraint.
- One final Rework QC result per abnormal-processing ticket.

## Phase 6 — Disposition + Compensation

- Final disposal = initial disposal + NG after Rework.
- Compensation is an independent customer obligation.
- Compensation quantity is explicitly created from the customer/order obligation and is never inferred from NG/rework/disposal.
- Delivery cannot exceed the outstanding compensation quantity.

## Phase 7 — Workflow

Workflow remains data-driven through `sys_WorkflowTransitions`.
All Phase 4-9 transitions are audited in `FVN_PXLB_WorkflowAudit`.

## Phase 8 — Application/UI boundary

The module factory now exposes the Phase 5-9 application service and the shared workflow service so existing QT Chung forms can consume the same business guards instead of duplicating quantity rules.

Existing forms remain the presentation layer; no quantity/business rule is allowed to be implemented in the form itself.

## Phase 9 — Reporting

`vFVN_PXLB_ProcessReport` provides one consolidated row per abnormal-processing ticket:

- affected quantity
- Initial QC OK/NG
- Rework approved/exported/delivered
- Rework QC OK/NG
- final disposal
- compensation required/delivered/remaining

Compensation is deliberately not derived from NG.
