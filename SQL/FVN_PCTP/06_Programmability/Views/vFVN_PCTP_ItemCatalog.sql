/*
 dbo.vFVN_PCTP_ItemCatalog  - replaces legacy vB20Item / B20Item reads (item master) in PCTP C# code.
 Source of truth: IFS part master (IFS.FVN_InventoryPart) via dbo.vFVN_PCTP_Part (migration 014). No B7R2_FCC/BRAVO dependency.
 Column shape follows what C# queries use from vB20Item: Id, Code, Name, Model, MinCloseQty (+ SiteCode, UnitMeas).
 Single-site assumption: with >1 SiteCode a Code can repeat; callers must then filter SiteCode.
 Id = FVN_PartIdentity.LegacyB20ItemId (NULL for parts that never existed in Bravo).
 Run after 014. Idempotent. SQL Server 2016.
 Rollback: DROP VIEW dbo.vFVN_PCTP_ItemCatalog;
*/
IF DB_NAME() <> N'FVN_PCTP' THROW 51401, 'Run only in database FVN_PCTP.', 1;
IF OBJECT_ID(N'dbo.vFVN_PCTP_Part', N'V') IS NULL THROW 51402, 'dbo.vFVN_PCTP_Part is missing. Run migration 014 first.', 1;
GO
CREATE OR ALTER VIEW dbo.vFVN_PCTP_ItemCatalog
AS
SELECT
    p.LegacyB20ItemId AS Id,
    p.PartNo          AS Code,
    p.Description     AS Name,
    p.ModelCode       AS Model,
    p.UnitMeas        AS UnitMeas,
    p.DefaultPackageQty AS MinCloseQty,   /* 00I: PCTP-owned packaging rule (FVN_PartOperationalProfile). OWNER MUST CONFIRM data load; NULL => C# ISNULL(...,0) path */
    p.SiteCode        AS SiteCode
FROM dbo.vFVN_PCTP_Part AS p;
GO
