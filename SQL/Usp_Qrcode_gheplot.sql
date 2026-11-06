USE [B7R2_FCC]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROC [dbo].[Usp_Qrcode_gheplot]
    @TMPTABLE nvarchar(128) = N'TMPPHIEUGIAOHANG',
    @IFSTABLE nvarchar(128) = N'IFSPHIEUGIAOHANG',
    @USEGEAR bit = 0,
    @EXPECTEDTMPTABLE nvarchar(128) = NULL,
    @VIEWTABLEPREFIX nvarchar(128) = NULL 
AS  
BEGIN  

    SET NOCOUNT ON;

    /*
      Unified Gear path (YMVN MP):
      - Gear text on TMP stores B20Gear.Name (A..F); LOT character 13 stores B20Gear.Code (0..6).
      - Resolve Name -> Code once through B20Gear. Never compare the LOT character to Name.
      - Allocate against an in-memory stock snapshot; this read-only preview never updates STOCKTP.
      - Keep allocation state isolated per item/gear so one gear cannot consume another gear's pending quantity.
    */
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

        DECLARE @GearCol sysname =
            CASE WHEN @TMPTABLE = N'YMVN_TMPPHIEUGIAOHANG' THEN N'TTPHIEU' ELSE N'GEAR' END;

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@GearObjectId AND name=N'MAHANG')
           OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@GearObjectId AND name=N'SOLUONG')
           OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@GearObjectId AND name=N'NGAYGIAO')
           OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@GearObjectId AND name=N'GIOGIAO')
           OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@GearObjectId AND name=@GearCol)
            THROW 51223, N'TMP Gear thiếu MAHANG/SOLUONG/NGAYGIAO/GIOGIAO/GEAR.', 1;

        CREATE TABLE #GearSrc
        (
            MAHANG nvarchar(56) NOT NULL,
            SOLUONG int NOT NULL,
            NGAYGIAO datetime NULL,
            GIOGIAO nvarchar(50) NULL,
            GearText nvarchar(1000) NULL
        );

        DECLARE @GearSql nvarchar(max) =
            N'INSERT INTO #GearSrc(MAHANG,SOLUONG,NGAYGIAO,GIOGIAO,GearText)
              SELECT MAHANG, TRY_CONVERT(int,SOLUONG), NGAYGIAO, GIOGIAO,' + QUOTENAME(@GearCol) +
              N' FROM dbo.' + QUOTENAME(@TMPTABLE) +
              N' WHERE ISNULL(TRY_CONVERT(int,SOLUONG),0) > 0 AND ISNULL(MAHANG,'''') <> '''';';
        EXEC sys.sp_executesql @GearSql;

        CREATE TABLE #GearStock
        (
            StockId int IDENTITY(1,1) PRIMARY KEY,
            PART nvarchar(100) NOT NULL,
            LOT13 nvarchar(13) NOT NULL,
            GearCode nvarchar(10) NULL,
            Qty int NOT NULL,
            SortKey decimal(38,0) NULL
        );

        INSERT INTO #GearStock(PART,LOT13,GearCode,Qty,SortKey)
        SELECT s.PART, LEFT(s.LOT,13), SUBSTRING(LEFT(s.LOT,13),13,1),
               CONVERT(int,s.SLCONLAI),
               TRY_CONVERT(decimal(38,0),LEFT(s.LOT,13))
        FROM dbo.STOCKTP s
        WHERE s.SLCONLAI > 0
          AND LEN(ISNULL(s.LOT,'')) >= 13
          AND EXISTS(SELECT 1 FROM #GearSrc src WHERE src.MAHANG=s.PART);

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
                @LotKey nvarchar(13), @StockQty int, @Pending int, @Remainder int,
                @LotList nvarchar(1000), @Need int, @ParsedQty int, @DigitStart int,
                @Digits nvarchar(40), @RowGearName nvarchar(100), @RowGearCode nvarchar(10);

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
              Input examples:
                Gear D : 5000pcs, Gear E : 300pcs
              The letter is a B20Gear.Name; LOT[13] is B20Gear.Code.
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
                -- Reset allocation state for EACH gear item.
                SET @Need = @ItemQty;
                SET @Pending = 0;
                SET @LotList = N'';

                DECLARE GearLotCur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT StockId,LOT13,Qty
                    FROM #GearStock
                    WHERE PART=@Part AND GearCode=@ItemGearCode AND Qty>0
                    ORDER BY SortKey,LOT13,StockId;
                DECLARE @StockId int;
                OPEN GearLotCur;
                FETCH NEXT FROM GearLotCur INTO @StockId,@LotKey,@StockQty;
                WHILE @@FETCH_STATUS=0 AND @Need>0
                BEGIN
                    IF @StockQty < @Need
                    BEGIN
                        SET @Remainder = @StockQty % @PackQty;
                        IF @Remainder=0
                        BEGIN
                            SET @Need = @Need - @StockQty;
                            UPDATE #GearStock SET Qty=0 WHERE StockId=@StockId;
                        END
                        ELSE
                        BEGIN
                            SET @Pending = @PackQty - @Remainder;
                            SET @LotList = CASE WHEN @LotList=N'' THEN N'' ELSE @LotList+N',' END
                                          + @LotKey+N'-'+CONVERT(nvarchar(20),@Remainder);
                            SET @Need = @Need - @StockQty;
                            UPDATE #GearStock SET Qty=0 WHERE StockId=@StockId;
                        END;
                    END
                    ELSE
                    BEGIN
                        IF @Pending>0
                        BEGIN
                            SET @LotList = CASE WHEN @LotList=N'' THEN N'' ELSE @LotList+N',' END
                                          + @LotKey+N'-'+CONVERT(nvarchar(20),@Pending);
                            INSERT INTO #GearResult(MH,GG,LG,GearName)
                            VALUES(@Part,@DeliveryTime,@LotList,@ItemGearName);
                            SET @LotList=N'';
                            SET @Pending=0;
                        END;
                        UPDATE #GearStock SET Qty=Qty-@Need WHERE StockId=@StockId;
                        SET @Need=0;
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

  
    DECLARE @MAHANG NVARCHAR(56), @SOLUONG INT, @NGAYGIAO SMALLDATETIME, @GIOGIAO NVARCHAR(50),  @EnforceFifo BIT = 0,
            @QCDG INT, @STT INT, @HT_LOT NVARCHAR(30), @HT_SL INT, @SQL NVARCHAR(MAX);  
    SET @STT = 0;  
  
    IF OBJECT_ID('TempDb..#TMPGHEPLOT') IS NOT NULL DROP TABLE #TMPGHEPLOT;  
  
    SELECT TOP 0 CAST('' AS NVARCHAR(56)) AS MH, CAST('' AS NVARCHAR(50)) AS GG, CAST('' AS NVARCHAR(256)) AS LG  
    INTO #TMPGHEPLOT;  
  
    -- ── Cập nhật SLCONLAITMP ban đầu từ SLCONLAI thật ────────────────────  
    SET @SQL = N'  
    UPDATE STOCKTP SET SLCONLAITMP = SLCONLAI  
    WHERE PART IN (SELECT MAHANG FROM ' + QUOTENAME(@IFSTABLE) + N' GROUP BY MAHANG)  
      AND SLCONLAI > 0';  
    EXEC sp_executesql @SQL;  
  
    -- ── Cursor 1: duyệt từng mã hàng/ngày giao/giờ giao trong IFS ────────  
    SET @SQL = N'  
    SELECT MAHANG, SUM(SOLUONG) AS SOLUONG, NGAYGIAO, GIOGIAO  
    FROM ' + QUOTENAME(@IFSTABLE) + N'  
    GROUP BY MAHANG, NGAYGIAO, GIOGIAO';  
  
    DECLARE @IFSCursorTable TABLE (MAHANG NVARCHAR(56), SOLUONG INT, NGAYGIAO SMALLDATETIME, GIOGIAO NVARCHAR(50));  
    INSERT INTO @IFSCursorTable  
    EXEC sp_executesql @SQL;  
  
    DECLARE IFSPGH CURSOR FOR  
        SELECT MAHANG, SOLUONG, NGAYGIAO, GIOGIAO FROM @IFSCursorTable;  
  
    OPEN IFSPGH;  
    FETCH NEXT FROM IFSPGH INTO @MAHANG, @SOLUONG, @NGAYGIAO, @GIOGIAO;  
  
    WHILE @@FETCH_STATUS = 0  
    BEGIN  
        SET @STT = @STT + 1;  
  
        -- ★ SỬA LỖI 2 — QCDG phải > 0, nếu không có/không hợp lệ thì fallback = 1  
        -- (thay cho -1 cũ, tránh CEILING(x / -1) ra số âm vô nghĩa).  
        -- ⚠️ CẦN XÁC NHẬN: fallback = 1 có đúng nghiệp vụ khi mã hàng chưa  
        -- khai báo MinCloseQty không, hay nên chặn hẳn (raise lỗi) để buộc  
        -- khai báo QC đóng gói trước khi ghép lot?  
        SELECT @QCDG = CASE WHEN ISNULL(CAST(MinCloseQty AS INT), 0) > 0  
                            THEN CAST(MinCloseQty AS INT)  
                            ELSE 1 END  
        FROM B20Item WHERE Code = @MAHANG;  
  
        IF @QCDG IS NULL SET @QCDG = 1;  
        SELECT @EnforceFifo = ISNULL(EnforceFifo, 0) FROM FVN_ItemFifoConfig WHERE ItemCode = @MAHANG;
        -- ★ SỬA LỖI 1 — bỏ điều kiện CUA/TRUYEN (luôn NULL, khiến UPDATE  
        -- không bao giờ chạm dòng nào). Chỉ giữ điều kiện thật cần thiết.  
        SET @SQL = N'  
        UPDATE ' + QUOTENAME(@TMPTABLE) + N'  
        SET STT = @p_stt, HOP = CEILING(CAST(@p_sl AS FLOAT) / @p_qcdg)  
        WHERE MAHANG = @p_mahang AND SOLUONG = @p_sl AND GIOGIAO = @p_giogiao';  
  
        EXEC sp_executesql @SQL,  
            N'@p_stt INT, @p_sl INT, @p_qcdg INT, @p_mahang NVARCHAR(56), @p_giogiao NVARCHAR(50)',  
            @STT, @SOLUONG, @QCDG, @MAHANG, @GIOGIAO;  
  
        -- ── Duyệt ghép LOT ────────────────────────────────────────────────  
        DECLARE @LOTGHEP NVARCHAR(250) = '', @SLCANGHEP INT = 0, @SLGHEP INT = 0, @SLGIAO_CONLAI INT;  
        SET @LOTGHEP = '';  
  
        -- ★ SỬA LỖI 3 — chuẩn hóa LOT nhất quán: lưu CẢ độ dài đã cắt vào  
        -- biến riêng (@HT_LOT_LEN), dùng lại đúng độ dài đó khi UPDATE  
        -- STOCKTP phía dưới — không còn giả định cứng "13 ký tự" cho mọi  
        -- trường hợp (sai với khách 0100002/YMVN có LOT dài khác).  
      DECLARE DHT CURSOR FOR
    SELECT
        CASE
            WHEN LEN(b.LOT) > 19 THEN
                CASE WHEN a.CustomerCode = '0100002'
                    THEN SUBSTRING(b.LOT, 1, LEN(b.LOT) - 6)
                    ELSE SUBSTRING(b.LOT, 1, 13)
                END
            WHEN LEN(b.LOT) = 16 THEN SUBSTRING(b.LOT, 1, 13)
            ELSE b.LOT
        END AS LotChuan,
        b.SLCONLAITMP
    FROM STOCKTP b
    LEFT OUTER JOIN B20ItemQuyCach a ON a.ItemCode = b.PART
    WHERE b.PART = @MAHANG AND b.SLCONLAI > 0
    ORDER BY
        CASE WHEN @EnforceFifo = 1
            THEN b.NGAYNHAP          -- ★ FIFO thật: ngày nhập kho sớm nhất trước
            ELSE NULL
        END ASC,
        CASE WHEN @EnforceFifo = 0
            THEN b.SLCONLAI          -- ★ Không FIFO: xem lựa chọn bên dưới
            ELSE NULL
        END DESC; 
  
        OPEN DHT;          FETCH NEXT FROM DHT INTO @HT_LOT, @HT_SL;  
  
        WHILE @@FETCH_STATUS = 0  
        BEGIN  
            IF (@QCDG IS NULL)  
                BREAK;  
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
                            -- ★ SỬA LỖI 3 — so khớp bằng LOT đã chuẩn hóa từ cursor DHT  
                            -- (@HT_LOT), KHÔNG còn giả định cắt cứng 13 ký tự ở đây.  
                            UPDATE STOCKTP SET SLCONLAITMP = 0  
                            WHERE PART = @MAHANG AND (  
                                CASE WHEN LEN(LOT) > 19 THEN  
                                    CASE WHEN EXISTS (SELECT 1 FROM B20ItemQuyCach q WHERE q.ItemCode = STOCKTP.PART AND q.CustomerCode = '0100002')  
                                        THEN SUBSTRING(LOT, 1, LEN(LOT) - 6)  
                                        ELSE SUBSTRING(LOT, 1, 13) END  
                                WHEN LEN(LOT) = 16 THEN SUBSTRING(LOT, 1, 13)  
                                ELSE LOT END  
                            ) = @HT_LOT;  
                        END  
                        ELSE  
                        BEGIN  
                            SET @LOTGHEP = @LOTGHEP + ',' + @HT_LOT + '-' + CAST(@SLCANGHEP AS NVARCHAR);  
                            INSERT INTO #TMPGHEPLOT VALUES (@MAHANG, @GIOGIAO, @LOTGHEP);  
                            SET @LOTGHEP = '';  
                            SET @SLGHEP = (@HT_SL - @SLCANGHEP) % @QCDG;  
                            IF (@SLGHEP = 0)  
                                SET @SLCANGHEP = 0;  
                            ELSE  
                            BEGIN  
                                SET @SLCANGHEP = @QCDG - @SLGHEP;  
                                SET @LOTGHEP = @HT_LOT + '-' + CAST(@SLGHEP AS NVARCHAR);  
                            END  
                            SET @SOLUONG = @SOLUONG - @HT_SL;  
                            UPDATE STOCKTP SET SLCONLAITMP = 0  
                            WHERE PART = @MAHANG AND (  
                                CASE WHEN LEN(LOT) > 19 THEN  
                                    CASE WHEN EXISTS (SELECT 1 FROM B20ItemQuyCach q WHERE q.ItemCode = STOCKTP.PART AND q.CustomerCode = '0100002')  
                                        THEN SUBSTRING(LOT, 1, LEN(LOT) - 6)  
                                        ELSE SUBSTRING(LOT, 1, 13) END  
                                WHEN LEN(LOT) = 16 THEN SUBSTRING(LOT, 1, 13)  
                                ELSE LOT END  
                            ) = @HT_LOT;  
                        END  
                    END  
                    ELSE  
                    BEGIN  
                        SET @SLGHEP = @HT_SL % @QCDG;  
                        IF (@SLGHEP = 0)  
                            SET @SLCANGHEP = 0;  
                        ELSE  
                            SET @SLCANGHEP = @QCDG - @SLGHEP;  
  
                        IF (@SLGHEP > 0)  
                        BEGIN  
                            IF @LOTGHEP = ''  
                            BEGIN  
                                SET @LOTGHEP = @HT_LOT + '-' + CAST(@SLGHEP AS NVARCHAR);  
                                UPDATE STOCKTP SET SLCONLAITMP = 0  
                                WHERE PART = @MAHANG AND (  
                                    CASE WHEN LEN(LOT) > 19 THEN  
                                        CASE WHEN EXISTS (SELECT 1 FROM B20ItemQuyCach q WHERE q.ItemCode = STOCKTP.PART AND q.CustomerCode = '0100002')  
                                            THEN SUBSTRING(LOT, 1, LEN(LOT) - 6)  
               ELSE SUBSTRING(LOT, 1, 13) END  
                                    WHEN LEN(LOT) = 16 THEN SUBSTRING(LOT, 1, 13)  
                                    ELSE LOT END  
                                ) = @HT_LOT;  
                                SET @SOLUONG = @SOLUONG - @HT_SL;  
                            END  
                            ELSE  
                            BEGIN  
                                SET @LOTGHEP = @LOTGHEP + ',' + @HT_LOT + '-' + CAST(@SLGHEP AS NVARCHAR);  
                                UPDATE STOCKTP SET SLCONLAITMP = 0  
                                WHERE PART = @MAHANG AND (  
                                    CASE WHEN LEN(LOT) > 19 THEN  
                                        CASE WHEN EXISTS (SELECT 1 FROM B20ItemQuyCach q WHERE q.ItemCode = STOCKTP.PART AND q.CustomerCode = '0100002')  
                                            THEN SUBSTRING(LOT, 1, LEN(LOT) - 6)  
                                            ELSE SUBSTRING(LOT, 1, 13) END  
                                    WHEN LEN(LOT) = 16 THEN SUBSTRING(LOT, 1, 13)  
                                    ELSE LOT END  
                                ) = @HT_LOT;  
                                SET @SLGIAO_CONLAI = @SOLUONG - @HT_SL;  
                            END  
                        END  
                        ELSE  
                        BEGIN  
                            SET @SLCANGHEP = 0;  
                            SET @SOLUONG = @SOLUONG - @HT_SL;  
                        END  
                    END  
                END  
                ELSE  
                BEGIN  
                    IF (@SLCANGHEP > 0)  
                    BEGIN  
                        SET @LOTGHEP = @LOTGHEP + ',' + @HT_LOT + '-' + CAST(@SLCANGHEP AS NVARCHAR);  
                        INSERT INTO #TMPGHEPLOT VALUES (@MAHANG, @GIOGIAO, @LOTGHEP);  
                        SET @LOTGHEP = '';  
                        SET @SLCANGHEP = 0;  
                        UPDATE STOCKTP SET SLCONLAITMP = @HT_SL - @SOLUONG  
                        WHERE PART = @MAHANG AND (  
                            CASE WHEN LEN(LOT) > 19 THEN  
                                CASE WHEN EXISTS (SELECT 1 FROM B20ItemQuyCach q WHERE q.ItemCode = STOCKTP.PART AND q.CustomerCode = '0100002')  
                                    THEN SUBSTRING(LOT, 1, LEN(LOT) - 6)  
                                    ELSE SUBSTRING(LOT, 1, 13) END  
                            WHEN LEN(LOT) = 16 THEN SUBSTRING(LOT, 1, 13)  
                            ELSE LOT END  
                        ) = @HT_LOT;  
                        SET @SOLUONG = 0;  
                        BREAK;  
                    END  
                    ELSE  
                    BEGIN  
                        SET @SLCANGHEP = 0;  
                        UPDATE STOCKTP SET SLCONLAITMP = @HT_SL - @SOLUONG  
                        WHERE PART = @MAHANG AND (  
                            CASE WHEN LEN(LOT) > 19 THEN  
                                CASE WHEN EXISTS (SELECT 1 FROM B20ItemQuyCach q WHERE q.ItemCode = STOCKTP.PART AND q.CustomerCode = '0100002')  
                                    THEN SUBSTRING(LOT, 1, LEN(LOT) - 6)  
                                    ELSE SUBSTRING(LOT, 1, 13) END  
                            WHEN LEN(LOT) = 16 THEN SUBSTRING(LOT, 1, 13)  
                            ELSE LOT END  
                        ) = @HT_LOT;  
                        BREAK;  
                    END  
                END  
            END  
  
            FETCH NEXT FROM DHT INTO @HT_LOT, @HT_SL;  
        END  
  
        CLOSE DHT;  
        DEALLOCATE DHT;  
  
        FETCH NEXT FROM IFSPGH INTO @MAHANG, @SOLUONG, @NGAYGIAO, @GIOGIAO;  
    END  
  
    CLOSE IFSPGH;  
    DEALLOCATE IFSPGH;  
  
    SELECT * FROM #TMPGHEPLOT WHERE CHARINDEX(',', LG, 1) > 0;  
END

GO
