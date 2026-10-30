-- Verified catalog dependencies for dbo.vB20Item, exported from DB57 B7R2_FCC.
-- Deploy only to the disposable PCTP_QC_CI database. No business rows are copied.
-- dbo.CodeType is resolved using the repository's SQL2-verified mapping: nvarchar(16).
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.B20Class', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.B20Class (
        Id int IDENTITY(1,1) NOT NULL,
        ParentId int NOT NULL CONSTRAINT DF_B20Class_ParentId DEFAULT ((-1)),
        IsGroup bit NOT NULL CONSTRAINT DF_B20Class_IsGroup DEFAULT ((0)),
        ParentCode varchar(24) NOT NULL CONSTRAINT DF_B20Class_ParentCode DEFAULT (''),
        Code nvarchar(24) NOT NULL,
        Name nvarchar(128) NOT NULL CONSTRAINT DF_B20Class_Name DEFAULT (''),
        Name_English nvarchar(128) NULL,
        Name_French nvarchar(128) NULL,
        Name_Japanese nvarchar(128) NULL,
        Name_Chinese nvarchar(128) NULL,
        Name_Korean nvarchar(128) NULL,
        Name_Custom nvarchar(128) NULL,
        UniqueCode AS ([ParentCode] + [Code]),
        IsActive bit NOT NULL CONSTRAINT DF_B20Class_IsActive DEFAULT ((1)),
        CreatedBy int NOT NULL CONSTRAINT DF_B20Class_CreatedBy DEFAULT ((-1)),
        CreatedAt datetime NOT NULL CONSTRAINT DF_B20Class_CreatedAt DEFAULT (getutcdate()),
        ModifiedBy int NOT NULL CONSTRAINT DF_B20Class_ModifiedBy DEFAULT ((-1)),
        ModifiedAt datetime NOT NULL CONSTRAINT DF_B20Class_ModifiedAt DEFAULT (getutcdate()),
        [timestamp] timestamp NOT NULL,
        CONSTRAINT PK_B20DmLoai PRIMARY KEY NONCLUSTERED (Id ASC) WITH (FILLFACTOR = 90),
        CONSTRAINT IX_B20Class_Unique UNIQUE NONCLUSTERED (ParentCode ASC, Code ASC) WITH (FILLFACTOR = 90)
    );
END;
GO

IF OBJECT_ID(N'dbo.B20ItemGroup', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.B20ItemGroup (
        Id int IDENTITY(1,1) NOT NULL,
        ParentId int NOT NULL CONSTRAINT DF_B20ItemGroup_ParentId DEFAULT ((-1)),
        IsGroup bit NOT NULL CONSTRAINT DF_B20ItemGroup_IsGroup DEFAULT ((0)),
        BranchCode varchar(3) NULL,
        Code nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_Code DEFAULT (''),
        Name nvarchar(128) NOT NULL CONSTRAINT DF_B20ItemGroup_Name DEFAULT (''),
        Name2 nvarchar(128) NOT NULL CONSTRAINT DF_B20ItemGroup_Name2 DEFAULT (''),
        ItemCatgCode nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_ItemCatgCode DEFAULT (''),
        ItemAccount nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_ItemAccount DEFAULT (''),
        COGSAccount nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_COGSAccount DEFAULT (''),
        SalesAccount nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_SalesAccount DEFAULT (''),
        SalesReturnAccount nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_SalesReturnAccount DEFAULT (''),
        SalesDiscountAccount nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_SalesDiscountAccount DEFAULT (''),
        ItemAccount1 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_ItemAccount1 DEFAULT (''),
        COGSAccount1 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_COGSAccount1 DEFAULT (''),
        SalesAccount1 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_SalesAccount1 DEFAULT (''),
        SalesReturnAccount1 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_SalesReturnAccount1 DEFAULT (''),
        SalesDiscountAccount1 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_SalesDiscountAccount1 DEFAULT (''),
        ItemAccount2 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_ItemAccount2 DEFAULT (''),
        COGSAccount2 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_COGSAccount2 DEFAULT (''),
        SalesAccount2 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_SalesAccount2 DEFAULT (''),
        SalesReturnAccount2 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_SalesReturnAccount2 DEFAULT (''),
        SalesDiscountAccount2 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_SalesDiscountAccount2 DEFAULT (''),
        ItemAccount3 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_ItemAccount3 DEFAULT (''),
        COGSAccount3 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_COGSAccount3 DEFAULT (''),
        SalesAccount3 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_SalesAccount3 DEFAULT (''),
        SalesReturnAccount3 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_SalesReturnAccount3 DEFAULT (''),
        SalesDiscountAccount3 nvarchar(16) NOT NULL CONSTRAINT DF_B20ItemGroup_SalesDiscountAccount3 DEFAULT (''),
        IsActive bit NOT NULL CONSTRAINT DF_B20ItemGroup_IsActive DEFAULT ((1)),
        CreatedBy int NOT NULL CONSTRAINT DF_B20ItemGroup_CreatedBy DEFAULT ((-1)),
        CreatedAt datetime NOT NULL CONSTRAINT DF_B20ItemGroup_CreatedAt DEFAULT (getutcdate()),
        ModifiedBy int NOT NULL CONSTRAINT DF_B20ItemGroup_ModifiedBy DEFAULT ((-1)),
        ModifiedAt datetime NOT NULL CONSTRAINT DF_B20ItemGroup_ModifiedAt DEFAULT (getutcdate()),
        [timestamp] timestamp NOT NULL,
        _ParentCode0 nvarchar(16) NULL,
        CONSTRAINT PK_B20ItemGroup PRIMARY KEY CLUSTERED (Id ASC) WITH (FILLFACTOR = 90),
        CONSTRAINT IX_B20ItemGroup_Code UNIQUE NONCLUSTERED (Code ASC) WITH (FILLFACTOR = 90)
    );
END;
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER VIEW dbo.vB20Item
AS
SELECT dmvt.Id, dmvt.ParentId, dmvt.IsGroup, dmvt.BranchCode, dmvt.Code, dmvt.Name, dmvt.Name2,
    dmvt.Unit, dmvt.Unit0, dmvt.ConvertRate0, dmvt.Unit1, dmvt.ConvertRate1, dmvt.Unit2, dmvt.ConvertRate2,
    dmvt.Weight, dmvt.UnitCost, dmvt.UnitPrice, dmvt.MinCloseQty, dmvt.MaxCloseQty,
    dmvt.ExciseTaxCode, dmvt.ExciseTaxRate,
    dmvt.EnvCode, dmvt.EnvTaxRate, dmvt.SalesTaxCode,
    dmvt.ItemType, dmvt.ItemCatgCode, dmvt.ItemGroupCode,
    itemgroup.ItemAccount, itemgroup.COGSAccount, itemgroup.SalesAccount,
    itemgroup.SalesReturnAccount, itemgroup.SalesDiscountAccount,
    itemgroup.ItemAccount1, itemgroup.COGSAccount1, itemgroup.SalesAccount1,
    itemgroup.SalesReturnAccount1, itemgroup.SalesDiscountAccount1,
    itemgroup.ItemAccount2, itemgroup.COGSAccount2, itemgroup.SalesAccount2,
    itemgroup.SalesReturnAccount2, itemgroup.SalesDiscountAccount2,
    itemgroup.ItemAccount3, itemgroup.COGSAccount3, itemgroup.SalesAccount3,
    itemgroup.SalesReturnAccount3, itemgroup.SalesDiscountAccount3,
    dmvt.ProductCostId,
    CASE WHEN dmvt.IsGroup = 1 THEN 0 WHEN dmvt.ItemType = 3 THEN 2 ELSE 1 END AS ItemGroup,
    dmvt.Length, dmvt.Width, dmvt.Height,
    dmvt.IsItemWithSerialNo, dmvt.IsItemWithInstall, dmvt.IsItemWithTransport,
    dmvt.IsItemWithScale, dmvt.IsItemWithLocation, dmvt.IsItemWithLot,
    dmvt.ShelfLife, dmvt.ManufacturerCode,
    dmvt.ProductItemCode, loai.Name AS ItemTypeName,
    dmvt.ClassCode1, dmvt.ClassCode2, dmvt.ClassCode3,
    CAST(REPLACE(STR(ROW_NUMBER() OVER (ORDER BY DmVt.Code), 6), SPACE(1), '0') AS NVARCHAR(6)) AS _GroupOrder,
    CAST(0 AS TinyInt) AS AutoCreateProductCode,
    CAST(NULL AS NVARCHAR(24)) AS CodeTmp,
    Img.Image, Img.ParentId AS attachedParentId,
    dmvt.BarCode, dmvt.Comment,
    dmvt.IsActive, dmvt.CreatedBy, dmvt.CreatedAt, dmvt.ModifiedBy, dmvt.ModifiedAt, dmvt.timestamp,
    dmvt.Model
FROM dbo.B20Item AS dmvt
LEFT OUTER JOIN dbo.B20Class AS loai
    ON dmvt.ItemType = loai.Code AND loai.ParentCode = 'DMVT_Loai_Vt'
LEFT OUTER JOIN dbo.B20ItemGroup AS itemgroup
    ON dmvt.ItemGroupCode = itemgroup.Code
LEFT OUTER JOIN dbo.B20ImageStore AS Img
    ON Img.ParentId = dmvt.Id AND Img.Type = 'ITEM';
GO
