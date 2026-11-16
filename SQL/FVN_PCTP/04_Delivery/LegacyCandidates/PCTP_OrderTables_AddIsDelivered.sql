/*
  PCTP_OrderTables_AddIsDelivered.sql
  Thêm cột IsDelivered / DeliveredDate cho các bảng đơn riêng nếu CHƯA có, để màn "Đơn đã upload"
  (UploadedOrderManagerControl) biết dòng nào đã giao và cho sửa/xóa dòng chưa giao.

  - Chạy được nhiều lần (cột đã có thì bỏ qua); bảng không tồn tại thì bỏ qua.
  - LƯU Ý: mọi dòng CŨ sẽ có IsDelivered = 0 (chưa giao). Nếu bảng đã có dòng đã giao từ trước khi có cột này,
    hãy đánh dấu chúng = 1 TRƯỚC khi cho người dùng sửa/xóa. Nếu DB của bạn đã có cột (code giao hàng đã
    dùng IsDelivered) thì KHÔNG cần chạy script này.
  - Chạy trên DB test trước.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @bang TABLE (ten sysname);
INSERT INTO @bang (ten) VALUES (N'Purchase_Order_YMVN'), (N'Purchase_Order_HTN');

DECLARE @t sysname, @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT ten FROM @bang;
OPEN c;
FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + QUOTENAME(@t), N'U') IS NULL
        PRINT N'Bỏ qua (không có bảng): ' + @t;
    ELSE
    BEGIN
        IF COL_LENGTH(N'dbo.' + @t, N'IsDelivered') IS NULL
        BEGIN
            SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@t) +
                       N' ADD IsDelivered bit NOT NULL CONSTRAINT ' + QUOTENAME(N'DF_' + @t + N'_IsDelivered') +
                       N' DEFAULT (0)';
            EXEC (@sql);
            PRINT N'Đã thêm IsDelivered vào ' + @t;
        END
        ELSE PRINT N'Đã có IsDelivered: ' + @t;

        IF COL_LENGTH(N'dbo.' + @t, N'DeliveredDate') IS NULL
        BEGIN
            SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@t) + N' ADD DeliveredDate datetime NULL';
            EXEC (@sql);
            PRINT N'Đã thêm DeliveredDate vào ' + @t;
        END
    END
    FETCH NEXT FROM c INTO @t;
END
CLOSE c; DEALLOCATE c;

COMMIT TRANSACTION;
