/*
 Migration 065: tem thành phẩm của LỆNH SẢN XUẤT (hai loại tem in từ cùng một lệnh).

   CARTON  Tem thùng thành phẩm   (Ca SX / Ngày SX / Tên hàng / Mã hàng / QR / Nơi SX / Xuất xứ / SL / số LOT 28 ký tự)
   PALLET  Phiếu quản lý nhập hàng thành phẩm ca (Pallet, Tên SP, Mã SP, Lot No, Số lượng, QR, khung xác nhận giao nhận)

 Số tem (LabelNo, 28 ký tự) = LOT sản xuất 20 ký tự của lệnh (yymmdd + ItemId5 + Ca + Line + Bộ phận, do
 MES.usp_FVN_BuildProductionLotNo dựng, giống MES.usp_FVN_ProductionLot_Create) + STT tem (4 số) + SL (4 số).

 QR (khớp hai mẫu tem thực tế, phân tách bằng dấu ':'):
   CARTON  <LabelNo>:<PartNo>:<dd/MM/yyyy>:<SL>
   PALLET  <LabelNo>:<PartNo>:<dd/MM/yyyy>:<SL>:<STT palet>:<Mã palet>

 1. MES.FVN_ProductionPackSpec        SL / thùng và SL / palet của thành phẩm (theo Site + PartNo).
 2. MES.FVN_ProductionLabelProfile    Tên nhà máy, địa chỉ, xuất xứ in trên tem (theo Site; thiếu thì dùng mặc định).
 3. MES.FVN_ProductionOrderLabel      Mỗi dòng = một tem thùng hoặc một phiếu palet đã tạo cho lệnh.
 4. MES.FVN_ProductionOrderLabelHistory  INITIAL_PRINT / REPRINT (in lại bắt buộc lý do).
 5. MES.usp_FVN_ProductionOrder_LabelContext / _LabelBuild / _LabelData / _LabelLog.

 Tem chỉ tạo cho lệnh RELEASED / RUNNING / PAUSED / COMPLETED; in lại được cả lệnh CLOSED.
 Tổng SL tem mỗi loại không vượt PlannedQty của lệnh. Không ghi tồn kho, không đổi trạng thái lệnh.
 Apply after migration 064 (cần 036 cho workflow lệnh). SQL Server 2016 SP1+.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 54900, 'Migration 065 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 54901, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '064')
    THROW 54902, 'Migration 064 must be applied before 065.', 1;
IF OBJECT_ID(N'MES.FVN_ProductionOrder', N'U') IS NULL
   OR OBJECT_ID(N'MES.usp_FVN_BuildProductionLotNo', N'P') IS NULL
    THROW 54903, 'Production order (034) and LOT builder (029) are required.', 1;
GO

/* ---------------------------------------------------------------- tables */
IF OBJECT_ID(N'MES.FVN_ProductionPackSpec', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_ProductionPackSpec
    (
        SiteCode      nvarchar(30)  NOT NULL,
        PartNo        nvarchar(100) NOT NULL,
        QtyPerCarton  int           NOT NULL,
        QtyPerPallet  int           NOT NULL,
        IsActive      bit           NOT NULL CONSTRAINT DF_MES_FVN_ProductionPackSpec_Active DEFAULT (1),
        UpdatedAtUtc  datetime2(3)  NOT NULL CONSTRAINT DF_MES_FVN_ProductionPackSpec_At DEFAULT (SYSUTCDATETIME()),
        UpdatedBy     nvarchar(100) NOT NULL,
        CONSTRAINT PK_MES_FVN_ProductionPackSpec PRIMARY KEY (SiteCode, PartNo),
        CONSTRAINT CK_MES_FVN_ProductionPackSpec_Qty CHECK (QtyPerCarton BETWEEN 1 AND 9999 AND QtyPerPallet BETWEEN 1 AND 9999)
    );
END;
GO

IF OBJECT_ID(N'MES.FVN_ProductionLabelProfile', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_ProductionLabelProfile
    (
        SiteCode        nvarchar(30)  NOT NULL,
        FactoryName     nvarchar(200) NOT NULL,
        DepartmentText  nvarchar(100) NULL,
        FactoryAddress  nvarchar(300) NULL,
        OriginText      nvarchar(60)  NOT NULL CONSTRAINT DF_MES_FVN_ProductionLabelProfile_Origin DEFAULT (N'Việt Nam'),
        UpdatedAtUtc    datetime2(3)  NOT NULL CONSTRAINT DF_MES_FVN_ProductionLabelProfile_At DEFAULT (SYSUTCDATETIME()),
        UpdatedBy       nvarchar(100) NOT NULL,
        CONSTRAINT PK_MES_FVN_ProductionLabelProfile PRIMARY KEY (SiteCode)
    );
END;
GO

IF OBJECT_ID(N'MES.FVN_ProductionPalletCodeSequence', N'SO') IS NULL
    CREATE SEQUENCE MES.FVN_ProductionPalletCodeSequence AS bigint START WITH 1 INCREMENT BY 1;
GO

IF OBJECT_ID(N'MES.FVN_ProductionOrderLabel', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_ProductionOrderLabel
    (
        ProductionOrderLabelId bigint IDENTITY(1,1) NOT NULL,
        ProductionOrderId      bigint        NOT NULL,
        SiteCode               nvarchar(30)  NOT NULL,
        LabelKind              varchar(10)   NOT NULL,
        Seq                    int           NOT NULL,
        LabelNo                varchar(28)   NOT NULL,
        Qty                    int           NOT NULL,
        PalletCode             varchar(20)   NULL,
        QrPayload              nvarchar(500) NOT NULL,
        CreatedAtUtc           datetime2(3)  NOT NULL CONSTRAINT DF_MES_FVN_ProductionOrderLabel_At DEFAULT (SYSUTCDATETIME()),
        CreatedBy              nvarchar(100) NOT NULL,
        CONSTRAINT PK_MES_FVN_ProductionOrderLabel PRIMARY KEY (ProductionOrderLabelId),
        CONSTRAINT UQ_MES_FVN_ProductionOrderLabel_Seq UNIQUE (ProductionOrderId, LabelKind, Seq),
        CONSTRAINT UQ_MES_FVN_ProductionOrderLabel_No UNIQUE (LabelKind, LabelNo),
        CONSTRAINT CK_MES_FVN_ProductionOrderLabel_Kind CHECK (LabelKind IN ('CARTON', 'PALLET')),
        CONSTRAINT CK_MES_FVN_ProductionOrderLabel_Qty CHECK (Qty BETWEEN 1 AND 9999 AND Seq BETWEEN 1 AND 9999),
        CONSTRAINT CK_MES_FVN_ProductionOrderLabel_Pallet CHECK ((LabelKind = 'PALLET' AND PalletCode IS NOT NULL) OR (LabelKind = 'CARTON' AND PalletCode IS NULL)),
        CONSTRAINT FK_MES_FVN_ProductionOrderLabel_Order FOREIGN KEY (ProductionOrderId) REFERENCES MES.FVN_ProductionOrder (ProductionOrderId)
    );
    CREATE INDEX IX_MES_FVN_ProductionOrderLabel_Order ON MES.FVN_ProductionOrderLabel (ProductionOrderId, LabelKind) INCLUDE (Qty);
END;
GO

IF OBJECT_ID(N'MES.FVN_ProductionOrderLabelHistory', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_ProductionOrderLabelHistory
    (
        LabelHistoryId         bigint IDENTITY(1,1) NOT NULL,
        ProductionOrderLabelId bigint        NOT NULL,
        ActionCode             varchar(20)   NOT NULL,
        Reason                 nvarchar(300) NULL,
        RequestedBy            nvarchar(100) NOT NULL,
        RequestedAtUtc         datetime2(3)  NOT NULL CONSTRAINT DF_MES_FVN_ProductionOrderLabelHistory_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MES_FVN_ProductionOrderLabelHistory PRIMARY KEY (LabelHistoryId),
        CONSTRAINT CK_MES_FVN_ProductionOrderLabelHistory_Action CHECK (ActionCode IN ('INITIAL_PRINT', 'REPRINT')),
        CONSTRAINT FK_MES_FVN_ProductionOrderLabelHistory_Label FOREIGN KEY (ProductionOrderLabelId)
            REFERENCES MES.FVN_ProductionOrderLabel (ProductionOrderLabelId)
    );
    CREATE INDEX IX_MES_FVN_ProductionOrderLabelHistory_Label ON MES.FVN_ProductionOrderLabelHistory (ProductionOrderLabelId, ActionCode);
END;
GO

/* ---------------------------------------------------------------- procedures */

/* Ngữ cảnh một lệnh cho hộp thoại in: Site / bộ phận (để kiểm quyền), trạng thái, SL đã tạo tem và quy cách đóng gói. */
 Create PROCEDURE MES.usp_FVN_ProductionOrder_LabelContext
    @ProductionOrderId bigint
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.ProductionOrderId, o.SiteCode, o.OrderNo, o.DepartmentCode, o.ProductPartNo, o.Status,
           CONVERT(int, FLOOR(o.PlannedQty)) AS PlannedQty,
           ISNULL((SELECT SUM(l.Qty) FROM MES.FVN_ProductionOrderLabel l
                    WHERE l.ProductionOrderId = o.ProductionOrderId AND l.LabelKind = 'CARTON'), 0) AS CartonLabelQty,
           ISNULL((SELECT SUM(l.Qty) FROM MES.FVN_ProductionOrderLabel l
                    WHERE l.ProductionOrderId = o.ProductionOrderId AND l.LabelKind = 'PALLET'), 0) AS PalletLabelQty,
           s.QtyPerCarton, s.QtyPerPallet
      FROM MES.FVN_ProductionOrder o
      LEFT JOIN MES.FVN_ProductionPackSpec s ON s.SiteCode = o.SiteCode AND s.PartNo = o.ProductPartNo AND s.IsActive = 1
     WHERE o.ProductionOrderId = @ProductionOrderId;
END;
GO

/*
 Tạo tem cho lệnh, idempotent theo SL: chỉ tạo phần SL chưa có tem của loại đó.
   @Qty        SL cần tạo tem lần này (mặc định = phần còn lại của PlannedQty).
   @QtyPerPack SL / thùng (CARTON) hoặc SL / palet (PALLET); mặc định lấy từ MES.FVN_ProductionPackSpec.
 Tem cuối có thể lẻ (SL còn lại < SL / gói).
*/
CREATE  PROCEDURE MES.usp_FVN_ProductionOrder_LabelBuild
    @ProductionOrderId bigint,
    @LabelKind         varchar(10),
    @ActorCode         nvarchar(100),
    @Qty               int = NULL,
    @QtyPerPack        int = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @ProductionOrderId IS NULL OR @ProductionOrderId <= 0 THROW 54910, 'ProductionOrderId is required.', 1;
    IF @LabelKind NOT IN ('CARTON', 'PALLET') THROW 54911, 'LabelKind must be CARTON or PALLET.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 54912, 'ActorCode is required.', 1;
    IF @Qty IS NOT NULL AND @Qty <= 0 THROW 54913, 'Quantity must be positive.', 1;
    IF @QtyPerPack IS NOT NULL AND (@QtyPerPack < 1 OR @QtyPerPack > 9999) THROW 54914, 'Quantity per pack must be between 1 and 9999.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Site nvarchar(30), @Part nvarchar(100), @Status varchar(20), @Planned int, @ProdDate date,
                @Shift nvarchar(20), @LotLine varchar(3), @Dept varchar(4);
        SELECT @Site = SiteCode, @Part = ProductPartNo, @Status = Status, @Planned = CONVERT(int, FLOOR(PlannedQty)),
               @ProdDate = ProductionDate, @Shift = ShiftCode, @LotLine = LotLineCode, @Dept = DepartmentCode
          FROM MES.FVN_ProductionOrder WITH (UPDLOCK, HOLDLOCK) WHERE ProductionOrderId = @ProductionOrderId;
        IF @Site IS NULL THROW 54915, 'Production order was not found.', 1;
        IF @Status NOT IN ('RELEASED', 'RUNNING', 'PAUSED', 'COMPLETED')
            THROW 54916, 'Labels can be created only for a released, running, paused or completed production order.', 1;
        IF @ProdDate IS NULL OR @Shift IS NULL OR @LotLine IS NULL OR @Dept IS NULL
            THROW 54917, 'The production order has no date / shift / line / department for the LOT key.', 1;

        DECLARE @Labeled int = ISNULL((SELECT SUM(Qty) FROM MES.FVN_ProductionOrderLabel
                                        WHERE ProductionOrderId = @ProductionOrderId AND LabelKind = @LabelKind), 0);
        DECLARE @Remaining int = @Planned - @Labeled;
        IF @Remaining <= 0 THROW 54918, 'Labels already cover the whole planned quantity of this production order.', 1;
        IF @Qty IS NULL SET @Qty = @Remaining;
        IF @Qty > @Remaining THROW 54919, 'Label quantity exceeds the planned quantity that has no label yet.', 1;

        IF @QtyPerPack IS NULL
            SELECT @QtyPerPack = CASE @LabelKind WHEN 'CARTON' THEN QtyPerCarton ELSE QtyPerPallet END
              FROM MES.FVN_ProductionPackSpec WHERE SiteCode = @Site AND PartNo = @Part AND IsActive = 1;
        IF @QtyPerPack IS NULL
            THROW 54920, 'Chưa khai báo SL / thùng và SL / palet của thành phẩm trong MES.FVN_ProductionPackSpec (hoặc nhập SL / gói khi in).', 1;

        DECLARE @Lot char(20), @ItemId5 char(5), @ShiftCode varchar(2) = CONVERT(varchar(2), LTRIM(RTRIM(@Shift)));
        EXEC MES.usp_FVN_BuildProductionLotNo @RequestDate = @ProdDate, @SiteCode = @Site, @PartNo = @Part,
             @ShiftCode = @ShiftCode, @LineCode = @LotLine, @DepartmentCode = @Dept,
             @LotNo = @Lot OUTPUT, @ItemId5 = @ItemId5 OUTPUT;

        DECLARE @DateText varchar(10) = CONVERT(varchar(10), @ProdDate, 103);
        DECLARE @Seq int = ISNULL((SELECT MAX(Seq) FROM MES.FVN_ProductionOrderLabel
                                    WHERE ProductionOrderId = @ProductionOrderId AND LabelKind = @LabelKind), 0);
        DECLARE @Left int = @Qty, @Take int, @LabelNo varchar(28), @PalletCode varchar(20), @Payload nvarchar(500), @Created int = 0;
        WHILE @Left > 0
        BEGIN
            SET @Take = CASE WHEN @Left < @QtyPerPack THEN @Left ELSE @QtyPerPack END;
            SET @Seq += 1;
            IF @Seq > 9999 THROW 54921, 'A production order cannot have more than 9999 labels of one kind.', 1;
            SET @LabelNo = @Lot + RIGHT('0000' + CONVERT(varchar(4), @Seq), 4) + RIGHT('0000' + CONVERT(varchar(4), @Take), 4);
            IF @LabelKind = 'PALLET'
            BEGIN
                SET @PalletCode = 'A' + RIGHT(REPLICATE('0', 15) + CONVERT(varchar(15), NEXT VALUE FOR MES.FVN_ProductionPalletCodeSequence), 15);
                SET @Payload = @LabelNo + N':' + @Part + N':' + @DateText + N':' + CONVERT(nvarchar(10), @Take)
                             + N':' + CONVERT(nvarchar(10), @Seq) + N':' + @PalletCode;
            END
            ELSE
            BEGIN
                SET @PalletCode = NULL;
                SET @Payload = @LabelNo + N':' + @Part + N':' + @DateText + N':' + CONVERT(nvarchar(10), @Take);
            END;
            INSERT MES.FVN_ProductionOrderLabel (ProductionOrderId, SiteCode, LabelKind, Seq, LabelNo, Qty, PalletCode, QrPayload, CreatedBy)
            VALUES (@ProductionOrderId, @Site, @LabelKind, @Seq, @LabelNo, @Take, @PalletCode, @Payload, @ActorCode);
            SET @Left -= @Take; SET @Created += 1;
        END;
        COMMIT TRANSACTION;
        SELECT @LabelKind AS LabelKind, @Created AS LabelsCreated, @Qty AS QtyLabeled;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

/* Dữ liệu in. LabelKind = CARTON hoặc PALLET; @OnlyUnprinted = 1 chỉ lấy tem chưa in lần đầu. */
CREATE  PROCEDURE MES.usp_FVN_ProductionOrder_LabelData
    @ProductionOrderId bigint,
    @LabelKind         varchar(10),
    @OnlyUnprinted     bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    IF @LabelKind NOT IN ('CARTON', 'PALLET') THROW 54930, 'LabelKind must be CARTON or PALLET.', 1;
    ;WITH x AS
    (
        SELECT l.ProductionOrderLabelId, l.ProductionOrderId, l.LabelKind, l.Seq, l.LabelNo, l.QrPayload, l.Qty, l.PalletCode,
               o.SiteCode, o.OrderNo, o.ProductPartNo, ip.Description AS ProductDescription, o.ProductionDate,
               o.ShiftCode, ISNULL(sh.ShiftName, o.ShiftCode) AS ShiftName, o.DepartmentCode, o.ProductionLineCode,
               ISNULL(p.FactoryName, N'FCC VIET NAM CO.L.T.D') AS FactoryName,
               ISNULL(p.DepartmentText, o.DepartmentCode) AS DepartmentText,
               ISNULL(p.FactoryAddress, N'ĐC: LÔ A5 , KCN THĂNG LONG , THIÊN LỘC ,HÀ NỘI') AS FactoryAddress,
               ISNULL(p.OriginText, N'Việt Nam') AS OriginText,
               COUNT(*) OVER (PARTITION BY l.ProductionOrderId, l.LabelKind) AS LabelCount,
               CASE WHEN EXISTS (SELECT 1 FROM MES.FVN_ProductionOrderLabelHistory h
                                  WHERE h.ProductionOrderLabelId = l.ProductionOrderLabelId AND h.ActionCode = 'INITIAL_PRINT')
                    THEN 1 ELSE 0 END AS IsPrinted
          FROM MES.FVN_ProductionOrderLabel l
          JOIN MES.FVN_ProductionOrder o ON o.ProductionOrderId = l.ProductionOrderId
          LEFT JOIN IFS.FVN_InventoryPart ip ON ip.SiteCode = o.SiteCode AND ip.PartNo = o.ProductPartNo
          LEFT JOIN MES.FVN_ProductionShift sh ON sh.SiteCode = o.SiteCode AND sh.ShiftCode = o.ShiftCode
          LEFT JOIN MES.FVN_ProductionLabelProfile p ON p.SiteCode = o.SiteCode
         WHERE l.ProductionOrderId = @ProductionOrderId AND l.LabelKind = @LabelKind
    )
    SELECT * FROM x WHERE @OnlyUnprinted = 0 OR IsPrinted = 0 ORDER BY Seq;
END;
GO

CREATE  PROCEDURE MES.usp_FVN_ProductionOrder_LabelLog
    @ProductionOrderId bigint,
    @ActionCode        varchar(20),
    @Reason            nvarchar(300) = NULL,
    @RequestedBy       nvarchar(100),
    @LabelIdsJson      nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @ActionCode NOT IN ('INITIAL_PRINT', 'REPRINT') THROW 54940, 'ActionCode must be INITIAL_PRINT or REPRINT.', 1;
    IF @ActionCode = 'REPRINT' AND NULLIF(LTRIM(RTRIM(@Reason)), N'') IS NULL THROW 54941, 'Reprint reason is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@RequestedBy)), N'') IS NULL THROW 54942, 'RequestedBy is required.', 1;
    DECLARE @Ids TABLE (ProductionOrderLabelId bigint PRIMARY KEY);
    INSERT @Ids SELECT DISTINCT CONVERT(bigint, [value]) FROM OPENJSON(@LabelIdsJson);
    IF NOT EXISTS (SELECT 1 FROM @Ids) THROW 54943, 'No label to log.', 1;
    IF EXISTS (SELECT 1 FROM @Ids i WHERE NOT EXISTS (
                   SELECT 1 FROM MES.FVN_ProductionOrderLabel l
                    WHERE l.ProductionOrderLabelId = i.ProductionOrderLabelId AND l.ProductionOrderId = @ProductionOrderId))
        THROW 54944, 'A label does not belong to this production order.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Lock int = (SELECT TOP (1) 1 FROM MES.FVN_ProductionOrder WITH (UPDLOCK, HOLDLOCK) WHERE ProductionOrderId = @ProductionOrderId);
        INSERT MES.FVN_ProductionOrderLabelHistory (ProductionOrderLabelId, ActionCode, Reason, RequestedBy)
        SELECT i.ProductionOrderLabelId, @ActionCode, NULLIF(LTRIM(RTRIM(@Reason)), N''), @RequestedBy
          FROM @Ids i
         WHERE @ActionCode = 'REPRINT'
            OR NOT EXISTS (SELECT 1 FROM MES.FVN_ProductionOrderLabelHistory h
                            WHERE h.ProductionOrderLabelId = i.ProductionOrderLabelId AND h.ActionCode = 'INITIAL_PRINT');
        DECLARE @Logged int = @@ROWCOUNT;
        COMMIT TRANSACTION;
        SELECT @Logged AS LoggedLabels;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '065')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('065', N'Production order carton / pallet labels, pack spec, label history');
GO
SELECT MigrationVersion, MigrationName FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '065';
GO