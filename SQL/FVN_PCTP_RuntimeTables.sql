/*
 FVN-owned runtime tables missing from the legacy SQL baseline.
 Deploy only to a reviewed test database first. This script creates schema only;
 it does not import production/business data.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.FVN_Purchase_Order_HVNDB', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_Purchase_Order_HVNDB
    (
        Id                INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_FVN_Purchase_Order_HVNDB PRIMARY KEY,
        Oder_no           NVARCHAR(20) NULL,
        Part_no           NVARCHAR(100) NULL,
        Part_name         NVARCHAR(120) NULL,
        NgayGiao           SMALLDATETIME NULL,
        Slgiao             INT NULL,
        QCDG               INT NULL,
        Gear               NVARCHAR(160) NULL,
        CUA                NCHAR(20) NULL,
        IsDelivered        BIT NULL,
        DeliveredDate      DATETIME NULL
    );

    CREATE INDEX IX_FVN_Purchase_Order_HVNDB_NgayGiao
        ON dbo.FVN_Purchase_Order_HVNDB(NgayGiao);

    CREATE INDEX IX_FVN_Purchase_Order_HVNDB_OrderPart
        ON dbo.FVN_Purchase_Order_HVNDB(Oder_no, Part_no);
END;

IF OBJECT_ID(N'dbo.FVN_sys_WorkflowTransitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_sys_WorkflowTransitions
    (
        Id          INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_FVN_sys_WorkflowTransitions PRIMARY KEY,
        ProcessCode NVARCHAR(50) NOT NULL,
        FromStatus  INT NOT NULL,
        ToStatus    INT NOT NULL,
        ActionName  NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        IsActive    BIT NOT NULL
            CONSTRAINT DF_FVN_sys_WorkflowTransitions_IsActive DEFAULT (1)
    );

    CREATE INDEX IX_FVN_sys_WorkflowTransitions_Process_Active
        ON dbo.FVN_sys_WorkflowTransitions(ProcessCode, IsActive, Id)
        INCLUDE (FromStatus, ToStatus, ActionName, Description);
END;

COMMIT TRANSACTION;
GO
