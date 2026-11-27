/*
 Diagnostic / optional cleanup for production-line mapping confusion.
 Default is READ-ONLY. Set @ApplyDeactivateStaleMappings = 1 only after reviewing
 the preview. This script does NOT DROP tables: the similarly named objects have
 different responsibilities (IFS source mirror vs MES LOT master vs bridge).
*/
USE FVN_PCTP;
GO
SET NOCOUNT ON;
DECLARE @ApplyDeactivateStaleMappings bit = 0; -- 0 = preview only; 1 = deactivate stale mappings

/* 1. Object inventory: same base name does not mean duplicate table. */
SELECT s.name AS SchemaName, o.name AS ObjectName, o.type_desc AS ObjectType,
       SUM(p.rows) AS ApproxRows
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
LEFT JOIN sys.partitions AS p ON p.object_id = o.object_id AND p.index_id IN (0,1)
WHERE o.name IN
(
    N'FVN_Site', N'FVN_PCTPSite', N'FVN_ProductionLine',
    N'FVN_ProductionDepartment', N'FVN_ProductionLotCodeMap',
    N'FVN_WorkCenter'
)
GROUP BY s.name, o.name, o.type_desc
ORDER BY o.name, s.name;

/* 2. Role of the objects:
      IFS.FVN_Site                  = synchronized IFS Site mirror
      dbo.FVN_PCTPSite              = PCTP active-site allow-list
      IFS.FVN_ProductionLine        = synchronized IFS line (long IFS code)
      dbo.FVN_ProductionLine        = MES LOT line master (3-char LotLineCode)
      dbo.FVN_ProductionDepartment   = MES LOT department master (4-char DepartmentCode)
      IFS.FVN_WorkCenter            = synchronized IFS work center
      MES.FVN_ProductionLotCodeMap  = bridge (Site + IFS line -> LOT line + department)
   These are NOT safe candidates for DROP. */

/* 3. Preview mappings whose source IFS site/line is missing or inactive,
      or whose PCTP site is disabled. */
SELECT m.SiteCode, m.PlanProductionLineCode AS IFSLineCode,
       m.LotLineCode, m.DepartmentCode, m.IsActive,
       CASE
         WHEN ISNULL(fs.IsPresentInIfs,0)=0 THEN N'IFS Site missing/inactive'
         WHEN ISNULL(p.IsActive,0)=0 THEN N'PCTP Site disabled'
         WHEN ISNULL(l.IsActive,0)=0 THEN N'IFS Production Line inactive'
         WHEN l.ProductionLineCode IS NULL THEN N'IFS Production Line missing'
         ELSE N'OK'
       END AS CleanupReason
FROM MES.FVN_ProductionLotCodeMap AS m
LEFT JOIN IFS.FVN_Site AS fs ON fs.SiteCode=m.SiteCode
LEFT JOIN dbo.FVN_PCTPSite AS p ON p.SiteCode=m.SiteCode
LEFT JOIN IFS.FVN_ProductionLine AS l
  ON l.SiteCode=m.SiteCode AND l.ProductionLineCode=m.PlanProductionLineCode
WHERE m.IsActive=1
  AND
  (
      ISNULL(fs.IsPresentInIfs,0)=0
      OR ISNULL(p.IsActive,0)=0
      OR l.ProductionLineCode IS NULL
      OR ISNULL(l.IsActive,0)=0
  )
ORDER BY m.SiteCode, m.PlanProductionLineCode;

/* 4. Optional cleanup: deactivate only stale mapping rows; preserve history.
      This is deliberately not a DELETE and never drops master tables. */
IF @ApplyDeactivateStaleMappings = 1
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE m
           SET IsActive=0
        FROM MES.FVN_ProductionLotCodeMap AS m
        LEFT JOIN IFS.FVN_Site AS fs ON fs.SiteCode=m.SiteCode
        LEFT JOIN dbo.FVN_PCTPSite AS p ON p.SiteCode=m.SiteCode
        LEFT JOIN IFS.FVN_ProductionLine AS l
          ON l.SiteCode=m.SiteCode AND l.ProductionLineCode=m.PlanProductionLineCode
        WHERE m.IsActive=1
          AND
          (
              ISNULL(fs.IsPresentInIfs,0)=0
              OR ISNULL(p.IsActive,0)=0
              OR l.ProductionLineCode IS NULL
              OR ISNULL(l.IsActive,0)=0
          );

        SELECT @@ROWCOUNT AS DeactivatedStaleMappings;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END
ELSE
    PRINT N'Preview only. Set @ApplyDeactivateStaleMappings = 1 to deactivate the listed stale mappings.';
GO

/* 5. Dependency check before any manual DROP/rename operation. */
SELECT fk.name AS ForeignKeyName,
       OBJECT_SCHEMA_NAME(fk.parent_object_id) AS ReferencingSchema,
       OBJECT_NAME(fk.parent_object_id) AS ReferencingTable,
       OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS ReferencedSchema,
       OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable
FROM sys.foreign_keys AS fk
WHERE fk.referenced_object_id IN
(
    OBJECT_ID(N'dbo.FVN_ProductionLine'),
    OBJECT_ID(N'dbo.FVN_ProductionDepartment'),
    OBJECT_ID(N'MES.FVN_ProductionLotCodeMap')
)
ORDER BY ReferencedSchema, ReferencedTable, ForeignKeyName;
GO
