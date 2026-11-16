# MES domain

The migration scripts here are the existing versioned MES schema foundation moved from the old `SQL/MES` location. They are committed candidates, not SQL Server runtime-tested or approved for production. Review target database/schema assumptions, PCTP vs MES ownership, stock-ledger authority and all migration dependencies before execution. `SourceReview/` and `LegacyCandidates/` are not deployment steps. The former `01_MES_Quantity_Core.sql` is retained only for comparison and must not be deployed alongside the versioned migrations.


All physical tables use the `FVN_` prefix after migration 013. IFS master and source BOM snapshots are maintained separately under the
`IFS` schema by migration 012 and are physically named `IFS.FVN_*`. The physical tables `MES.FVN_BomRevision` and
`MES.FVN_BomComponent` remain the controlled, operational BOM/approval model;
they are not overwritten by raw IFS synchronization.
