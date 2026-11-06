USE [B7R2_FCC]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
/*
    Usp_Qrcode_gheplotYMVN  —  ghép LOT YMVN (100002), cùng thuật toán với Usp_Qrcode_gheplotYM1 (legacy,
    form GIAOHANGYMN) nhưng:
      1. Nhận @TMPTABLE (legacy hard-code YMVN_TMPPHIEUGIAOHANG, nên không đọc được TMPPHIEUGIAOHANG_100002
         mà luồng HVN_PGH hiện hành đang ghi).
      2. Chuỗi Gear ("Gear D : 5000pcs, Gear E : 300pcs") đọc từ cột GEAR của bảng TMP hiện hành. Bảng legacy
         YMVN_TMPPHIEUGIAOHANG vẫn đọc từ TTPHIEU như cũ. (Bảng TMPPHIEUGIAOHANG_100002 dùng TTPHIEU cho
         ORDER_NO rồi 'DG' sau khi hoàn thành, không còn chứa Gear.)
      3. KHÔNG ghi STOCKTP.SLCONLAITMP. Legacy reset + trừ dần cột đó trên bảng dùng chung chỉ để tính; ở đây tồn
         tạm nằm trong #Stk nên nút "Kiểm tra ghép lot" không còn tác dụng phụ lên tồn kho.
    Kết quả: MH, GG (giờ giao), LG (LOT ghép "lot-sl,lot-sl"), GearName — chỉ các dòng có ghép (LG chứa dấu ',').

    Khác biệt có chủ ý so với YM1 (xem docs/YMVN_GHEPLOT_PARITY.md):
      - MinCloseQty = 0 coi như không có quy cách (-1) thay vì chia cho 0.
      - Gear không có trong B20Gear => không cấp phát (YM1 dùng nhầm Gear của dòng trước).
      - Thứ tự duyệt cố định: MAHANG, NGAYGIAO, Gear, GIOGIAO.
      - Bỏ LEFT JOIN B20ItemQuyCach (không dùng cột nào; chỉ có thể nhân đôi dòng tồn).
*/
CREATE OR ALTER PROC [dbo].[Usp_Qrcode_gheplotYMVN]
    @TMPTABLE nvarchar(100) = N'TMPPHIEUGIAOHANG_100002',
    @EXPECTEDTMPTABLE nvarchar(100) = N'TMPPHIEUGIAOHANG_100002',
    @VIEWTABLEPREFIX nvarchar(100) = N'TMPPHIEUGIAOHANGView_100002'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Expected TMP and view prefix are supplied by CustomerTableConfig.
    -- The view-mode name is machine-scoped (prefix + '_' + sanitized host name).
    IF @TMPTABLE IS NULL OR @EXPECTEDTMPTABLE IS NULL OR @VIEWTABLEPREFIX IS NULL
       OR (@TMPTABLE <> @EXPECTEDTMPTABLE
           AND @TMPTABLE <> N'YMVN_TMPPHIEUGIAOHANG'
           AND LEFT(@TMPTABLE, LEN(@VIEWTABLEPREFIX) + 1) <> @VIEWTABLEPREFIX + N'_')
    BEGIN
        DECLARE @invalidName nvarchar(2048) =
            N'Tên bảng TMP YMVN không khớp CustomerTableConfig: ' + ISNULL(@TMPTABLE, N'<NULL>');
        THROW 51221, @invalidName, 1;
    END;

    DECLARE @tbl nvarchar(300) = N'dbo.' + QUOTENAME(@TMPTABLE);
    DECLARE @objectId int = OBJECT_ID(@tbl);
    -- View-only terminals use TMPPHIEUGIAOHANGView_<customer>_<machine>.
    -- That object may be a SQL VIEW, not a user table. Both are valid read sources.
    IF @objectId IS NULL OR NOT EXISTS
       (SELECT 1 FROM sys.objects WHERE object_id = @objectId AND type IN (N'U', N'V'))
    BEGIN
        DECLARE @missingObject nvarchar(2048) =
            N'Không tìm thấy bảng/VIEW TMP YMVN trong dbo: ' + @TMPTABLE;
        THROW 51222, @missingObject, 1;
    END;

    DECLARE @gearCol sysname = CASE WHEN @TMPTABLE = N'YMVN_TMPPHIEUGIAOHANG' THEN N'TTPHIEU' ELSE N'GEAR' END;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @objectId AND name = N'MAHANG')
       OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @objectId AND name = N'SOLUONG')
       OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @objectId AND name = N'NGAYGIAO')
       OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @objectId AND name = N'GIOGIAO')
       OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @objectId AND name = @gearCol)
    BEGIN
        DECLARE @missingColumns nvarchar(2048) =
            N'Bảng/VIEW TMP YMVN thiếu cột MAHANG/SOLUONG/NGAYGIAO/GIOGIAO/' + @gearCol
            + N'. Object: ' + @TMPTABLE;
        THROW 51223, @missingColumns, 1;
    END;

    -- ── Nguồn: đúng tập dòng của phiên (đã lọc giờ + bỏ dòng OK khi nạp TMP) ─────────────
    CREATE TABLE #Src (MAHANG nvarchar(56) NULL, SOLUONG int NOT NULL, NGAYGIAO datetime NULL,
                       GIOGIAO nvarchar(50) NULL, TTGEAR nvarchar(256) NULL);
    DECLARE @sql nvarchar(max) = N'
        INSERT INTO #Src (MAHANG, SOLUONG, NGAYGIAO, GIOGIAO, TTGEAR)
        SELECT MAHANG, ISNULL(TRY_CAST(SOLUONG AS int), 0), NGAYGIAO, GIOGIAO, ' + QUOTENAME(@gearCol) + N'
        FROM ' + @tbl + N';';
    EXEC sp_executesql @sql;

    -- ── Tồn tạm trong bộ nhớ (thay cho STOCKTP.SLCONLAITMP) ────────────────────────────────
    CREATE TABLE #Stk (Id int IDENTITY(1,1) PRIMARY KEY, PART nvarchar(100) NULL, LOT13 nvarchar(13) NULL,
                       GEARCH nvarchar(1) NULL, SLTMP int NOT NULL, SORTKEY float NULL);
    INSERT INTO #Stk (PART, LOT13, GEARCH, SLTMP, SORTKEY)
    SELECT b.PART, SUBSTRING(b.LOT, 1, 13), RIGHT(SUBSTRING(b.LOT, 1, 13), 1),
           CAST(b.SLCONLAI AS int), TRY_CAST(SUBSTRING(b.LOT, 1, 13) AS float)
    FROM dbo.STOCKTP b
    WHERE b.SLCONLAI > 0 AND b.PART IN (SELECT MAHANG FROM #Src);

    CREATE TABLE #Res (Id int IDENTITY(1,1) PRIMARY KEY, MH nvarchar(56), GG nvarchar(50),
                       LG nvarchar(1000), GearName nvarchar(1));

    DECLARE @MAHANG nvarchar(56), @SOLUONG int, @NGAYGIAO datetime, @GIOGIAO nvarchar(50),
            @TTGEAR nvarchar(256), @GearCode nvarchar(1), @GearName nvarchar(1),
            @QCDG int, @LOTGHEP nvarchar(1000), @SLCANGHEP int, @SLGHEP int,
            @ItemSL int, @ItemGear nvarchar(1), @HT_LOT nvarchar(13), @HT_SL int,
            @nItems int;

    CREATE TABLE #Items (Idx int IDENTITY(1,1) PRIMARY KEY, SL int NULL, GearCode nvarchar(1) NULL);

    DECLARE RowCur CURSOR LOCAL FAST_FORWARD FOR
        SELECT PGH.MAHANG, SUM(PGH.SOLUONG), PGH.NGAYGIAO, PGH.GIOGIAO, PGH.TTGEAR, G.Code, MAX(G.Name)
        FROM #Src PGH
        LEFT JOIN B20Gear G ON SUBSTRING(PGH.TTGEAR, 6, 1) = G.Name
        GROUP BY PGH.MAHANG, PGH.NGAYGIAO, G.Code, PGH.GIOGIAO, PGH.TTGEAR
        ORDER BY PGH.MAHANG, PGH.NGAYGIAO, G.Code, PGH.GIOGIAO, PGH.TTGEAR;
    OPEN RowCur;
    FETCH NEXT FROM RowCur INTO @MAHANG, @SOLUONG, @NGAYGIAO, @GIOGIAO, @TTGEAR, @GearCode, @GearName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Quy cách đóng gói
        SET @QCDG = -1;
        IF EXISTS (SELECT 1 FROM B20Item WHERE Code = @MAHANG)
            SELECT @QCDG = CAST(MinCloseQty AS int) FROM B20Item WHERE Code = @MAHANG;
        IF @QCDG = 0 SET @QCDG = -1;

        SET @LOTGHEP = N'';
        SET @SLCANGHEP = 0;
        SET @SLGHEP = 0;

        -- Các phần cần ghép: nhiều Gear "Gear D : 5000pcs, Gear E : 300pcs" => mỗi Gear một phần
        TRUNCATE TABLE #Items;
        SELECT @nItems = COUNT(*) FROM dbo.Split(@TTGEAR, ',');
        IF @nItems > 1
        BEGIN
            INSERT INTO #Items (SL, GearCode)
            SELECT TRY_CAST(LEFT(SUBSTRING(s.strval, PATINDEX('%[0-9]%', s.strval), LEN(s.strval)),
                       PATINDEX('%[^0-9]%', SUBSTRING(s.strval, PATINDEX('%[0-9]%', s.strval), LEN(s.strval)) + 'X') - 1) AS int),
                   (SELECT TOP 1 g.Code FROM B20Gear g WHERE g.Name = SUBSTRING(s.strval, 6, 1))
            FROM dbo.Split(@TTGEAR, ',') s
            ORDER BY CHARINDEX(s.strval, @TTGEAR);
        END
        ELSE
            INSERT INTO #Items (SL, GearCode) VALUES (@SOLUONG, @GearCode);

        DECLARE ItemCur CURSOR LOCAL FAST_FORWARD FOR SELECT SL, GearCode FROM #Items ORDER BY Idx;
        OPEN ItemCur;
        FETCH NEXT FROM ItemCur INTO @ItemSL, @ItemGear;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @SOLUONG = ISNULL(@ItemSL, 0);

            DECLARE LotCur CURSOR LOCAL STATIC FOR
                SELECT LOT13, SLTMP FROM #Stk
                WHERE PART = @MAHANG AND GEARCH = @ItemGear
                ORDER BY SORTKEY, Id;
            OPEN LotCur;
            FETCH NEXT FROM LotCur INTO @HT_LOT, @HT_SL;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                IF @QCDG IS NULL BREAK;

                IF @HT_SL < @SOLUONG
                BEGIN
                    IF @SLCANGHEP > 0
                    BEGIN
                        IF @HT_SL < @SLCANGHEP
                        BEGIN
                            SET @LOTGHEP = @LOTGHEP + N',' + @HT_LOT + N'-' + CAST(@HT_SL AS nvarchar(20));
                            SET @SLCANGHEP = @SLCANGHEP - @HT_SL;
                            SET @SOLUONG = @SOLUONG - @HT_SL;
                            UPDATE #Stk SET SLTMP = 0 WHERE PART = @MAHANG AND LOT13 = @HT_LOT;
                        END
                        ELSE
                        BEGIN
                            SET @LOTGHEP = @LOTGHEP + N',' + @HT_LOT + N'-' + CAST(@SLCANGHEP AS nvarchar(20));
                            INSERT INTO #Res (MH, GG, LG, GearName) VALUES (@MAHANG, @GIOGIAO, @LOTGHEP, @GearName);
                            SET @LOTGHEP = N'';
                            SET @SLGHEP = (@HT_SL - @SLCANGHEP) % @QCDG;
                            IF @SLGHEP = 0
                            BEGIN
                                SET @SLCANGHEP = 0;
                            END
                            ELSE
                            BEGIN
                                SET @SLCANGHEP = @QCDG - @SLGHEP;
                                SET @LOTGHEP = @HT_LOT + N'-' + CAST(@SLGHEP AS nvarchar(20));
                            END;
                            SET @SOLUONG = @SOLUONG - @HT_SL;
                            UPDATE #Stk SET SLTMP = 0 WHERE PART = @MAHANG AND LOT13 = @HT_LOT;
                        END;
                    END
                    ELSE
                    BEGIN
                        SET @SLGHEP = @HT_SL % @QCDG;
                        IF @SLGHEP = 0
                        BEGIN
                            SET @SLCANGHEP = 0;
                        END
                        ELSE
                        BEGIN
                            SET @SLCANGHEP = @QCDG - @SLGHEP;
                        END;
                        IF @SLGHEP > 0
                        BEGIN
                            IF @LOTGHEP = N''
                            BEGIN
                                SET @LOTGHEP = @HT_LOT + N'-' + CAST(@SLGHEP AS nvarchar(20));
                                UPDATE #Stk SET SLTMP = 0 WHERE PART = @MAHANG AND LOT13 = @HT_LOT;
                                SET @SOLUONG = @SOLUONG - @HT_SL;
                            END
                            ELSE
                            BEGIN
                                -- Giữ đúng YM1: nhánh này không trừ @SOLUONG.
                                SET @LOTGHEP = @LOTGHEP + N',' + @HT_LOT + N'-' + CAST(@SLGHEP AS nvarchar(20));
                                UPDATE #Stk SET SLTMP = 0 WHERE PART = @MAHANG AND LOT13 = @HT_LOT;
                            END;
                        END
                        ELSE
                        BEGIN
                            SET @SLCANGHEP = 0;
                            SET @SOLUONG = @SOLUONG - @HT_SL;
                        END;
                    END;
                END
                ELSE
                BEGIN
                    IF @SLCANGHEP > 0
                    BEGIN
                        SET @LOTGHEP = @LOTGHEP + N',' + @HT_LOT + N'-' + CAST(@SLCANGHEP AS nvarchar(20));
                        INSERT INTO #Res (MH, GG, LG, GearName) VALUES (@MAHANG, @GIOGIAO, @LOTGHEP, @GearName);
                        SET @LOTGHEP = N'';
                        SET @SLCANGHEP = 0;
                        UPDATE #Stk SET SLTMP = @HT_SL - @SOLUONG WHERE PART = @MAHANG AND LOT13 = @HT_LOT;
                        SET @SOLUONG = 0;
                        BREAK;
                    END
                    ELSE
                    BEGIN
                        SET @SLCANGHEP = 0;
                        UPDATE #Stk SET SLTMP = @HT_SL - @SOLUONG WHERE PART = @MAHANG AND LOT13 = @HT_LOT;
                        BREAK;
                    END;
                END;

                FETCH NEXT FROM LotCur INTO @HT_LOT, @HT_SL;
            END;
            CLOSE LotCur;
            DEALLOCATE LotCur;

            FETCH NEXT FROM ItemCur INTO @ItemSL, @ItemGear;
        END;
        CLOSE ItemCur;
        DEALLOCATE ItemCur;

        FETCH NEXT FROM RowCur INTO @MAHANG, @SOLUONG, @NGAYGIAO, @GIOGIAO, @TTGEAR, @GearCode, @GearName;
    END;
    CLOSE RowCur;
    DEALLOCATE RowCur;

    SELECT MH, GG, LG, GearName FROM #Res WHERE CHARINDEX(N',', LG, 1) > 0 ORDER BY Id;
END
GO
