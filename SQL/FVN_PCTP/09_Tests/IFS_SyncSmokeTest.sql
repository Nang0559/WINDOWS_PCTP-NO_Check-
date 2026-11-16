/*
 IFS synchronization smoke test.
 Read-only: run after migrations 012–013 and at least one successful sync. Legacy object names below resolve through compatibility synonyms; physical tables use FVN_ names.
*/
SET NOCOUNT ON;

DECLARE @BatchId bigint;
SELECT TOP (1) @BatchId=SyncBatchId
FROM IFS.SyncBatch
WHERE Status='APPLIED'
ORDER BY SyncBatchId DESC;

IF @BatchId IS NULL
    THROW 51300,'No successfully applied IFS synchronization batch was found.',1;

SELECT SyncBatchId,Status,StartedAtUtc,CompletedAtUtc,
       PartCatalogRows,InventoryPartRows,BomHeaderRows,
       BomAlternativeRows,BomComponentRows,InsertCount,UpdateCount
FROM IFS.SyncBatch
WHERE SyncBatchId=@BatchId;

SELECT 'PART_CATALOG' EntityName,COUNT(*) RowsSeen
FROM IFS.PartCatalog WHERE LastSeenBatchId=@BatchId
UNION ALL
SELECT 'INVENTORY_PART',COUNT(*)
FROM IFS.InventoryPart WHERE LastSeenBatchId=@BatchId
UNION ALL
SELECT 'BOM_HEADER',COUNT(*)
FROM IFS.BomHeader WHERE LastSeenBatchId=@BatchId
UNION ALL
SELECT 'BOM_ALTERNATIVE',COUNT(*)
FROM IFS.BomAlternative WHERE LastSeenBatchId=@BatchId
UNION ALL
SELECT 'BOM_COMPONENT',COUNT(*)
FROM IFS.BomComponent WHERE LastSeenBatchId=@BatchId;

IF EXISTS(
    SELECT 1 FROM IFS.BomAlternative a
    WHERE NOT EXISTS(
        SELECT 1 FROM IFS.BomHeader h
        WHERE h.SiteCode=a.SiteCode AND h.PartNo=a.PartNo
          AND h.EngChgLevel=a.EngChgLevel AND h.BomTypeDb=a.BomTypeDb))
    THROW 51301,'Orphan BOM Alternative detected.',1;

IF EXISTS(
    SELECT 1 FROM IFS.BomComponent c
    WHERE NOT EXISTS(
        SELECT 1 FROM IFS.BomAlternative a
        WHERE a.SiteCode=c.SiteCode AND a.PartNo=c.PartNo
          AND a.EngChgLevel=c.EngChgLevel AND a.BomTypeDb=c.BomTypeDb
          AND a.AlternativeNo=c.AlternativeNo))
    THROW 51302,'Orphan BOM Component detected.',1;

SELECT
    h.SiteCode,
    h.BomTypeDb,
    COUNT(*) HeaderCount
FROM IFS.BomHeader h
GROUP BY h.SiteCode,h.BomTypeDb
ORDER BY h.SiteCode,h.BomTypeDb;

SELECT
    a.SiteCode,
    a.BomTypeDb,
    COUNT(*) AlternativeCount,
    SUM(CASE WHEN NOT EXISTS(
        SELECT 1 FROM IFS.BomComponent c
        WHERE c.SiteCode=a.SiteCode AND c.PartNo=a.PartNo
          AND c.EngChgLevel=a.EngChgLevel AND c.BomTypeDb=a.BomTypeDb
          AND c.AlternativeNo=a.AlternativeNo
    ) THEN 1 ELSE 0 END) AlternativesWithoutComponents
FROM IFS.BomAlternative a
GROUP BY a.SiteCode,a.BomTypeDb
ORDER BY a.SiteCode,a.BomTypeDb;

SELECT TOP (100) *
FROM IFS.SyncError
ORDER BY SyncErrorId DESC;
