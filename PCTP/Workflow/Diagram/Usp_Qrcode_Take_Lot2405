USE [B7R2_FCC]
GO

/****** Object:  StoredProcedure [dbo].[Usp_Qrcode_Take_Lot2405]    Script Date: 9/8/2026 3:29:19 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

create PROC [dbo].[Usp_Qrcode_Take_Lot2405]
    @_MaFCC     NVARCHAR(50) = N'',
    @_STTP      INT          = 0,
    @_DeM       INT          = 0,
    @_SLGIAO    INT          = 0,
    -- ── THÊM 2 THAM SỐ MỚI ─────────────────────────────────────────
    @DOCQRTABLE NVARCHAR(128) = N'DOCQRCODE',
    @TMPTABLE   NVARCHAR(128) = N'TMPPHIEUGIAOHANG'
AS
BEGIN
    SET NOCOUNT ON;

    -- ── WHITELIST chống SQL injection ───────────────────────────────
    IF @DOCQRTABLE NOT IN (
        N'DOCQRCODE',
        N'DOCQRCODE_SP',
        N'DOCQRCODE_100003'
    )
    BEGIN
        RAISERROR(N'Tên bảng DOCQRTABLE không hợp lệ: %s', 16, 1, @DOCQRTABLE);
        RETURN;
    END

    IF @TMPTABLE NOT IN (
        N'TMPPHIEUGIAOHANG',
        N'TMPPHIEUGIAOHANG_SP',
        N'TMPPHIEUGIAOHANG_100003'
    )
    BEGIN
        RAISERROR(N'Tên bảng TMPTABLE không hợp lệ: %s', 16, 1, @TMPTABLE);
        RETURN;
    END

    DECLARE
        @_LOTHVN    NVARCHAR(24),
        @_LOTFCC    NVARCHAR(56),
        @_SLTEMFCC  INT,
        @_SLTEMHVN  INT,
        @_ID        NVARCHAR(10),
        @_Count_R   INT,
        @_STTDOC    INT,
        @SLTT       INT,
        @sql        NVARCHAR(MAX)

    SELECT @_ID = STUFF('00000', 5-LEN(Id)+1, LEN(Id), Id)
    FROM B20ITEM WHERE code = @_MaFCC

    -- ── Đếm tổng SLTEMHVN từ DOCQRTABLE động ───────────────────────
    SET @sql = N'SELECT @cnt = SUM(SLTEMHVN) FROM [' + @DOCQRTABLE + N']
                 WHERE MAHANGFCC = @ma AND KETQUA <> ''DG'''
    EXEC sp_executesql @sql,
        N'@cnt INT OUTPUT, @ma NVARCHAR(50)',
        @_SLTEMFCC OUTPUT, @_MaFCC

    IF OBJECT_ID('TempDb..#CtTmp') IS NOT NULL DROP TABLE #CtTmp
    SELECT TOP 0 LOTFCC, SLTEMFCC, SLTEMHVN
    INTO #CtTmp
    FROM DOCQRCODE  -- chỉ dùng để lấy schema, không lấy data

    SET @SLTT = 0

    -- ── Nạp dữ liệu QR vào bảng tạm để CURSOR không dùng dynamic ───
    IF OBJECT_ID('TempDb..#QRData') IS NOT NULL DROP TABLE #QRData
    CREATE TABLE #QRData (
        LOTFCC   NVARCHAR(56),
        SLTEMFCC INT,
        SLTEMHVN INT,
        STT      INT
    )

    SET @sql = N'
        INSERT INTO #QRData (LOTFCC, SLTEMFCC, SLTEMHVN, STT)
        SELECT LOTFCC, SLTEMFCC, SLTEMHVN, STT
        FROM [' + @DOCQRTABLE + N']
        WHERE MAHANGFCC = @ma AND KETQUA <> ''DG''
        ORDER BY STT'
    EXEC sp_executesql @sql, N'@ma NVARCHAR(50)', @_MaFCC

    -- ── CURSOR đọc từ #QRData (không dynamic) ───────────────────────
    DECLARE QRcode CURSOR FOR
        SELECT LOTFCC, SLTEMFCC, SLTEMHVN, STT FROM #QRData

    OPEN QRcode
    FETCH NEXT FROM QRcode INTO @_LOTFCC, @_SLTEMFCC, @_SLTEMHVN, @_STTDOC

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF (@SLTT < @_SLGIAO)
        BEGIN
            SET @SLTT = @SLTT + @_SLTEMFCC

            -- UPDATE STTBAN vào DOCQRTABLE động
            SET @sql = N'UPDATE [' + @DOCQRTABLE + N']
                         SET STTBAN = @sttp
                         WHERE STT = @sttdoc AND MAHANGFCC = @ma'
            EXEC sp_executesql @sql,
                N'@sttp INT, @sttdoc INT, @ma NVARCHAR(50)',
                @_STTP, @_STTDOC, @_MaFCC

            IF (CHARINDEX(',', @_LOTFCC) <> 0)
            BEGIN
                DECLARE @pos INT, @posTach INT
                DECLARE @len INT, @lenTach INT
                DECLARE @value NVARCHAR(56), @valueLOT NVARCHAR(30), @valueSL NVARCHAR(5)
                SET @pos = 0
                SET @len = 0

                WHILE CHARINDEX(',', CONCAT(RTRIM(@_LOTFCC), ','), @pos+1) > 0
                BEGIN
                    SET @len   = CHARINDEX(',', CONCAT(RTRIM(@_LOTFCC), ','), @pos+1) - @pos
                    SET @value = SUBSTRING(CONCAT(RTRIM(@_LOTFCC), ','), @pos, @len)

                    IF (@value <> '')
                    BEGIN
                        SET @lenTach  = CHARINDEX('-', @value, 1)
                        SET @valueLOT = SUBSTRING(@value, 1, @lenTach-1)
                        SET @valueSL  = SUBSTRING(@value, @lenTach+1, LEN(@value)-@lenTach)

                        IF EXISTS (SELECT 1 FROM #CtTmp WHERE LOTFCC = @valueLOT)
                            UPDATE #CtTmp SET SLTEMFCC = SLTEMFCC + @valueSL WHERE LOTFCC = @valueLOT
                        ELSE
                            INSERT INTO #CtTmp VALUES (@valueLOT, @valueSL, @valueSL)
                    END

                    SET @pos = CHARINDEX(',', CONCAT(RTRIM(@_LOTFCC), ','), @pos+@len) + 1
                END
            END
            ELSE
            BEGIN
                IF EXISTS (SELECT 1 FROM #CtTmp WHERE LOTFCC = @_LOTFCC)
                    UPDATE #CtTmp SET SLTEMFCC = SLTEMFCC + @_SLTEMFCC WHERE LOTFCC = RTRIM(@_LOTFCC)
                ELSE
                    INSERT INTO #CtTmp VALUES (RTRIM(@_LOTFCC), @_SLTEMFCC, @_SLTEMHVN)
            END
        END

        FETCH NEXT FROM QRcode INTO @_LOTFCC, @_SLTEMFCC, @_SLTEMHVN, @_STTDOC
    END

    CLOSE QRcode
    DEALLOCATE QRcode

    -- ── Xử lý kết quả ───────────────────────────────────────────────
    IF (@SLTT = @_SLGIAO)
    BEGIN
        -- UPDATE DOCQRTABLE động
        SET @sql = N'
            UPDATE [' + @DOCQRTABLE + N']
            SET KETQUA = ''DG'', STTBAN = @sttp, GIO = @sttp
            WHERE STTBAN = @sttp AND MAHANGFCC = @ma'
        EXEC sp_executesql @sql,
            N'@sttp INT, @ma NVARCHAR(50)',
            @_STTP, @_MaFCC

        -- UPDATE TMPTABLE động
        SET @sql = N'
            UPDATE [' + @TMPTABLE + N']
            SET TTPHIEU = N''Đã Thống Kê QR'', STATUSDOC = ''OK''
            WHERE STT = @sttp AND MAHANG = @ma'
        EXEC sp_executesql @sql,
            N'@sttp INT, @ma NVARCHAR(50)',
            @_STTP, @_MaFCC

        SET @SLTT = 0
        SELECT LOTFCC, SUM(SLTEMFCC) AS FCC, SUM(SLTEMHVN) AS HVN
        FROM #CtTmp GROUP BY LOTFCC
    END
    ELSE
    BEGIN
        -- Rollback STTBAN
        SET @sql = N'
            UPDATE [' + @DOCQRTABLE + N']
            SET STTBAN = NULL
            WHERE STTBAN = @sttp AND MAHANGFCC = @ma'
        EXEC sp_executesql @sql,
            N'@sttp INT, @ma NVARCHAR(50)',
            @_STTP, @_MaFCC

        SET @SLTT = 0
    END

END
GO


