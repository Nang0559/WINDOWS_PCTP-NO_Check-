USE [B7R2_FCC]
GO

/****** Object:  StoredProcedure [dbo].[Usp_Qrcode_Take_LotYMVN2405]    Script Date: 9/8/2026 3:30:27 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROC [dbo].[Usp_Qrcode_Take_LotYMVN2405]       
    @TMPTABLE    nvarchar(100) = N'TMPPHIEUGIAOHANG',       
    @DOCQRTABLE  nvarchar(100) = N'DOCQRCODE',       
    @ISLOAISP    int           = 0       
AS       
BEGIN       
    SET NOCOUNT ON;       
    SET XACT_ABORT ON;

    -- YMVN/HTN have their own configured table families. These identifiers
    -- are intentionally separate from the HVN exact-allocation procedure.
    IF @TMPTABLE IS NULL OR @TMPTABLE NOT IN (
        N'TMPPHIEUGIAOHANG',
        N'TMPPHIEUGIAOHANG_100002',
        N'TMPPHIEUGIAOHANG_100003',
        N'TMPPHIEUGIAOHANG_SP',
        N'SP_TMPPHIEUGIAOHANG'
    )
    BEGIN
        THROW 51201, N'Tên bảng YMVN TMPTABLE không hợp lệ.', 1;
    END;

    IF @DOCQRTABLE IS NULL OR @DOCQRTABLE NOT IN (
        N'DOCQRCODE',
        N'YMVN_DOCQRCODE',
        N'DOCQRCODE_100003',
        N'DOCQRCODE_SP',
        N'SP_DOCQRCODE'
    )
    BEGIN
        THROW 51202, N'Tên bảng YMVN DOCQRTABLE không hợp lệ.', 1;
    END;

    IF @ISLOAISP IS NULL OR @ISLOAISP NOT IN (0, 1)
        THROW 51203, N'Loại phiếu YMVN không hợp lệ.', 1;

    IF @ISLOAISP = 1
       AND (@TMPTABLE <> N'SP_TMPPHIEUGIAOHANG' OR @DOCQRTABLE <> N'SP_DOCQRCODE')
        THROW 51204, N'YMVN SP phải sử dụng đúng cặp bảng SP đã cấu hình.', 1;

    IF @ISLOAISP = 0
       AND (@TMPTABLE = N'SP_TMPPHIEUGIAOHANG' OR @DOCQRTABLE = N'SP_DOCQRCODE')
        THROW 51205, N'Không được dùng bảng SP cho luồng YMVN MP.', 1;

    IF @TMPTABLE = N'TMPPHIEUGIAOHANG_100002' AND @DOCQRTABLE <> N'YMVN_DOCQRCODE'
        THROW 51206, N'Cặp bảng YMVN MP không khớp cấu hình.', 1;

    IF @TMPTABLE = N'TMPPHIEUGIAOHANG_100003' AND @DOCQRTABLE <> N'DOCQRCODE_100003'
        THROW 51207, N'Cặp bảng khách hàng 100003 không khớp cấu hình.', 1;

    -- ── Schema guard (fail-closed): bảng đã whitelist phải có đủ cột để claim QR ──
    DECLARE @tmpTbl nvarchar(300) = N'dbo.' + QUOTENAME(@TMPTABLE);
    DECLARE @qrTbl  nvarchar(300) = N'dbo.' + QUOTENAME(@DOCQRTABLE);

    IF COL_LENGTH(@qrTbl, N'TGLUU') IS NULL OR COL_LENGTH(@qrTbl, N'STTBAN') IS NULL
       OR COL_LENGTH(@qrTbl, N'GIO') IS NULL OR COL_LENGTH(@qrTbl, N'KETQUA') IS NULL
       OR COL_LENGTH(@qrTbl, N'MAHANGFCC') IS NULL OR COL_LENGTH(@qrTbl, N'SLTEMHVN') IS NULL
       OR COL_LENGTH(@qrTbl, N'LOTHVN') IS NULL OR COL_LENGTH(@qrTbl, N'LOTFCC') IS NULL
        THROW 51210, N'Bảng DOCQR YMVN thiếu cột bắt buộc để claim QR (TGLUU/STTBAN/GIO/KETQUA/MAHANGFCC/SLTEMHVN/LOTHVN/LOTFCC).', 1;

    IF COL_LENGTH(@tmpTbl, N'STT') IS NULL OR COL_LENGTH(@tmpTbl, N'MAHANG') IS NULL
       OR COL_LENGTH(@tmpTbl, N'SOLUONG') IS NULL OR COL_LENGTH(@tmpTbl, N'LOT') IS NULL
       OR COL_LENGTH(@tmpTbl, N'STATUSDOC') IS NULL OR COL_LENGTH(@tmpTbl, N'TTPHIEU') IS NULL
        THROW 51211, N'Bảng TMP YMVN thiếu cột bắt buộc (STT/MAHANG/SOLUONG/LOT/STATUSDOC/TTPHIEU).', 1;

    DECLARE @OwnTran bit = 0, @Savepoint bit = 0, @sql nvarchar(max),
            @expected int = 0, @affected int = 0;

    BEGIN TRY
        IF @@TRANCOUNT = 0
        BEGIN
            BEGIN TRANSACTION;
            SET @OwnTran = 1;
        END
        ELSE
        BEGIN
            SAVE TRANSACTION PctpTakeLotYmvn2405;
            SET @Savepoint = 1;
        END;

        -- ── 1. Dòng phiếu cần cấp phát (khóa dòng TMP trong transaction) ──────
        CREATE TABLE #Tmp (
            STT     int           NOT NULL,
            MAHANG  nvarchar(120) NOT NULL,
            SL_GIAO bigint        NOT NULL,
            PRIMARY KEY (STT, MAHANG)
        );

        SET @sql = N'
        INSERT INTO #Tmp (STT, MAHANG, SL_GIAO)
        SELECT TRY_CAST(t.STT AS int), RTRIM(t.MAHANG), ISNULL(TRY_CAST(t.SOLUONG AS bigint), 0)
        FROM ' + @tmpTbl + N' t WITH (UPDLOCK, HOLDLOCK)
        WHERE TRY_CAST(t.STT AS int) IS NOT NULL
          AND NULLIF(RTRIM(t.MAHANG), N'''') IS NOT NULL;';
        EXEC sp_executesql @sql;

        -- ── 2. QR ứng viên: QR tự do, hoặc QR đã thuộc đúng một dòng phiếu này ─
        -- STTBAN = GIO = TMP.STT (cùng ngữ nghĩa với Usp_Qrcode_Take_Lot2405 và
        -- LayLaiLotNo/FIFO validation). TGLUU (rowversion) là khóa dòng QR vì
        -- DOCQR.STT không duy nhất.
        CREATE TABLE #Qr (
            Rid       binary(8)      NOT NULL PRIMARY KEY,
            QSTT      int            NOT NULL,
            MAHANG    nvarchar(120)  NOT NULL,
            SLHVN     bigint         NOT NULL,
            LOT       nvarchar(1000) NOT NULL,
            OrigOwner int            NOT NULL,
            Owner     int            NOT NULL
        );

        SET @sql = N'
        INSERT INTO #Qr (Rid, QSTT, MAHANG, SLHVN, LOT, OrigOwner, Owner)
        SELECT d.TGLUU,
               ISNULL(TRY_CAST(d.STT AS int), 0),
               RTRIM(d.MAHANGFCC),
               ISNULL(TRY_CAST(d.SLTEMHVN AS bigint), 0),
               RTRIM(ISNULL(NULLIF(d.LOTHVN, N''''), ISNULL(d.LOTFCC, N''''))),
               ISNULL(d.STTBAN, 0),
               ISNULL(d.STTBAN, 0)
        FROM ' + @qrTbl + N' d WITH (UPDLOCK, HOLDLOCK)
        WHERE ISNULL(TRY_CAST(d.SLTEMHVN AS bigint), 0) > 0
          AND RTRIM(d.MAHANGFCC) IN (SELECT MAHANG FROM #Tmp)
          AND (
                ISNULL(d.STTBAN, 0) IN (SELECT STT FROM #Tmp)
             OR (ISNULL(d.STTBAN, 0) = 0
                 AND ISNULL(d.GIO, N'''') = N''''
                 AND ISNULL(d.KETQUA, N'''') <> N''DG'')
              );';
        EXEC sp_executesql @sql;

        -- ── 3. Cấp phát từng dòng phiếu theo thứ tự STT (trong bộ nhớ) ─────────
        CREATE TABLE #Res (
            STT        int           NOT NULL,
            MAHANG     nvarchar(120) NOT NULL,
            LOT_GHEP   nvarchar(max) NULL,
            TONG_SLHVN bigint        NOT NULL,
            SL_GIAO    bigint        NOT NULL,
            IsOK       bit           NOT NULL,
            PRIMARY KEY (STT, MAHANG)
        );
        CREATE TABLE #Pick (Rid binary(8) NOT NULL PRIMARY KEY);

        DECLARE @stt int, @mh nvarchar(120), @need bigint, @avail bigint,
                @claimed bigint, @lot nvarchar(max);

        DECLARE TmpCursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT STT, MAHANG, SL_GIAO FROM #Tmp ORDER BY STT, MAHANG;
        OPEN TmpCursor;
        FETCH NEXT FROM TmpCursor INTO @stt, @mh, @need;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            DELETE FROM #Pick;

            SELECT @avail = ISNULL(SUM(SLHVN), 0)
            FROM #Qr
            WHERE MAHANG = @mh AND (Owner = @stt OR Owner = 0);

            IF @need > 0 AND @avail >= @need
            BEGIN
                -- QR đã thuộc dòng này được ưu tiên (chạy lại không đổi kết quả),
                -- sau đó QR tự do theo thứ tự STT; dừng khi vừa đủ.
                ;WITH c AS (
                    SELECT Rid, SLHVN,
                           SUM(SLHVN) OVER (
                               ORDER BY CASE WHEN Owner = @stt THEN 0 ELSE 1 END, QSTT, Rid
                               ROWS UNBOUNDED PRECEDING) AS Cum
                    FROM #Qr
                    WHERE MAHANG = @mh AND (Owner = @stt OR Owner = 0)
                )
                INSERT INTO #Pick (Rid)
                SELECT Rid FROM c WHERE Cum - SLHVN < @need;

                UPDATE q SET Owner = 0
                FROM #Qr q
                WHERE q.MAHANG = @mh AND q.Owner = @stt
                  AND NOT EXISTS (SELECT 1 FROM #Pick p WHERE p.Rid = q.Rid);

                UPDATE q SET Owner = @stt
                FROM #Qr q INNER JOIN #Pick p ON p.Rid = q.Rid;

                SELECT @claimed = ISNULL(SUM(SLHVN), 0)
                FROM #Qr WHERE Owner = @stt AND MAHANG = @mh;

                SELECT @lot = STUFF((
                    SELECT N',' + q.LOT + N'-' + CONVERT(nvarchar(20), q.SLHVN)
                    FROM #Qr q
                    WHERE q.Owner = @stt AND q.MAHANG = @mh
                    ORDER BY q.QSTT, q.Rid
                    FOR XML PATH(N''), TYPE
                ).value(N'(./text())[1]', N'nvarchar(max)'), 1, 1, N'');

                INSERT INTO #Res (STT, MAHANG, LOT_GHEP, TONG_SLHVN, SL_GIAO, IsOK)
                VALUES (@stt, @mh, @lot, @claimed, @need, 1);
            END
            ELSE
            BEGIN
                -- Không đủ QR: không claim gì; trả lại QR từng giữ cho dòng này.
                UPDATE #Qr SET Owner = 0 WHERE MAHANG = @mh AND Owner = @stt;

                INSERT INTO #Res (STT, MAHANG, LOT_GHEP, TONG_SLHVN, SL_GIAO, IsOK)
                VALUES (@stt, @mh, NULL, @avail, @need, 0);
            END;

            FETCH NEXT FROM TmpCursor INTO @stt, @mh, @need;
        END;
        CLOSE TmpCursor;
        DEALLOCATE TmpCursor;

        -- ── 4. Ghi claim vào DOCQR: chỉ các dòng QR thật sự đổi chủ ────────────
        SELECT @expected = COUNT(*) FROM #Qr WHERE Owner <> OrigOwner;

        SET @sql = N'
        UPDATE d
        SET d.KETQUA = CASE WHEN q.Owner > 0 THEN N''DG'' ELSE N''NG'' END,
            d.STTBAN = CASE WHEN q.Owner > 0 THEN q.Owner END,
            d.GIO    = CASE WHEN q.Owner > 0 THEN CONVERT(nvarchar(100), q.Owner) END
        FROM ' + @qrTbl + N' d
        INNER JOIN #Qr q ON q.Rid = d.TGLUU
        WHERE q.Owner <> q.OrigOwner;
        SET @n = @@ROWCOUNT;';
        EXEC sp_executesql @sql, N'@n int OUTPUT', @affected OUTPUT;

        IF @affected <> @expected
            THROW 51212, N'QR đã bị tiến trình khác thay đổi trong lúc cấp phát; hủy toàn bộ.', 1;

        -- ── 5. Ghi kết quả vào TMP (chỉ dòng đạt mới đổi LOT) ──────────────────
        SET @sql = N'
        UPDATE t
        SET t.LOT       = CASE WHEN r.IsOK = 1 THEN r.LOT_GHEP ELSE t.LOT END,
            t.STATUSDOC = CASE WHEN r.IsOK = 1 THEN N''OK'' ELSE N''NG'' END,
            t.TTPHIEU   = CASE WHEN r.IsOK = 1 THEN N''DG'' ELSE N'''' END
        FROM ' + @tmpTbl + N' t
        INNER JOIN #Res r ON r.STT = TRY_CAST(t.STT AS int) AND r.MAHANG = RTRIM(t.MAHANG);';
        EXEC sp_executesql @sql;

        -- ── 6. Kết quả (giữ nguyên các cột caller đang đọc) ────────────────────
        SET @sql = N'
        SELECT t.STT, t.MAHANG, t.TENHANG, t.LOT, t.SOLUONG, t.STATUS, t.STATUSDOC,
               ISNULL(r.TONG_SLHVN, 0) AS TONG_SLHVN,
               ISNULL(TRY_CAST(t.SOLUONG AS bigint), 0) AS SL_GIAO,
               CASE WHEN ISNULL(r.IsOK, 0) = 1 THEN 1 ELSE 0 END AS IsOK
        FROM ' + @tmpTbl + N' t
        LEFT JOIN #Res r ON r.STT = TRY_CAST(t.STT AS int) AND r.MAHANG = RTRIM(t.MAHANG)
        ORDER BY t.STT;';
        EXEC sp_executesql @sql;

        IF @OwnTran = 1 COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local', 'TmpCursor') >= 0
        BEGIN
            CLOSE TmpCursor;
            DEALLOCATE TmpCursor;
        END;

        IF @OwnTran = 1 AND XACT_STATE() <> 0
            ROLLBACK TRANSACTION;
        ELSE IF @Savepoint = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION PctpTakeLotYmvn2405;

        THROW;
    END CATCH;
END
GO
