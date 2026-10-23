CREATE PROC [dbo].[Usp_Qrcode_Update_Stock_SP]  
    @GIOGIAOFCC NVARCHAR(50) = N'',  
    @NHAMAY     NVARCHAR(50) = N''  
AS  
BEGIN  
    SET NOCOUNT ON;  
  
    IF OBJECT_ID('TempDb..#STOK_SP') IS NOT NULL  
        DROP TABLE #STOK_SP;  
  
    IF OBJECT_ID('TempDb..#ERR_SP') IS NOT NULL  
        DROP TABLE #ERR_SP;  
  
    CREATE TABLE #STOK_SP  
    (  
        STT      NVARCHAR(50),  
        LOT      NVARCHAR(50),  
        SOLUONG  INT,  
        STATUS   NVARCHAR(20)  
    );  
  
    CREATE TABLE #ERR_SP  
    (  
        MH       NVARCHAR(50),  
        LOT      NVARCHAR(50),  
        SLCAN    INT,  
        SLCO     INT,  
        STATUS   NVARCHAR(20)  
    );  
  
    DECLARE  
        @stt        NVARCHAR(50),  
        @maHang     NVARCHAR(50),  
        @slCan      INT,  
        @lotChon    NVARCHAR(50),  
        @slConLai   INT;  
  
    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR  
        SELECT  
            STT,  
            MAHANG,  
            SOLUONG  
        FROM TMPPHIEUGIAOHANG  
        WHERE ISNULL(STATUS, '') <> 'OK';  
  
    OPEN cur;  
  
    FETCH NEXT FROM cur  
        INTO @stt, @maHang, @slCan;  
  
    WHILE @@FETCH_STATUS = 0  
    BEGIN  
        SET @lotChon = NULL;  
        SET @slConLai = NULL;  
  
        /*  
        ============================================================  
        FIFO THEO LOT  
        ------------------------------------------------------------  
        6 ký tự đầu : YYMMDD  
        Ký tự thứ 7 : ca  
            0 = hành chính  
            1 = ca 1  
            2 = ca 2  
            3 = ca 3  
  
        FIFO = ngày sản xuất trước  
             -> ca trước  
        ============================================================  
        */  
  
        SELECT TOP 1  
            @lotChon  = s.LOT,  
            @slConLai = ISNULL(s.SLCONLAI, 0)  
        FROM STOCKTP s  
        WHERE s.SP = @maHang  
          AND ISNULL(s.SLCONLAI, 0) > 0  
  
          -- LOT phải có tối thiểu 7 ký tự  
          AND LEN(ISNULL(s.LOT, '')) >= 7  
  
          -- 6 ký tự đầu phải là ngày YYMMDD  
          AND TRY_CONVERT(  
                DATE,  
                LEFT(s.LOT, 6),  
                12  
              ) IS NOT NULL  
  
          -- Ký tự thứ 7 phải là ca 0-3  
          AND SUBSTRING(s.LOT, 7, 1) IN ('0', '1', '2', '3')  
  
        ORDER BY  
            LEFT(s.LOT, 6) ASC,  
  
            CASE SUBSTRING(s.LOT, 7, 1)  
                WHEN '0' THEN 0  
                WHEN '1' THEN 1  
                WHEN '2' THEN 2  
                WHEN '3' THEN 3  
            END ASC,  
  
            s.LOT ASC;  
  
        /*  
        ============================================================  
        KHÔNG CÓ LOT HOẶC LOT KHÔNG ĐỦ  
        ============================================================  
        */  
  
        IF @lotChon IS NULL  
           OR @slConLai < @slCan  
        BEGIN  
            INSERT INTO #ERR_SP  
            (  
                MH,  
                LOT,  
                SLCAN,  
                SLCO,  
                STATUS  
            )  
            VALUES  
            (  
                @maHang,  
                ISNULL(@lotChon, ''),  
                @slCan,  
                ISNULL(@slConLai, 0),  
                'THIEU_TON'  
            );  
        END  
        ELSE  
        BEGIN  
            /*  
            ========================================================  
            TRỪ TỒN STOCKTP  
            ========================================================  
            */  
  
            UPDATE STOCKTP  
            SET  
                SLXUAT   = ISNULL(SLXUAT, 0) + @slCan,  
                SLCONLAI = SLCONLAI - @slCan,  
                NGAYXUAT = GETDATE()  
            WHERE LOT = @lotChon  
              AND SP = @maHang;  
  
            /*  
            ========================================================  
            CẬP NHẬT PHIẾU TẠM  
            ========================================================  
            */  
  
            UPDATE TMPPHIEUGIAOHANG  
            SET  
                LOT = @lotChon,  
                STATUS = 'OK'  
            WHERE STT = @stt;  
  
            /*  
            ========================================================  
            KẾT QUẢ  
            ========================================================  
            */  
  
            INSERT INTO #STOK_SP  
            (  
                STT,  
                LOT,  
                SOLUONG,  
                STATUS  
            )  
            VALUES  
            (  
                @stt,  
                @lotChon,  
                @slCan,  
                'OK'  
            );  
        END;  
  
        FETCH NEXT FROM cur  
            INTO @stt, @maHang, @slCan;  
    END;  
  
    CLOSE cur;  
    DEALLOCATE cur;  
  
    /*  
    ================================================================  
    LƯU PHIẾU GIAO  
    ================================================================  
    */  
  
    INSERT INTO LUUPHIEUGIAOHANG  
    (  
        STT,  
        CUA,  
        TRUYEN,  
        MAHANG,  
        TENHANG,  
        LOT,  
        DV,  
        SOLUONG,  
        NGAYGIAO,  
        GIOGIAO,  
        STATUS,  
        TTPHIEU,  
        NHAMAY,  
        GIOGIAOFCC,  
        HOP,  
        STATUSDOC,  
        Note,  
        PO_NO,  
        PO_ITEM  
    )  
    SELECT  
        STT,  
        CUA,  
        TRUYEN,  
        MAHANG,  
        TENHANG,  
        LOT,  
        DV,  
        SOLUONG,  
        NGAYGIAO,  
        GIOGIAO,  
        'OK',  
        TTPHIEU,  
        @NHAMAY,  
        @GIOGIAOFCC,  
        HOP,  
        STATUSDOC,  
        Note,  
        PO_NO,  
        PO_ITEM  
    FROM TMPPHIEUGIAOHANG  
    WHERE STATUS = 'OK';  
  
    /*  
    ================================================================  
    TRẢ KẾT QUẢ  
    ================================================================  
    */  
  
    SELECT *  
    FROM #STOK_SP;  
  
    SELECT *  
    FROM #ERR_SP;  
END  
