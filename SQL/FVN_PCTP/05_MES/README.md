# MES domain

The migration scripts here are the existing versioned MES schema foundation moved from the old `SQL/MES` location. They are committed candidates, not SQL Server runtime-tested or approved for production. Review target database/schema assumptions, PCTP vs MES ownership, stock-ledger authority and all migration dependencies before execution. `SourceReview/` and `LegacyCandidates/` are not deployment steps. The former `01_MES_Quantity_Core.sql` is retained only for comparison and must not be deployed alongside the versioned migrations.
