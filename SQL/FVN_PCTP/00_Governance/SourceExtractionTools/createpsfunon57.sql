SET NOCOUNT ON;

DECLARE @Objects TABLE (ObjectName SYSNAME);
INSERT INTO @Objects (ObjectName) VALUES
('IFSPHIEUGIAOHANGView'),
('vFVN_PXLB_ProcessReport'),
('vNhapTP'),
('ufn_QRcode_ADD_CMD_MANG'),
('sp_BaoCao_DeliveryTrace'),
('sp_GetCurrentStockStatus'),
('Usp_BarCodeView_ThongKe_Tmp1'),
('Usp_DAT_BarCodeView_ThongKe'),
('usp_Dat_Get_ItemLotCodeTP_YAM'),
('Usp_gheplotPrint'),
('Usp_ItemFifoConfig_Upsert'),
('Usp_Qrcode_gheplot'),
('Usp_Qrcode_LOAD_PHIEU_DOCQR2405'),
('Usp_Qrcode_Take_LotYMVN2405'),
('Usp_Qrcode_Update_Stock_SP'),
('Usp_Qrcode_Update_Stock2405'),
('Usp_Qrcode_Update_Stock2405.bak'),
('usp_Slot_SaveLots');

DECLARE @ObjName SYSNAME,
        @Definition NVARCHAR(MAX),
        @Type CHAR(2),
        @FullName NVARCHAR(300),
        @Kind NVARCHAR(20),
        @Pos INT,
        @Chunk NVARCHAR(4000),
        @i INT;

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT ObjectName FROM @Objects;
OPEN cur;
FETCH NEXT FROM cur INTO @ObjName;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Reset để không dính dữ liệu của vòng trước
    SET @Definition = NULL;
    SET @Type = NULL;
    SET @FullName = NULL;

    SELECT @Definition = m.definition,
           @Type       = o.type,
           @FullName   = QUOTENAME(SCHEMA_NAME(o.schema_id)) + N'.' + QUOTENAME(o.name)
    FROM sys.sql_modules m
    INNER JOIN sys.objects o ON m.object_id = o.object_id
    WHERE o.name = @ObjName
      AND o.type IN ('P','V','FN','IF','TF');

    IF @Definition IS NULL
    BEGIN
        PRINT '-- NOT FOUND: ' + @ObjName;
        PRINT '';
    END
    ELSE
    BEGIN
        SET @Kind = CASE WHEN @Type = 'P' THEN N'PROCEDURE'
                         WHEN @Type = 'V' THEN N'VIEW'
                         ELSE N'FUNCTION' END;

        PRINT '--------------------------------------------------';
        PRINT '-- OBJECT: ' + @ObjName;
        PRINT '--------------------------------------------------';

        IF @Type = 'P'
        BEGIN
            -- Tạo stub nếu chưa có, rồi ALTER (giữ permission, tương thích SQL 2014)
            PRINT 'IF OBJECT_ID(N''' + @FullName + ''') IS NULL EXEC(N''CREATE PROCEDURE ' + @FullName + ' AS RETURN 0;'');';
            PRINT 'GO';
        END
        ELSE IF @Type = 'V'
        BEGIN
            PRINT 'IF OBJECT_ID(N''' + @FullName + ''') IS NULL EXEC(N''CREATE VIEW ' + @FullName + ' AS SELECT 1 AS c;'');';
            PRINT 'GO';
        END
        ELSE
        BEGIN
            -- Function: DROP rồi CREATE lại
            PRINT 'IF OBJECT_ID(N''' + @FullName + ''') IS NOT NULL DROP FUNCTION ' + @FullName + ';';
            PRINT 'GO';
        END

        -- Với proc/view: đổi CREATE đầu tiên (theo sau là space/tab/xuống dòng) thành ALTER
        IF @Type IN ('P','V')
        BEGIN
            SET @Pos = PATINDEX(
                '%CREATE[ ' + CHAR(9) + CHAR(13) + CHAR(10) + ']%',
                @Definition COLLATE Latin1_General_CI_AS);
            IF @Pos > 0
                SET @Definition = STUFF(@Definition, @Pos, 6, 'ALTER');
        END

        -- In theo từng khúc <= 4000 ký tự, cắt tại cuối dòng (tránh PRINT bị cắt)
        WHILE DATALENGTH(@Definition) > 0
        BEGIN
            SET @Chunk = LEFT(@Definition, 4000);
            IF DATALENGTH(@Definition) / 2 > 4000
            BEGIN
                SET @i = CHARINDEX(CHAR(10), REVERSE(@Chunk));
                IF @i > 0 SET @Chunk = LEFT(@Chunk, 4000 - @i + 1);
            END
            PRINT @Chunk;
            SET @Definition = SUBSTRING(@Definition, (DATALENGTH(@Chunk) / 2) + 1, DATALENGTH(@Definition) / 2);
        END

        PRINT 'GO';
        PRINT '';
    END

    FETCH NEXT FROM cur INTO @ObjName;
END

CLOSE cur;
DEALLOCATE cur;