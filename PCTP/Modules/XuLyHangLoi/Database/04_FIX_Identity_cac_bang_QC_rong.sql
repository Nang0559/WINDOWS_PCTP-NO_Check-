/*
  Sửa HÀNG LOẠT các bảng xử lý hàng lỗi bị mất IDENTITY ở cột Id (lỗi "Cannot insert the value NULL into column 'Id'").
  Cách làm: bảng nào có cột Id KHÔNG phải IDENTITY và đang RỖNG thì DROP, sau đó chạy lại các script PHASE trong repo
  (các script này tạo lại bảng đúng IDENTITY, có IF NOT EXISTS nên bảng đang đúng không bị đụng tới).
  - Nếu bảng sai IDENTITY mà ĐÃ CÓ DỮ LIỆU: script dừng và liệt kê, không xóa gì (xử lý riêng như file 02).
  - Chạy trong 1 transaction. BACKUP DB trước khi chạy.

  Sau khi script này chạy xong, chạy tiếp theo thứ tự (thư mục PCTP/Modules/XuLyHangLoi/Database):
    PHASE3_InitialQC.sql
    PHASE4_9_ReworkDispositionCompensationWorkflowReport.sql
    PHASE13_QTChung_ReworkTables.sql
    PHASE14_ReworkExportIdempotency.sql
  (PHASE2 AffectedLot đã có file 02 riêng.)
*/
USE [B7R2_FCC];
SET XACT_ABORT ON;
GO
DECLARE @t TABLE (Ord INT, Name SYSNAME);
INSERT @t VALUES
 (1,'FVN_PXLB_CompensationMovement'),(2,'FVN_PXLB_Compensation'),(3,'FVN_PXLB_ReworkMovement'),
 (4,'FVN_PXLB_ReworkPlan'),(5,'FVN_PXLB_ReworkQC'),(6,'FVN_PXLB_Disposition'),(7,'FVN_PXLB_WorkflowAudit'),
 (8,'FVN_PhieuXuLyBatThuongQCInitial'),
 (9,'FVN_TraHangQTChungXuat'),(10,'FVN_TraHangQTChungGiao'),(11,'FVN_TraHangQTChungQC'),(12,'FVN_TraHangQTChungNhapNG');

-- Bảng sai IDENTITY
DECLARE @bad TABLE (Ord INT, Name SYSNAME, SoDong BIGINT);
INSERT @bad
SELECT x.Ord, x.Name,
       (SELECT SUM(p.rows) FROM sys.partitions p WHERE p.object_id = o.object_id AND p.index_id IN (0,1))
FROM @t x
JOIN sys.objects o ON o.object_id = OBJECT_ID(N'dbo.' + x.Name) AND o.type = 'U'
WHERE COLUMNPROPERTY(o.object_id, N'Id', 'IsIdentity') = 0;

SELECT Name AS BangSaiIdentity, SoDong FROM @bad ORDER BY Ord;

IF EXISTS (SELECT 1 FROM @bad WHERE SoDong > 0)
    THROW 61001, N'Có bảng sai IDENTITY nhưng đã có dữ liệu (xem kết quả trên). Không xóa gì - cần rebuild giữ dữ liệu như file 02.', 1;

BEGIN TRAN;
DECLARE @n SYSNAME, @sql NVARCHAR(400);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Name FROM @bad ORDER BY Ord;
OPEN c; FETCH NEXT FROM c INTO @n;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'DROP TABLE dbo.' + QUOTENAME(@n) + N';';
    EXEC (@sql);
    PRINT N'Đã drop (rỗng, sai IDENTITY): ' + @n;
    FETCH NEXT FROM c INTO @n;
END
CLOSE c; DEALLOCATE c;
COMMIT;
PRINT N'Xong. Chạy tiếp các script PHASE (ghi ở đầu file) để tạo lại bảng đúng IDENTITY.';
GO
