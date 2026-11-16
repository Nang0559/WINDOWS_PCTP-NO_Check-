# Programmability

Only reviewed, current views/functions/procedures/triggers should be promoted into ordered deployment folders here. `LegacyCandidates/` are retained unchanged for source comparison and must not be executed as a bundle. Each promoted module needs a caller map, read/write classification, transaction/locking semantics, external dependency review and SQL Server 2016 validation.


Naming rule: physical tables are `<Schema>.FVN_<Name>`. Programmable objects follow the implemented standard in `00_Governance/00K_PCTP_ProgrammabilityCutoverMap.md`: `dbo.usp_FVN_<Domain>_<Action>`, `dbo.vFVN_<Name>`, `dbo.ufn_FVN_<Name>`; objects inside the `MES`/`IFS` schemas use `usp_<Action>` / `fn_<Name>` because the schema carries the domain. Reference the physical `<Schema>.FVN_<Table>` names in all newly authored modules. Compatibility synonyms for old table names are temporary and must not be used by new modules or bulk-load code.
