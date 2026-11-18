/*
 Migration 031: MES-owned component purchase orders, release-time LOT and reusable QR labels.
 Prerequisites: migrations 000..030.
 IFS remains a read-only source for Site/Part/Supplier master data only.
 PO status, release, LOT issuance and label reprints are owned by MES_PCTP.
 SQL Server 2016 compatible.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 53100, 'Migration 031 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL
    THROW 53101, 'MES.FVN_SchemaMigration is missing.', 1;
IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='030')
    THROW 53102, 'Migration 030 must be applied before migration 031.', 1;
IF EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='031')
    RETURN;
IF OBJECT_ID(N'MES.FVN_AllocateItemId5',N'P') IS NULL
 OR OBJECT_ID(N'MES.FVN_AllocateSupplierId5',N'P') IS NULL
    THROW 53103, 'Run migration 029 identity allocators first.', 1;

BEGIN TRY
BEGIN TRANSACTION;

IF OBJECT_ID(N'MES.FVN_ComponentPurchaseOrder',N'U') IS NULL
BEGIN
 CREATE TABLE MES.FVN_ComponentPurchaseOrder
 (
  PurchaseOrderId bigint IDENTITY(1,1) NOT NULL,
  SiteCode nvarchar(30) NOT NULL,
  PurchaseOrderNo nvarchar(60) NOT NULL,
  SupplierCode nvarchar(60) NOT NULL,
  SupplierNameSnapshot nvarchar(250) NOT NULL,
  OrderDate date NOT NULL,
  Status varchar(20) NOT NULL CONSTRAINT DF_MES_FVN_ComponentPO_Status DEFAULT('DRAFT'),
  ReleasedAtUtc datetime2(3) NULL,
  ReleasedBy nvarchar(100) NULL,
  CancelledAtUtc datetime2(3) NULL,
  CancelledBy nvarchar(100) NULL,
  Remarks nvarchar(500) NULL,
  CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_FVN_ComponentPO_Created DEFAULT(SYSUTCDATETIME()),
  CreatedBy nvarchar(100) NULL,
  UpdatedAtUtc datetime2(3) NULL,
  UpdatedBy nvarchar(100) NULL,
  CONSTRAINT PK_MES_FVN_ComponentPurchaseOrder PRIMARY KEY(PurchaseOrderId),
  CONSTRAINT UQ_MES_FVN_ComponentPO_SiteNo UNIQUE(SiteCode,PurchaseOrderNo),
  CONSTRAINT FK_MES_FVN_ComponentPO_Site FOREIGN KEY(SiteCode) REFERENCES dbo.FVN_PCTPSite(SiteCode),
  CONSTRAINT FK_MES_FVN_ComponentPO_Supplier FOREIGN KEY(SupplierCode) REFERENCES MES.FVN_SupplierIdentity(SupplierCode),
  CONSTRAINT CK_MES_FVN_ComponentPO_Status CHECK(Status IN('DRAFT','VALIDATED','RELEASED','PARTIALLY_RECEIVED','RECEIVED','CLOSED','CANCELLED'))
 );
END;

IF OBJECT_ID(N'MES.FVN_ComponentPurchaseOrderLine',N'U') IS NULL
BEGIN
 CREATE TABLE MES.FVN_ComponentPurchaseOrderLine
 (
  PurchaseOrderLineId bigint IDENTITY(1,1) NOT NULL,
  PurchaseOrderId bigint NOT NULL,
  LineNo int NOT NULL,
  SiteCode nvarchar(30) NOT NULL,
  PartNo nvarchar(100) NOT NULL,
  PartDescription nvarchar(250) NULL,
  OrderQty decimal(18,3) NOT NULL,
  ReceivedQty decimal(18,3) NOT NULL CONSTRAINT DF_MES_FVN_ComponentPOL_Received DEFAULT(0),
  UnitCode nvarchar(20) NULL,
  NeedByDate date NULL,
  CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_FVN_ComponentPOL_Created DEFAULT(SYSUTCDATETIME()),
  CONSTRAINT PK_MES_FVN_ComponentPurchaseOrderLine PRIMARY KEY(PurchaseOrderLineId),
  CONSTRAINT UQ_MES_FVN_ComponentPOL_Line UNIQUE(PurchaseOrderId,LineNo),
  CONSTRAINT FK_MES_FVN_ComponentPOL_Header FOREIGN KEY(PurchaseOrderId) REFERENCES MES.FVN_ComponentPurchaseOrder(PurchaseOrderId),
  CONSTRAINT FK_MES_FVN_ComponentPOL_Part FOREIGN KEY(SiteCode,PartNo) REFERENCES IFS.FVN_InventoryPart(SiteCode,PartNo),
  CONSTRAINT CK_MES_FVN_ComponentPOL_Qty CHECK(OrderQty>0 AND ReceivedQty>=0 AND ReceivedQty<=OrderQty)
 );
END;

IF OBJECT_ID(N'MES.FVN_ComponentLot',N'U') IS NULL
BEGIN
 CREATE TABLE MES.FVN_ComponentLot
 (
  ComponentLotId bigint IDENTITY(1,1) NOT NULL,
  PurchaseOrderLineId bigint NOT NULL,
  SiteCode nvarchar(30) NOT NULL,
  PartNo nvarchar(100) NOT NULL,
  SupplierCode nvarchar(60) NOT NULL,
  LotNo nvarchar(120) NOT NULL,
  LotIssueDate date NOT NULL,
  IssuedQty decimal(18,3) NOT NULL,
  ReceivedQty decimal(18,3) NOT NULL CONSTRAINT DF_MES_FVN_ComponentLot_Received DEFAULT(0),
  Status varchar(20) NOT NULL CONSTRAINT DF_MES_FVN_ComponentLot_Status DEFAULT('ISSUED'),
  QrPayload nvarchar(500) NOT NULL,
  CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_FVN_ComponentLot_Created DEFAULT(SYSUTCDATETIME()),
  CONSTRAINT PK_MES_FVN_ComponentLot PRIMARY KEY(ComponentLotId),
  CONSTRAINT UQ_MES_FVN_ComponentLot_Lot UNIQUE(LotNo),
  CONSTRAINT FK_MES_FVN_ComponentLot_POL FOREIGN KEY(PurchaseOrderLineId) REFERENCES MES.FVN_ComponentPurchaseOrderLine(PurchaseOrderLineId),
  CONSTRAINT CK_MES_FVN_ComponentLot_Qty CHECK(IssuedQty>0 AND ReceivedQty>=0 AND ReceivedQty<=IssuedQty),
  CONSTRAINT CK_MES_FVN_ComponentLot_Status CHECK(Status IN('ISSUED','PARTIALLY_RECEIVED','RECEIVED','BLOCKED','CANCELLED'))
 );
 CREATE INDEX IX_MES_FVN_ComponentLot_POline ON MES.FVN_ComponentLot(PurchaseOrderLineId,ComponentLotId);
 CREATE INDEX IX_MES_FVN_ComponentLot_SitePart ON MES.FVN_ComponentLot(SiteCode,PartNo,LotNo);
END;

IF OBJECT_ID(N'MES.FVN_ComponentLotLabelHistory',N'U') IS NULL
BEGIN
 CREATE TABLE MES.FVN_ComponentLotLabelHistory
 (
  LabelHistoryId bigint IDENTITY(1,1) NOT NULL,
  ComponentLotId bigint NOT NULL,
  ActionCode varchar(20) NOT NULL,
  CopyCount int NOT NULL,
  Reason nvarchar(300) NULL,
  RequestedBy nvarchar(100) NOT NULL,
  RequestedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_FVN_ComponentLotLabelHistory_Date DEFAULT(SYSUTCDATETIME()),
  CONSTRAINT PK_MES_FVN_ComponentLotLabelHistory PRIMARY KEY(LabelHistoryId),
  CONSTRAINT FK_MES_FVN_ComponentLotLabelHistory_Lot FOREIGN KEY(ComponentLotId) REFERENCES MES.FVN_ComponentLot(ComponentLotId),
  CONSTRAINT CK_MES_FVN_ComponentLotLabelHistory_Action CHECK(ActionCode IN('INITIAL_PRINT','REPRINT')),
  CONSTRAINT CK_MES_FVN_ComponentLotLabelHistory_Count CHECK(CopyCount>0)
 );
END;

EXEC(N'
CREATE PROCEDURE MES.usp_FVN_ComponentPO_Release
 @PurchaseOrderId bigint,
 @ReleasedBy nvarchar(100),
 @LotIssueDate date=NULL
AS
BEGIN
 SET NOCOUNT ON;
 SET XACT_ABORT ON;
 IF @PurchaseOrderId IS NULL OR @PurchaseOrderId<=0 THROW 53110,''PurchaseOrderId is required.'',1;
 IF NULLIF(LTRIM(RTRIM(@ReleasedBy)),N'''') IS NULL THROW 53111,''ReleasedBy is required.'',1;
 SET @LotIssueDate=ISNULL(@LotIssueDate,CONVERT(date,GETDATE()));
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Status varchar(20),@SupplierCode nvarchar(60),@SiteCode nvarchar(30);
  SELECT @Status=Status,@SupplierCode=SupplierCode,@SiteCode=SiteCode
  FROM MES.FVN_ComponentPurchaseOrder WITH(UPDLOCK,HOLDLOCK)
  WHERE PurchaseOrderId=@PurchaseOrderId;
  IF @Status IS NULL THROW 53112,''Component purchase order was not found.'',1;
  IF @Status=''CANCELLED'' OR @Status=''CLOSED'' THROW 53113,''Closed/cancelled order cannot be released.'',1;
  IF @Status=''RELEASED'' OR @Status=''PARTIALLY_RECEIVED'' OR @Status=''RECEIVED''
  BEGIN
   COMMIT TRANSACTION;
   SELECT PurchaseOrderId,Status FROM MES.FVN_ComponentPurchaseOrder WHERE PurchaseOrderId=@PurchaseOrderId;
   RETURN;
  END;
  IF @Status<>''VALIDATED'' THROW 53114,''Only a VALIDATED MES order can be released.'',1;
  IF NOT EXISTS(SELECT 1 FROM MES.FVN_SupplierIdentity WHERE SupplierCode=@SupplierCode AND SupplierId5 IS NOT NULL)
     THROW 53115,''Supplier identity is not allocated.'',1;
  IF EXISTS(SELECT 1 FROM MES.FVN_ComponentPurchaseOrderLine WHERE PurchaseOrderId=@PurchaseOrderId AND (SiteCode<>@SiteCode OR OrderQty<=0))
     THROW 53116,''Order line SiteCode or quantity is invalid.'',1;
  IF NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentPurchaseOrderLine WHERE PurchaseOrderId=@PurchaseOrderId)
     THROW 53117,''Order has no lines.'',1;

  DECLARE @LineId bigint,@PartNo nvarchar(100),@Qty decimal(18,3),@ItemId5 char(5),@SupplierId5 char(5),@DocNo nvarchar(40),@LotNo nvarchar(120),@Qr nvarchar(500);
  EXEC MES.usp_FVN_AllocateSupplierId5 @SupplierCode=@SupplierCode,@SupplierId5=@SupplierId5 OUTPUT;
  DECLARE c CURSOR LOCAL FAST_FORWARD FOR
   SELECT PurchaseOrderLineId,PartNo,OrderQty
   FROM MES.FVN_ComponentPurchaseOrderLine WITH(UPDLOCK,HOLDLOCK)
   WHERE PurchaseOrderId=@PurchaseOrderId ORDER BY LineNo;
  OPEN c;
  FETCH NEXT FROM c INTO @LineId,@PartNo,@Qty;
  WHILE @@FETCH_STATUS=0
  BEGIN
   IF NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentLot WHERE PurchaseOrderLineId=@LineId)
   BEGIN
    EXEC MES.usp_FVN_AllocateItemId5 @SiteCode=@SiteCode,@PartNo=@PartNo,@ItemId5=@ItemId5 OUTPUT;
    SET @DocNo=CONVERT(varchar(20),@PurchaseOrderId)+N''-''+CONVERT(varchar(20),@LineId);
    SET @LotNo=CONVERT(char(6),@LotIssueDate,12)+@SupplierId5+@ItemId5+@DocNo;
    IF LEN(@LotNo)>120 THROW 53118,''Generated component LOT exceeds 120 characters.'',1;
    SET @Qr=N''FVN1|''+@LotNo;
    INSERT MES.FVN_ComponentLot(PurchaseOrderLineId,SiteCode,PartNo,SupplierCode,LotNo,LotIssueDate,IssuedQty,QrPayload)
    VALUES(@LineId,@SiteCode,@PartNo,@SupplierCode,@LotNo,@LotIssueDate,@Qty,@Qr);
   END;
   FETCH NEXT FROM c INTO @LineId,@PartNo,@Qty;
  END;
  CLOSE c; DEALLOCATE c;
  UPDATE MES.FVN_ComponentPurchaseOrder
    SET Status=''RELEASED'',ReleasedAtUtc=SYSUTCDATETIME(),ReleasedBy=@ReleasedBy,
        UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@ReleasedBy
  WHERE PurchaseOrderId=@PurchaseOrderId;
  COMMIT TRANSACTION;
  SELECT l.ComponentLotId,l.LotNo,l.QrPayload,l.IssuedQty,p.LineNo,p.PartNo,p.PartDescription
  FROM MES.FVN_ComponentLot l
  JOIN MES.FVN_ComponentPurchaseOrderLine p ON p.PurchaseOrderLineId=l.PurchaseOrderLineId
  WHERE p.PurchaseOrderId=@PurchaseOrderId ORDER BY p.LineNo,l.ComponentLotId;
 END TRY
 BEGIN CATCH
  IF CURSOR_STATUS(''local'',''c'')>=0 CLOSE c;
  IF CURSOR_STATUS(''local'',''c'')>-3 DEALLOCATE c;
  IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
  THROW;
 END CATCH;
END;');

EXEC(N'
CREATE PROCEDURE MES.usp_FVN_ComponentLot_ReprintLabel
 @ComponentLotId bigint,
 @CopyCount int,
 @Reason nvarchar(300),
 @RequestedBy nvarchar(100)
AS
BEGIN
 SET NOCOUNT ON;
 IF @ComponentLotId IS NULL OR @ComponentLotId<=0 THROW 53120,''ComponentLotId is required.'',1;
 IF @CopyCount IS NULL OR @CopyCount<=0 OR @CopyCount>500 THROW 53121,''CopyCount must be between 1 and 500.'',1;
 IF NULLIF(LTRIM(RTRIM(@Reason)),N'''') IS NULL THROW 53122,''Reprint reason is required.'',1;
 IF NULLIF(LTRIM(RTRIM(@RequestedBy)),N'''') IS NULL THROW 53123,''RequestedBy is required.'',1;
 IF NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentLot WHERE ComponentLotId=@ComponentLotId AND Status<>''CANCELLED'')
    THROW 53124,''Component LOT is not active.'',1;
 INSERT MES.FVN_ComponentLotLabelHistory(ComponentLotId,ActionCode,CopyCount,Reason,RequestedBy)
 VALUES(@ComponentLotId,''REPRINT'',@CopyCount,@Reason,@RequestedBy);
 SELECT l.ComponentLotId,l.LotNo,l.QrPayload,l.SiteCode,l.PartNo,l.SupplierCode,l.IssuedQty,
        h.LabelHistoryId,h.CopyCount,h.RequestedAtUtc
 FROM MES.FVN_ComponentLot l
 JOIN MES.FVN_ComponentLotLabelHistory h ON h.ComponentLotId=l.ComponentLotId
 WHERE h.LabelHistoryId=SCOPE_IDENTITY();
END;');

INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
VALUES('031',N'MES-owned component PO release, LOT issuance and QR label reprint');
COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
GO

SELECT MigrationVersion,MigrationName FROM MES.FVN_SchemaMigration WHERE MigrationVersion='031';
GO
