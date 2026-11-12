USE [B7R2_FCC]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
/*
    Usp_Qrcode_gheplot (gop HVN + YMVN Gear)
    - @USEGEAR = 1 : nhanh Gear (YMVN MP). TMP co the la BANG hoac VIEW.
        + Cot Gear: uu tien GEAR, khong co thi dung TTPHIEU (theo cot thuc su ton tai).
        + Neu thieu cot, thong bao loi neu ro ten cot thieu.
        + Bo qua cac dong TMP da Cap nhat kho (STATUS = 'OK') khi nap #GearSrc.
    - @USEGEAR = 0 : nhanh HVN (logic OLD):
        1) Khoa lot = LEFT(LOT, 20).
        2) Thu tu duyet lot: LOT so tang dan (lot cu truoc).
        3) Da XOA dong debug "select * from #IFSCursorTable" (gay grid Ghep lot trong,
           vi C# doc nham result set dau tien).
        4) Cursor lot doc SLCONLAITMP (ton tam sau khi tru cac don truoc) thay vi SLCONLAI,
           giong Usp_Qrcode_gheplot(OLD). Doc SLCONLAI thi don nao cung thay ton day du
           => khong bao gio sinh dong ghep.
        5) Bo UPDATE STT/HOP tren TMP (gay trung STT giua cac dong cung MAHANG+SOLUONG+GIOGIAO).
        6) Sua nhanh cuoi (@HT_SL >= @SOLUONG, @SLCANGHEP > 0): dung @SLCANGHEP truoc khi reset,
           them truong hop @HT_SL < @SLCANGHEP.
        7) MOI: Loai cac dong da Cap nhat kho (TMP.STATUS = 'OK', da tru SLCONLAI) khoi danh sach
           can ghep lot. Khop dong bang MAHANG+SOLUONG+NGAYGIAO+GIOGIAO, danh so RN trong nhom
           trung de chi loai dung so dong da OK (dong trung chua giao van duoc xu ly).
        8) MOI: #IFSCursorTable nap theo NGAYGIAO, GIOGIAO => gio som lay lot truoc.
    Chi tra DUNG 1 result set cuoi cung (MH, GG, LG[, GearName]).
*/
ALTER PROC [dbo].[Usp_Qrcode_gheplot]
    @TMPTABLE nvarchar(128) = N'TMPPHIEUGIAOHANG',
    @IFSTABLE nvarchar(128) = N'IFSPHIEUGIAOHANG',
    @USEGEAR bit = 0,
    @EXPECTEDTMPTABLE nvarchar(128) = NULL,
    @VIEWTABLEPREFIX nvarchar(128) = NULL
AS
BEGIN

    SET NOCOUNT ON;

    /* ======================= NHANH GEAR (@USEGEAR = 1) ======================= */
    IF @USEGEAR = 1
    BEGIN
        IF @EXPECTEDTMPTABLE IS NULL OR @VIEWTABLEPREFIX IS NULL
           OR (@TMPTABLE <> @EXPECTEDTMPTABLE
               AND LEFT(@TMPTABLE, LEN(@VIEWTABLEPREFIX) + 1) <> @VIEWTABLEPREFIX + N'_'
               AND @TMPTABLE <> N'YMVN_TMPPHIEUGIAOHANG')
            THROW 51221, N'Tên TMP không khớp CustomerTableConfig cho xử lý Gear.', 1;

        DECLARE @GearObjectId int = OBJECT_ID(N'dbo.' + QUOTENAME(@TMPTABLE));
        IF @GearObjectId IS NULL OR NOT EXISTS
           (SELECT 1 FROM sys.objects WHERE object_id = @GearObjectId AND type IN (N'U', N'V'))
            THROW 51222, N'Không tìm thấy bảng/VIEW TMP dùng cho xử lý Gear.', 1;

        -- Chon cot Gear theo cot thuc su ton tai trong bang/VIEW
        DECLARE @TmpFull nvarchar(300) = N'dbo.' + QUOTENAME(@TMPTABLE);

        DECLARE @GearCol sysname =
            CASE WHEN COL_LENGTH(@TmpFull, N'GEAR')    IS NOT NULL THEN N'GEAR'
                 WHEN COL_LENGTH(@TmpFull, N'TTPHIEU') IS NOT NULL THEN N'TTPHIEU'
                 ELSE N'GEAR' END;

        DECLARE @MissingCols nvarchar(500) = N'';
        SELECT @MissingCols = @MissingCols + N',' + c.ColName
        FROM (VALUES (N'MAHANG'),(N'SOLUONG'),(N'NGAYGIAO'),(N'GIOGIAO'),(@GearCol)) AS c(ColName)
        WHERE COL_LENGTH(@TmpFull, c.ColName) IS NULL;

        IF @MissingCols <> N''
        BEGIN
            DECLARE @Msg nvarchar(600) =
                N'TMP Gear thiếu cột: ' + STUFF(@MissingCols, 1, 1, N'') + N' (' + @TMPTABLE + N')';
            THROW 51223, @Msg, 1;
        END;

        CREATE TABLE #GearSrc
        (
            MAHANG nvarchar(56) NOT NULL,
            SOLUONG int NOT NULL,
            NGAYGIAO datetime NULL,
            GIOGIAO nvarchar(50) NULL,
            GearText nvarchar(1000) NULL
        );

        -- Bo qua dong da Cap nhat kho (STATUS = OK) neu TMP co cot STATUS
        DECLARE @GearStatusFilter nvarchar(200) =
            CASE WHEN COL_LENGTH(@TmpFull, N'STATUS') IS NOT NULL
                 THEN N' AND UPPER(LTRIM(RTRIM(ISNULL([STATUS], N'''')))) <> N''OK'''
                 ELSE N'' END;

        DECLARE @GearSql nvarchar(max) =
            N'INSERT INTO #GearSrc(MAHANG,SOLUONG,NGAYGIAO,GIOGIAO,GearText)
              SELECT MAHANG, TRY_CONVERT(int,SOLUONG), NGAYGIAO, GIOGIAO,' + QUOTENAME(@GearCol) +
              N' FROM dbo.' + QUOTENAME(@TMPTABLE) +
              N' WHERE ISNULL(TRY_CONVERT(int,SOLUONG),0) > 0 AND ISNULL(MAHANG,'''') <> ''''' +
              @GearStatusFilter + N';';
        EXEC sys.sp_executesql @GearSql;

        CREATE TABLE #GearStock
        (
            StockId int IDENTITY(1,1) PRIMARY KEY,
            PART nvarchar(100) NOT NULL,
            LOT13 nvarchar(20) NOT NULL,
            GearCode nvarchar(10) NULL,
            Qty int NOT NULL,
            SortKey decimal(38,0) NULL
        );

        INSERT INTO #GearStock (PART, LOT13, GearCode, Qty, SortKey)
        SELECT
            s.PART,
            LEFT(s.LOT, 20),                           -- 20 ky tu dau lam khoa LOT
            SUBSTRING(s.LOT, 13, 1),                   -- ky tu thu 13 = B20Gear.Code
            CONVERT(int, s.SLCONLAI),
            TRY_CONVERT(decimal(38,0), LEFT(s.LOT, 20))
        FROM dbo.STOCKTP s
        WHERE s.SLCONLAI > 0
          AND LEN(ISNULL(s.LOT, '')) >= 20
          AND EXISTS (SELECT 1 FROM #GearSrc src WHERE src.MAHANG = s.PART);

        CREATE TABLE #GearResult
        (
            ResultId int IDENTITY(1,1) PRIMARY KEY,
            MH nvarchar(56) NOT NULL,
            GG nvarchar(50) NULL,
            LG nvarchar(1000) NOT NULL,
            GearName nvarchar(100) NULL
        );

        CREATE TABLE #GearItems
        (
            ItemId int IDENTITY(1,1) PRIMARY KEY,
            ItemQty int NOT NULL,
            GearName nvarchar(100) NOT NULL,
            GearCode nvarchar(10) NOT NULL
        );

        DECLARE @Part nvarchar(56), @OrderQty int, @DeliveryDate datetime, @DeliveryTime nvarchar(50),
                @GearTextValue nvarchar(1000), @PackQty int, @GearToken nvarchar(400),
                @ResolvedGearName nvarchar(100), @ResolvedGearCode nvarchar(10),
                @ItemQty int, @ItemGearName nvarchar(100), @ItemGearCode nvarchar(10),
                @LotKey nvarchar(20), @StockQty int, @Pending int,
                @LotList nvarchar(1000), @Need int, @ParsedQty int, @DigitStart int, @GearPartialQty int,
                @Digits nvarchar(40), @StockId int;

        DECLARE GearOrderCur CURSOR LOCAL FAST_FORWARD FOR
            SELECT MAHANG, SOLUONG, NGAYGIAO, GIOGIAO, GearText
            FROM #GearSrc
            ORDER BY MAHANG, NGAYGIAO, GearText, GIOGIAO;

        OPEN GearOrderCur;
        FETCH NEXT FROM GearOrderCur INTO @Part,@OrderQty,@DeliveryDate,@DeliveryTime,@GearTextValue;
        WHILE @@FETCH_STATUS=0
        BEGIN
            SET @PackQty = NULL;
            SELECT TOP(1) @PackQty = TRY_CONVERT(int,MinCloseQty)
            FROM dbo.B20Item WHERE Code=@Part;
            IF ISNULL(@PackQty,0) <= 0 SET @PackQty = 1;

            TRUNCATE TABLE #GearItems;

            /*
              Vi du chuoi Gear: "Gear D : 5000pcs, Gear E : 300pcs"
              Chu cai = B20Gear.Name ; LOT[13] = B20Gear.Code.
            */
            IF CHARINDEX(N',',ISNULL(@GearTextValue,N'')) > 0
            BEGIN
                DECLARE GearTokenCur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT LTRIM(RTRIM(strval))
                    FROM dbo.Split(@GearTextValue, N',')
                    WHERE LTRIM(RTRIM(strval)) <> N'';
                OPEN GearTokenCur;
                FETCH NEXT FROM GearTokenCur INTO @GearToken;
                WHILE @@FETCH_STATUS=0
                BEGIN
                    SET @ResolvedGearName = LTRIM(RTRIM(SUBSTRING(@GearToken,6,1)));
                    SET @DigitStart = PATINDEX('%[0-9]%',@GearToken);
                    SET @Digits = CASE WHEN @DigitStart > 0 THEN SUBSTRING(@GearToken,@DigitStart,40) ELSE N'' END;
                    SET @ParsedQty = TRY_CONVERT(int,LEFT(@Digits,
                        CASE WHEN PATINDEX('%[^0-9]%',@Digits+N'X') > 0
                             THEN PATINDEX('%[^0-9]%',@Digits+N'X')-1 ELSE LEN(@Digits) END));

                    SET @ResolvedGearCode = NULL;
                    SELECT TOP(1) @ResolvedGearCode = CONVERT(nvarchar(10),Code)
                    FROM dbo.B20Gear
                    WHERE Name=@ResolvedGearName AND ISNULL(IsActive,1)=1
                    ORDER BY Id;

                    IF @ResolvedGearCode IS NULL OR @ParsedQty IS NULL OR @ParsedQty <= 0
                    BEGIN
                        CLOSE GearTokenCur; DEALLOCATE GearTokenCur;
                        CLOSE GearOrderCur; DEALLOCATE GearOrderCur;
                        THROW 51224, N'Không phân tích được Gear hoặc số lượng trong TMP YMVN. Kiểm tra B20Gear.Name/Code và chuỗi GEAR.', 1;
                    END;

                    INSERT INTO #GearItems(ItemQty,GearName,GearCode)
                    VALUES(@ParsedQty,@ResolvedGearName,@ResolvedGearCode);

                    FETCH NEXT FROM GearTokenCur INTO @GearToken;
                END;
                CLOSE GearTokenCur;
                DEALLOCATE GearTokenCur;
            END
            ELSE
            BEGIN
                SET @ResolvedGearName = LTRIM(RTRIM(SUBSTRING(ISNULL(@GearTextValue,N''),6,1)));
                SET @ResolvedGearCode = NULL;
                SELECT TOP(1) @ResolvedGearCode = CONVERT(nvarchar(10),Code)
                FROM dbo.B20Gear
                WHERE Name=@ResolvedGearName AND ISNULL(IsActive,1)=1
                ORDER BY Id;

                IF @ResolvedGearCode IS NULL
                BEGIN
                    CLOSE GearOrderCur; DEALLOCATE GearOrderCur;
                    THROW 51224, N'Không ánh xạ được Gear.Name sang Gear.Code trong B20Gear.', 1;
                END;

                INSERT INTO #GearItems(ItemQty,GearName,GearCode)
                VALUES(@OrderQty,@ResolvedGearName,@ResolvedGearCode);
            END;

            DECLARE GearItemCur CURSOR LOCAL FAST_FORWARD FOR
                SELECT ItemQty,GearName,GearCode FROM #GearItems ORDER BY ItemId;
            OPEN GearItemCur;
            FETCH NEXT FROM GearItemCur INTO @ItemQty,@ItemGearName,@ItemGearCode;
            WHILE @@FETCH_STATUS=0
            BEGIN
                -- Reset trang thai ghep cho MOI gear item
                SET @Need = @ItemQty;
                SET @Pending = 0;
                SET @LotList = N'';

                DECLARE GearLotCur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT StockId,LOT13,Qty
                    FROM #GearStock
                    WHERE PART=@Part AND GearCode=@ItemGearCode AND Qty>0
                    ORDER BY SortKey,LOT13,StockId;
                OPEN GearLotCur;
                FETCH NEXT FROM GearLotCur INTO @StockId,@LotKey,@StockQty;
                WHILE @@FETCH_STATUS=0
                BEGIN
                    IF @StockQty < @Need
                    BEGIN
                        IF @Pending > 0
                        BEGIN
                            IF @StockQty < @Pending
                            BEGIN
                                SET @LotList = @LotList + N',' + @LotKey + N'-' + CONVERT(nvarchar(20),@StockQty);
                                SET @Pending = @Pending - @StockQty;
                                SET @Need = @Need - @StockQty;
                                UPDATE #GearStock SET Qty=0 WHERE StockId=@StockId;
                            END
                            ELSE
                            BEGIN
                                SET @LotList = @LotList + N',' + @LotKey + N'-' + CONVERT(nvarchar(20),@Pending);
                                INSERT INTO #GearResult(MH,GG,LG,GearName)
                                VALUES(@Part,@DeliveryTime,@LotList,@ItemGearName);
                                SET @LotList = N'';
                                SET @GearPartialQty = (@StockQty - @Pending) % @PackQty;
                                IF @GearPartialQty = 0
                                    SET @Pending = 0;
                                ELSE
                                BEGIN
                                    SET @Pending = @PackQty - @GearPartialQty;
                                    SET @LotList = @LotKey + N'-' + CONVERT(nvarchar(20),@GearPartialQty);
                                END;
                                SET @Need = @Need - @StockQty;
                                UPDATE #GearStock SET Qty=0 WHERE StockId=@StockId;
                            END;
                        END
                        ELSE
                        BEGIN
                            SET @GearPartialQty = @StockQty % @PackQty;
                            IF @GearPartialQty = 0
                                SET @Pending = 0;
                            ELSE
                                SET @Pending = @PackQty - @GearPartialQty;

                            IF @GearPartialQty > 0
                            BEGIN
                                IF @LotList = N''
                                BEGIN
                                    SET @LotList = @LotKey + N'-' + CONVERT(nvarchar(20),@GearPartialQty);
                                    SET @Need = @Need - @StockQty;
                                END
                                ELSE
                                BEGIN
                                    SET @LotList = @LotList + N',' + @LotKey + N'-' + CONVERT(nvarchar(20),@GearPartialQty);
                                END;
                                UPDATE #GearStock SET Qty=0 WHERE StockId=@StockId;
                            END
                            ELSE
                            BEGIN
                                SET @Pending = 0;
                                SET @Need = @Need - @StockQty;
                            END;
                        END;
                    END
                    ELSE
                    BEGIN
                        IF @Pending > 0
                        BEGIN
                            SET @LotList = @LotList + N',' + @LotKey + N'-' + CONVERT(nvarchar(20),@Pending);
                            INSERT INTO #GearResult(MH,GG,LG,GearName)
                            VALUES(@Part,@DeliveryTime,@LotList,@ItemGearName);
                            SET @LotList = N'';
                            SET @Pending = 0;
                            UPDATE #GearStock SET Qty=@StockQty-@Need WHERE StockId=@StockId;
                            SET @Need = 0;
                            BREAK;
                        END
                        ELSE
                        BEGIN
                            UPDATE #GearStock SET Qty=@StockQty-@Need WHERE StockId=@StockId;
                            BREAK;
                        END;
                    END;

                    FETCH NEXT FROM GearLotCur INTO @StockId,@LotKey,@StockQty;
                END;
                CLOSE GearLotCur;
                DEALLOCATE GearLotCur;

                FETCH NEXT FROM GearItemCur INTO @ItemQty,@ItemGearName,@ItemGearCode;
            END;
            CLOSE GearItemCur;
            DEALLOCATE GearItemCur;

            FETCH NEXT FROM GearOrderCur INTO @Part,@OrderQty,@DeliveryDate,@DeliveryTime,@GearTextValue;
        END;
        CLOSE GearOrderCur;
        DEALLOCATE GearOrderCur;

        SELECT MH,GG,LG,GearName
        FROM #GearResult
        WHERE CHARINDEX(N',',LG)>0
        ORDER BY ResultId;
        RETURN;
    END;

    /* ======================= NHANH HVN (@USEGEAR = 0) ======================= */
    DECLARE @SQL NVARCHAR(MAX);
    DECLARE @MAHANG NVARCHAR(56), @SOLUONG INT,
            @NGAYGIAO SMALLDATETIME, @GIOGIAO NVARCHAR(50), @QCDG INT, @STT INT,
            @HT_LOT NVARCHAR(30), @HT_SL INT,
            @LOTGHEP NVARCHAR(500), @SLCANGHEP INT, @SLGHEP INT, @DuDuoi INT;
    SET @STT = 0;

    IF OBJECT_ID('TempDb..#TMPGHEPLOT') IS NOT NULL DROP TABLE #TMPGHEPLOT;
    CREATE TABLE #TMPGHEPLOT (MH NVARCHAR(56), GG NVARCHAR(50), LG NVARCHAR(500));

    -- Dat lai ton tam theo ton thuc te
    SET @SQL = N'UPDATE STOCKTP SET SLCONLAITMP = SLCONLAI
        WHERE PART IN (SELECT MAHANG FROM ' + QUOTENAME(@IFSTABLE) + N' GROUP BY MAHANG) AND SLCONLAI > 0';
    EXEC sp_executesql @SQL;

    IF OBJECT_ID('TempDb..#IFSCursorTable') IS NOT NULL DROP TABLE #IFSCursorTable;
    CREATE TABLE #IFSCursorTable (
        ID INT IDENTITY(1,1),
        MAHANG NVARCHAR(56),
        SOLUONG INT,
        NGAYGIAO SMALLDATETIME,
        GIOGIAO NVARCHAR(50)
    );

    -- Nap danh sach can ghep lot: LOAI cac dong da Cap nhat kho (TMP.STATUS = 'OK', da tru SLCONLAI)
    DECLARE @TmpHasStatus BIT =
        CASE WHEN COL_LENGTH(N'dbo.' + QUOTENAME(@TMPTABLE), N'STATUS') IS NOT NULL THEN 1 ELSE 0 END;

    IF @TmpHasStatus = 1
    BEGIN
        SET @SQL = N'
        ;WITH I AS (
            SELECT MAHANG, SOLUONG, NGAYGIAO, GIOGIAO,
                   ROW_NUMBER() OVER (
                       PARTITION BY MAHANG, SOLUONG, CONVERT(date, NGAYGIAO), LTRIM(RTRIM(GIOGIAO))
                       ORDER BY (SELECT 1)) AS RN
            FROM ' + QUOTENAME(@IFSTABLE) + N'
        ),
        D AS (
            SELECT MAHANG, SOLUONG, CONVERT(date, NGAYGIAO) AS NG, LTRIM(RTRIM(GIOGIAO)) AS GG,
                   COUNT(*) AS CNT
            FROM dbo.' + QUOTENAME(@TMPTABLE) + N'
            WHERE UPPER(LTRIM(RTRIM(ISNULL([STATUS], N'''')))) = N''OK''
            GROUP BY MAHANG, SOLUONG, CONVERT(date, NGAYGIAO), LTRIM(RTRIM(GIOGIAO))
        )
        INSERT INTO #IFSCursorTable (MAHANG, SOLUONG, NGAYGIAO, GIOGIAO)
        SELECT I.MAHANG, I.SOLUONG, I.NGAYGIAO, I.GIOGIAO
        FROM I
        LEFT JOIN D
               ON D.MAHANG = I.MAHANG
              AND D.SOLUONG = I.SOLUONG
              AND D.NG = CONVERT(date, I.NGAYGIAO)
              AND D.GG = LTRIM(RTRIM(I.GIOGIAO))
        WHERE I.RN > ISNULL(D.CNT, 0)
        ORDER BY I.NGAYGIAO, I.GIOGIAO;';
    END
    ELSE
    BEGIN
        SET @SQL = N'INSERT INTO #IFSCursorTable (MAHANG, SOLUONG, NGAYGIAO, GIOGIAO)
                     SELECT MAHANG, SOLUONG, NGAYGIAO, GIOGIAO FROM ' + QUOTENAME(@IFSTABLE) + N'
                     ORDER BY NGAYGIAO, GIOGIAO;';
    END;
    EXEC sp_executesql @SQL;

    -- (da xoa dong debug: select * from #IFSCursorTable)

    DECLARE IFSPGH CURSOR FOR
        SELECT MAHANG, SOLUONG, NGAYGIAO, GIOGIAO FROM #IFSCursorTable ORDER BY ID;

    OPEN IFSPGH;
    FETCH NEXT FROM IFSPGH INTO @MAHANG, @SOLUONG, @NGAYGIAO, @GIOGIAO;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @STT = @STT + 1;

        SET @QCDG = -1;
        IF EXISTS (SELECT 1 FROM B20Item WHERE Code = @MAHANG)
            SELECT @QCDG = CAST(MinCloseQty AS INT) FROM B20Item WHERE Code = @MAHANG;

        -- KHONG cap nhat STT/HOP tren TMP o day. STT/HOP da duoc C# gan duy nhat tung dong
        -- (EnrichSttHop). Ban OLD co UPDATE nay nhung WHERE CUA/TRUYEN = NULL nen khong khop dong nao.
        -- UPDATE khong co CUA/TRUYEN se ghi DE cung mot STT len moi dong trung MAHANG+SOLUONG+GIOGIAO
        -- => form 'List trung ma so luong' hien 2 dong cung STT.

        ---- Duyet ghep lo cho tung moc so luong
        SET @LOTGHEP = '';
        SET @SLCANGHEP = 0;
        SET @SLGHEP = 0;

        DECLARE DHT CURSOR LOCAL STATIC FORWARD_ONLY READ_ONLY FOR
            SELECT LEFT(b.LOT, 20) AS LotKey, b.SLCONLAITMP   -- ton TAM (da tru cac don truoc), giong OLD
            FROM STOCKTP b
            WHERE b.PART = @MAHANG AND b.SLCONLAI > 0
            ORDER BY TRY_CAST(LEFT(b.LOT, 20) AS DECIMAL(38, 0)), b.LOT;

        OPEN DHT;
        FETCH NEXT FROM DHT INTO @HT_LOT, @HT_SL;

        WHILE @@FETCH_STATUS = 0 AND @SOLUONG > 0
        BEGIN
            IF (@HT_SL <= 0)
            BEGIN
                FETCH NEXT FROM DHT INTO @HT_LOT, @HT_SL;
                CONTINUE;
            END

            IF (@QCDG IS NULL OR @QCDG <= 0)
            BEGIN
                IF @HT_SL >= @SOLUONG
                BEGIN
                    SET @LOTGHEP = @HT_LOT + '-' + CAST(@SOLUONG AS NVARCHAR);
                    INSERT INTO #TMPGHEPLOT VALUES (@MAHANG, @GIOGIAO, @LOTGHEP);
                    UPDATE STOCKTP SET SLCONLAITMP = SLCONLAITMP - @SOLUONG WHERE PART = @MAHANG AND LEFT(LOT, 20) = @HT_LOT;
                    SET @SOLUONG = 0;
                END
                ELSE
                BEGIN
                    SET @LOTGHEP = @HT_LOT + '-' + CAST(@HT_SL AS NVARCHAR);
                    INSERT INTO #TMPGHEPLOT VALUES (@MAHANG, @GIOGIAO, @LOTGHEP);
                    SET @SOLUONG = @SOLUONG - @HT_SL;
                    UPDATE STOCKTP SET SLCONLAITMP = 0 WHERE PART = @MAHANG AND LEFT(LOT, 20) = @HT_LOT;
                END
            END
            ELSE
            BEGIN
                IF (@HT_SL < @SOLUONG)
                BEGIN
                    IF (@SLCANGHEP > 0)
                    BEGIN
                        IF (@HT_SL < @SLCANGHEP)
                        BEGIN
                            SET @LOTGHEP = @LOTGHEP + ',' + @HT_LOT + '-' + CAST(@HT_SL AS NVARCHAR);
                            SET @SLCANGHEP = @SLCANGHEP - @HT_SL;
                            SET @SOLUONG = @SOLUONG - @HT_SL;
                            UPDATE STOCKTP SET SLCONLAITMP = 0 WHERE PART = @MAHANG AND LEFT(LOT, 20) = @HT_LOT;
                        END
                        ELSE
                        BEGIN
                            SET @LOTGHEP = @LOTGHEP + ',' + @HT_LOT + '-' + CAST(@SLCANGHEP AS NVARCHAR);
                            INSERT INTO #TMPGHEPLOT VALUES (@MAHANG, @GIOGIAO, @LOTGHEP);
                            SET @LOTGHEP = '';

                            SET @DuDuoi = (@HT_SL - @SLCANGHEP) % @QCDG;
                            IF (@DuDuoi = 0)
                                SET @SLCANGHEP = 0;
                            ELSE
                            BEGIN
                                SET @SLCANGHEP = @QCDG - @DuDuoi;
                                SET @LOTGHEP = @HT_LOT + '-' + CAST(@DuDuoi AS NVARCHAR);
                            END

                            SET @SOLUONG = @SOLUONG - @HT_SL;
                            UPDATE STOCKTP SET SLCONLAITMP = 0 WHERE PART = @MAHANG AND LEFT(LOT, 20) = @HT_LOT;
                        END
                    END
                    ELSE
                    BEGIN
                        SET @SLGHEP = @HT_SL % @QCDG;
                        IF (@SLGHEP > 0)
                        BEGIN
                            SET @SLCANGHEP = (@SOLUONG % @QCDG) - @SLGHEP;
                            IF @SLCANGHEP < 0 SET @SLCANGHEP = @QCDG - @SLGHEP;

                            IF @LOTGHEP = ''
                                SET @LOTGHEP = @HT_LOT + '-' + CAST(@SLGHEP AS NVARCHAR);
                            ELSE
                                SET @LOTGHEP = @LOTGHEP + ',' + @HT_LOT + '-' + CAST(@SLGHEP AS NVARCHAR);
                        END
                        ELSE
                        BEGIN
                            SET @SLCANGHEP = 0;
                            IF @LOTGHEP <> ''
                            BEGIN
                                INSERT INTO #TMPGHEPLOT VALUES (@MAHANG, @GIOGIAO, @LOTGHEP);
                                SET @LOTGHEP = '';
                            END
                        END
                        SET @SOLUONG = @SOLUONG - @HT_SL;
                        UPDATE STOCKTP SET SLCONLAITMP = 0 WHERE PART = @MAHANG AND LEFT(LOT, 20) = @HT_LOT;
                    END
                END
                ELSE
                BEGIN
                    -- @HT_SL >= @SOLUONG
                    IF (@SLCANGHEP > 0)
                    BEGIN
                        IF (@HT_SL >= @SLCANGHEP)
                        BEGIN
                            SET @LOTGHEP = @LOTGHEP + ',' + @HT_LOT + '-' + CAST(@SLCANGHEP AS NVARCHAR);
                            INSERT INTO #TMPGHEPLOT VALUES (@MAHANG, @GIOGIAO, @LOTGHEP);
                            -- dung @SLCANGHEP TRUOC khi reset
                            UPDATE STOCKTP SET SLCONLAITMP = @HT_SL - @SLCANGHEP
                                WHERE PART = @MAHANG AND LEFT(LOT, 20) = @HT_LOT;
                            SET @LOTGHEP = '';
                            SET @SLCANGHEP = 0;
                            SET @SOLUONG = 0;
                            BREAK;
                        END
                        ELSE
                        BEGIN
                            SET @LOTGHEP = @LOTGHEP + ',' + @HT_LOT + '-' + CAST(@HT_SL AS NVARCHAR);
                            SET @SLCANGHEP = @SLCANGHEP - @HT_SL;
                            SET @SOLUONG = @SOLUONG - @HT_SL;
                            UPDATE STOCKTP SET SLCONLAITMP = 0
                                WHERE PART = @MAHANG AND LEFT(LOT, 20) = @HT_LOT;
                        END
                    END
                    ELSE
                    BEGIN
                        UPDATE STOCKTP SET SLCONLAITMP = @HT_SL - @SOLUONG
                            WHERE PART = @MAHANG AND LEFT(LOT, 20) = @HT_LOT;
                        SET @SOLUONG = 0;
                        BREAK;
                    END
                END
            END

            FETCH NEXT FROM DHT INTO @HT_LOT, @HT_SL;
        END

        CLOSE DHT;
        DEALLOCATE DHT;
        -------------------------------
        FETCH NEXT FROM IFSPGH INTO @MAHANG, @SOLUONG, @NGAYGIAO, @GIOGIAO;
    END
    CLOSE IFSPGH;
    DEALLOCATE IFSPGH;

    SELECT MH, GG, LG FROM #TMPGHEPLOT WHERE CHARINDEX(',', LG, 1) > 0;
END
GO