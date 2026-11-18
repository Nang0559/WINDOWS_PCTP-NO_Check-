# MES domain

The migration scripts here are the existing versioned MES schema foundation moved from the old `SQL/MES` location. They are committed candidates, not SQL Server runtime-tested or approved for production. Review target database/schema assumptions, PCTP vs MES ownership, stock-ledger authority and all migration dependencies before execution. `SourceReview/` and `LegacyCandidates/` are not deployment steps. The former `01_MES_Quantity_Core.sql` is retained only for comparison and must not be deployed alongside the versioned migrations.


All physical tables use the `FVN_` prefix after migration 013. IFS master and source BOM snapshots are maintained separately under the
`IFS` schema by migration 012 and are physically named `IFS.FVN_*`. The physical tables `MES.FVN_BomRevision` and
`MES.FVN_BomComponent` remain the controlled, operational BOM/approval model;
they are not overwritten by raw IFS synchronization.

## Migration 035 — per-product labels prepared at each process

Run `035_MES_PerUnitProcessLabels.sql` after migration 034. Each physical
product receives a unique MES `LabelCode`; the label references, but never
replaces, the existing 20-character production LOT. Labels are reserved for
the selected routing operation when production prepares that process. The
target quantity is the cumulative number of labels required for that LOT and
operation; increasing it creates only the additional labels.

Use `MES.usp_FVN_ProductionLabel_Prepare` before printing,
`MES.usp_FVN_ProductionLabel_Print` to authorize initial print/reprint,
`MES.usp_FVN_ProductionLabel_Apply` after scanning the label at the process,
and `MES.usp_FVN_ProductionLabel_Void` to cancel an unused label with a
mandatory reason. Applied labels cannot be voided. Reprints return the same
LabelCode and QR payload; they do not allocate another product identity.
Label reservations and print/apply/void events are audited independently of
actual production quantities and genealogy.

The current WinForms print surface sends a readable label sheet containing
LabelCode, production LOT, operation and QR payload text. A QR-capable label
printer/template must encode the exact `QrPayload` value as a QR symbol;
plain text printing is not a substitute for a scannable QR symbol.
