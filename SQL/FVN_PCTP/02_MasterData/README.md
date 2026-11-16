# IFS master data and synchronization

The deployable IFS mirror and synchronization contract is introduced by
`05_MES/Migrations/012_IFS_MasterDataSync.sql` (after migrations 000 and 001).

## Source views (read-only)

- `IFSAPP.PART_CATALOG` -> `IFS.PartCatalog`
- `IFSAPP.INVENTORY_PART` -> `IFS.InventoryPart` and the MES compatibility projection `MES.IfsInventoryPart`
- `IFSAPP.MANUF_STRUCTURE_HEAD` -> `IFS.BomHeader`
- `IFSAPP.MANUF_STRUCT_ALTERNATE` -> `IFS.BomAlternative`
- `IFSAPP.MANUF_STRUCTURE` -> `IFS.BomComponent`

The C# service is `PCTP/Modules/IfsSync/IfsSyncService.cs`. It uses the existing
Oracle Managed Data Access provider through `IFSPROVIDER`, then bulk-loads
JSON-preserving rows into `IFS.SyncStageRow` in the dedicated `FVN_PCTP`
SQL Server database. It does not use Linked Server or write to IFS.

## Data rules

- Raw IFS `*_DB` codes are stored as business values; decoded labels remain in
  the source JSON payload for audit/display mapping.
- Full source row payloads are retained as JSON so fields not yet promoted to
  typed columns are not lost.
- BOM identity retains Site, Part, Engineering Change Level, raw BOM type,
  Alternative and Line Item keys.
- Alternative may legitimately have zero Components. The reverse orphan check
  supplied for the current Sites returned no rows.
- Source rows missing from a later run are not automatically deleted or
  deactivated. Site visibility is controlled by IFS `USER_ALLOWED_SITE_PUB`.
- A batch is applied only after all five views have returned non-empty data and
  the staged BOM parent-child relationships validate.
- `OBJVERSION` is stored for provenance, not used as an incremental watermark.

## Deployment and configuration

1. Deploy migrations 000 and 001, then 012.
2. Add `IFS_ORACLE` and `FVN_PCTP` connection strings to the protected runtime
   `PCTP.Database.config`. The SQL target catalog must be exactly `FVN_PCTP`.
3. Build PCTP and call `new IfsSyncService().SyncAll()` from an authorized
   administrative workflow. Do not call it on ordinary form/page load.
4. Check `IFS.SyncBatch`, `IFS.SyncError`, `IFS.SyncStageRow`,
   `IFS.PartCatalog`, `IFS.InventoryPart`, and the three `IFS.Bom*` tables.
5. Run the read-only verification script `09_Tests/IFS_SyncSmokeTest.sql`.

This is a FULL snapshot/upsert implementation. It intentionally does not yet
claim incremental sync or automatic retirement; those require validated IFS
change semantics and a reviewed site-scope policy.
