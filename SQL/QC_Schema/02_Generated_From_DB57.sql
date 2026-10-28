/*
 AUTO-GENERATED from SQL2 DB57 column metadata for the reviewed table allowlist.
 PK/unique indexes, FKs and CHECK constraints are emitted separately by 03_Generated_From_DB57_Constraints.sql.
 Triggers, permissions and procedure dependencies require separate review. Alias types require a verified map.
 Apply only to disposable PCTP_QC_CI. Never apply to DB57 or production.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.b20dept', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[b20dept] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ParentId] int NOT NULL DEFAULT ((-1)),
        [IsGroup] bit NOT NULL DEFAULT ((0)),
        [BranchCode] nvarchar(1) NOT NULL DEFAULT (''),
        [Code] nvarchar(16) NOT NULL,
        [Name] nvarchar(32) NOT NULL DEFAULT (''),
        [Name2] nvarchar(32) NOT NULL DEFAULT (''),
        [ClassCode1] nvarchar(16) NOT NULL DEFAULT (''),
        [ClassCode2] nvarchar(16) NOT NULL DEFAULT (''),
        [ClassCode3] nvarchar(16) NOT NULL DEFAULT (''),
        [IsActive] bit NOT NULL DEFAULT ((1)),
        [CreatedBy] int NOT NULL DEFAULT ((-1)),
        [CreatedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [ModifiedBy] int NOT NULL DEFAULT ((-1)),
        [ModifiedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [timestamp] timestamp NOT NULL,
        [_ParentCode0] nvarchar(16) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.b20dept', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.b20dept', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.b20dept') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ParentId', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (3, N'IsGroup', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (4, N'BranchCode', N'nvarchar', 3, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (5, N'Code', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Name', N'nvarchar', 64, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (7, N'Name2', N'nvarchar', 64, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (8, N'ClassCode1', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (9, N'ClassCode2', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (10, N'ClassCode3', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (11, N'IsActive', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((1))', N'', N''), (12, N'CreatedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (13, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (14, N'ModifiedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (15, N'ModifiedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (16, N'timestamp', N'timestamp', 8, 0, 0, 0, 0, 0, N'', N'', N'', N''), (17, N'_ParentCode0', N'nvarchar', 32, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ParentId', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (3, N'IsGroup', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (4, N'BranchCode', N'nvarchar', 3, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (5, N'Code', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Name', N'nvarchar', 64, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (7, N'Name2', N'nvarchar', 64, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (8, N'ClassCode1', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (9, N'ClassCode2', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (10, N'ClassCode3', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (11, N'IsActive', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((1))', N'', N''), (12, N'CreatedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (13, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (14, N'ModifiedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (15, N'ModifiedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (16, N'timestamp', N'timestamp', 8, 0, 0, 0, 0, 0, N'', N'', N'', N''), (17, N'_ParentCode0', N'nvarchar', 32, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.b20dept'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.b20dept', 1;
GO

IF OBJECT_ID(N'dbo.b20gear', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[b20gear] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ParentId] int NOT NULL DEFAULT ((-1)),
        [IsGroup] bit NOT NULL DEFAULT ((0)),
        [BranchCode] nvarchar(1) NOT NULL DEFAULT (''),
        [Code] int NOT NULL,
        [Name] nvarchar(5) NOT NULL,
        [Name2] nvarchar(32) NOT NULL DEFAULT (''),
        [IsActive] bit NOT NULL DEFAULT ((1)),
        [CreatedBy] int NOT NULL DEFAULT ((-1)),
        [CreatedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [ModifiedBy] int NOT NULL DEFAULT ((-1)),
        [ModifiedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [timestamp] timestamp NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.b20gear', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.b20gear', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.b20gear') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ParentId', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (3, N'IsGroup', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (4, N'BranchCode', N'nvarchar', 3, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (5, N'Code', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Name', N'nvarchar', 10, 0, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'Name2', N'nvarchar', 64, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (8, N'IsActive', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((1))', N'', N''), (9, N'CreatedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (10, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (11, N'ModifiedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (12, N'ModifiedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (13, N'timestamp', N'timestamp', 8, 0, 0, 0, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ParentId', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (3, N'IsGroup', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (4, N'BranchCode', N'nvarchar', 3, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (5, N'Code', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Name', N'nvarchar', 10, 0, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'Name2', N'nvarchar', 64, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (8, N'IsActive', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((1))', N'', N''), (9, N'CreatedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (10, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (11, N'ModifiedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (12, N'ModifiedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (13, N'timestamp', N'timestamp', 8, 0, 0, 0, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.b20gear'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.b20gear', 1;
GO

IF OBJECT_ID(N'dbo.b20imagestore', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[b20imagestore] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ParentId] int NOT NULL DEFAULT ((-1)),
        [IsGroup] bit NOT NULL DEFAULT ((0)),
        [Image] varbinary(MAX) NULL,
        [Type] varchar(8) NOT NULL DEFAULT ('HRM'),
        [IsActive] bit NOT NULL DEFAULT ((1)),
        [CreatedBy] int NOT NULL DEFAULT ((-1)),
        [CreatedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [ModifiedBy] int NOT NULL DEFAULT ((-1)),
        [ModifiedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [timestamp] timestamp NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.b20imagestore', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.b20imagestore', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.b20imagestore') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ParentId', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (3, N'IsGroup', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (4, N'Image', N'varbinary', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'Type', N'varchar', 8, 0, 0, 0, 0, 0, N'', N'(''HRM'')', N'', N''), (6, N'IsActive', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((1))', N'', N''), (7, N'CreatedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (8, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (9, N'ModifiedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (10, N'ModifiedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (11, N'timestamp', N'timestamp', 8, 0, 0, 0, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ParentId', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (3, N'IsGroup', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (4, N'Image', N'varbinary', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'Type', N'varchar', 8, 0, 0, 0, 0, 0, N'', N'(''HRM'')', N'', N''), (6, N'IsActive', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((1))', N'', N''), (7, N'CreatedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (8, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (9, N'ModifiedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (10, N'ModifiedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (11, N'timestamp', N'timestamp', 8, 0, 0, 0, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.b20imagestore'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.b20imagestore', 1;
GO

IF OBJECT_ID(N'dbo.b20item', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[b20item] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ParentId] int NOT NULL DEFAULT ((-1)),
        [IsGroup] bit NOT NULL DEFAULT ((0)),
        [BranchCode] varchar(3) NOT NULL DEFAULT (''),
        [Code] nvarchar(25) NOT NULL,
        [BarCode] nvarchar(24) NOT NULL DEFAULT (''),
        [Name] nvarchar(96) NOT NULL DEFAULT (''),
        [Name2] nvarchar(96) NOT NULL DEFAULT (''),
        [Unit] nvarchar(4) NOT NULL DEFAULT (''),
        [Unit0] nvarchar(4) NOT NULL DEFAULT (''),
        [ConvertRate0] numeric(15,5) NOT NULL DEFAULT ((1)),
        [Unit1] nvarchar(4) NOT NULL DEFAULT (''),
        [ConvertRate1] numeric(15,5) NOT NULL DEFAULT ((1)),
        [Unit2] nvarchar(4) NOT NULL DEFAULT (''),
        [ConvertRate2] numeric(15,5) NOT NULL DEFAULT ((1)),
        [Weight] numeric(15,4) NOT NULL DEFAULT ((1)),
        [UnitCost] numeric(18,2) NOT NULL DEFAULT ((0)),
        [UnitPrice] numeric(18,2) NOT NULL DEFAULT ((0)),
        [MinCloseQty] numeric(15,4) NOT NULL DEFAULT ((0)),
        [MaxCloseQty] numeric(15,4) NOT NULL DEFAULT ((0)),
        [SalesTaxCode] nvarchar(16) NOT NULL DEFAULT (''),
        [ExciseTaxCode] nvarchar(16) NOT NULL DEFAULT (''),
        [ExciseTaxRate] numeric(8,4) NOT NULL DEFAULT ((0)),
        [ItemType] tinyint NOT NULL DEFAULT ((2)),
        [ItemCatgCode] nvarchar(16) NOT NULL DEFAULT (''),
        [ItemGroupCode] nvarchar(16) NOT NULL DEFAULT (''),
        [ProductCostId] varchar(16) NOT NULL DEFAULT (''),
        [ProductItemCode] nvarchar(24) NOT NULL DEFAULT (''),
        [EnvCode] nvarchar(16) NOT NULL DEFAULT (''),
        [EnvTaxRate] numeric(15,5) NOT NULL DEFAULT ((1)),
        [Length] numeric(15,4) NOT NULL DEFAULT ((0)),
        [Width] numeric(15,4) NOT NULL DEFAULT ((0)),
        [Height] numeric(15,4) NOT NULL DEFAULT ((0)),
        [IsItemWithSerialNo] tinyint NOT NULL DEFAULT ((0)),
        [IsItemWithInstall] tinyint NOT NULL DEFAULT ((0)),
        [IsItemWithTransport] tinyint NOT NULL DEFAULT ((0)),
        [IsItemWithScale] tinyint NOT NULL DEFAULT ((0)),
        [IsItemWithLocation] tinyint NOT NULL DEFAULT ((0)),
        [IsItemWithLot] tinyint NOT NULL DEFAULT ((0)),
        [ShelfLife] tinyint NOT NULL DEFAULT ((0)),
        [ManufacturerCode] nvarchar(16) NOT NULL DEFAULT (''),
        [Comment] nvarchar(96) NOT NULL DEFAULT (''),
        [ClassCode1] nvarchar(16) NOT NULL DEFAULT (''),
        [ClassCode2] nvarchar(16) NOT NULL DEFAULT (''),
        [ClassCode3] nvarchar(16) NOT NULL DEFAULT (''),
        [IsActive] bit NOT NULL DEFAULT ((1)),
        [CreatedBy] int NOT NULL DEFAULT ((-1)),
        [CreatedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [ModifiedBy] int NOT NULL DEFAULT ((-1)),
        [ModifiedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [timestamp] timestamp NOT NULL,
        [_ParentCode0] nvarchar(24) NULL,
        [Model] nvarchar(12) NOT NULL DEFAULT (''),
        [DeptCode] nvarchar(16) NULL,
        [LinesCode] nvarchar(16) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.b20item', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.b20item', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.b20item') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ParentId', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (3, N'IsGroup', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (4, N'BranchCode', N'varchar', 3, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (5, N'Code', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'BarCode', N'nvarchar', 48, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (7, N'Name', N'nvarchar', 192, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (8, N'Name2', N'nvarchar', 192, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (9, N'Unit', N'nvarchar', 8, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (10, N'Unit0', N'nvarchar', 8, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (11, N'ConvertRate0', N'numeric', 9, 15, 5, 0, 0, 0, N'', N'((1))', N'', N''), (12, N'Unit1', N'nvarchar', 8, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (13, N'ConvertRate1', N'numeric', 9, 15, 5, 0, 0, 0, N'', N'((1))', N'', N''), (14, N'Unit2', N'nvarchar', 8, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (15, N'ConvertRate2', N'numeric', 9, 15, 5, 0, 0, 0, N'', N'((1))', N'', N''), (16, N'Weight', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((1))', N'', N''), (17, N'UnitCost', N'numeric', 9, 18, 2, 0, 0, 0, N'', N'((0))', N'', N''), (18, N'UnitPrice', N'numeric', 9, 18, 2, 0, 0, 0, N'', N'((0))', N'', N''), (19, N'MinCloseQty', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((0))', N'', N''), (20, N'MaxCloseQty', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((0))', N'', N''), (21, N'SalesTaxCode', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (22, N'ExciseTaxCode', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (23, N'ExciseTaxRate', N'numeric', 5, 8, 4, 0, 0, 0, N'', N'((0))', N'', N''), (24, N'ItemType', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((2))', N'', N''), (25, N'ItemCatgCode', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (26, N'ItemGroupCode', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (27, N'ProductCostId', N'varchar', 16, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (28, N'ProductItemCode', N'nvarchar', 48, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (29, N'EnvCode', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (30, N'EnvTaxRate', N'numeric', 9, 15, 5, 0, 0, 0, N'', N'((1))', N'', N''), (31, N'Length', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((0))', N'', N''), (32, N'Width', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((0))', N'', N''), (33, N'Height', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((0))', N'', N''), (34, N'IsItemWithSerialNo', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (35, N'IsItemWithInstall', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (36, N'IsItemWithTransport', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (37, N'IsItemWithScale', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (38, N'IsItemWithLocation', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (39, N'IsItemWithLot', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (40, N'ShelfLife', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (41, N'ManufacturerCode', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (42, N'Comment', N'nvarchar', 192, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (43, N'ClassCode1', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (44, N'ClassCode2', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (45, N'ClassCode3', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (46, N'IsActive', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((1))', N'', N''), (47, N'CreatedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (48, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (49, N'ModifiedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (50, N'ModifiedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (51, N'timestamp', N'timestamp', 8, 0, 0, 0, 0, 0, N'', N'', N'', N''), (52, N'_ParentCode0', N'nvarchar', 48, 0, 0, 1, 0, 0, N'', N'', N'', N''), (53, N'Model', N'nvarchar', 24, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (54, N'DeptCode', N'nvarchar', 32, 0, 0, 1, 0, 0, N'', N'', N'', N''), (55, N'LinesCode', N'nvarchar', 32, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ParentId', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (3, N'IsGroup', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (4, N'BranchCode', N'varchar', 3, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (5, N'Code', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'BarCode', N'nvarchar', 48, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (7, N'Name', N'nvarchar', 192, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (8, N'Name2', N'nvarchar', 192, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (9, N'Unit', N'nvarchar', 8, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (10, N'Unit0', N'nvarchar', 8, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (11, N'ConvertRate0', N'numeric', 9, 15, 5, 0, 0, 0, N'', N'((1))', N'', N''), (12, N'Unit1', N'nvarchar', 8, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (13, N'ConvertRate1', N'numeric', 9, 15, 5, 0, 0, 0, N'', N'((1))', N'', N''), (14, N'Unit2', N'nvarchar', 8, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (15, N'ConvertRate2', N'numeric', 9, 15, 5, 0, 0, 0, N'', N'((1))', N'', N''), (16, N'Weight', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((1))', N'', N''), (17, N'UnitCost', N'numeric', 9, 18, 2, 0, 0, 0, N'', N'((0))', N'', N''), (18, N'UnitPrice', N'numeric', 9, 18, 2, 0, 0, 0, N'', N'((0))', N'', N''), (19, N'MinCloseQty', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((0))', N'', N''), (20, N'MaxCloseQty', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((0))', N'', N''), (21, N'SalesTaxCode', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (22, N'ExciseTaxCode', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (23, N'ExciseTaxRate', N'numeric', 5, 8, 4, 0, 0, 0, N'', N'((0))', N'', N''), (24, N'ItemType', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((2))', N'', N''), (25, N'ItemCatgCode', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (26, N'ItemGroupCode', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (27, N'ProductCostId', N'varchar', 16, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (28, N'ProductItemCode', N'nvarchar', 48, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (29, N'EnvCode', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (30, N'EnvTaxRate', N'numeric', 9, 15, 5, 0, 0, 0, N'', N'((1))', N'', N''), (31, N'Length', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((0))', N'', N''), (32, N'Width', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((0))', N'', N''), (33, N'Height', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((0))', N'', N''), (34, N'IsItemWithSerialNo', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (35, N'IsItemWithInstall', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (36, N'IsItemWithTransport', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (37, N'IsItemWithScale', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (38, N'IsItemWithLocation', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (39, N'IsItemWithLot', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (40, N'ShelfLife', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (41, N'ManufacturerCode', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (42, N'Comment', N'nvarchar', 192, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (43, N'ClassCode1', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (44, N'ClassCode2', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (45, N'ClassCode3', N'nvarchar', 32, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (46, N'IsActive', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((1))', N'', N''), (47, N'CreatedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (48, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (49, N'ModifiedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (50, N'ModifiedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (51, N'timestamp', N'timestamp', 8, 0, 0, 0, 0, 0, N'', N'', N'', N''), (52, N'_ParentCode0', N'nvarchar', 48, 0, 0, 1, 0, 0, N'', N'', N'', N''), (53, N'Model', N'nvarchar', 24, 0, 0, 0, 0, 0, N'', N'('''')', N'', N''), (54, N'DeptCode', N'nvarchar', 32, 0, 0, 1, 0, 0, N'', N'', N'', N''), (55, N'LinesCode', N'nvarchar', 32, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.b20item'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.b20item', 1;
GO

IF OBJECT_ID(N'dbo.b20itemquycach', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[b20itemquycach] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ParentId] int NOT NULL DEFAULT ((-1)),
        [IsGroup] bit NOT NULL DEFAULT ((0)),
        [BuiltinOrder] smallint NOT NULL DEFAULT ((0)),
        [CustomerCode] nvarchar(24) NOT NULL,
        [ItemCode] nvarchar(25) NULL,
        [Quantity] numeric(15,4) NOT NULL DEFAULT ((0)),
        [IsActive] bit NOT NULL DEFAULT ((1)),
        [CreatedBy] int NOT NULL DEFAULT ((-1)),
        [CreatedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [ModifiedBy] int NOT NULL DEFAULT ((-1)),
        [ModifiedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [timestamp] timestamp NOT NULL,
        [MaxCloseQty] numeric(15,4) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.b20itemquycach', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.b20itemquycach', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.b20itemquycach') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ParentId', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (3, N'IsGroup', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (4, N'BuiltinOrder', N'smallint', 2, 5, 0, 0, 0, 0, N'', N'((0))', N'', N''), (5, N'CustomerCode', N'nvarchar', 48, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'ItemCode', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'Quantity', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((0))', N'', N''), (8, N'IsActive', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((1))', N'', N''), (9, N'CreatedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (10, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (11, N'ModifiedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (12, N'ModifiedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (13, N'timestamp', N'timestamp', 8, 0, 0, 0, 0, 0, N'', N'', N'', N''), (14, N'MaxCloseQty', N'numeric', 9, 15, 4, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ParentId', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (3, N'IsGroup', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (4, N'BuiltinOrder', N'smallint', 2, 5, 0, 0, 0, 0, N'', N'((0))', N'', N''), (5, N'CustomerCode', N'nvarchar', 48, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'ItemCode', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'Quantity', N'numeric', 9, 15, 4, 0, 0, 0, N'', N'((0))', N'', N''), (8, N'IsActive', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((1))', N'', N''), (9, N'CreatedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (10, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (11, N'ModifiedBy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((-1))', N'', N''), (12, N'ModifiedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getutcdate())', N'', N''), (13, N'timestamp', N'timestamp', 8, 0, 0, 0, 0, 0, N'', N'', N'', N''), (14, N'MaxCloseQty', N'numeric', 9, 15, 4, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.b20itemquycach'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.b20itemquycach', 1;
GO

IF OBJECT_ID(N'dbo.docqrcode', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[docqrcode] (
        [LOTFCC] nvarchar(250) NULL,
        [MAHANGFCC] nvarchar(30) NULL,
        [SLTEMFCC] smallint NULL,
        [LOTHVN] nvarchar(250) NULL,
        [MAHANGHVN] nvarchar(30) NULL,
        [SLTEMHVN] smallint NULL,
        [STATUS] smallint NULL,
        [MAFCC] nvarchar(25) NULL,
        [STT] smallint NULL,
        [KETQUA] nvarchar(15) NULL,
        [CUA] nvarchar(5) NULL,
        [TRUYEN] nvarchar(25) NULL,
        [GIO] nvarchar(25) NULL,
        [STTBAN] int NULL,
        [SUALOTHVN] nchar(15) NULL,
        [TGLUU] timestamp NULL,
        [FindTem] nvarchar(25) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.docqrcode', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.docqrcode', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.docqrcode') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOTFCC', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANGFCC', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'SLTEMFCC', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'LOTHVN', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'MAHANGHVN', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLTEMHVN', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'STATUS', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'MAFCC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'KETQUA', N'nvarchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'CUA', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'GIO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'STTBAN', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'SUALOTHVN', N'nchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'TGLUU', N'timestamp', 8, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'FindTem', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOTFCC', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANGFCC', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'SLTEMFCC', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'LOTHVN', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'MAHANGHVN', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLTEMHVN', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'STATUS', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'MAFCC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'KETQUA', N'nvarchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'CUA', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'GIO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'STTBAN', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'SUALOTHVN', N'nchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'TGLUU', N'timestamp', 8, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'FindTem', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.docqrcode'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.docqrcode', 1;
GO

IF OBJECT_ID(N'dbo.docqrcode_100003', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[docqrcode_100003] (
        [LOTFCC] nvarchar(250) NULL,
        [MAHANGFCC] nvarchar(30) NULL,
        [SLTEMFCC] int NULL,
        [LOTHVN] nvarchar(250) NULL,
        [MAHANGHVN] nvarchar(30) NULL,
        [SLTEMHVN] int NULL,
        [STATUS] nvarchar(5) NULL,
        [MAFCC] nvarchar(25) NULL,
        [STT] smallint NULL,
        [KETQUA] nvarchar(15) NULL,
        [CUA] nvarchar(5) NULL,
        [TRUYEN] nvarchar(25) NULL,
        [GIO] nvarchar(25) NULL,
        [STTBAN] int NULL,
        [SUALOTHVN] nchar(15) NULL,
        [TGLUU] timestamp NULL,
        [FindTem] nvarchar(25) NULL,
        [SOPHIEU] nvarchar(25) NULL,
        [GEAR] nvarchar(25) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.docqrcode_100003', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.docqrcode_100003', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.docqrcode_100003') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOTFCC', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANGFCC', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'SLTEMFCC', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'LOTHVN', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'MAHANGHVN', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLTEMHVN', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'STATUS', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'MAFCC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'KETQUA', N'nvarchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'CUA', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'GIO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'STTBAN', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'SUALOTHVN', N'nchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'TGLUU', N'timestamp', 8, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'FindTem', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'SOPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'GEAR', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOTFCC', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANGFCC', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'SLTEMFCC', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'LOTHVN', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'MAHANGHVN', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLTEMHVN', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'STATUS', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'MAFCC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'KETQUA', N'nvarchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'CUA', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'GIO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'STTBAN', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'SUALOTHVN', N'nchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'TGLUU', N'timestamp', 8, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'FindTem', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'SOPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'GEAR', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.docqrcode_100003'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.docqrcode_100003', 1;
GO

IF OBJECT_ID(N'dbo.docqrcode_sp', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[docqrcode_sp] (
        [LOTFCC] nvarchar(250) NULL,
        [MAHANGFCC] nvarchar(30) NULL,
        [SLTEMFCC] smallint NULL,
        [LOTHVN] nvarchar(250) NULL,
        [MAHANGHVN] nvarchar(30) NULL,
        [SLTEMHVN] smallint NULL,
        [STATUS] smallint NULL,
        [MAFCC] nvarchar(25) NULL,
        [STT] smallint NULL,
        [KETQUA] nvarchar(15) NULL,
        [CUA] nvarchar(5) NULL,
        [TRUYEN] nvarchar(25) NULL,
        [GIO] nvarchar(25) NULL,
        [STTBAN] int NULL,
        [SUALOTHVN] nchar(15) NULL,
        [TGLUU] timestamp NULL,
        [FindTem] nvarchar(25) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.docqrcode_sp', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.docqrcode_sp', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.docqrcode_sp') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOTFCC', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANGFCC', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'SLTEMFCC', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'LOTHVN', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'MAHANGHVN', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLTEMHVN', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'STATUS', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'MAFCC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'KETQUA', N'nvarchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'CUA', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'GIO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'STTBAN', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'SUALOTHVN', N'nchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'TGLUU', N'timestamp', 8, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'FindTem', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOTFCC', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANGFCC', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'SLTEMFCC', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'LOTHVN', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'MAHANGHVN', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLTEMHVN', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'STATUS', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'MAFCC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'KETQUA', N'nvarchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'CUA', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'GIO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'STTBAN', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'SUALOTHVN', N'nchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'TGLUU', N'timestamp', 8, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'FindTem', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.docqrcode_sp'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.docqrcode_sp', 1;
GO

IF OBJECT_ID(N'dbo.fvn_deliverycommandledger', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_deliverycommandledger] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [BusinessKey] nvarchar(100) NOT NULL,
        [CustomerCode] nvarchar(25) NOT NULL,
        [DeliveryReference] nvarchar(50) NOT NULL,
        [PerformedBy] nvarchar(50) NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [TotalQuantity] int NOT NULL,
        [PendingSttCsv] nvarchar(500) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (sysdatetime())
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_deliverycommandledger', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_deliverycommandledger', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_deliverycommandledger') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'BusinessKey', N'nvarchar', 200, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'CustomerCode', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'DeliveryReference', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'PerformedBy', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'OccurredAt', N'datetime2', 6, 19, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'TotalQuantity', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'PendingSttCsv', N'nvarchar', 1000, 0, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'CreatedAt', N'datetime2', 6, 19, 0, 0, 0, 0, N'', N'(sysdatetime())', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'BusinessKey', N'nvarchar', 200, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'CustomerCode', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'DeliveryReference', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'PerformedBy', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'OccurredAt', N'datetime2', 6, 19, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'TotalQuantity', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'PendingSttCsv', N'nvarchar', 1000, 0, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'CreatedAt', N'datetime2', 6, 19, 0, 0, 0, 0, N'', N'(sysdatetime())', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_deliverycommandledger'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_deliverycommandledger', 1;
GO

IF OBJECT_ID(N'dbo.fvn_deliverycommandqr', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_deliverycommandqr] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [LedgerId] int NOT NULL,
        [PartCode] nvarchar(50) NOT NULL,
        [QrKey] nvarchar(225) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (sysdatetime())
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_deliverycommandqr', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_deliverycommandqr', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_deliverycommandqr') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'LedgerId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'PartCode', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'QrKey', N'nvarchar', 450, 0, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'CreatedAt', N'datetime2', 6, 19, 0, 0, 0, 0, N'', N'(sysdatetime())', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'LedgerId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'PartCode', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'QrKey', N'nvarchar', 450, 0, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'CreatedAt', N'datetime2', 6, 19, 0, 0, 0, 0, N'', N'(sysdatetime())', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_deliverycommandqr'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_deliverycommandqr', 1;
GO

IF OBJECT_ID(N'dbo.fvn_hangchogiao', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_hangchogiao] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [MaHang] nvarchar(25) NOT NULL,
        [LotThung] nvarchar(25) NULL,
        [LotGoc] nvarchar(25) NULL,
        [SoLuong] int NOT NULL,
        [SlotIdNguon] int NULL,
        [LoaiYeuCauGiao] nvarchar(15) NOT NULL,
        [PhieuKhachTraId] int NULL,
        [PhieuXuLyBatThuongId] int NULL,
        [DinhDanhPhieuGiao] nvarchar(100) NULL,
        [TrangThai] nvarchar(15) NOT NULL,
        [NgayXuatKho] datetime NULL,
        [NguoiXuatKho] nvarchar(25) NULL,
        [NgayGiao] datetime NULL,
        [NguoiGiao] nvarchar(25) NULL,
        [Note] nvarchar(125) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_hangchogiao', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_hangchogiao', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_hangchogiao') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'MaHang', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'LotThung', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'LotGoc', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SoLuong', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'SlotIdNguon', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'LoaiYeuCauGiao', N'nvarchar', 30, 0, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'PhieuKhachTraId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'DinhDanhPhieuGiao', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'TrangThai', N'nvarchar', 30, 0, 0, 0, 0, 0, N'', N'', N'', N''), (12, N'NgayXuatKho', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (13, N'NguoiXuatKho', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'NgayGiao', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (15, N'NguoiGiao', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'MaHang', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'LotThung', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'LotGoc', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SoLuong', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'SlotIdNguon', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'LoaiYeuCauGiao', N'nvarchar', 30, 0, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'PhieuKhachTraId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'DinhDanhPhieuGiao', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'TrangThai', N'nvarchar', 30, 0, 0, 0, 0, 0, N'', N'', N'', N''), (12, N'NgayXuatKho', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (13, N'NguoiXuatKho', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'NgayGiao', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (15, N'NguoiGiao', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_hangchogiao'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_hangchogiao', 1;
GO

IF OBJECT_ID(N'dbo.fvn_hangchogiaopickdetail', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_hangchogiaopickdetail] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [HangChoGiaoId] int NOT NULL,
        [SlotIdNguon] int NOT NULL,
        [ItemCode] nvarchar(50) NOT NULL,
        [LotNo] nvarchar(250) NOT NULL,
        [Quantity] int NOT NULL,
        [TemCode] nvarchar(250) NULL,
        [QrData] nvarchar(MAX) NULL,
        [ImportDate] datetime NULL,
        [PhieuStatus] tinyint NOT NULL DEFAULT ((0)),
        [CreatedAt] datetime NOT NULL DEFAULT (getdate())
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_hangchogiaopickdetail', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_hangchogiaopickdetail', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_hangchogiaopickdetail') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'HangChoGiaoId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SlotIdNguon', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'ItemCode', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'LotNo', N'nvarchar', 500, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Quantity', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'TemCode', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'QrData', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'ImportDate', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (10, N'PhieuStatus', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (11, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'HangChoGiaoId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SlotIdNguon', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'ItemCode', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'LotNo', N'nvarchar', 500, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Quantity', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'TemCode', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'QrData', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'ImportDate', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (10, N'PhieuStatus', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N''), (11, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_hangchogiaopickdetail'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_hangchogiaopickdetail', 1;
GO

IF OBJECT_ID(N'dbo.fvn_itemfifoconfig', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_itemfifoconfig] (
        [ItemCode] nvarchar(30) NOT NULL,
        [EnforceFifo] bit NOT NULL DEFAULT ((0)),
        [UpdatedAt] datetime NULL,
        [UpdatedBy] nvarchar(25) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_itemfifoconfig', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_itemfifoconfig', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_itemfifoconfig') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'ItemCode', N'nvarchar', 60, 0, 0, 0, 0, 0, N'', N'', N'', N''), (2, N'EnforceFifo', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (3, N'UpdatedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (4, N'UpdatedBy', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'ItemCode', N'nvarchar', 60, 0, 0, 0, 0, 0, N'', N'', N'', N''), (2, N'EnforceFifo', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (3, N'UpdatedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (4, N'UpdatedBy', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_itemfifoconfig'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_itemfifoconfig', 1;
GO

IF OBJECT_ID(N'dbo.fvn_itemfifoconfig_history', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_itemfifoconfig_history] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ItemCode] nvarchar(30) NOT NULL,
        [EnforceFifoCu] bit NULL,
        [EnforceFifoMoi] bit NOT NULL,
        [ThoiGian] datetime NOT NULL DEFAULT (getdate()),
        [NguoiThucHien] nvarchar(25) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_itemfifoconfig_history', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_itemfifoconfig_history', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_itemfifoconfig_history') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ItemCode', N'nvarchar', 60, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'EnforceFifoCu', N'bit', 1, 1, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'EnforceFifoMoi', N'bit', 1, 1, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'ThoiGian', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (6, N'NguoiThucHien', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ItemCode', N'nvarchar', 60, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'EnforceFifoCu', N'bit', 1, 1, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'EnforceFifoMoi', N'bit', 1, 1, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'ThoiGian', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (6, N'NguoiThucHien', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_itemfifoconfig_history'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_itemfifoconfig_history', 1;
GO

IF OBJECT_ID(N'dbo.fvn_pctpquarantinereleaseaudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_pctpquarantinereleaseaudit] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [SlotLotId] int NOT NULL,
        [SlotId] int NOT NULL,
        [LotNo] nvarchar(250) NOT NULL,
        [ItemCode] nvarchar(50) NOT NULL,
        [Quantity] int NOT NULL,
        [PerformedBy] nvarchar(100) NOT NULL,
        [ReferenceId] nvarchar(100) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [ReleasedAtUtc] datetime2(3) NOT NULL DEFAULT (sysutcdatetime())
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_pctpquarantinereleaseaudit', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_pctpquarantinereleaseaudit', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pctpquarantinereleaseaudit') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'bigint', 8, 19, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'SlotLotId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SlotId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'LotNo', N'nvarchar', 500, 0, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'ItemCode', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Quantity', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'PerformedBy', N'nvarchar', 200, 0, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'ReferenceId', N'nvarchar', 200, 0, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'Reason', N'nvarchar', 1000, 0, 0, 0, 0, 0, N'', N'', N'', N''), (10, N'ReleasedAtUtc', N'datetime2', 7, 23, 3, 0, 0, 0, N'', N'(sysutcdatetime())', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'bigint', 8, 19, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'SlotLotId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SlotId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'LotNo', N'nvarchar', 500, 0, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'ItemCode', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Quantity', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'PerformedBy', N'nvarchar', 200, 0, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'ReferenceId', N'nvarchar', 200, 0, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'Reason', N'nvarchar', 1000, 0, 0, 0, 0, 0, N'', N'', N'', N''), (10, N'ReleasedAtUtc', N'datetime2', 7, 23, 3, 0, 0, 0, N'', N'(sysutcdatetime())', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pctpquarantinereleaseaudit'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_pctpquarantinereleaseaudit', 1;
GO

IF OBJECT_ID(N'dbo.fvn_phieutrahang', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_phieutrahang] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Nguon] int NOT NULL,
        [SoPhieu] nvarchar(25) NULL,
        [NguonKhachTra] int NULL,
        [SoPhieuKhach] nvarchar(25) NULL,
        [NgayPhatHanh] datetime NULL,
        [SlipNo] nvarchar(25) NULL,
        [Ca] nvarchar(5) NULL,
        [PhongBan] nvarchar(50) NULL,
        [LyDo] nvarchar(250) NULL,
        [TenKhachHang] nvarchar(100) NULL,
        [BoPhanPhatHienLoi] nvarchar(50) NULL,
        [XacNhanBPPhatHienLoi] nvarchar(50) NULL,
        [XacNhanQCKhach] nvarchar(50) NULL,
        [XacNhanNhaCungCap] nvarchar(50) NULL,
        [NgayNhanKho] datetime NULL,
        [TongSoLuongNhan] int NOT NULL DEFAULT ((0)),
        [Status] int NOT NULL DEFAULT ((0)),
        [BoPhanNhanLai] nvarchar(50) NULL,
        [SoLuongGiaoLai] int NULL,
        [NgayGiaoLaiBoPhan] datetime NULL,
        [NguoiGiaoLaiBoPhan] nvarchar(25) NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetime NOT NULL DEFAULT (getdate()),
        [CreatedBy] nvarchar(25) NULL,
        [UpdatedAt] datetime NULL,
        [UpdatedBy] nvarchar(25) NULL,
        [CustomerNo] nvarchar(10) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_phieutrahang', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_phieutrahang', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_phieutrahang') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'Nguon', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoPhieu', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'NguonKhachTra', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SoPhieuKhach', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'NgayPhatHanh', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (7, N'SlipNo', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'Ca', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'PhongBan', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'LyDo', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'TenKhachHang', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'BoPhanPhatHienLoi', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'XacNhanBPPhatHienLoi', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'XacNhanQCKhach', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'XacNhanNhaCungCap', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'NgayNhanKho', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (17, N'TongSoLuongNhan', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (18, N'Status', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (19, N'BoPhanNhanLai', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (20, N'SoLuongGiaoLai', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (21, N'NgayGiaoLaiBoPhan', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (22, N'NguoiGiaoLaiBoPhan', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (23, N'Note', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N''), (24, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (25, N'CreatedBy', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (26, N'UpdatedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (27, N'UpdatedBy', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (28, N'CustomerNo', N'nvarchar', 20, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'Nguon', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoPhieu', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'NguonKhachTra', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SoPhieuKhach', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'NgayPhatHanh', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (7, N'SlipNo', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'Ca', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'PhongBan', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'LyDo', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'TenKhachHang', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'BoPhanPhatHienLoi', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'XacNhanBPPhatHienLoi', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'XacNhanQCKhach', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'XacNhanNhaCungCap', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'NgayNhanKho', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (17, N'TongSoLuongNhan', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (18, N'Status', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (19, N'BoPhanNhanLai', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (20, N'SoLuongGiaoLai', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (21, N'NgayGiaoLaiBoPhan', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (22, N'NguoiGiaoLaiBoPhan', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (23, N'Note', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N''), (24, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (25, N'CreatedBy', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (26, N'UpdatedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (27, N'UpdatedBy', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (28, N'CustomerNo', N'nvarchar', 20, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_phieutrahang'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_phieutrahang', 1;
GO

IF OBJECT_ID(N'dbo.fvn_phieutrahangct', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_phieutrahangct] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuTraHangId] int NOT NULL,
        [SlotIdNguon] int NULL,
        [MaHang] nvarchar(25) NULL,
        [TenHang] nvarchar(100) NULL,
        [LotNo] nvarchar(50) NULL,
        [SoLuong] int NOT NULL DEFAULT ((0)),
        [LyDoNg] nvarchar(250) NULL,
        [DinhDanhPhieuGiao] nvarchar(50) NULL,
        [PoNo] nvarchar(25) NULL,
        [NgayGiao] datetime NULL,
        [NhaMay] nvarchar(50) NULL,
        [PhieuXuLyBatThuongId] int NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_phieutrahangct', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_phieutrahangct', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_phieutrahangct') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuTraHangId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SlotIdNguon', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MaHang', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TenHang', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LotNo', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'SoLuong', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (8, N'LyDoNg', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'DinhDanhPhieuGiao', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'PoNo', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'NgayGiao', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (12, N'NhaMay', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuTraHangId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SlotIdNguon', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MaHang', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TenHang', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LotNo', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'SoLuong', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (8, N'LyDoNg', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'DinhDanhPhieuGiao', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'PoNo', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'NgayGiao', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (12, N'NhaMay', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_phieutrahangct'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_phieutrahangct', 1;
GO

IF OBJECT_ID(N'dbo.fvn_phieuxulybatthuong', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_phieuxulybatthuong] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [SoPhieu] nvarchar(25) NOT NULL,
        [Nguon] int NOT NULL,
        [PhieuTraHangId] int NULL,
        [PhieuTraHangCTId] int NOT NULL,
        [SoPhieuTraHangGoc] nvarchar(25) NULL,
        [PhieuKhachTraId] int NULL,
        [SlotIdNguon] int NULL,
        [LotNguon] nvarchar(25) NULL,
        [Model] nvarchar(50) NULL,
        [MaSanPham] nvarchar(25) NULL,
        [SoLo] nvarchar(25) NULL,
        [SoLoLoi] nvarchar(25) NULL,
        [SoLuongLoi] int NOT NULL DEFAULT ((0)),
        [NoiDungBatThuong] nvarchar(250) NULL,
        [PhanLoaiXuLy] nvarchar(50) NULL,
        [BoPhanPhatHanh] nvarchar(50) NULL,
        [Status] int NOT NULL DEFAULT ((0)),
        [HuongXuLy] int NOT NULL DEFAULT ((0)),
        [NgayDinhHuong] datetime NULL,
        [NguoiDinhHuong] nvarchar(25) NULL,
        [CreatedAt] datetime NOT NULL DEFAULT (getdate()),
        [CreatedBy] nvarchar(25) NULL,
        [UpdatedAt] datetime NULL,
        [UpdatedBy] nvarchar(25) NULL,
        [LyDoHuy] nvarchar(250) NULL,
        [NgayHuy] datetime NULL,
        [NguoiHuy] nvarchar(25) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_phieuxulybatthuong', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_phieuxulybatthuong', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_phieuxulybatthuong') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'SoPhieu', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'Nguon', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'PhieuTraHangId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'PhieuTraHangCTId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'SoPhieuTraHangGoc', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'PhieuKhachTraId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SlotIdNguon', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'LotNguon', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'Model', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'MaSanPham', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'SoLo', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'SoLoLoi', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'SoLuongLoi', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (15, N'NoiDungBatThuong', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'PhanLoaiXuLy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'BoPhanPhatHanh', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'Status', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (19, N'HuongXuLy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (20, N'NgayDinhHuong', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (21, N'NguoiDinhHuong', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (22, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (23, N'CreatedBy', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (24, N'UpdatedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (25, N'UpdatedBy', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (26, N'LyDoHuy', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (27, N'NgayHuy', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (28, N'NguoiHuy', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'SoPhieu', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'Nguon', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'PhieuTraHangId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'PhieuTraHangCTId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'SoPhieuTraHangGoc', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'PhieuKhachTraId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SlotIdNguon', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'LotNguon', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'Model', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'MaSanPham', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'SoLo', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'SoLoLoi', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'SoLuongLoi', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (15, N'NoiDungBatThuong', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'PhanLoaiXuLy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'BoPhanPhatHanh', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'Status', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (19, N'HuongXuLy', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (20, N'NgayDinhHuong', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (21, N'NguoiDinhHuong', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (22, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (23, N'CreatedBy', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (24, N'UpdatedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (25, N'UpdatedBy', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (26, N'LyDoHuy', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (27, N'NgayHuy', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (28, N'NguoiHuy', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_phieuxulybatthuong'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_phieuxulybatthuong', 1;
GO

IF OBJECT_ID(N'dbo.fvn_phieuxulybatthuongaffectedlot', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_phieuxulybatthuongaffectedlot] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuXuLyBatThuongId] int NOT NULL,
        [SourceType] int NOT NULL,
        [SourceReference] nvarchar(125) NULL,
        [SlotId] int NULL,
        [LotNo] nvarchar(50) NOT NULL,
        [MaSanPham] nvarchar(50) NOT NULL,
        [Model] nvarchar(50) NULL,
        [SoLuongAnhHuong] int NOT NULL DEFAULT ((0)),
        [SoLuongDaKiemTra] int NOT NULL DEFAULT ((0)),
        [SoLuongOK] int NOT NULL DEFAULT ((0)),
        [SoLuongNG] int NOT NULL DEFAULT ((0)),
        [SoLuongRework] int NOT NULL DEFAULT ((0)),
        [SoLuongLoaiBo] int NOT NULL DEFAULT ((0)),
        [SnapshotAt] datetime NOT NULL,
        [SnapshotBy] nvarchar(50) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_phieuxulybatthuongaffectedlot', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_phieuxulybatthuongaffectedlot', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_phieuxulybatthuongaffectedlot') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SourceType', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SourceReference', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SlotId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LotNo', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'MaSanPham', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'Model', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'SoLuongAnhHuong', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (10, N'SoLuongDaKiemTra', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (11, N'SoLuongOK', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (12, N'SoLuongNG', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (13, N'SoLuongRework', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (14, N'SoLuongLoaiBo', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (15, N'SnapshotAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'', N'', N''), (16, N'SnapshotBy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SourceType', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SourceReference', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SlotId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LotNo', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'MaSanPham', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'Model', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'SoLuongAnhHuong', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (10, N'SoLuongDaKiemTra', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (11, N'SoLuongOK', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (12, N'SoLuongNG', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (13, N'SoLuongRework', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (14, N'SoLuongLoaiBo', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (15, N'SnapshotAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'', N'', N''), (16, N'SnapshotBy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_phieuxulybatthuongaffectedlot'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_phieuxulybatthuongaffectedlot', 1;
GO

IF OBJECT_ID(N'dbo.fvn_phieuxulybatthuongqcinitial', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_phieuxulybatthuongqcinitial] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuXuLyBatThuongId] int NOT NULL,
        [SoLuongAnhHuong] int NOT NULL,
        [SoLuongDaKiemTra] int NOT NULL,
        [SoLuongOK] int NOT NULL,
        [SoLuongNG] int NOT NULL,
        [SoLuongRework] int NOT NULL,
        [SoLuongLoaiBoBanDau] int NOT NULL,
        [NoiDungKiemTra] nvarchar(500) NULL,
        [KetLuan] nvarchar(500) NULL,
        [ConfirmedAt] datetime NULL,
        [ConfirmedBy] nvarchar(50) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_phieuxulybatthuongqcinitial', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_phieuxulybatthuongqcinitial', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_phieuxulybatthuongqcinitial') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoLuongAnhHuong', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SoLuongDaKiemTra', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'SoLuongOK', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'SoLuongNG', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'SoLuongRework', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'SoLuongLoaiBoBanDau', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'NoiDungKiemTra', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'KetLuan', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'ConfirmedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (12, N'ConfirmedBy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoLuongAnhHuong', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SoLuongDaKiemTra', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'SoLuongOK', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'SoLuongNG', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'SoLuongRework', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'SoLuongLoaiBoBanDau', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'NoiDungKiemTra', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'KetLuan', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'ConfirmedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (12, N'ConfirmedBy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_phieuxulybatthuongqcinitial'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_phieuxulybatthuongqcinitial', 1;
GO

IF OBJECT_ID(N'dbo.fvn_purchase_order_hvndb', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_purchase_order_hvndb] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Oder_no] nvarchar(10) NULL,
        [Part_no] nvarchar(50) NULL,
        [Part_name] nvarchar(60) NULL,
        [NgayGiao] smalldatetime NULL,
        [Slgiao] int NULL,
        [QCDG] int NULL,
        [Gear] nvarchar(80) NULL,
        [CUA] nchar(10) NULL,
        [IsDelivered] bit NULL,
        [DeliveredDate] datetime NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_purchase_order_hvndb', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_purchase_order_hvndb', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_purchase_order_hvndb') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'Oder_no', N'nvarchar', 20, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'Part_no', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'Part_name', N'nvarchar', 120, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'NgayGiao', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'Slgiao', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'QCDG', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'Gear', N'nvarchar', 160, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'CUA', N'nchar', 20, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'IsDelivered', N'bit', 1, 1, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'DeliveredDate', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'Oder_no', N'nvarchar', 20, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'Part_no', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'Part_name', N'nvarchar', 120, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'NgayGiao', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'Slgiao', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'QCDG', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'Gear', N'nvarchar', 160, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'CUA', N'nchar', 20, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'IsDelivered', N'bit', 1, 1, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'DeliveredDate', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_purchase_order_hvndb'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_purchase_order_hvndb', 1;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_compensation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_pxlb_compensation] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuXuLyBatThuongId] int NOT NULL,
        [SoLuongYeuCau] int NOT NULL,
        [SoLuongDaGiao] int NOT NULL DEFAULT ((0)),
        [Status] int NOT NULL DEFAULT ((0)),
        [SourceReference] nvarchar(100) NULL,
        [CreatedAt] datetime NOT NULL DEFAULT (getdate()),
        [CreatedBy] nvarchar(50) NOT NULL,
        [CompletedAt] datetime NULL,
        [CompletedBy] nvarchar(50) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_compensation', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_pxlb_compensation', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_compensation') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoLuongYeuCau', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SoLuongDaGiao', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (5, N'Status', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (6, N'SourceReference', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (8, N'CreatedBy', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'CompletedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (10, N'CompletedBy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoLuongYeuCau', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SoLuongDaGiao', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (5, N'Status', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (6, N'SourceReference', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (8, N'CreatedBy', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'CompletedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (10, N'CompletedBy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_compensation'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_pxlb_compensation', 1;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_compensationmovement', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_pxlb_compensationmovement] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [CompensationId] int NOT NULL,
        [LotNo] nvarchar(50) NOT NULL,
        [SlotId] int NULL,
        [SoLuong] int NOT NULL,
        [ThoiGian] datetime NOT NULL DEFAULT (getdate()),
        [NguoiThucHien] nvarchar(50) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_compensationmovement', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_pxlb_compensationmovement', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_compensationmovement') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'CompensationId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'LotNo', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SlotId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SoLuong', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'ThoiGian', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (7, N'NguoiThucHien', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'CompensationId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'LotNo', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SlotId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SoLuong', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'ThoiGian', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (7, N'NguoiThucHien', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_compensationmovement'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_pxlb_compensationmovement', 1;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_disposition', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_pxlb_disposition] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuXuLyBatThuongId] int NOT NULL,
        [SoLuongLoaiBoBanDau] int NOT NULL,
        [SoLuongNGRework] int NOT NULL,
        [SoLuongLoaiBoCuoi] int NOT NULL,
        [Reason] nvarchar(500) NULL,
        [ConfirmedAt] datetime NOT NULL DEFAULT (getdate()),
        [ConfirmedBy] nvarchar(50) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_disposition', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_pxlb_disposition', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_disposition') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoLuongLoaiBoBanDau', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SoLuongNGRework', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'SoLuongLoaiBoCuoi', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Reason', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'ConfirmedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (8, N'ConfirmedBy', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoLuongLoaiBoBanDau', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SoLuongNGRework', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'SoLuongLoaiBoCuoi', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Reason', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'ConfirmedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (8, N'ConfirmedBy', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_disposition'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_pxlb_disposition', 1;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_reworkmovement', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_pxlb_reworkmovement] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuXuLyBatThuongId] int NOT NULL,
        [ReworkPlanId] int NOT NULL,
        [MovementType] int NOT NULL,
        [SlotId] int NULL,
        [LotNo] nvarchar(50) NOT NULL,
        [MaSanPham] nvarchar(50) NULL,
        [SoLuong] int NOT NULL,
        [ThoiGian] datetime NOT NULL DEFAULT (getdate()),
        [NguoiThucHien] nvarchar(50) NOT NULL,
        [GhiChu] nvarchar(500) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_reworkmovement', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_pxlb_reworkmovement', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_reworkmovement') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'ReworkPlanId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'MovementType', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'SlotId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LotNo', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'MaSanPham', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SoLuong', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'ThoiGian', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (10, N'NguoiThucHien', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (11, N'GhiChu', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'ReworkPlanId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'MovementType', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'SlotId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LotNo', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'MaSanPham', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SoLuong', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'ThoiGian', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (10, N'NguoiThucHien', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (11, N'GhiChu', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_reworkmovement'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_pxlb_reworkmovement', 1;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_reworkplan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_pxlb_reworkplan] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuXuLyBatThuongId] int NOT NULL,
        [SoLuongKeHoach] int NOT NULL,
        [SoLuongDaXuat] int NOT NULL DEFAULT ((0)),
        [SoLuongDaGiao] int NOT NULL DEFAULT ((0)),
        [Status] int NOT NULL DEFAULT ((0)),
        [CreatedAt] datetime NOT NULL DEFAULT (getdate()),
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedAt] datetime NULL,
        [UpdatedBy] nvarchar(50) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_reworkplan', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_pxlb_reworkplan', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_reworkplan') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoLuongKeHoach', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SoLuongDaXuat', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (5, N'SoLuongDaGiao', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (6, N'Status', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (7, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (8, N'CreatedBy', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'UpdatedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (10, N'UpdatedBy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoLuongKeHoach', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SoLuongDaXuat', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (5, N'SoLuongDaGiao', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (6, N'Status', N'int', 4, 10, 0, 0, 0, 0, N'', N'((0))', N'', N''), (7, N'CreatedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (8, N'CreatedBy', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'UpdatedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (10, N'UpdatedBy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_reworkplan'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_pxlb_reworkplan', 1;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_reworkqc', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_pxlb_reworkqc] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuXuLyBatThuongId] int NOT NULL,
        [SoLuongRework] int NOT NULL,
        [SoLuongOK] int NOT NULL,
        [SoLuongNG] int NOT NULL,
        [ConfirmedAt] datetime NOT NULL DEFAULT (getdate()),
        [ConfirmedBy] nvarchar(50) NOT NULL,
        [KetLuan] nvarchar(500) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_reworkqc', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_pxlb_reworkqc', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_reworkqc') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoLuongRework', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SoLuongOK', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'SoLuongNG', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'ConfirmedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (7, N'ConfirmedBy', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'KetLuan', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoLuongRework', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SoLuongOK', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'SoLuongNG', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'ConfirmedAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (7, N'ConfirmedBy', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'KetLuan', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_reworkqc'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_pxlb_reworkqc', 1;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_workflowaudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_pxlb_workflowaudit] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuXuLyBatThuongId] int NOT NULL,
        [FromStatus] int NOT NULL,
        [ToStatus] int NOT NULL,
        [ActionName] nvarchar(50) NOT NULL,
        [Actor] nvarchar(50) NOT NULL,
        [OccurredAt] datetime NOT NULL DEFAULT (getdate()),
        [Note] nvarchar(500) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_pxlb_workflowaudit', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_pxlb_workflowaudit', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_workflowaudit') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'FromStatus', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'ToStatus', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'ActionName', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Actor', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'OccurredAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (8, N'Note', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'FromStatus', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'ToStatus', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'ActionName', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Actor', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'OccurredAt', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (8, N'Note', N'nvarchar', 1000, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_pxlb_workflowaudit'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_pxlb_workflowaudit', 1;
GO

IF OBJECT_ID(N'dbo.fvn_sys_workflowtransitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_sys_workflowtransitions] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ProcessCode] nvarchar(25) NOT NULL,
        [FromStatus] int NOT NULL,
        [ToStatus] int NOT NULL,
        [ActionName] nvarchar(50) NOT NULL,
        [Description] nvarchar(250) NULL,
        [IsActive] bit NOT NULL DEFAULT ((1))
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_sys_workflowtransitions', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_sys_workflowtransitions', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_sys_workflowtransitions') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ProcessCode', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'FromStatus', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'ToStatus', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'ActionName', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Description', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'IsActive', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((1))', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ProcessCode', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'FromStatus', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'ToStatus', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'ActionName', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'Description', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'IsActive', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((1))', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_sys_workflowtransitions'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_sys_workflowtransitions', 1;
GO

IF OBJECT_ID(N'dbo.fvn_trahangqtchunggiao', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_trahangqtchunggiao] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuXuLyBatThuongId] int NOT NULL,
        [LotGiao] nvarchar(50) NULL,
        [MaHang] nvarchar(50) NULL,
        [SoLuongGiao] int NOT NULL,
        [ThoiGian] datetime NOT NULL DEFAULT (getdate()),
        [NgayGiao] nvarchar(50) NULL,
        [NguoiNhan] nvarchar(50) NULL,
        [BoPhanNhan] nvarchar(50) NULL,
        [SoPhieuGiaoNhan] nvarchar(50) NULL,
        [Note] nvarchar(MAX) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_trahangqtchunggiao', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_trahangqtchunggiao', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_trahangqtchunggiao') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'LotGiao', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MaHang', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SoLuongGiao', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'ThoiGian', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (7, N'NgayGiao', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'NguoiNhan', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'BoPhanNhan', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'SoPhieuGiaoNhan', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'Note', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'LotGiao', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MaHang', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SoLuongGiao', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'ThoiGian', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (7, N'NgayGiao', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'NguoiNhan', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'BoPhanNhan', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'SoPhieuGiaoNhan', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'Note', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_trahangqtchunggiao'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_trahangqtchunggiao', 1;
GO

IF OBJECT_ID(N'dbo.fvn_trahangqtchungnhapng', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_trahangqtchungnhapng] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuXuLyBatThuongId] int NOT NULL,
        [SlotIdOK] int NULL,
        [SlotIdNG] int NULL,
        [LotNhapLai] nvarchar(50) NOT NULL,
        [MaHang] nvarchar(50) NOT NULL,
        [SoLuongNG] int NOT NULL,
        [SlotIdNhap] int NULL,
        [NgayNhap] datetime NOT NULL DEFAULT (getdate()),
        [NguoiNhap] nvarchar(50) NULL,
        [LyDo] nvarchar(250) NULL,
        [Note] nvarchar(MAX) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_trahangqtchungnhapng', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_trahangqtchungnhapng', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_trahangqtchungnhapng') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SlotIdOK', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'SlotIdNG', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'LotNhapLai', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'MaHang', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'SoLuongNG', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'SlotIdNhap', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NgayNhap', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (10, N'NguoiNhap', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'LyDo', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'Note', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SlotIdOK', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'SlotIdNG', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'LotNhapLai', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'MaHang', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'SoLuongNG', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'SlotIdNhap', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NgayNhap', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (10, N'NguoiNhap', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'LyDo', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'Note', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_trahangqtchungnhapng'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_trahangqtchungnhapng', 1;
GO

IF OBJECT_ID(N'dbo.fvn_trahangqtchungqc', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_trahangqtchungqc] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuXuLyBatThuongId] int NOT NULL,
        [SoLuongDaRework] int NOT NULL,
        [SoLuongOK] int NOT NULL,
        [SoLuongNG] int NOT NULL,
        [DaKiemTraTem] bit NOT NULL DEFAULT ((0)),
        [ThoiGian] datetime NOT NULL DEFAULT (getdate()),
        [NguoiQC] nvarchar(50) NULL,
        [KetLuan] nvarchar(50) NULL,
        [Note] nvarchar(MAX) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_trahangqtchungqc', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_trahangqtchungqc', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_trahangqtchungqc') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoLuongDaRework', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SoLuongOK', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'SoLuongNG', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'DaKiemTraTem', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (7, N'ThoiGian', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (8, N'NguoiQC', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'KetLuan', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'Note', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SoLuongDaRework', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'SoLuongOK', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'SoLuongNG', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'DaKiemTraTem', N'bit', 1, 1, 0, 0, 0, 0, N'', N'((0))', N'', N''), (7, N'ThoiGian', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (8, N'NguoiQC', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'KetLuan', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'Note', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_trahangqtchungqc'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_trahangqtchungqc', 1;
GO

IF OBJECT_ID(N'dbo.fvn_trahangqtchungxuat', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[fvn_trahangqtchungxuat] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuXuLyBatThuongId] int NOT NULL,
        [SlotIdNguon] int NOT NULL,
        [LotXuat] nvarchar(50) NOT NULL,
        [LoaiXuat] nvarchar(15) NULL,
        [MaHang] nvarchar(50) NOT NULL,
        [SoLuongXuat] int NOT NULL,
        [TonTruoc] int NOT NULL,
        [TonSau] int NOT NULL,
        [NgayXuat] datetime NOT NULL DEFAULT (getdate()),
        [NguoiXuat] nvarchar(50) NULL,
        [LyDo] nvarchar(250) NULL,
        [Note] nvarchar(MAX) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.fvn_trahangqtchungxuat', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.fvn_trahangqtchungxuat', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_trahangqtchungxuat') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SlotIdNguon', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'LotXuat', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'LoaiXuat', N'nvarchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'MaHang', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'SoLuongXuat', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'TonTruoc', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'TonSau', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (10, N'NgayXuat', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (11, N'NguoiXuat', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'LyDo', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'Note', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'PhieuXuLyBatThuongId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'SlotIdNguon', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'LotXuat', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'LoaiXuat', N'nvarchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'MaHang', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'SoLuongXuat', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (8, N'TonTruoc', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (9, N'TonSau', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (10, N'NgayXuat', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (11, N'NguoiXuat', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'LyDo', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'Note', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.fvn_trahangqtchungxuat'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.fvn_trahangqtchungxuat', 1;
GO

IF OBJECT_ID(N'dbo.inspectionconfig', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[inspectionconfig] (
        [ConfigId] int IDENTITY(1,1) NOT NULL,
        [ItemCode] nvarchar(50) NOT NULL,
        [DefaultQty] int NOT NULL DEFAULT ((1)),
        [CheckItemCode] bit NULL DEFAULT ((1)),
        [CheckLotNo] bit NULL DEFAULT ((1)),
        [CheckNSX] bit NULL DEFAULT ((1)),
        [IsActive] bit NULL DEFAULT ((1)),
        [Note] nvarchar(250) NULL,
        [CreatedAt] datetime NULL DEFAULT (getdate())
    );
END;
GO

IF OBJECT_ID(N'dbo.inspectionconfig', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.inspectionconfig', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.inspectionconfig') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'ConfigId', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ItemCode', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'DefaultQty', N'int', 4, 10, 0, 0, 0, 0, N'', N'((1))', N'', N''), (4, N'CheckItemCode', N'bit', 1, 1, 0, 1, 0, 0, N'', N'((1))', N'', N''), (5, N'CheckLotNo', N'bit', 1, 1, 0, 1, 0, 0, N'', N'((1))', N'', N''), (6, N'CheckNSX', N'bit', 1, 1, 0, 1, 0, 0, N'', N'((1))', N'', N''), (7, N'IsActive', N'bit', 1, 1, 0, 1, 0, 0, N'', N'((1))', N'', N''), (8, N'Note', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'CreatedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'(getdate())', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'ConfigId', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ItemCode', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'DefaultQty', N'int', 4, 10, 0, 0, 0, 0, N'', N'((1))', N'', N''), (4, N'CheckItemCode', N'bit', 1, 1, 0, 1, 0, 0, N'', N'((1))', N'', N''), (5, N'CheckLotNo', N'bit', 1, 1, 0, 1, 0, 0, N'', N'((1))', N'', N''), (6, N'CheckNSX', N'bit', 1, 1, 0, 1, 0, 0, N'', N'((1))', N'', N''), (7, N'IsActive', N'bit', 1, 1, 0, 1, 0, 0, N'', N'((1))', N'', N''), (8, N'Note', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'CreatedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'(getdate())', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.inspectionconfig'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.inspectionconfig', 1;
GO

IF OBJECT_ID(N'dbo.inspectionlog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[inspectionlog] (
        [LogId] int IDENTITY(1,1) NOT NULL,
        [InspectionCode] nvarchar(25) NOT NULL,
        [ItemCode] nvarchar(50) NULL,
        [TemCodeTong] nvarchar(100) NULL,
        [LotNoTong] nvarchar(100) NULL,
        [NSXTong] nvarchar(25) NULL,
        [SoLuongTong] int NULL,
        [MaPhieu] nvarchar(50) NULL,
        [BoxTemCode] nvarchar(100) NULL,
        [BoxLotNo] nvarchar(100) NULL,
        [BoxNSX] nvarchar(25) NULL,
        [IsMatch] bit NULL DEFAULT ((1)),
        [MismatchFields] nvarchar(250) NULL,
        [FinalResult] nvarchar(10) NULL,
        [CheckedAt] datetime NULL DEFAULT (getdate()),
        [CheckedBy] nvarchar(50) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.inspectionlog', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.inspectionlog', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.inspectionlog') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LogId', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'InspectionCode', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'ItemCode', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'TemCodeTong', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'LotNoTong', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'NSXTong', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'SoLuongTong', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'MaPhieu', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'BoxTemCode', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'BoxLotNo', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'BoxNSX', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'IsMatch', N'bit', 1, 1, 0, 1, 0, 0, N'', N'((1))', N'', N''), (13, N'MismatchFields', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'FinalResult', N'nvarchar', 20, 0, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'CheckedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'(getdate())', N'', N''), (16, N'CheckedBy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LogId', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'InspectionCode', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'ItemCode', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'TemCodeTong', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'LotNoTong', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'NSXTong', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'SoLuongTong', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'MaPhieu', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'BoxTemCode', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'BoxLotNo', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'BoxNSX', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'IsMatch', N'bit', 1, 1, 0, 1, 0, 0, N'', N'((1))', N'', N''), (13, N'MismatchFields', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'FinalResult', N'nvarchar', 20, 0, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'CheckedAt', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'(getdate())', N'', N''), (16, N'CheckedBy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.inspectionlog'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.inspectionlog', 1;
GO

IF OBJECT_ID(N'dbo.luudocqrcode', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[luudocqrcode] (
        [LOTFCC] nvarchar(250) NULL,
        [MAHANGFCC] nvarchar(50) NULL,
        [SLTEMFCC] smallint NULL,
        [LOTHVN] nvarchar(250) NULL,
        [MAHANGHVN] nvarchar(50) NULL,
        [SLTEMHVN] smallint NULL,
        [STATUS] smallint NULL,
        [MAFCC] nvarchar(25) NULL,
        [STT] smallint NULL,
        [KETQUA] nvarchar(25) NULL,
        [NGAYXUAT] smalldatetime NULL,
        [GIOXUAT] nvarchar(100) NULL,
        [NHAMAY] nvarchar(100) NULL,
        [GIOGIAO] nvarchar(25) NULL,
        [TGLUU] timestamp NULL,
        [TGLuuDuLieu] datetime NULL DEFAULT (getutcdate())
    );
END;
GO

IF OBJECT_ID(N'dbo.luudocqrcode', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.luudocqrcode', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.luudocqrcode') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOTFCC', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANGFCC', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'SLTEMFCC', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'LOTHVN', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'MAHANGHVN', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLTEMHVN', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'STATUS', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'MAFCC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'KETQUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'NGAYXUAT', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'GIOXUAT', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'TGLUU', N'timestamp', 8, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'TGLuuDuLieu', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'(getutcdate())', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOTFCC', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANGFCC', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'SLTEMFCC', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'LOTHVN', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'MAHANGHVN', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLTEMHVN', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'STATUS', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'MAFCC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'KETQUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'NGAYXUAT', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'GIOXUAT', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'TGLUU', N'timestamp', 8, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'TGLuuDuLieu', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'(getutcdate())', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.luudocqrcode'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.luudocqrcode', 1;
GO

IF OBJECT_ID(N'dbo.luuphieugiaohang', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[luuphieugiaohang] (
        [STT] smallint NULL,
        [CUA] nvarchar(25) NULL,
        [TRUYEN] nvarchar(25) NULL,
        [MAHANG] nvarchar(25) NULL,
        [TENHANG] nvarchar(30) NULL,
        [LOT] nvarchar(250) NULL,
        [DV] nvarchar(25) NULL,
        [SOLUONG] int NULL,
        [NGAYGIAO] smalldatetime NULL,
        [GIOGIAO] nvarchar(25) NULL,
        [STATUS] nvarchar(25) NULL,
        [TTPHIEU] nvarchar(25) NULL,
        [NHAMAY] nvarchar(30) NULL,
        [GIOGIAOFCC] nvarchar(25) NULL,
        [GearYMVN] nvarchar(25) NULL,
        [HOP] int NULL,
        [STATUSDOC] nchar(25) NULL,
        [Note] nvarchar(125) NULL,
        [TGLUU] timestamp NULL,
        [PO_NO] nvarchar(100) NULL,
        [PO_ITEM] nvarchar(100) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.luuphieugiaohang', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.luuphieugiaohang', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.luuphieugiaohang') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TENHANG', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOT', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SOLUONG', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUS', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TTPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'GIOGIAOFCC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'GearYMVN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'HOP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'STATUSDOC', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'TGLUU', N'timestamp', 8, 0, 0, 1, 0, 0, N'', N'', N'', N''), (20, N'PO_NO', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (21, N'PO_ITEM', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TENHANG', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOT', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SOLUONG', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUS', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TTPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'GIOGIAOFCC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'GearYMVN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'HOP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'STATUSDOC', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'TGLUU', N'timestamp', 8, 0, 0, 1, 0, 0, N'', N'', N'', N''), (20, N'PO_NO', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (21, N'PO_ITEM', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.luuphieugiaohang'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.luuphieugiaohang', 1;
GO

IF OBJECT_ID(N'dbo.purchase_order_htn', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[purchase_order_htn] (
        [Oder_no] nvarchar(5) NULL,
        [Part_no] nvarchar(25) NULL,
        [Part_name] nvarchar(30) NULL,
        [NgayGiao] smalldatetime NULL,
        [Slgiao] int NULL,
        [QCDG] int NULL,
        [Gear] nvarchar(40) NULL,
        [CUA] nchar(5) NULL,
        [IsDelivered] bit NULL DEFAULT ((0)),
        [DeliveredDate] datetime NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.purchase_order_htn', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.purchase_order_htn', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.purchase_order_htn') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Oder_no', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'Part_no', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'Part_name', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'NgayGiao', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'Slgiao', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'QCDG', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'Gear', N'nvarchar', 80, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'CUA', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'IsDelivered', N'bit', 1, 1, 0, 1, 0, 0, N'', N'((0))', N'', N''), (10, N'DeliveredDate', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Oder_no', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'Part_no', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'Part_name', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'NgayGiao', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'Slgiao', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'QCDG', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'Gear', N'nvarchar', 80, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'CUA', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'IsDelivered', N'bit', 1, 1, 0, 1, 0, 0, N'', N'((0))', N'', N''), (10, N'DeliveredDate', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.purchase_order_htn'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.purchase_order_htn', 1;
GO

IF OBJECT_ID(N'dbo.purchase_order_ymvn', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[purchase_order_ymvn] (
        [Oder_no] nvarchar(5) NULL,
        [Part_no] nvarchar(25) NULL,
        [Part_name] nvarchar(30) NULL,
        [NgayGiao] smalldatetime NULL,
        [Slgiao] int NULL,
        [QCDG] int NULL,
        [Gear] nvarchar(40) NULL,
        [CUA] nchar(5) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.purchase_order_ymvn', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.purchase_order_ymvn', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.purchase_order_ymvn') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Oder_no', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'Part_no', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'Part_name', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'NgayGiao', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'Slgiao', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'QCDG', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'Gear', N'nvarchar', 80, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'CUA', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Oder_no', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'Part_no', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'Part_name', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'NgayGiao', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'Slgiao', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'QCDG', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'Gear', N'nvarchar', 80, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'CUA', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.purchase_order_ymvn'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.purchase_order_ymvn', 1;
GO

IF OBJECT_ID(N'dbo.qrcode_changetime', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[qrcode_changetime] (
        [GioFCCVP] nvarchar(25) NULL,
        [GIOHVN] nvarchar(2) NULL,
        [GIOFCCHN] nvarchar(25) NULL,
        [ID] int NULL,
        [GIOCBVP] nvarchar(25) NULL,
        [GIOCBHN] nvarchar(25) NULL,
        [DisplayName] nvarchar(25) NOT NULL DEFAULT ('Name')
    );
END;
GO

IF OBJECT_ID(N'dbo.qrcode_changetime', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.qrcode_changetime', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.qrcode_changetime') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'GioFCCVP', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'GIOHVN', N'nvarchar', 5, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'GIOFCCHN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'ID', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'GIOCBVP', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'GIOCBHN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DisplayName', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'(''Name'')', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'GioFCCVP', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'GIOHVN', N'nvarchar', 5, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'GIOFCCHN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'ID', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'GIOCBVP', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'GIOCBHN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DisplayName', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'(''Name'')', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.qrcode_changetime'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.qrcode_changetime', 1;
GO

IF OBJECT_ID(N'dbo.rack', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[rack] (
        [RackId] int IDENTITY(1,1) NOT NULL,
        [WarehouseId] int NULL,
        [RackName] nvarchar(50) NOT NULL,
        [RackRowCount] int NULL,
        [ColumnCount] int NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.rack', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.rack', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.rack') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'RackId', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'WarehouseId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'RackName', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'RackRowCount', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'ColumnCount', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'RackId', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'WarehouseId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'RackName', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'RackRowCount', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'ColumnCount', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.rack'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.rack', 1;
GO

IF OBJECT_ID(N'dbo.slot', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[slot] (
        [SlotId] int IDENTITY(1,1) NOT NULL,
        [RackId] int NULL,
        [SlotNumber] int NOT NULL,
        [IsOccupied] bit NOT NULL,
        [ItemCode] nvarchar(MAX) NULL,
        [LotNo] nvarchar(MAX) NULL,
        [Quantity] int NULL,
        [ImportDate] datetime NULL,
        [TemCode] nvarchar(MAX) NULL,
        [Capacity] int NULL,
        [RowIndex] int NULL,
        [ColumnIndex] int NULL,
        [QrData] nvarchar(MAX) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.slot', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.slot', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.slot') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'SlotId', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'RackId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'SlotNumber', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'IsOccupied', N'bit', 1, 1, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'ItemCode', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LotNo', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'Quantity', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'ImportDate', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (9, N'TemCode', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'Capacity', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'RowIndex', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'ColumnIndex', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'QrData', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'SlotId', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'RackId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'SlotNumber', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'IsOccupied', N'bit', 1, 1, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'ItemCode', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LotNo', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'Quantity', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'ImportDate', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (9, N'TemCode', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'Capacity', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'RowIndex', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'ColumnIndex', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'QrData', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.slot'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.slot', 1;
GO

IF OBJECT_ID(N'dbo.slotlot', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[slotlot] (
        [SlotLotId] int IDENTITY(1,1) NOT NULL,
        [SlotId] int NOT NULL,
        [ItemCode] nvarchar(50) NOT NULL,
        [LotNo] nvarchar(100) NOT NULL,
        [Quantity] int NOT NULL,
        [TemCode] nvarchar(100) NULL,
        [QrData] nvarchar(MAX) NULL,
        [ImportDate] datetime NULL,
        [CreatedDate] datetime NOT NULL DEFAULT (getdate()),
        [NgaySX] nvarchar(10) NULL,
        [SoPhieuTong] nvarchar(25) NULL,
        [MaPhieu] nvarchar(50) NULL,
        [ParentSoPhieu] nvarchar(25) NULL,
        [PhieuStatus] tinyint NOT NULL DEFAULT ((0))
    );
END;
GO

IF OBJECT_ID(N'dbo.slotlot', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.slotlot', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.slotlot') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'SlotLotId', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'SlotId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'ItemCode', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'LotNo', N'nvarchar', 200, 0, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'Quantity', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'TemCode', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'QrData', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'ImportDate', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (9, N'CreatedDate', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (10, N'NgaySX', N'nvarchar', 20, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'SoPhieuTong', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'MaPhieu', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'ParentSoPhieu', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'PhieuStatus', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'SlotLotId', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'SlotId', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'ItemCode', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'LotNo', N'nvarchar', 200, 0, 0, 0, 0, 0, N'', N'', N'', N''), (5, N'Quantity', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (6, N'TemCode', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'QrData', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'ImportDate', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (9, N'CreatedDate', N'datetime', 8, 23, 3, 0, 0, 0, N'', N'(getdate())', N'', N''), (10, N'NgaySX', N'nvarchar', 20, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'SoPhieuTong', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'MaPhieu', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'ParentSoPhieu', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'PhieuStatus', N'tinyint', 1, 3, 0, 0, 0, 0, N'', N'((0))', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.slotlot'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.slotlot', 1;
GO

IF OBJECT_ID(N'dbo.sp_docqrcode', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[sp_docqrcode] (
        [STT] int NULL,
        [LOTNO] nvarchar(25) NULL,
        [MAHANG] nvarchar(25) NULL,
        [Cty] nvarchar(25) NULL,
        [SLTEM] int NULL,
        [PONO] nvarchar(25) NULL,
        [Gear] nchar(0) NULL,
        [KETQUA] nvarchar(2) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.sp_docqrcode', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.sp_docqrcode', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.sp_docqrcode') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'LOTNO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'Cty', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SLTEM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'PONO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'Gear', N'nchar', 1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'KETQUA', N'nvarchar', 5, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'LOTNO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'Cty', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SLTEM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'PONO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'Gear', N'nchar', 1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'KETQUA', N'nvarchar', 5, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.sp_docqrcode'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.sp_docqrcode', 1;
GO

IF OBJECT_ID(N'dbo.sp_tmpphieugiaohang', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[sp_tmpphieugiaohang] (
        [STT] int NULL,
        [MAHANG] nvarchar(25) NULL,
        [TENHANG] nvarchar(25) NULL,
        [CUA] nvarchar(5) NULL,
        [SLGIAO] int NULL,
        [LOTNO] nvarchar(25) NULL,
        [PO_NO] nvarchar(25) NULL,
        [NGAYGIAO] datetime NULL,
        [GIOGIAO] nvarchar(5) NULL,
        [NHAMAY] nvarchar(25) NULL,
        [STATUSFCC] nchar(5) NULL,
        [STATUSYMVN] nchar(5) NULL,
        [STATUS] nchar(5) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.sp_tmpphieugiaohang', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.sp_tmpphieugiaohang', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.sp_tmpphieugiaohang') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'CUA', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SLGIAO', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOTNO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'PO_NO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'NGAYGIAO', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (9, N'GIOGIAO', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'NHAMAY', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUSFCC', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'STATUSYMVN', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'STATUS', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'CUA', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'SLGIAO', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOTNO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'PO_NO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'NGAYGIAO', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (9, N'GIOGIAO', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'NHAMAY', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUSFCC', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'STATUSYMVN', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'STATUS', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.sp_tmpphieugiaohang'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.sp_tmpphieugiaohang', 1;
GO

IF OBJECT_ID(N'dbo.stockhistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[stockhistory] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ActionType] varchar(30) NOT NULL,
        [ItemCode] nvarchar(50) NULL,
        [TemCode] nvarchar(50) NULL,
        [LotNo] nvarchar(100) NULL,
        [Quantity] int NULL,
        [Date] datetime NULL,
        [SlotId] int NULL,
        [PerformedBy] nvarchar(50) NULL,
        [QrData] nvarchar(MAX) NULL,
        [MaPhieu] nvarchar(25) NULL,
        [FromSlotId] int NULL,
        [ToSlotId] int NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.stockhistory', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.stockhistory', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.stockhistory') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ActionType', N'varchar', 30, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'ItemCode', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'TemCode', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'LotNo', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'Quantity', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'Date', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (8, N'SlotId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'PerformedBy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'QrData', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'MaPhieu', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'FromSlotId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'ToSlotId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'Id', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'ActionType', N'varchar', 30, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'ItemCode', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'TemCode', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'LotNo', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'Quantity', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'Date', N'datetime', 8, 23, 3, 1, 0, 0, N'', N'', N'', N''), (8, N'SlotId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'PerformedBy', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'QrData', N'nvarchar', -1, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'MaPhieu', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'FromSlotId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'ToSlotId', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.stockhistory'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.stockhistory', 1;
GO

IF OBJECT_ID(N'dbo.stocktp', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[stocktp] (
        [LOT] nvarchar(25) NOT NULL,
        [PART] nvarchar(25) NULL,
        [NAME] nvarchar(50) NULL,
        [CASX] smallint NULL,
        [NGAYSX] smalldatetime NULL,
        [SLSX] int NULL,
        [NGAYNHAP] smalldatetime NULL,
        [SLNHAP] int NULL,
        [NGAYXUAT] smalldatetime NULL,
        [SLXUAT] int NULL,
        [SLCONLAI] int NOT NULL,
        [MODEL] nvarchar(15) NULL,
        [SLCONLAITMP] int NULL,
        [SATUS] smallint NULL,
        [NGAYNHAP1] smalldatetime NULL,
        [SLNHAP1] int NULL,
        [NGAYNHAP2] smalldatetime NULL,
        [SLNHAP2] int NULL,
        [NGAYNHAP3] smalldatetime NULL,
        [SLNHAP3] int NULL,
        [NGAYNHAP4] smalldatetime NULL,
        [SLNHAP4] int NULL,
        [NGAYNHAP5] smalldatetime NULL,
        [SLNHAP5] int NULL,
        [STATUSNHAP] nvarchar(25) NULL,
        [SP] nvarchar(50) NULL,
        [LineCodes] nvarchar(5) NULL,
        [DeptCode] nvarchar(5) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.stocktp', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.stocktp', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.stocktp') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOT', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (2, N'PART', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'NAME', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'CASX', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'NGAYSX', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLSX', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'NGAYNHAP', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SLNHAP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYXUAT', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'SLXUAT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'SLCONLAI', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (12, N'MODEL', N'nvarchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'SLCONLAITMP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'SATUS', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'NGAYNHAP1', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'SLNHAP1', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'NGAYNHAP2', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'SLNHAP2', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'NGAYNHAP3', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (20, N'SLNHAP3', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (21, N'NGAYNHAP4', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (22, N'SLNHAP4', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (23, N'NGAYNHAP5', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (24, N'SLNHAP5', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (25, N'STATUSNHAP', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (26, N'SP', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (27, N'LineCodes', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (28, N'DeptCode', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOT', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (2, N'PART', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'NAME', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'CASX', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'NGAYSX', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLSX', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'NGAYNHAP', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SLNHAP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYXUAT', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'SLXUAT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'SLCONLAI', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (12, N'MODEL', N'nvarchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'SLCONLAITMP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'SATUS', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'NGAYNHAP1', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'SLNHAP1', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'NGAYNHAP2', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'SLNHAP2', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'NGAYNHAP3', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (20, N'SLNHAP3', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (21, N'NGAYNHAP4', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (22, N'SLNHAP4', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (23, N'NGAYNHAP5', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (24, N'SLNHAP5', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (25, N'STATUSNHAP', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (26, N'SP', N'nvarchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (27, N'LineCodes', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (28, N'DeptCode', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.stocktp'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.stocktp', 1;
GO

IF OBJECT_ID(N'dbo.tbl_qr_comparepart', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[tbl_qr_comparepart] (
        [STT] int NULL,
        [PartNo] nvarchar(25) NULL,
        [PartName] nvarchar(30) NULL,
        [PartNoCompare] nvarchar(25) NULL,
        [timeSet] smalldatetime NULL,
        [IsActive] bit NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.tbl_qr_comparepart', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.tbl_qr_comparepart', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tbl_qr_comparepart') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'PartNo', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'PartName', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'PartNoCompare', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'timeSet', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'IsActive', N'bit', 1, 1, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'PartNo', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'PartName', N'nvarchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'PartNoCompare', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'timeSet', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'IsActive', N'bit', 1, 1, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tbl_qr_comparepart'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.tbl_qr_comparepart', 1;
GO

IF OBJECT_ID(N'dbo.tbl_qr_may_docqr', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[tbl_qr_may_docqr] (
        [STT] int IDENTITY(1,1) NOT NULL,
        [TenMay] nvarchar(25) NOT NULL,
        [LichSu] nvarchar(50) NOT NULL,
        [TT] bit NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.tbl_qr_may_docqr', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.tbl_qr_may_docqr', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tbl_qr_may_docqr') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'TenMay', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'LichSu', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'TT', N'bit', 1, 1, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'TenMay', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'LichSu', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N''), (4, N'TT', N'bit', 1, 1, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tbl_qr_may_docqr'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.tbl_qr_may_docqr', 1;
GO

IF OBJECT_ID(N'dbo.tmplotghep', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[tmplotghep] (
        [LOT] nchar(50) NULL,
        [MAHANG] nchar(25) NULL,
        [GIOXUAT] int NULL,
        [Flag] smallint NULL,
        [MachineName] nvarchar(25) NOT NULL DEFAULT ('')
    );
END;
GO

IF OBJECT_ID(N'dbo.tmplotghep', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.tmplotghep', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmplotghep') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOT', N'nchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANG', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'GIOXUAT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'Flag', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'MachineName', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'('''')', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOT', N'nchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANG', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'GIOXUAT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'Flag', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'MachineName', N'nvarchar', 50, 0, 0, 0, 0, 0, N'', N'('''')', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmplotghep'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.tmplotghep', 1;
GO

IF OBJECT_ID(N'dbo.tmplotgheptest', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[tmplotgheptest] (
        [LOT] nchar(50) NULL,
        [MAHANG] nchar(25) NULL,
        [GIOXUAT] int NULL,
        [Flag] smallint NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.tmplotgheptest', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.tmplotgheptest', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmplotgheptest') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOT', N'nchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANG', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'GIOXUAT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'Flag', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOT', N'nchar', 100, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANG', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'GIOXUAT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'Flag', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmplotgheptest'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.tmplotgheptest', 1;
GO

IF OBJECT_ID(N'dbo.tmplottach', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[tmplottach] (
        [MAHANG] nvarchar(25) NULL,
        [SL] int NULL,
        [Flag] int NULL,
        [STT] int NULL,
        [LOT] nvarchar(25) NULL,
        [SLTong] int NULL,
        [TEMNO] nvarchar(5) NULL,
        [TEMNOGOC] nchar(5) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.tmplottach', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.tmplottach', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmplottach') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'SL', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'Flag', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'STT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'LOT', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLTong', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'TEMNO', N'nvarchar', 11, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'TEMNOGOC', N'nchar', 11, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'SL', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'Flag', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'STT', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'LOT', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLTong', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'TEMNO', N'nvarchar', 11, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'TEMNOGOC', N'nchar', 11, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmplottach'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.tmplottach', 1;
GO

IF OBJECT_ID(N'dbo.tmpphieugiaodbhd', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[tmpphieugiaodbhd] (
        [IDP] int IDENTITY(1,1) NOT NULL,
        [NAME] nvarchar(125) NULL,
        [TRANGTHAIGIAO] bit NULL,
        [Note] nvarchar(125) NULL,
        [NGAYLAP] smalldatetime NULL,
        [NHAMAY] int NOT NULL,
        [NHAMAYNAME] nvarchar(25) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.tmpphieugiaodbhd', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.tmpphieugiaodbhd', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmpphieugiaodbhd') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'IDP', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'NAME', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRANGTHAIGIAO', N'bit', 1, 1, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'NGAYLAP', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'NHAMAY', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'NHAMAYNAME', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'IDP', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'NAME', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRANGTHAIGIAO', N'bit', 1, 1, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'NGAYLAP', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'NHAMAY', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (7, N'NHAMAYNAME', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmpphieugiaodbhd'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.tmpphieugiaodbhd', 1;
GO

IF OBJECT_ID(N'dbo.tmpphieugiaohang', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[tmpphieugiaohang] (
        [STT] smallint NULL,
        [CUA] nvarchar(25) NULL,
        [TRUYEN] nvarchar(25) NULL,
        [MAHANG] nvarchar(25) NULL,
        [TENHANG] nvarchar(25) NULL,
        [LOT] nvarchar(250) NULL,
        [DV] nvarchar(25) NULL,
        [SOLUONG] smallint NULL,
        [NGAYGIAO] smalldatetime NULL,
        [GIOGIAO] nvarchar(25) NULL,
        [STATUS] nvarchar(25) NULL,
        [TTPHIEU] nvarchar(25) NULL,
        [NHAMAY] varchar(50) NULL,
        [ADDNM] int NULL,
        [HOP] int NULL,
        [STATUSDOC] nchar(25) NULL,
        [Note] nvarchar(125) NULL,
        [PO_NO] nvarchar(100) NULL,
        [PO_ITEM] nvarchar(100) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.tmpphieugiaohang', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.tmpphieugiaohang', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmpphieugiaohang') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOT', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SOLUONG', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUS', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TTPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'varchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'ADDNM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'HOP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'STATUSDOC', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'PO_NO', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'PO_ITEM', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOT', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SOLUONG', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUS', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TTPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'varchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'ADDNM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'HOP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'STATUSDOC', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'PO_NO', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'PO_ITEM', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmpphieugiaohang'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.tmpphieugiaohang', 1;
GO

IF OBJECT_ID(N'dbo.tmpphieugiaohang_100002', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[tmpphieugiaohang_100002] (
        [STT] smallint NULL,
        [CUA] nvarchar(25) NULL,
        [TRUYEN] nvarchar(25) NULL,
        [MAHANG] nvarchar(25) NULL,
        [TENHANG] nvarchar(25) NULL,
        [LOT] nvarchar(250) NULL,
        [DV] nvarchar(25) NULL,
        [SOLUONG] smallint NULL,
        [NGAYGIAO] smalldatetime NULL,
        [GIOGIAO] nvarchar(25) NULL,
        [STATUS] nvarchar(25) NULL,
        [TTPHIEU] nvarchar(25) NULL,
        [NHAMAY] varchar(50) NULL,
        [ADDNM] int NULL,
        [HOP] int NULL,
        [STATUSDOC] nchar(25) NULL,
        [Note] nvarchar(125) NULL,
        [PO_NO] nvarchar(100) NULL,
        [PO_ITEM] nvarchar(100) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.tmpphieugiaohang_100002', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.tmpphieugiaohang_100002', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmpphieugiaohang_100002') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOT', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SOLUONG', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUS', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TTPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'varchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'ADDNM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'HOP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'STATUSDOC', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'PO_NO', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'PO_ITEM', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOT', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SOLUONG', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUS', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TTPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'varchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'ADDNM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'HOP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'STATUSDOC', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'PO_NO', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'PO_ITEM', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmpphieugiaohang_100002'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.tmpphieugiaohang_100002', 1;
GO

IF OBJECT_ID(N'dbo.tmpphieugiaohang_100003', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[tmpphieugiaohang_100003] (
        [STT] smallint NULL,
        [CUA] nvarchar(25) NULL,
        [TRUYEN] nvarchar(25) NULL,
        [MAHANG] nvarchar(25) NULL,
        [TENHANG] nvarchar(25) NULL,
        [LOT] nvarchar(250) NULL,
        [DV] nvarchar(25) NULL,
        [SOLUONG] int NULL,
        [NGAYGIAO] smalldatetime NULL,
        [GIOGIAO] nvarchar(25) NULL,
        [STATUS] nvarchar(5) NULL,
        [TTPHIEU] nvarchar(25) NULL,
        [NHAMAY] varchar(50) NULL,
        [ADDNM] int NULL,
        [HOP] int NULL,
        [STATUSDOC] nchar(25) NULL,
        [Note] nvarchar(125) NULL,
        [PO_NO] nvarchar(100) NULL,
        [PO_ITEM] nvarchar(100) NULL,
        [GEAR] nvarchar(25) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.tmpphieugiaohang_100003', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.tmpphieugiaohang_100003', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmpphieugiaohang_100003') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOT', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SOLUONG', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUS', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TTPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'varchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'ADDNM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'HOP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'STATUSDOC', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'PO_NO', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'PO_ITEM', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (20, N'GEAR', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOT', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SOLUONG', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUS', N'nvarchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TTPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'varchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'ADDNM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'HOP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'STATUSDOC', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'PO_NO', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'PO_ITEM', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (20, N'GEAR', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmpphieugiaohang_100003'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.tmpphieugiaohang_100003', 1;
GO

IF OBJECT_ID(N'dbo.tmpphieugiaohang_sp', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[tmpphieugiaohang_sp] (
        [STT] smallint NULL,
        [CUA] nvarchar(25) NULL,
        [TRUYEN] nvarchar(25) NULL,
        [MAHANG] nvarchar(25) NULL,
        [TENHANG] nvarchar(25) NULL,
        [LOT] nvarchar(250) NULL,
        [DV] nvarchar(25) NULL,
        [SOLUONG] smallint NULL,
        [NGAYGIAO] smalldatetime NULL,
        [GIOGIAO] nvarchar(25) NULL,
        [STATUS] nvarchar(25) NULL,
        [TTPHIEU] nvarchar(25) NULL,
        [NHAMAY] varchar(50) NULL,
        [ADDNM] int NULL,
        [HOP] int NULL,
        [STATUSDOC] nchar(25) NULL,
        [Note] nvarchar(125) NULL,
        [PO_NO] nvarchar(100) NULL,
        [PO_ITEM] nvarchar(100) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.tmpphieugiaohang_sp', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.tmpphieugiaohang_sp', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmpphieugiaohang_sp') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOT', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SOLUONG', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUS', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TTPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'varchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'ADDNM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'HOP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'STATUSDOC', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'PO_NO', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'PO_ITEM', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOT', N'nvarchar', 500, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SOLUONG', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUS', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TTPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'varchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'ADDNM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'HOP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'STATUSDOC', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'PO_NO', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'PO_ITEM', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmpphieugiaohang_sp'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.tmpphieugiaohang_sp', 1;
GO

IF OBJECT_ID(N'dbo.tmpphieugiaohangdbct', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[tmpphieugiaohangdbct] (
        [ID] int IDENTITY(1,1) NOT NULL,
        [IDP] int NOT NULL,
        [GGFCC] nvarchar(25) NULL,
        [STT] smallint NULL,
        [CUA] nvarchar(25) NULL,
        [TRUYEN] nvarchar(25) NULL,
        [MAHANG] nvarchar(25) NULL,
        [TENHANG] nvarchar(25) NULL,
        [LOT] nvarchar(100) NULL,
        [DV] nvarchar(25) NULL,
        [SOLUONG] smallint NULL,
        [NGAYGIAO] smalldatetime NULL,
        [GIOGIAO] nvarchar(25) NULL,
        [STATUS] nvarchar(25) NULL,
        [TTPHIEU] nvarchar(25) NULL,
        [NHAMAY] varchar(50) NULL,
        [ADDNM] int NULL,
        [HOP] int NULL,
        [STATUSDOC] nvarchar(25) NULL,
        [NGAYNHAN] smalldatetime NULL,
        [TTNHAN] bit NULL,
        [Note] nvarchar(125) NULL,
        [PO_NO] nvarchar(25) NULL,
        [PO_ITEM] nvarchar(25) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.tmpphieugiaohangdbct', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.tmpphieugiaohangdbct', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmpphieugiaohangdbct') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'ID', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'IDP', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'GGFCC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'LOT', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'SOLUONG', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'STATUS', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'TTPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'NHAMAY', N'varchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'ADDNM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'HOP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'STATUSDOC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (20, N'NGAYNHAN', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (21, N'TTNHAN', N'bit', 1, 1, 0, 1, 0, 0, N'', N'', N'', N''), (22, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (23, N'PO_NO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (24, N'PO_ITEM', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'ID', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'IDP', N'int', 4, 10, 0, 0, 0, 0, N'', N'', N'', N''), (3, N'GGFCC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'LOT', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'SOLUONG', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'STATUS', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'TTPHIEU', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'NHAMAY', N'varchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (17, N'ADDNM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (18, N'HOP', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (19, N'STATUSDOC', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (20, N'NGAYNHAN', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (21, N'TTNHAN', N'bit', 1, 1, 0, 1, 0, 0, N'', N'', N'', N''), (22, N'Note', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (23, N'PO_NO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (24, N'PO_ITEM', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.tmpphieugiaohangdbct'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.tmpphieugiaohangdbct', 1;
GO

IF OBJECT_ID(N'dbo.warehouse', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[warehouse] (
        [WarehouseId] int IDENTITY(1,1) NOT NULL,
        [Name] nvarchar(50) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.warehouse', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.warehouse', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.warehouse') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'WarehouseId', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'Name', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'WarehouseId', N'int', 4, 10, 0, 0, 1, 0, N'', N'', N'1', N'1'), (2, N'Name', N'nvarchar', 100, 0, 0, 0, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.warehouse'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.warehouse', 1;
GO

IF OBJECT_ID(N'dbo.ymvn_docqrcode', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ymvn_docqrcode] (
        [LOTFCC] nchar(60) NULL,
        [MAHANGFCC] nchar(30) NULL,
        [SLTEMFCC] smallint NULL,
        [LOTHVN] nchar(15) NULL,
        [MAHANGHVN] nchar(30) NULL,
        [SLTEMHVN] smallint NULL,
        [STATUS] smallint NULL,
        [MAFCC] nchar(25) NULL,
        [STT] smallint NULL,
        [KETQUA] nchar(15) NULL,
        [CUA] nchar(5) NULL,
        [TRUYEN] nchar(5) NULL,
        [GIO] nchar(5) NULL,
        [STTBAN] int NULL,
        [SUALOTHVN] nchar(15) NULL,
        [Gear] nchar(0) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.ymvn_docqrcode', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.ymvn_docqrcode', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.ymvn_docqrcode') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOTFCC', N'nchar', 120, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANGFCC', N'nchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'SLTEMFCC', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'LOTHVN', N'nchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'MAHANGHVN', N'nchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLTEMHVN', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'STATUS', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'MAFCC', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'KETQUA', N'nchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'CUA', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TRUYEN', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'GIO', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'STTBAN', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'SUALOTHVN', N'nchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'Gear', N'nchar', 1, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'LOTFCC', N'nchar', 120, 0, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'MAHANGFCC', N'nchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'SLTEMFCC', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'LOTHVN', N'nchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'MAHANGHVN', N'nchar', 60, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'SLTEMHVN', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'STATUS', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'MAFCC', N'nchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'KETQUA', N'nchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'CUA', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TRUYEN', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'GIO', N'nchar', 10, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'STTBAN', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'SUALOTHVN', N'nchar', 30, 0, 0, 1, 0, 0, N'', N'', N'', N''), (16, N'Gear', N'nchar', 1, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.ymvn_docqrcode'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.ymvn_docqrcode', 1;
GO

IF OBJECT_ID(N'dbo.ymvn_tmpphieugiaohang', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ymvn_tmpphieugiaohang] (
        [STT] smallint NULL,
        [CUA] nvarchar(25) NULL,
        [TRUYEN] nvarchar(25) NULL,
        [MAHANG] nvarchar(25) NULL,
        [TENHANG] nvarchar(25) NULL,
        [LOT] nvarchar(100) NULL,
        [DV] nvarchar(25) NULL,
        [SOLUONG] smallint NULL,
        [NGAYGIAO] smalldatetime NULL,
        [GIOGIAO] nvarchar(25) NULL,
        [STATUS] nvarchar(25) NULL,
        [TTPHIEU] nvarchar(125) NULL,
        [NHAMAY] varchar(50) NULL,
        [ADDNM] int NULL,
        [Gear] nvarchar(128) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.ymvn_tmpphieugiaohang', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.ymvn_tmpphieugiaohang', 1;
IF EXISTS (SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.ymvn_tmpphieugiaohang') EXCEPT SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOT', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SOLUONG', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUS', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TTPHIEU', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'varchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'ADDNM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'Gear', N'nvarchar', 256, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)) OR EXISTS (SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, v.seed_value, v.increment_value FROM (VALUES (1, N'STT', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (2, N'CUA', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (3, N'TRUYEN', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (4, N'MAHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (5, N'TENHANG', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (6, N'LOT', N'nvarchar', 200, 0, 0, 1, 0, 0, N'', N'', N'', N''), (7, N'DV', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (8, N'SOLUONG', N'smallint', 2, 5, 0, 1, 0, 0, N'', N'', N'', N''), (9, N'NGAYGIAO', N'smalldatetime', 4, 16, 0, 1, 0, 0, N'', N'', N'', N''), (10, N'GIOGIAO', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (11, N'STATUS', N'nvarchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (12, N'TTPHIEU', N'nvarchar', 250, 0, 0, 1, 0, 0, N'', N'', N'', N''), (13, N'NHAMAY', N'varchar', 50, 0, 0, 1, 0, 0, N'', N'', N'', N''), (14, N'ADDNM', N'int', 4, 10, 0, 1, 0, 0, N'', N'', N'', N''), (15, N'Gear', N'nvarchar', 256, 0, 0, 1, 0, 0, N'', N'', N'', N'')) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value) EXCEPT SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(N'dbo.ymvn_tmpphieugiaohang'))
    THROW 52202, N'SQL2 table/column definition mismatch: dbo.ymvn_tmpphieugiaohang', 1;
GO

/* PK/index/FK/CHECK constraints are emitted from verified SQL2 metadata in 03_Generated_From_DB57_Constraints.sql. */
