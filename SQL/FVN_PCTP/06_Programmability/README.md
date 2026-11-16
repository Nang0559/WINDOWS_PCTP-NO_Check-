# Programmability

Only reviewed, current views/functions/procedures/triggers should be promoted into ordered deployment folders here. `LegacyCandidates/` are retained unchanged for source comparison and must not be executed as a bundle. Each promoted module needs a caller map, read/write classification, transaction/locking semantics, external dependency review and SQL Server 2016 validation.


Naming rule: the `FVN_` prefix applies to physical tables only. Keep procedures, views, functions, triggers and types named by their business action/contract; reference the physical `<Schema>.FVN_<Table>` names in all newly authored modules. Compatibility synonyms for old table names are temporary and must not be used by new bulk-load code.
