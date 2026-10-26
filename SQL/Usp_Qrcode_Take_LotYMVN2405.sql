USE [B7R2_FCC]
GO

/****** Object:  StoredProcedure [dbo].[Usp_Qrcode_Take_LotYMVN2405]    Script Date: 9/8/2026 3:30:27 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROC [dbo].[Usp_Qrcode_Take_LotYMVN2405]       
    @TMPTABLE    nvarchar(100) = N'TMPPHIEUGIAOHANG',       
    @DOCQRTABLE  nvarchar(100) = N'DOCQRCODE',       
    @ISLOAISP    int           = 0       
AS       
BEGIN       
    SET NOCOUNT ON;       

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

    DECLARE @sql nvarchar(max);       
    
    -- ── Bước 0: Fix kiểu cột STATUS nếu đang là smallint ─────────────    
    SET @sql = N'    
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N''' + @TMPTABLE + N''' AND COLUMN_NAME = N''STATUS'' AND DATA_TYPE = N''smallint'')    
        ALTER TABLE [' + @TMPTABLE + N'] ALTER COLUMN STATUS nvarchar(10) NULL;    
        
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N''' + @DOCQRTABLE + N''' AND COLUMN_NAME = N''STATUS'' AND DATA_TYPE = N''smallint'')    
        ALTER TABLE [' + @DOCQRTABLE + N'] ALTER COLUMN STATUS nvarchar(10) NULL;';    
    EXEC sp_executesql @sql;    
    
    -- ── Bước 1: Tạo #LotTmp ─────────────────────────────────────────    
    IF OBJECT_ID('tempdb..#LotTmp') IS NOT NULL DROP TABLE #LotTmp;    
    
    CREATE TABLE #LotTmp (    
        MAHANG      nvarchar(50),    
        STT         int,    
        LOT_GHEP    nvarchar(max),    
        TONG_SLHVN  bigint,    
        TONG_SLFCC  bigint,    
        SL_GIAO     bigint    
    );    
    
    -- ── Bước 2: Tổng hợp LOT từ DOCQR ───────────────────────────────    
    -- Sử dụng TRY_CAST để tránh lỗi khi dữ liệu trong cột không phải là số hợp lệ    
    SET @sql = N'    
    INSERT INTO #LotTmp (MAHANG, STT, LOT_GHEP, TONG_SLHVN, TONG_SLFCC, SL_GIAO)    
    SELECT     
        MAHANG,     
        STT,     
        LOT_GHEP,     
        TONG_SLHVN,     
        TONG_SLFCC,     
        SL_GIAO    
    FROM (    
        SELECT    
            RTRIM(d.MAHANGFCC) AS MAHANG,    
            t.STT AS STT,    
            ISNULL(TRY_CAST(t.SOLUONG AS bigint), 0) AS SL_GIAO,    
            SUM(ISNULL(TRY_CAST(d.SLTEMHVN AS bigint), 0)) AS TONG_SLHVN,    
            SUM(ISNULL(TRY_CAST(d.SLTEMFCC AS bigint), 0)) AS TONG_SLFCC,    
            STUFF((    
                SELECT N'','' + RTRIM(ISNULL(d2.LOTHVN, d2.LOTFCC)) + N''-''     
                     + CONVERT(nvarchar(20), ISNULL(TRY_CAST(d2.SLTEMHVN AS bigint), 0))    
                FROM [' + @DOCQRTABLE + N'] d2    
                WHERE RTRIM(d2.MAHANGFCC) = RTRIM(d.MAHANGFCC)    
                ORDER BY d2.STT    
                FOR XML PATH(N''''), TYPE    
            ).value(N''(./text())[1]'', N''nvarchar(max)''), 1, 1, N'''') AS LOT_GHEP    
        FROM [' + @DOCQRTABLE + N'] d    
        INNER JOIN [' + @TMPTABLE + N'] t ON RTRIM(d.MAHANGFCC) = RTRIM(t.MAHANG)    
        GROUP BY RTRIM(d.MAHANGFCC), t.STT, t.SOLUONG    
    ) AS grp';    
    
    EXEC sp_executesql @sql;    
    
    -- ── Bước 3: UPDATE TMPTABLE ──────────────────────────────────────    
    SET @sql = N'    
    UPDATE t    
    SET    
        t.LOT       = l.LOT_GHEP,    
        -- t.STATUS    = CASE WHEN l.TONG_SLHVN >= l.SL_GIAO THEN N''OK'' ELSE N''NG'' END,    
        t.STATUSDOC = CASE WHEN l.TONG_SLHVN >= l.SL_GIAO THEN N''OK'' ELSE N''NG'' END,    
        t.TTPHIEU   = CASE WHEN l.TONG_SLHVN >= l.SL_GIAO THEN N''DG'' ELSE N'''' END    
    FROM [' + @TMPTABLE + N'] t    
    INNER JOIN #LotTmp l ON l.STT = ISNULL(TRY_CAST(t.STT AS int), -1) AND l.MAHANG = RTRIM(t.MAHANG)';    
    
    EXEC sp_executesql @sql;    
    
    -- ── Bước 4: UPDATE DOCQRTABLE ────────────────────────────────────    
    SET @sql = N'    
    UPDATE d    
    SET d.KETQUA = CASE WHEN l.TONG_SLHVN >= l.SL_GIAO THEN N''DG'' ELSE d.KETQUA END    
    FROM [' + @DOCQRTABLE + N'] d    
    INNER JOIN #LotTmp l ON RTRIM(d.MAHANGFCC) = l.MAHANG';    
    
    EXEC sp_executesql @sql;    
    
    -- ── Bước 5: Xuất kết quả ─────────────────────────────────────────    
    SET @sql = N'    
    SELECT    
        t.STT, t.MAHANG, t.TENHANG, t.LOT, t.SOLUONG, t.STATUS, t.STATUSDOC,    
        ISNULL(l.TONG_SLHVN, 0) AS TONG_SLHVN,    
        ISNULL(TRY_CAST(t.SOLUONG AS bigint), 0) AS SL_GIAO,    
        CASE WHEN ISNULL(l.TONG_SLHVN, 0) >= ISNULL(TRY_CAST(t.SOLUONG AS bigint), 0) THEN 1 ELSE 0 END AS IsOK    
    FROM [' + @TMPTABLE + N'] t    
    LEFT JOIN #LotTmp l ON l.STT = ISNULL(TRY_CAST(t.STT AS int), -1) AND l.MAHANG = RTRIM(t.MAHANG)    
    ORDER BY t.STT';    
    
    EXEC sp_executesql @sql;    
    
    DROP TABLE #LotTmp;    
END
GO


