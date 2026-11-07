/*
  Tạo bảng dbo.InspectionLog (lịch sử QC / Inspection) nếu DB hiện tại chưa có.
  Lỗi gốc ở form "Lịch sử QC / Inspection": Invalid object name 'InspectionLog'.
  - Chạy trên ĐÚNG database mà app kết nối (chuỗi B7R2_FCCdb trong PCTP.Database.config), KHÔNG tự đổi USE.
  - Cột và kiểu lấy từ DB57 (docs/SQL.text); LogId là IDENTITY vì code INSERT không đưa LogId.
  - Chạy lại an toàn.
*/
SET XACT_ABORT ON;
GO
SELECT DB_NAME() AS DangChayTrenDB, OBJECT_ID(N'dbo.InspectionLog', N'U') AS InspectionLog_ObjectId;

-- Nếu bảng đã nằm ở schema khác thì liệt kê để xem trước khi tạo mới
SELECT s.name AS SchemaName, t.name AS TableName
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.name = N'InspectionLog';

IF OBJECT_ID(N'dbo.InspectionLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InspectionLog
    (
        LogId          INT IDENTITY(1,1) NOT NULL,
        InspectionCode NVARCHAR(100)  NOT NULL,
        ItemCode       NVARCHAR(200)  NULL,
        TemCodeTong    NVARCHAR(400)  NULL,
        LotNoTong      NVARCHAR(400)  NULL,
        NSXTong        NVARCHAR(100)  NULL,
        SoLuongTong    INT            NULL,
        MaPhieu        NVARCHAR(200)  NULL,
        BoxTemCode     NVARCHAR(400)  NULL,
        BoxLotNo       NVARCHAR(400)  NULL,
        BoxNSX         NVARCHAR(100)  NULL,
        IsMatch        BIT            NULL CONSTRAINT DF_InspectionLog_IsMatch   DEFAULT ((1)),
        MismatchFields NVARCHAR(1000) NULL,
        FinalResult    NVARCHAR(40)   NULL,
        CheckedAt      DATETIME       NULL CONSTRAINT DF_InspectionLog_CheckedAt DEFAULT (GETDATE()),
        CheckedBy      NVARCHAR(200)  NULL,
        CONSTRAINT PK__Inspecti__5E54864800C0C605 PRIMARY KEY CLUSTERED (LogId)
    );

    CREATE INDEX IX_InspectionLog_InspectionCode ON dbo.InspectionLog (InspectionCode, LogId);
    CREATE INDEX IX_InspectionLog_ItemCode_CheckedAt ON dbo.InspectionLog (ItemCode, CheckedAt);
    PRINT N'Đã tạo dbo.InspectionLog.';
END
ELSE
    PRINT N'dbo.InspectionLog đã tồn tại - không làm gì.';

SELECT COLUMNPROPERTY(OBJECT_ID(N'dbo.InspectionLog'), N'LogId', 'IsIdentity') AS LogId_IsIdentity;
GO
