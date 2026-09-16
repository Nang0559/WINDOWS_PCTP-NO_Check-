/* ============================================================
   PHASE 4 -> 9 : REWORK / DISPOSITION / COMPENSATION / WORKFLOW / REPORT
   ============================================================
   Business source-of-truth:
   Phase 3 Initial QC -> SoLuongRework / SoLuongLoaiBoBanDau.
   Compensation is an independent customer obligation.
   ============================================================ */

IF OBJECT_ID(N'dbo.FVN_PXLB_ReworkPlan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_PXLB_ReworkPlan
    (
        Id INT IDENTITY(1,1) NOT NULL,
        PhieuXuLyBatThuongId INT NOT NULL,
        SoLuongKeHoach INT NOT NULL,
        SoLuongDaXuat INT NOT NULL CONSTRAINT DF_FVN_PXLB_ReworkPlan_DaXuat DEFAULT(0),
        SoLuongDaGiao INT NOT NULL CONSTRAINT DF_FVN_PXLB_ReworkPlan_DaGiao DEFAULT(0),
        Status INT NOT NULL CONSTRAINT DF_FVN_PXLB_ReworkPlan_Status DEFAULT(0),
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_FVN_PXLB_ReworkPlan_CreatedAt DEFAULT(GETDATE()),
        CreatedBy NVARCHAR(100) NOT NULL,
        UpdatedAt DATETIME NULL,
        UpdatedBy NVARCHAR(100) NULL,
        CONSTRAINT PK_FVN_PXLB_ReworkPlan PRIMARY KEY(Id),
        CONSTRAINT FK_FVN_PXLB_ReworkPlan_Phieu FOREIGN KEY(PhieuXuLyBatThuongId)
            REFERENCES dbo.FVN_PhieuXuLyBatThuong(Id),
        CONSTRAINT UQ_FVN_PXLB_ReworkPlan_Phieu UNIQUE(PhieuXuLyBatThuongId),
        CONSTRAINT CK_FVN_PXLB_ReworkPlan_Qty CHECK
        (SoLuongKeHoach >= 0 AND SoLuongDaXuat >= 0 AND SoLuongDaGiao >= 0
         AND SoLuongDaXuat <= SoLuongKeHoach AND SoLuongDaGiao <= SoLuongDaXuat)
    );
END;
GO

IF OBJECT_ID(N'dbo.FVN_PXLB_ReworkMovement', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_PXLB_ReworkMovement
    (
        Id INT IDENTITY(1,1) NOT NULL,
        PhieuXuLyBatThuongId INT NOT NULL,
        ReworkPlanId INT NOT NULL,
        MovementType INT NOT NULL, /* 1=Export, 2=Delivery */
        SlotId INT NULL,
        LotNo NVARCHAR(100) NOT NULL,
        MaSanPham NVARCHAR(100) NULL,
        SoLuong INT NOT NULL,
        ThoiGian DATETIME NOT NULL CONSTRAINT DF_FVN_PXLB_ReworkMovement_Time DEFAULT(GETDATE()),
        NguoiThucHien NVARCHAR(100) NOT NULL,
        GhiChu NVARCHAR(1000) NULL,
        CONSTRAINT PK_FVN_PXLB_ReworkMovement PRIMARY KEY(Id),
        CONSTRAINT FK_FVN_PXLB_ReworkMovement_Phieu FOREIGN KEY(PhieuXuLyBatThuongId)
            REFERENCES dbo.FVN_PhieuXuLyBatThuong(Id),
        CONSTRAINT FK_FVN_PXLB_ReworkMovement_Plan FOREIGN KEY(ReworkPlanId)
            REFERENCES dbo.FVN_PXLB_ReworkPlan(Id),
        CONSTRAINT CK_FVN_PXLB_ReworkMovement_Qty CHECK(SoLuong > 0),
        CONSTRAINT CK_FVN_PXLB_ReworkMovement_Type CHECK(MovementType IN(1,2))
    );
END;
GO

IF OBJECT_ID(N'dbo.FVN_PXLB_ReworkQC', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_PXLB_ReworkQC
    (
        Id INT IDENTITY(1,1) NOT NULL,
        PhieuXuLyBatThuongId INT NOT NULL,
        SoLuongRework INT NOT NULL,
        SoLuongOK INT NOT NULL,
        SoLuongNG INT NOT NULL,
        ConfirmedAt DATETIME NOT NULL CONSTRAINT DF_FVN_PXLB_ReworkQC_Time DEFAULT(GETDATE()),
        ConfirmedBy NVARCHAR(100) NOT NULL,
        KetLuan NVARCHAR(1000) NULL,
        CONSTRAINT PK_FVN_PXLB_ReworkQC PRIMARY KEY(Id),
        CONSTRAINT FK_FVN_PXLB_ReworkQC_Phieu FOREIGN KEY(PhieuXuLyBatThuongId)
            REFERENCES dbo.FVN_PhieuXuLyBatThuong(Id),
        CONSTRAINT UQ_FVN_PXLB_ReworkQC_Phieu UNIQUE(PhieuXuLyBatThuongId),
        CONSTRAINT CK_FVN_PXLB_ReworkQC_Qty CHECK
        (SoLuongRework >= 0 AND SoLuongOK >= 0 AND SoLuongNG >= 0
         AND SoLuongRework = SoLuongOK + SoLuongNG)
    );
END;
GO

IF OBJECT_ID(N'dbo.FVN_PXLB_Disposition', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_PXLB_Disposition
    (
        Id INT IDENTITY(1,1) NOT NULL,
        PhieuXuLyBatThuongId INT NOT NULL,
        SoLuongLoaiBoBanDau INT NOT NULL,
        SoLuongNGRework INT NOT NULL,
        SoLuongLoaiBoCuoi INT NOT NULL,
        Reason NVARCHAR(1000) NULL,
        ConfirmedAt DATETIME NOT NULL CONSTRAINT DF_FVN_PXLB_Disposition_Time DEFAULT(GETDATE()),
        ConfirmedBy NVARCHAR(100) NOT NULL,
        CONSTRAINT PK_FVN_PXLB_Disposition PRIMARY KEY(Id),
        CONSTRAINT FK_FVN_PXLB_Disposition_Phieu FOREIGN KEY(PhieuXuLyBatThuongId)
            REFERENCES dbo.FVN_PhieuXuLyBatThuong(Id),
        CONSTRAINT UQ_FVN_PXLB_Disposition_Phieu UNIQUE(PhieuXuLyBatThuongId),
        CONSTRAINT CK_FVN_PXLB_Disposition_Qty CHECK
        (SoLuongLoaiBoBanDau >= 0 AND SoLuongNGRework >= 0 AND SoLuongLoaiBoCuoi >= 0
         AND SoLuongLoaiBoCuoi = SoLuongLoaiBoBanDau + SoLuongNGRework)
    );
END;
GO

IF OBJECT_ID(N'dbo.FVN_PXLB_Compensation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_PXLB_Compensation
    (
        Id INT IDENTITY(1,1) NOT NULL,
        PhieuXuLyBatThuongId INT NOT NULL,
        SoLuongYeuCau INT NOT NULL,
        SoLuongDaGiao INT NOT NULL CONSTRAINT DF_FVN_PXLB_Comp_DaGiao DEFAULT(0),
        Status INT NOT NULL CONSTRAINT DF_FVN_PXLB_Comp_Status DEFAULT(0),
        SourceReference NVARCHAR(200) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_FVN_PXLB_Comp_CreatedAt DEFAULT(GETDATE()),
        CreatedBy NVARCHAR(100) NOT NULL,
        CompletedAt DATETIME NULL,
        CompletedBy NVARCHAR(100) NULL,
        CONSTRAINT PK_FVN_PXLB_Compensation PRIMARY KEY(Id),
        CONSTRAINT FK_FVN_PXLB_Compensation_Phieu FOREIGN KEY(PhieuXuLyBatThuongId)
            REFERENCES dbo.FVN_PhieuXuLyBatThuong(Id),
        CONSTRAINT UQ_FVN_PXLB_Compensation_Phieu UNIQUE(PhieuXuLyBatThuongId),
        CONSTRAINT CK_FVN_PXLB_Compensation_Qty CHECK
        (SoLuongYeuCau >= 0 AND SoLuongDaGiao >= 0 AND SoLuongDaGiao <= SoLuongYeuCau)
    );
END;
GO

IF OBJECT_ID(N'dbo.FVN_PXLB_CompensationMovement', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_PXLB_CompensationMovement
    (
        Id INT IDENTITY(1,1) NOT NULL,
        CompensationId INT NOT NULL,
        LotNo NVARCHAR(100) NOT NULL,
        SlotId INT NULL,
        SoLuong INT NOT NULL,
        ThoiGian DATETIME NOT NULL CONSTRAINT DF_FVN_PXLB_CompMove_Time DEFAULT(GETDATE()),
        NguoiThucHien NVARCHAR(100) NOT NULL,
        CONSTRAINT PK_FVN_PXLB_CompensationMovement PRIMARY KEY(Id),
        CONSTRAINT FK_FVN_PXLB_CompMove_Comp FOREIGN KEY(CompensationId)
            REFERENCES dbo.FVN_PXLB_Compensation(Id),
        CONSTRAINT CK_FVN_PXLB_CompMove_Qty CHECK(SoLuong > 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.FVN_PXLB_WorkflowAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_PXLB_WorkflowAudit
    (
        Id INT IDENTITY(1,1) NOT NULL,
        PhieuXuLyBatThuongId INT NOT NULL,
        FromStatus INT NOT NULL,
        ToStatus INT NOT NULL,
        ActionName NVARCHAR(100) NOT NULL,
        Actor NVARCHAR(100) NOT NULL,
        OccurredAt DATETIME NOT NULL CONSTRAINT DF_FVN_PXLB_WorkflowAudit_Time DEFAULT(GETDATE()),
        Note NVARCHAR(1000) NULL,
        CONSTRAINT PK_FVN_PXLB_WorkflowAudit PRIMARY KEY(Id),
        CONSTRAINT FK_FVN_PXLB_WorkflowAudit_Phieu FOREIGN KEY(PhieuXuLyBatThuongId)
            REFERENCES dbo.FVN_PhieuXuLyBatThuong(Id)
    );
END;
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.FVN_PXLB_ReworkMovement') AND name=N'IX_FVN_PXLB_ReworkMovement_Phieu')
    CREATE INDEX IX_FVN_PXLB_ReworkMovement_Phieu ON dbo.FVN_PXLB_ReworkMovement(PhieuXuLyBatThuongId, MovementType);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.FVN_PXLB_WorkflowAudit') AND name=N'IX_FVN_PXLB_WorkflowAudit_Phieu')
    CREATE INDEX IX_FVN_PXLB_WorkflowAudit_Phieu ON dbo.FVN_PXLB_WorkflowAudit(PhieuXuLyBatThuongId, OccurredAt, Id);
GO

/* Reporting view: 1 dòng / phiếu, không suy diễn Compensation từ NG. */
IF OBJECT_ID(N'dbo.vFVN_PXLB_ProcessReport', N'V') IS NOT NULL
    DROP VIEW dbo.vFVN_PXLB_ProcessReport;
GO
CREATE VIEW dbo.vFVN_PXLB_ProcessReport
AS
SELECT
    p.Id AS PhieuXuLyBatThuongId,
    p.SoPhieu,
    p.Model,
    p.MaSanPham,
    p.Status,
    p.HuongXuLy,
    qc.SoLuongAnhHuong,
    qc.SoLuongOK AS InitialOK,
    qc.SoLuongNG AS InitialNG,
    qc.SoLuongRework,
    qc.SoLuongLoaiBoBanDau,
    rp.SoLuongDaXuat AS ReworkDaXuat,
    rp.SoLuongDaGiao AS ReworkDaGiao,
    rq.SoLuongOK AS ReworkOK,
    rq.SoLuongNG AS ReworkNG,
    d.SoLuongLoaiBoCuoi,
    c.SoLuongYeuCau AS CompensationRequired,
    c.SoLuongDaGiao AS CompensationDelivered,
    CASE WHEN c.Id IS NULL THEN 0 ELSE c.SoLuongYeuCau - c.SoLuongDaGiao END AS CompensationRemaining
FROM dbo.FVN_PhieuXuLyBatThuong p
LEFT JOIN dbo.FVN_PhieuXuLyBatThuongQCInitial qc ON qc.PhieuXuLyBatThuongId=p.Id
LEFT JOIN dbo.FVN_PXLB_ReworkPlan rp ON rp.PhieuXuLyBatThuongId=p.Id
LEFT JOIN dbo.FVN_PXLB_ReworkQC rq ON rq.PhieuXuLyBatThuongId=p.Id
LEFT JOIN dbo.FVN_PXLB_Disposition d ON d.PhieuXuLyBatThuongId=p.Id
LEFT JOIN dbo.FVN_PXLB_Compensation c ON c.PhieuXuLyBatThuongId=p.Id;
GO
