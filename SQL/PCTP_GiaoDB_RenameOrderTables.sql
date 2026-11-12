/*
 PCTP - Giao DB: đổi tên bảng NGHIỆP VỤ cho khỏi nhầm với bảng tạm (TMP).

     TMPPHIEUGIAODBHD        ->  FVN_Purchase_Order_DB_HD   (header phiếu Giao DB)
     TMPPHIEUGIAOHANGDBCT    ->  FVN_Purchase_Order_DB_CT   (chi tiết phiếu Giao DB)

 Bảng làm việc TMPPHIEUGIAOHANGDB (và _IFS) KHÔNG đổi: đó mới là bảng tạm, SP
 Usp_Qrcode_LOAD_PHIEU_DOCQR2405 xóa và dựng lại mỗi lần upload.

 Đặc điểm:
   - Idempotent: chạy lại không lỗi nếu đã đổi tên.
   - Một transaction: lỗi giữa chừng thì rollback toàn bộ.
   - Từ chối chạy nếu còn view/SP/function tham chiếu tên cũ (sp_rename KHÔNG sửa các đối tượng đó).
     Sửa chúng trước, hoặc đặt @AllowDependents = 1 khi đã biết rõ.
   - Tự dò tên PK/FK hiện có (tên tự sinh PK__TMPPHIEU__... khác nhau giữa các DB) rồi đổi sang tên chuẩn.

 TRIỂN KHAI: app phiên bản cũ sẽ lỗi "Invalid object name" ngay sau khi đổi tên.
   Nếu còn máy chạy bản cũ trong giai đoạn chuyển tiếp, đặt @CreateCompatSynonyms = 1 để tạo synonym
   tên cũ trỏ sang tên mới (SELECT/INSERT/UPDATE vẫn chạy). Xóa synonym khi mọi máy đã lên bản mới.
   Thử trên DB test trước khi chạy production.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @AllowDependents      bit = 0;
DECLARE @CreateCompatSynonyms bit = 0;

BEGIN TRANSACTION;

------------------------------------------------------------------------------
-- 0. Kiểm tra trước khi đổi
------------------------------------------------------------------------------
-- 0a. Bảng TÊN MỚI đã có sẵn nhưng RỖNG (vd bị tạo nhầm từ 02_Generated_From_DB57.sql, cột hẹp
--     hơn bảng thật -> lỗi "String or binary data would be truncated"): xóa bảng rỗng đó để đổi tên
--     bảng cũ (còn dữ liệu) sang. Nếu bảng mới đã có dòng dữ liệu thì KHÔNG tự xóa (xem 53001/53002).
IF OBJECT_ID(N'dbo.TMPPHIEUGIAODBHD', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_HD', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_CT', N'U') IS NOT NULL
BEGIN
    DECLARE @newRows int;
    EXEC sp_executesql
        N'SELECT @n = (SELECT COUNT(*) FROM dbo.FVN_Purchase_Order_DB_HD) + (SELECT COUNT(*) FROM dbo.FVN_Purchase_Order_DB_CT)',
        N'@n int OUTPUT', @newRows OUTPUT;

    IF @newRows = 0
    BEGIN
        EXEC (N'DROP TABLE dbo.FVN_Purchase_Order_DB_CT');
        EXEC (N'DROP TABLE dbo.FVN_Purchase_Order_DB_HD');
        PRINT N'Đã xóa 2 bảng tên mới RỖNG (tạo nhầm); sẽ đổi tên bảng cũ còn dữ liệu.';
    END;
END;

IF OBJECT_ID(N'dbo.TMPPHIEUGIAODBHD', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_HD', N'U') IS NOT NULL
    THROW 53001, N'HD: cả bảng cũ TMPPHIEUGIAODBHD và bảng mới FVN_Purchase_Order_DB_HD cùng tồn tại. Kiểm tra thủ công, script không tự gộp dữ liệu.', 1;

IF OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_CT', N'U') IS NOT NULL
    THROW 53002, N'CT: cả bảng cũ TMPPHIEUGIAOHANGDBCT và bảng mới FVN_Purchase_Order_DB_CT cùng tồn tại. Kiểm tra thủ công, script không tự gộp dữ liệu.', 1;

IF OBJECT_ID(N'dbo.TMPPHIEUGIAODBHD', N'U') IS NULL
   AND OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_HD', N'U') IS NULL
    THROW 53003, N'Không tìm thấy bảng HD (cả tên cũ lẫn tên mới). Đang chạy nhầm database?', 1;

IF OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT', N'U') IS NULL
   AND OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_CT', N'U') IS NULL
    THROW 53004, N'Không tìm thấy bảng CT (cả tên cũ lẫn tên mới). Đang chạy nhầm database?', 1;

IF @AllowDependents = 0
   AND EXISTS (SELECT 1
               FROM sys.sql_expression_dependencies d
               WHERE d.referenced_id IN (OBJECT_ID(N'dbo.TMPPHIEUGIAODBHD'),
                                         OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT')))
BEGIN
    SELECT OBJECT_SCHEMA_NAME(d.referencing_id) AS referencing_schema,
           OBJECT_NAME(d.referencing_id)        AS referencing_object,
           d.referenced_entity_name             AS dang_tham_chieu_ten_cu
    FROM sys.sql_expression_dependencies d
    WHERE d.referenced_id IN (OBJECT_ID(N'dbo.TMPPHIEUGIAODBHD'),
                              OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT'));

    THROW 53005, N'Còn view/SP/function tham chiếu tên bảng cũ (xem danh sách ở trên). Sửa chúng sang tên mới rồi chạy lại, hoặc đặt @AllowDependents = 1.', 1;
END;

------------------------------------------------------------------------------
-- 1. Đổi tên bảng
------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.TMPPHIEUGIAODBHD', N'U') IS NOT NULL
    EXEC sp_rename N'dbo.TMPPHIEUGIAODBHD', N'FVN_Purchase_Order_DB_HD', N'OBJECT';

IF OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT', N'U') IS NOT NULL
    EXEC sp_rename N'dbo.TMPPHIEUGIAOHANGDBCT', N'FVN_Purchase_Order_DB_CT', N'OBJECT';

------------------------------------------------------------------------------
-- 2. Đổi tên PK / FK sang tên chuẩn (tên cũ có thể là hash tự sinh, khác nhau giữa các DB)
------------------------------------------------------------------------------
DECLARE @name sysname, @full nvarchar(300);

SELECT @name = kc.name
FROM sys.key_constraints kc
WHERE kc.parent_object_id = OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_HD') AND kc.type = 'PK';
IF @name IS NOT NULL AND @name <> N'PK_FVN_Purchase_Order_DB_HD'
BEGIN
    SET @full = N'dbo.' + QUOTENAME(@name);
    EXEC sp_rename @full, N'PK_FVN_Purchase_Order_DB_HD', N'OBJECT';
END;

SET @name = NULL;
SELECT @name = kc.name
FROM sys.key_constraints kc
WHERE kc.parent_object_id = OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_CT') AND kc.type = 'PK';
IF @name IS NOT NULL AND @name <> N'PK_FVN_Purchase_Order_DB_CT'
BEGIN
    SET @full = N'dbo.' + QUOTENAME(@name);
    EXEC sp_rename @full, N'PK_FVN_Purchase_Order_DB_CT', N'OBJECT';
END;

SET @name = NULL;
SELECT TOP (1) @name = fk.name
FROM sys.foreign_keys fk
WHERE fk.parent_object_id     = OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_CT')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_HD')
ORDER BY fk.object_id;
IF @name IS NOT NULL AND @name <> N'FK_FVN_Purchase_Order_DB_CT_HD'
BEGIN
    SET @full = N'dbo.' + QUOTENAME(@name);
    EXEC sp_rename @full, N'FK_FVN_Purchase_Order_DB_CT_HD', N'OBJECT';
END;

------------------------------------------------------------------------------
-- 3. (Tuỳ chọn) Synonym tên cũ cho giai đoạn chuyển tiếp
------------------------------------------------------------------------------
IF @CreateCompatSynonyms = 1
BEGIN
    IF OBJECT_ID(N'dbo.TMPPHIEUGIAODBHD') IS NULL
        EXEC (N'CREATE SYNONYM dbo.TMPPHIEUGIAODBHD FOR dbo.FVN_Purchase_Order_DB_HD');
    IF OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT') IS NULL
        EXEC (N'CREATE SYNONYM dbo.TMPPHIEUGIAOHANGDBCT FOR dbo.FVN_Purchase_Order_DB_CT');
END;

------------------------------------------------------------------------------
-- 4. Xác nhận kết quả
------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_HD', N'U') IS NULL THROW 53010, N'Sau khi đổi: thiếu FVN_Purchase_Order_DB_HD.', 1;
IF OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_CT', N'U') IS NULL THROW 53011, N'Sau khi đổi: thiếu FVN_Purchase_Order_DB_CT.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id     = OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_CT')
                 AND referenced_object_id = OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_HD'))
    THROW 53012, N'Sau khi đổi: mất liên kết khóa ngoại CT -> HD.', 1;

COMMIT TRANSACTION;

SELECT t.name AS bang, (SELECT SUM(p.rows) FROM sys.partitions p
                        WHERE p.object_id = t.object_id AND p.index_id IN (0,1)) AS so_dong
FROM sys.tables t
WHERE t.name IN (N'FVN_Purchase_Order_DB_HD', N'FVN_Purchase_Order_DB_CT');

/*
 ROLLBACK thủ công (nếu cần quay lại tên cũ):
   EXEC sp_rename N'dbo.FVN_Purchase_Order_DB_HD', N'TMPPHIEUGIAODBHD',     N'OBJECT';
   EXEC sp_rename N'dbo.FVN_Purchase_Order_DB_CT', N'TMPPHIEUGIAOHANGDBCT', N'OBJECT';
   -- (đổi lại PK/FK nếu cần; nếu đã bật synonym thì DROP SYNONYM trước)
*/
