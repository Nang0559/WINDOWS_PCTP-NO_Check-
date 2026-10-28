-- FIXTURE ONLY (disposable PCTP_QC_* databases). Not production DDL.
-- Minimal projection of the two source tables read by dbo.vNhapTP that are not part of
-- 02_Generated_From_DB57.sql: dbo.B30AccDoc (MES production documents) and dbo.STOCKTPTRAHANG
-- (finished-goods returns). Only the columns the view reads are created, so the receiving screen
-- (NhapKho) can open and query the view against an empty fixture.
-- Replace with the exact DDL exported from DB57 when it is available (see README note in the PR).
IF OBJECT_ID(N'dbo.B30AccDoc', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.B30AccDoc (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_B30AccDoc_Fixture PRIMARY KEY,
        DocDate datetime NULL,
        ShiftCode nvarchar(10) NOT NULL CONSTRAINT DF_B30AccDoc_Fx_Shift DEFAULT (''),
        Model nvarchar(25) NOT NULL CONSTRAINT DF_B30AccDoc_Fx_Model DEFAULT (''),
        ReceiptItemCode nvarchar(25) NOT NULL CONSTRAINT DF_B30AccDoc_Fx_Item DEFAULT (''),
        ReceiptQuantity numeric(15,4) NOT NULL CONSTRAINT DF_B30AccDoc_Fx_Qty DEFAULT ((0)),
        IsActive bit NOT NULL CONSTRAINT DF_B30AccDoc_Fx_Active DEFAULT ((1)),
        MachinesCode nvarchar(10) NOT NULL CONSTRAINT DF_B30AccDoc_Fx_Machine DEFAULT (''),
        GearCode int NOT NULL CONSTRAINT DF_B30AccDoc_Fx_Gear DEFAULT ((0)),
        ItemLotCodeTP nvarchar(50) NOT NULL CONSTRAINT DF_B30AccDoc_Fx_LotTP DEFAULT ('')
    );
END;
GO

IF OBJECT_ID(N'dbo.STOCKTPTRAHANG', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.STOCKTPTRAHANG (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_STOCKTPTRAHANG_Fixture PRIMARY KEY,
        LOT nvarchar(25) NOT NULL,
        SLTRA int NULL,
        SLCONLAI int NOT NULL CONSTRAINT DF_STOCKTPTRAHANG_Fx_Left DEFAULT ((0)),
        LY_DO_NG nvarchar(100) NULL,
        STATUS smallint NULL
    );
END;
GO
