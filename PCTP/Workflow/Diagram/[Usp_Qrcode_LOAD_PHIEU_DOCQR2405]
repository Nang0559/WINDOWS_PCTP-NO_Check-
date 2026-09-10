CREATE PROC [dbo].[Usp_Qrcode_LOAD_PHIEU_DOCQR2405]    
    @NGAYGIAO   nvarchar(20),    
    @NHAMAY     nvarchar(50)  = N'',    
    @GIOFCC     nvarchar(200) = N'',    
    @ADDNM      int,    
    @TMPTABLE   nvarchar(100) = N'TMPPHIEUGIAOHANG',    
    @IFSTABLE   nvarchar(100) = N'IFSPHIEUGIAOHANG',    
    @DOCQRTABLE nvarchar(100) = N'DOCQRCODE'    
AS    
BEGIN    
    SET NOCOUNT ON;    
    
    -- ── 1. Build #GioList (giữ INT như SP cũ) ───────────────────────────    
    CREATE TABLE #GioList (GioInt int)    
    INSERT INTO #GioList    
    SELECT TRY_CAST(LTRIM(RTRIM(REPLACE(value, '''', ''))) AS INT)    
    FROM   STRING_SPLIT(REPLACE(@GIOFCC, '''', ''), ',')    
    WHERE  LTRIM(RTRIM(REPLACE(value, '''', ''))) <> ''    
      AND  LTRIM(RTRIM(REPLACE(value, '''', ''))) <> '(GIAO DB)'    
      AND  TRY_CAST(LTRIM(RTRIM(REPLACE(value, '''', ''))) AS INT) IS NOT NULL    
    
    -- ── 2. Đếm IFS ──────────────────────────────────────────────────────    
    DECLARE @TTIFS int    
    DECLARE @SQL   nvarchar(max)    
    SET @SQL = N'SELECT @CNT = COUNT(*) FROM ' + QUOTENAME(@IFSTABLE)    
    EXEC sp_executesql @SQL, N'@CNT int OUTPUT', @TTIFS OUTPUT    
    
    -- ── 3. Chuẩn hóa NHAMAY ─────────────────────────────────────────────    
    IF CHARINDEX('(NHA MAY HA NAM)', @NHAMAY) > 0    
        SET @NHAMAY = '%NHA MAY HA NAM%'    
    ELSE    
        SET @NHAMAY = '%(NHA MAY VP)'    
    
    IF @NHAMAY LIKE '%NHA MAY VP%'    
        SET @ADDNM = 1    
    ELSE    
        SET @ADDNM = 2    
    
    -- ── 4. Normalize LUUPHIEUGIAOHANG — chỉ theo ngày+nhà máy ──────────    
    -- GIỮ từ SP mới: filter NGAYGIAO + NHAMAY, không update toàn bảng    
    UPDATE LUUPHIEUGIAOHANG    
    SET GIOGIAO = CASE    
        WHEN GIOGIAO = '0H'    THEN '00H'    
        WHEN GIOGIAO LIKE '%H' THEN REPLACE(GIOGIAO, 'H', '')    
        ELSE GIOGIAO END    
    WHERE NHAMAY   LIKE @NHAMAY    
      AND NGAYGIAO  = @NGAYGIAO    
      AND (GIOGIAO = '0H' OR GIOGIAO LIKE '%H')    
    
    UPDATE LUUPHIEUGIAOHANG    
    SET NHAMAY = 'HON DA - VIET NAM(NHA MAY VP)'    
    WHERE NHAMAY   LIKE '%VP)'    
      AND NGAYGIAO  = @NGAYGIAO    
    
    UPDATE LUUPHIEUGIAOHANG    
    SET GIOGIAOFCC = GIOGIAO    
    WHERE GIOGIAOFCC = '(GIAO DB)'    
      AND GIOGIAO   <> '05'    
      AND NGAYGIAO   = @NGAYGIAO    
      AND NHAMAY    LIKE @NHAMAY    
    
    -- ── 5. Chuẩn hóa GIOGIAOFCC — SET-BASED (GIỮ từ SP mới) ────────────    
    UPDATE luu    
    SET luu.GIOGIAOFCC = CASE    
        WHEN @NHAMAY LIKE '%NHA MAY HA NAM%' THEN ct.GIOFCCHN    
        ELSE ct.GioFCCVP    
    END    
    FROM LUUPHIEUGIAOHANG luu    
    INNER JOIN QRCODE_CHANGETIME ct    
           ON  ct.GIOHVN        = luu.GIOGIAOFCC    
    WHERE  luu.NHAMAY          LIKE @NHAMAY    
      AND  luu.NGAYGIAO         = @NGAYGIAO    
      AND  luu.GIOGIAOFCC NOT LIKE '%H'    
      AND  luu.GIOGIAOFCC NOT LIKE '(O TYPE%'    
      AND  luu.NHAMAY          <> 'YAMAHA - VIET NAM'    
    
    -- ── 6. GIAO DB ───────────────────────────────────────────────────────    
    IF @GIOFCC = '(GIAO DB)'    
    BEGIN    
        SET @SQL =    
            N'DELETE ' + QUOTENAME(@TMPTABLE) + N';' +    
            N'INSERT INTO ' + QUOTENAME(@TMPTABLE) +    
            N' (STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,' +    
            N'  STATUS,TTPHIEU,NHAMAY,ADDNM,HOP,STATUSDOC,Note,PO_NO,PO_ITEM)' +    
            N' SELECT STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,' +    
            N'        STATUS,TTPHIEU,NHAMAY,ADDNM,HOP,STATUSDOC,Note,' +    
            N'        ISNULL(PO_NO,''''),ISNULL(PO_ITEM,'''')' +    
            N' FROM ' + QUOTENAME(@IFSTABLE)    
        EXEC sp_executesql @SQL    
        GOTO XUAT_KQ    
    END    
    
    -- ── 7. Kiểm tra DOCQR — đang bắn dở → giữ nguyên TMP ───────────────    
    DECLARE @CntDocQR int = 0    
    SET @SQL = N'SELECT @CNT = COUNT(*) FROM ' + QUOTENAME(@DOCQRTABLE)    
    EXEC sp_executesql @SQL, N'@CNT int OUTPUT', @CntDocQR OUTPUT    
    
    IF @CntDocQR > 0    
        GOTO XUAT_KQ    
    
    -- ── 8. Không bắn QR → load mới ──────────────────────────────────────    
    SET @SQL = N'DELETE ' + QUOTENAME(@TMPTABLE)    
    EXEC sp_executesql @SQL    
    
    -- ── 9. TTIFS = 0 → IFS đã close → đọc thẳng LUUPHIEUGIAOHANG ────────    
    IF @TTIFS = 0    
    BEGIN    
        SET @SQL = N'    
            INSERT INTO ' + QUOTENAME(@TMPTABLE) + N'    
                (STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,    
                 NGAYGIAO,GIOGIAO,STATUS,TTPHIEU,NHAMAY,ADDNM,    
                 HOP,STATUSDOC,Note,PO_NO,PO_ITEM)    
            SELECT    
                STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,    
                NGAYGIAO,GIOGIAO,STATUS,TTPHIEU,NHAMAY,@ADDNM,    
                HOP,STATUSDOC,Note,    
                ISNULL(PO_NO,''''), ISNULL(PO_ITEM,'''')    
            FROM LUUPHIEUGIAOHANG    
            WHERE CAST(NGAYGIAO AS date) = CAST(@NGAYGIAO AS date)    
              AND NHAMAY LIKE @NHAMAY    
              AND GIOGIAO NOT LIKE ''%[^0-9]%''    
              AND GIOGIAO <> ''''    
              AND (    
                  NOT EXISTS (SELECT 1 FROM #GioList)    
                  OR TRY_CAST(GIOGIAO AS INT) IN (SELECT GioInt FROM #GioList)    
              )'    
        EXEC sp_executesql @SQL,    
            N'@NGAYGIAO nvarchar(20), @NHAMAY nvarchar(50), @ADDNM int',    
            @NGAYGIAO, @NHAMAY, @ADDNM    
        GOTO XUAT_KQ    
    END    
    
    -- ── 10. TTIFS > 0 → IFS có data → #LUU → #LUUGIO → LEFT JOIN ────────    
    -- GIỮ NGUYÊN từ SP cũ: đây là flow đúng, không dùng LEFT JOIN thô    
    CREATE TABLE #LUU (    
        MAHANG     nvarchar(50),    
        NGAYGIAO   date,    
        CUA        nvarchar(20),    
        TRUYEN     nvarchar(20),    
        SOLUONG    int,    
        GIOGIAO    nvarchar(10),    
        GIOGIAOFCC nvarchar(50),    
        LOT        nvarchar(500),    
        STATUS     nvarchar(20),    
        STATUSDOC  nvarchar(50)    
    )    
    
    CREATE TABLE #LUUGIO (    
        MAHANG    nvarchar(50),    
        NGAYGIAO  date,    
        CUA       nvarchar(20),    
        TRUYEN    nvarchar(20),    
        SOLUONG   int,    
        GIO_PARSE nvarchar(10),    
        LOT       nvarchar(500),    
        STATUS    nvarchar(20),    
        STATUSDOC nvarchar(50)    
    )    
    
    -- ── 11. INSERT #LUU — lấy dòng mới nhất theo TGLUU ──────────────────    
    INSERT INTO #LUU    
    SELECT    
        RTRIM(MAHANG), CAST(NGAYGIAO AS date),    
        RTRIM(CUA), RTRIM(TRUYEN),    
        SOLUONG, GIOGIAO, GIOGIAOFCC, LOT, STATUS, STATUSDOC    
    FROM (    
        SELECT *,    
            ROW_NUMBER() OVER (    
                PARTITION BY RTRIM(MAHANG), CAST(NGAYGIAO AS date),    
                             RTRIM(CUA), RTRIM(TRUYEN),    
                             SOLUONG, GIOGIAO, RTRIM(NHAMAY)    
                ORDER BY TGLUU DESC    
            ) AS RN    
        FROM LUUPHIEUGIAOHANG    
        WHERE CAST(NGAYGIAO AS date) = CAST(@NGAYGIAO AS date)    
          AND NHAMAY LIKE @NHAMAY    
          AND GIOGIAO NOT LIKE '%[^0-9]%'    
          AND GIOGIAO <> ''    
          AND (    
              NOT EXISTS (SELECT 1 FROM #GioList)    
              OR TRY_CAST(GIOGIAO AS INT) IN (SELECT GioInt FROM #GioList)    
          )    
    ) x    
    WHERE x.RN = 1    
    
    -- ── 12. INSERT #LUUGIO — chuẩn hóa GIO_PARSE ────────────────────────    
    INSERT INTO #LUUGIO    
    SELECT    
        l.MAHANG, l.NGAYGIAO, l.CUA, l.TRUYEN, l.SOLUONG,    
        RIGHT('00' + LTRIM(RTRIM(l.GIOGIAO)), 2) AS GIO_PARSE,    
        l.LOT, l.STATUS, l.STATUSDOC    
    FROM #LUU l    
    WHERE l.GIOGIAO <> ''    
      AND (    
          NOT EXISTS (SELECT 1 FROM #GioList)    
          OR TRY_CAST(l.GIOGIAO AS INT) IN (SELECT GioInt FROM #GioList)    
      )    
    
    -- Index để tăng tốc JOIN ở bước 13    
    CREATE NONCLUSTERED INDEX IX_LUUGIO    
        ON #LUUGIO (MAHANG, CUA, TRUYEN, SOLUONG, GIO_PARSE)    
    
    -- ── 13. INSERT vào TMP — LEFT JOIN IFS + #LUUGIO ─────────────────────    
    -- FIX NULL: ISNULL(CUA,'') và ISNULL(TRUYEN,'') — giữ từ SP cũ đã fix    
    SET @SQL = N'    
        INSERT INTO ' + QUOTENAME(@TMPTABLE) + N'    
            (STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,    
             STATUS,TTPHIEU,NHAMAY,ADDNM,HOP,STATUSDOC,Note,PO_NO,PO_ITEM)    
        SELECT    
            i.STT, i.CUA, i.TRUYEN, i.MAHANG, i.TENHANG,    
            ISNULL(l.LOT,       i.LOT),       i.DV, i.SOLUONG, i.NGAYGIAO, i.GIOGIAO,    
            ISNULL(l.STATUS,    i.STATUS),    i.TTPHIEU, i.NHAMAY, @ADDNM,    
            i.HOP,    
            ISNULL(l.STATUSDOC, i.STATUSDOC), i.Note,    
            ISNULL(i.PO_NO,  '''') AS PO_NO,    
            ISNULL(i.PO_ITEM,'''') AS PO_ITEM    
        FROM ' + QUOTENAME(@IFSTABLE) + N' i    
        LEFT JOIN #LUUGIO l    
            ON  l.MAHANG                  = RTRIM(i.MAHANG)    
            AND ISNULL(l.CUA,   '''')     = ISNULL(RTRIM(i.CUA),   '''')    
            AND ISNULL(l.TRUYEN,'''')     = ISNULL(RTRIM(i.TRUYEN),'''')    
            AND l.SOLUONG                 = i.SOLUONG    
            AND l.GIO_PARSE               = RIGHT(''00'' + LTRIM(RTRIM(i.GIOGIAO)), 2)'    
    EXEC sp_executesql @SQL, N'@ADDNM int', @ADDNM    
    
    DROP TABLE #LUU    
    DROP TABLE #LUUGIO    
    
    XUAT_KQ:    
    DROP TABLE #GioList    
    
    -- ── 14. Xuất kết quả ─────────────────────────────────────────────────    
    SET @SQL =    
        N'SELECT STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,' +    
        N'       NGAYGIAO,GIOGIAO,STATUS,TTPHIEU,NHAMAY,ADDNM,' +    
        N'       HOP,STATUSDOC,Note,' +    
        N'       ISNULL(PO_NO,'''')   AS PO_NO,' +    
        N'       ISNULL(PO_ITEM,'''') AS PO_ITEM' +    
        N' FROM ' + QUOTENAME(@TMPTABLE) +    
        N' ORDER BY TRY_CAST(STT AS INT), STT'    
    EXEC sp_executesql @SQL    
END
