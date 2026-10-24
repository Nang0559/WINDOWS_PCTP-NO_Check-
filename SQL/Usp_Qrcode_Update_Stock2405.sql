CREATE OR ALTER PROC [dbo].[Usp_Qrcode_Update_Stock2405]  
    @GIOGIAOFCC  NVARCHAR(250) = N'',  
    @NHAMAY      NVARCHAR(250) = N'',  
    @TMPTABLE    NVARCHAR(128) = N'TMPPHIEUGIAOHANG',  
    @DOCQRTABLE  NVARCHAR(128) = N'DOCQRCODE'  
AS  
BEGIN  
    SET NOCOUNT ON;  
  
    /*  
        ================================================================  
        QUY ƯỚC LOT  
        ================================================================  
  
        STOCKTP.LOT đầy đủ là định danh chính xác của LOT vật lý.

        - Nếu đầu vào dài hơn 13 ký tự: chỉ khớp RTRIM(STOCKTP.LOT) = RTRIM(@LOT).
        - Nếu đầu vào chỉ có mã gốc 13 ký tự: chỉ chấp nhận khi mã gốc ánh xạ tới
          đúng một LOT vật lý; nếu có nhiều LOT vật lý thì ghi lỗi và dừng trước
          mọi cập nhật tồn.
        - Tuyệt đối không cộng tồn của nhiều LOT vật lý theo tiền tố rồi trừ một
          LOT bất kỳ. Kiểm tra khả dụng và bước trừ kho phải dùng cùng quy tắc.
    */  
  
    --------------------------------------------------------------------  
    -- 1. WHITELIST TÊN BẢNG  
    --------------------------------------------------------------------  
    IF @TMPTABLE NOT IN  
    (  
        N'TMPPHIEUGIAOHANG',  
        N'TMPPHIEUGIAOHANG_SP',  
        N'TMPPHIEUGIAOHANG_100003'  
    )  
    BEGIN  
        RAISERROR(  
            N'Tên bảng TMPTABLE không hợp lệ: %s',  
            16,  
            1,  
            @TMPTABLE  
        );  
        RETURN;  
    END;  
  
    IF @DOCQRTABLE NOT IN  
    (  
        N'DOCQRCODE',  
        N'DOCQRCODE_SP',  
        N'DOCQRCODE_100003'  
    )  
    BEGIN  
        RAISERROR(  
            N'Tên bảng DOCQRTABLE không hợp lệ: %s',  
            16,  
            1,  
            @DOCQRTABLE  
        );  
        RETURN;  
    END;  
  
    --------------------------------------------------------------------  
    -- 2. KHAI BÁO BIẾN  
    --------------------------------------------------------------------  
    DECLARE @NgayGiao SMALLDATETIME = CAST(GETDATE() AS SMALLDATETIME);  
    DECLARE @sql NVARCHAR(MAX);
    DECLARE @StartedTransaction BIT = 0;
    DECLARE @SavepointCreated BIT = 0;  
  
    DECLARE  
        @_LOT          NVARCHAR(MAX),  
        @_SLCONLAI     INT,  
        @STT           INT,  
        @MH            NVARCHAR(60),  
        @_LOTSL_Tach   NVARCHAR(500),  
        @_LOTOK        NVARCHAR(500),  
        @_SLOK         INT,  
        @_KTPC         INT,  
        @_LOTUD        NVARCHAR(500),  
        @_SLUD         INT,  
        @_STTUD        INT,  
        @_STUP         NVARCHAR(15),  
        @LotThuc       NVARCHAR(500),
        @LockResource  NVARCHAR(255),
        @LockResult    INT;  
  
    --------------------------------------------------------------------  
    -- 3. XÓA TEMP TABLE CŨ  
    --------------------------------------------------------------------  
    IF OBJECT_ID('TempDb..#TMPUPDATE') IS NOT NULL  
        DROP TABLE #TMPUPDATE;  
  
    IF OBJECT_ID('TempDb..#STOK') IS NOT NULL  
        DROP TABLE #STOK;  
  
    IF OBJECT_ID('TempDb..#STNG') IS NOT NULL  
        DROP TABLE #STNG;  
  
    IF OBJECT_ID('TempDb..#PHIEU') IS NOT NULL  
        DROP TABLE #PHIEU;  
  
    IF OBJECT_ID('TempDb..#GomNhomCheck') IS NOT NULL  
        DROP TABLE #GomNhomCheck;  
  
    IF OBJECT_ID('TempDb..#SplitResult') IS NOT NULL  
        DROP TABLE #SplitResult;  

    IF OBJECT_ID('TempDb..#StockByLot') IS NOT NULL
        DROP TABLE #StockByLot;

    IF OBJECT_ID('TempDb..#LotKeyCount') IS NOT NULL
        DROP TABLE #LotKeyCount;
  
    --------------------------------------------------------------------  
    -- 4. TEMP TABLE  
    --------------------------------------------------------------------  
    CREATE TABLE #TMPUPDATE  
    (  
        STT     INT,  
        MH      NVARCHAR(60),  
        LOT     NVARCHAR(MAX),  
        SOLUONG INT,  
        STATUS  NVARCHAR(50)  
    );  
  
    CREATE TABLE #STOK  
    (  
        STT     INT,  
        LOT     NVARCHAR(MAX),  
        SOLUONG INT,  
        STATUS  NVARCHAR(50)  
    );  
  
    CREATE TABLE #STNG  
    (  
        MH       NVARCHAR(100),  
        LOT      NVARCHAR(MAX),  
        SLCAN    INT,  
        SLCO     INT,  
        SLTHIEU  INT,  
        STATUS   NVARCHAR(500)  
    );  
  
    CREATE TABLE #PHIEU  
    (  
        STT INT,  
        LOT NVARCHAR(MAX),  
        MH  NVARCHAR(60)  
    );  
  
    BEGIN TRY

        -- Own a transaction when called directly; otherwise participate in the
        -- repository UnitOfWork through a savepoint. Start before the idempotency
        -- check so concurrent confirmations serialize before reading/writing stock.
        IF @@TRANCOUNT = 0
        BEGIN
            BEGIN TRANSACTION;
            SET @StartedTransaction = 1;
        END
        ELSE
        BEGIN
            SAVE TRANSACTION UspQrcodeUpdateStock2405;
            SET @SavepointCreated = 1;
        END;
  
        ----------------------------------------------------------------  
        -- 5. NẠP PHIẾU  
        ----------------------------------------------------------------  
        SET @sql = N'  
            INSERT INTO #PHIEU  
            (  
                STT,  
                LOT,  
                MH  
            )  
            SELECT  
                STT,  
                LOT,  
                MAHANG  
            FROM [' + @TMPTABLE + N']  
            WHERE ISNULL(LOT, '''') <> ''''  
              AND (STATUS <> ''OK'' OR STATUS IS NULL);  
        ';  
  
        EXEC sp_executesql @sql;  
  
        ----------------------------------------------------------------  
        -- Không có phiếu  
        ----------------------------------------------------------------  
        IF NOT EXISTS (SELECT 1 FROM #PHIEU)  
        BEGIN
            IF @StartedTransaction = 1
                COMMIT TRANSACTION;
  
            SELECT  
                STT,  
                LOT,  
                SOLUONG,  
                STATUS  
            FROM #STOK;  
  
            SELECT  
                MH,  
                LOT,  
                SLCAN AS SLC,  
                SLCO AS SLTK,  
                SLTHIEU AS SLT,  
                STATUS  
            FROM #STNG;  

            RETURN;  
        END;  
  
        ----------------------------------------------------------------  
        -- 6. DUYỆT PHIẾU  
        ----------------------------------------------------------------  
        DECLARE PGH CURSOR LOCAL FAST_FORWARD FOR  
            SELECT  
                STT,  
                LOT,  
                MH  
            FROM #PHIEU;  
  
        OPEN PGH;  
  
        FETCH NEXT FROM PGH  
        INTO @STT, @_LOT, @MH;  
  
        WHILE @@FETCH_STATUS = 0  
        BEGIN  
            DECLARE @DaTon BIT = 0;  
  
            DECLARE @CheckResult TABLE  
            (  
                Found INT  
            );  
  
            ------------------------------------------------------------
            -- 6.1. Serialize the complete delivery identity before checking for
            -- an existing confirmation. The lock is transaction-owned and therefore
            -- remains held through STOCKTP, ledger, and pending-delivery writes.
            ------------------------------------------------------------
            SET @LockResource = NULL;
            SET @sql = N'
                SELECT @Resource =
                    N''PCTP_DELIVERY:'' +
                    CONVERT(NVARCHAR(64), HASHBYTES(''SHA2_256'', CONCAT(
                        UPPER(RTRIM(ISNULL(MAHANG, N''''))), N''|'',
                        ISNULL(CONVERT(NVARCHAR(10), CAST(NGAYGIAO AS DATE), 23), N''''), N''|'',
                        UPPER(RTRIM(ISNULL(CUA, N''''))), N''|'',
                        UPPER(RTRIM(ISNULL(TRUYEN, N''''))), N''|'',
                        ISNULL(CONVERT(NVARCHAR(20), SOLUONG), N''''), N''|'',
                        UPPER(RTRIM(ISNULL(GIOGIAO, N''''))), N''|'',
                        UPPER(RTRIM(LEFT(ISNULL(@NHAMAY, N''''), 50)))
                    )), 2)
                FROM [' + @TMPTABLE + N']
                WHERE STT = @STT;';

            EXEC sp_executesql
                @sql,
                N'@STT INT, @NHAMAY NVARCHAR(250), @Resource NVARCHAR(255) OUTPUT',
                @STT = @STT,
                @NHAMAY = @NHAMAY,
                @Resource = @LockResource OUTPUT;

            IF @LockResource IS NULL
                RAISERROR(N'Không tạo được khóa chống xác nhận giao hàng trùng.', 16, 1);

            EXEC @LockResult = sys.sp_getapplock
                @Resource = @LockResource,
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 10000;

            IF @LockResult < 0
                RAISERROR(N'Không lấy được khóa xác nhận giao hàng; vui lòng thử lại.', 16, 1);

            ------------------------------------------------------------
            -- 6.1. KIỂM TRA PHIẾU ĐÃ LƯU
            ------------------------------------------------------------
            SET @sql = N'  
                SELECT TOP 1 1  
                FROM [' + @TMPTABLE + N'] tmp  
                INNER JOIN LUUPHIEUGIAOHANG luu  
                    ON RTRIM(luu.MAHANG) =  
                       RTRIM(tmp.MAHANG)  
  
                   AND CAST(luu.NGAYGIAO AS DATE) =  
                       CAST(tmp.NGAYGIAO AS DATE)  
  
                   AND RTRIM(luu.CUA) =  
                       RTRIM(tmp.CUA)  
  
                   AND RTRIM(luu.TRUYEN) =  
                       RTRIM(tmp.TRUYEN)  
  
                   AND luu.SOLUONG =  
                       tmp.SOLUONG  
  
                   AND RTRIM(luu.GIOGIAO) =  
                       RTRIM(tmp.GIOGIAO)  
  
                   AND RTRIM(luu.NHAMAY) =  
                       RTRIM(LEFT(@NHAMAY, 50))  
  
                WHERE tmp.STT = @STT;  
            ';  
  
            INSERT INTO @CheckResult  
            EXEC sp_executesql  
                @sql,  
                N'@NHAMAY NVARCHAR(250), @STT INT',  
                @NHAMAY,  
                @STT;  
  
            IF EXISTS (SELECT 1 FROM @CheckResult)  
                SET @DaTon = 1;  
  
            DELETE FROM @CheckResult;  
  
            ------------------------------------------------------------  
            -- 6.2. ĐÃ TỒN TẠI  
            ------------------------------------------------------------  
            IF @DaTon = 1  
            BEGIN  
                INSERT INTO #STNG  
                (  
                    MH,  
                    LOT,  
                    SLCAN,  
                    SLCO,  
                    SLTHIEU,  
                    STATUS  
                )  
                VALUES  
                (  
                    @MH,  
                    @_LOT,  
               0,  
                    0,  
                    0,  
                    N'Mã hàng này đã được lưu trước đó!'  
                );  
            END  
            ELSE  
            BEGIN  
  
                --------------------------------------------------------  
                -- 6.3. TÁCH LOT-SL  
                --------------------------------------------------------  
                IF OBJECT_ID('TempDb..#SplitResult') IS NOT NULL  
                    DROP TABLE #SplitResult;  
  
                CREATE TABLE #SplitResult  
                (  
                    LotTach NVARCHAR(500)  
                );  
  
                INSERT INTO #SplitResult  
                SELECT *  
                FROM dbo.Split(@_LOT, ',');  
  
                DECLARE LOT_IN_PGH CURSOR LOCAL FAST_FORWARD FOR  
                    SELECT LotTach  
                    FROM #SplitResult;  
  
                OPEN LOT_IN_PGH;  
  
                FETCH NEXT FROM LOT_IN_PGH  
                INTO @_LOTSL_Tach;  
  
                WHILE @@FETCH_STATUS = 0  
                BEGIN  
                    ----------------------------------------------------  
                    -- 6.4. TÁCH LOT-SL  
                    ----------------------------------------------------  
                    SET @_KTPC =  
                        CHARINDEX('-', @_LOTSL_Tach);  
  
                    IF @_KTPC > 1  
                    BEGIN  
                        SET @_LOTOK =  
                            SUBSTRING(  
                                @_LOTSL_Tach,  
                                1,  
                                @_KTPC - 1  
                            );  
  
                        SET @_SLOK =  
                            TRY_CAST(  
                                RIGHT(  
                                    @_LOTSL_Tach,  
                                    LEN(@_LOTSL_Tach) - @_KTPC  
                                )  
                                AS INT  
                            );  
  
                        ------------------------------------------------  
                        -- SL không hợp lệ  
                        ------------------------------------------------  
                        IF @_SLOK IS NULL  
                        BEGIN  
                            INSERT INTO #STNG  
                            (  
                                MH,  
                                LOT,  
                                SLCAN,  
                                SLCO,  
                                SLTHIEU,  
                                STATUS  
                            )  
                            VALUES  
                            (  
                                @MH,  
                                @_LOTSL_Tach,  
                                0,  
                                0,  
                                0,  
                                N'Số lượng trong chuỗi LOT-SL không hợp lệ'  
                            );  
                        END  
                        ELSE  
                        BEGIN  
  
                            ------------------------------------------------  
                            -- 6.5. TÌM TỒN THEO LOT KEY 13 KÝ TỰ  
                            ------------------------------------------------  
                            SET @_SLCONLAI = 0;

                            -- Full physical LOT is the exact identity. A 13-character
                            -- root is accepted only when it maps to one physical LOT.
                            IF LEN(RTRIM(@_LOTOK)) > 13
                            BEGIN
                                SELECT @_SLCONLAI = ISNULL(SLCONLAI, 0)
                                FROM STOCKTP
                                WHERE RTRIM(LOT) = RTRIM(@_LOTOK);
                            END
                            ELSE IF (
                                SELECT COUNT(DISTINCT RTRIM(LOT))
                                FROM STOCKTP
                                WHERE LEFT(RTRIM(LOT), 13) = LEFT(RTRIM(@_LOTOK), 13)
                            ) = 1
                            BEGIN
                                SELECT TOP (1) @_SLCONLAI = ISNULL(SLCONLAI, 0)
                                FROM STOCKTP
                                WHERE LEFT(RTRIM(LOT), 13) = LEFT(RTRIM(@_LOTOK), 13)
                                ORDER BY LOT ASC;
                            END
                            ELSE IF (
                                SELECT COUNT(DISTINCT RTRIM(LOT))
                                FROM STOCKTP
                                WHERE LEFT(RTRIM(LOT), 13) = LEFT(RTRIM(@_LOTOK), 13)
                            ) > 1
                            BEGIN
                                INSERT INTO #STNG(MH, LOT, SLCAN, SLCO, SLTHIEU, STATUS)
                                VALUES(@MH, @_LOTOK, @_SLOK, 0, @_SLOK,
                                       N'Mã LOT gốc ánh xạ nhiều LOT vật lý; vui lòng cung cấp LOT đầy đủ');
                            END;  
  
                            ------------------------------------------------  
                            -- 6.6. KIỂM TRA LOT TỒN TẠI. Skip duplicate diagnostics if root is ambiguous.
                            IF NOT EXISTS
                            (
                                SELECT 1 FROM #STNG
                                WHERE RTRIM(MH) = RTRIM(@MH)
                                  AND RTRIM(LOT) = RTRIM(@_LOTOK)
                                  AND STATUS = N'Mã LOT gốc ánh xạ nhiều LOT vật lý; vui lòng cung cấp LOT đầy đủ'
                            )
                            AND EXISTS
                            (
                                SELECT 1
                                FROM STOCKTP
                                WHERE (LEN(RTRIM(@_LOTOK)) > 13 AND RTRIM(LOT) = RTRIM(@_LOTOK))
                                   OR (LEN(RTRIM(@_LOTOK)) <= 13 AND LEFT(RTRIM(LOT), 13) = LEFT(RTRIM(@_LOTOK), 13))
                            )
                            BEGIN  
  
                                ------------------------------------------------  
                                -- ĐỦ TỒN  
                                ------------------------------------------------  
                                IF @_SLCONLAI >= @_SLOK  
                                BEGIN  
                                    INSERT INTO #TMPUPDATE  
                                    (  
                                        STT,  
                                        MH,  
                                        LOT,  
                                        SOLUONG,  
                                        STATUS  
                                    )  
                                    VALUES  
                                    (  
                                        @STT,  
                                        @MH,  
                                        @_LOTOK,  
                                        @_SLOK,  
                                        'OK'  
                                    );  
                                END  
                                ELSE  
                                BEGIN  
  
                                    ------------------------------------------------  
                                    -- KHÔNG ĐỦ TỒN  
                                    ------------------------------------------------  
                                    INSERT INTO #STNG  
                                    (  
                                        MH,  
                                        LOT,  
                                        SLCAN,  
                                        SLCO,  
                                        SLTHIEU,  
                                        STATUS  
                                    )  
                                    VALUES  
                                    (  
                                        @MH,  
                                        @_LOTOK,  
                                        @_SLOK,  
                                        @_SLCONLAI,  
                                        @_SLOK - @_SLCONLAI,  
                                        N'Không đủ tồn kho ban đầu'  
                                    );  
                                END;  
                            END  
                            ELSE  
                            BEGIN  
  
                                ------------------------------------------------  
                                -- KHÔNG TỒN TẠI LOT  
                                ------------------------------------------------  
                                INSERT INTO #STNG  
                                (  
                                    MH,  
                                    LOT,  
                                    SLCAN,  
                                    SLCO,  
                                    SLTHIEU,  
                                    STATUS  
                                )  
                                VALUES  
                                (  
                                    @MH,  
                                    @_LOTOK,  
                                    @_SLOK,  
                                    0,  
                                    @_SLOK,  
                                    N'Không tồn tại LOT trong STOCKTP'  
                                );  
                            END;  
                        END;  
                    END  
                    ELSE  
                    BEGIN  
  
                        ----------------------------------------------------  
                        -- FORMAT LOT-SL KHÔNG HỢP LỆ  
   ----------------------------------------------------  
                        INSERT INTO #STNG  
                        (  
                            MH,  
                            LOT,  
                            SLCAN,  
                            SLCO,  
                            SLTHIEU,  
                            STATUS  
                        )  
                        VALUES  
                        (  
                            @MH,  
                            @_LOTSL_Tach,  
                            0,  
                            0,  
                            0,  
                            N'Định dạng chuỗi LOT-SL không hợp lệ'  
                        );  
                    END;  
  
                    FETCH NEXT FROM LOT_IN_PGH  
                    INTO @_LOTSL_Tach;  
                END;  
  
                CLOSE LOT_IN_PGH;  
                DEALLOCATE LOT_IN_PGH;  
            END;  
  
            FETCH NEXT FROM PGH  
            INTO @STT, @_LOT, @MH;  
        END;  
  
        CLOSE PGH;  
        DEALLOCATE PGH;  
  
        ----------------------------------------------------------------  
        -- 7. KIỂM TRA TỔNG SL CẦN XUẤT VS TỒN  
        ----------------------------------------------------------------  
        IF EXISTS  
        (  
            SELECT 1  
            FROM #TMPUPDATE  
            WHERE STATUS = 'OK'  
        )  
        BEGIN  
  
            SELECT  
                MH,  
                LOT,  
                SUM(SOLUONG) AS TONG_CAN_XUAT  
            INTO #GomNhomCheck  
            FROM #TMPUPDATE T
            WHERE T.STATUS = 'OK'
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM #STNG E
                  WHERE RTRIM(E.MH) = RTRIM(T.MH)
                    AND RTRIM(E.LOT) = RTRIM(T.LOT)
              )
            GROUP BY
                MH,
                LOT;  
  
            ------------------------------------------------------------
            -- Aggregate STOCKTP once (not once per delivery line).
            --
            -- The previous form LEFT JOINed the aggregate with an ON clause that
            -- contained OR plus a correlated COUNT(DISTINCT ...) over STOCKTP. SQL
            -- Server could not hash/merge-join it, so it compared every delivery
            -- line with every physical LOT and re-ran that sub-query each time
            -- (minutes on ~66k STOCKTP rows). Same rules, computed once:
            --   * input LOT longer than 13 chars -> exact physical LOT only;
            --   * 13-char root -> only when it maps to exactly ONE physical LOT
            --     in all of STOCKTP (counted without the SLCONLAI > 0 filter).
            ------------------------------------------------------------
            SELECT
                RTRIM(LOT) AS PhysicalLot,
                LEFT(RTRIM(LOT), 13) AS LotKey,
                SUM(ISNULL(SLCONLAI, 0)) AS SLCONLAI
            INTO #StockByLot
            FROM STOCKTP
            WHERE ISNULL(SLCONLAI, 0) > 0
            GROUP BY RTRIM(LOT), LEFT(RTRIM(LOT), 13);

            CREATE CLUSTERED INDEX IX_StockByLot_PhysicalLot ON #StockByLot(PhysicalLot);
            CREATE NONCLUSTERED INDEX IX_StockByLot_LotKey ON #StockByLot(LotKey);

            SELECT
                LEFT(RTRIM(LOT), 13) AS LotKey,
                COUNT(DISTINCT RTRIM(LOT)) AS PhysicalLotCount
            INTO #LotKeyCount
            FROM STOCKTP
            GROUP BY LEFT(RTRIM(LOT), 13);

            CREATE CLUSTERED INDEX IX_LotKeyCount_LotKey ON #LotKeyCount(LotKey);

            INSERT INTO #STNG
            (
                MH,
                LOT,
                SLCAN,
                SLCO,
                SLTHIEU,
                STATUS
            )
            SELECT
                G.MH,
                G.LOT,
                G.TONG_CAN_XUAT,
                X.SLCONLAI,
                G.TONG_CAN_XUAT - X.SLCONLAI,
                N'Không đủ tồn kho (kiểm tra lần 2)'
            FROM #GomNhomCheck G
            CROSS APPLY
            (
                SELECT ISNULL(
                    CASE
                        WHEN LEN(RTRIM(G.LOT)) > 13
                            THEN (SELECT SUM(S.SLCONLAI)
                                  FROM #StockByLot S
                                  WHERE S.PhysicalLot = RTRIM(G.LOT))
                        WHEN EXISTS (SELECT 1
                                     FROM #LotKeyCount K
                                     WHERE K.LotKey = LEFT(RTRIM(G.LOT), 13)
                                       AND K.PhysicalLotCount = 1)
                            THEN (SELECT SUM(S.SLCONLAI)
                                  FROM #StockByLot S
                                  WHERE S.LotKey = LEFT(RTRIM(G.LOT), 13))
                    END, 0) AS SLCONLAI
            ) X
            WHERE
                X.SLCONLAI -
                G.TONG_CAN_XUAT < 0;

            DROP TABLE #StockByLot;
            DROP TABLE #LotKeyCount;
  
            DROP TABLE #GomNhomCheck;  
        END;  
  
        ----------------------------------------------------------------  
        -- 8. NẾU CÓ LỖI → KHÔNG UPDATE STOCK  
        ----------------------------------------------------------------  
        IF EXISTS  
        (  
            SELECT 1  
            FROM #STNG  
        )  
        BEGIN  
            SELECT  
                STT,  
                LOT,  
                SOLUONG,  
                STATUS  
            FROM #STOK;  
  
            SELECT  
                MH,  
                LOT,  
                SLCAN AS SLC,  
                SLCO AS SLTK,  
                SLTHIEU AS SLT,  
                STATUS  
            FROM #STNG;  

            -- Roll back this procedure's work before returning business errors.
            -- A doomed caller transaction must be rolled back by its owner.
            IF @StartedTransaction = 1
                ROLLBACK TRANSACTION;
            ELSE IF XACT_STATE() = 1 AND @SavepointCreated = 1
                ROLLBACK TRANSACTION UspQrcodeUpdateStock2405;
            ELSE IF XACT_STATE() = -1
                THROW;

            RETURN;  
        END;  
  
        ----------------------------------------------------------------  
        -- 9. KHÔNG CÓ DỮ LIỆU UPDATE  
        ----------------------------------------------------------------  
        IF NOT EXISTS  
        (  
            SELECT 1  
            FROM #TMPUPDATE  
            WHERE STT NOT IN  
            (  
                SELECT DISTINCT STT  
                FROM #STNG  
            )  
        )  
        BEGIN  
            SELECT  
                STT,  
                LOT,  
                SOLUONG,  
                STATUS  
            FROM #STOK;  
  
            SELECT  
                MH,  
                LOT,  
                SLCAN AS SLC,  
                SLCO AS SLTK,  
                SLTHIEU AS SLT,  
                STATUS  
            FROM #STNG;  

            -- No stock rows are eligible: release this invocation's savepoint
            -- and transaction-owned application locks before returning.
            IF @StartedTransaction = 1
                ROLLBACK TRANSACTION;
            ELSE IF XACT_STATE() = 1 AND @SavepointCreated = 1
                ROLLBACK TRANSACTION UspQrcodeUpdateStock2405;
            ELSE IF XACT_STATE() = -1
                THROW;

            RETURN;  
        END;  
  
        ----------------------------------------------------------------  
        -- 10. TRANSACTION  
        ----------------------------------------------------------------  
        -- The transaction/savepoint was established before the duplicate check;
        -- all stock writes below remain inside that same transaction scope.
  
        DECLARE UPDATETMP CURSOR LOCAL FAST_FORWARD FOR  
            SELECT  
                STT,  
                LOT,  
                SOLUONG,  
                STATUS  
            FROM #TMPUPDATE  
            WHERE STT NOT IN  
            (  
                SELECT DISTINCT STT  
                FROM #STNG  
            );  
  
        OPEN UPDATETMP;  
  
        FETCH NEXT FROM UPDATETMP  
        INTO @_STTUD, @_LOTUD, @_SLUD, @_STUP;  
  
        WHILE @@FETCH_STATUS = 0  
        BEGIN  
  
            SET @LotThuc = NULL;  
  
            ----------------------------------------------------------------  
            -- 10.1. TÌM LOT VẬT LÝ THẬT  
            --  
            -- Input:  
            --     @_LOTUD = 2609100101410  
            --  
            -- STOCKTP:  
            --     26091001014100861700  
            --  
            -- Match:  
            --     LEFT(LOT,13) = LEFT(@_LOTUD,13)  
            --  
            -- Sau khi tìm được mới UPDATE WHERE LOT = @LotThuc.  
            ----------------------------------------------------------------  
            IF LEN(RTRIM(@_LOTUD)) > 13
            BEGIN
                SELECT @LotThuc = LOT
                FROM STOCKTP
                WHERE RTRIM(LOT) = RTRIM(@_LOTUD)
                  AND ISNULL(SLCONLAI, 0) >= @_SLUD;
            END
            ELSE IF (
                SELECT COUNT(DISTINCT RTRIM(LOT))
                FROM STOCKTP
                WHERE LEFT(RTRIM(LOT), 13) = LEFT(RTRIM(@_LOTUD), 13)
            ) = 1
            BEGIN
                SELECT TOP (1) @LotThuc = LOT
                FROM STOCKTP
                WHERE LEFT(RTRIM(LOT), 13) = LEFT(RTRIM(@_LOTUD), 13)
                  AND ISNULL(SLCONLAI, 0) >= @_SLUD
                ORDER BY NGAYNHAP ASC, LOT ASC;
            END  
  
            ----------------------------------------------------------------  
            -- 10.2. Không tìm được LOT vật lý  
            ----------------------------------------------------------------  
            IF @LotThuc IS NULL  
            BEGIN  
                INSERT INTO #STNG  
                (  
                    MH,  
                    LOT,  
                    SLCAN,  
                    SLCO,  
                    SLTHIEU,  
                    STATUS  
                )  
                VALUES  
                (  
                    @MH,  
                    @_LOTUD,  
                    @_SLUD,  
                    0,  
                    @_SLUD,  
                    N'Không xác định được LOT tồn kho hợp lệ khi trừ kho'  
                );  
            END  
            ELSE  
            BEGIN  
  
                ----------------------------------------------------------------  
                -- 10.3. UPDATE ĐÚNG LOT VẬT LÝ  
                ----------------------------------------------------------------  
                UPDATE STOCKTP  
                SET  
                    SLCONLAI =  
                        ISNULL(SLCONLAI, 0) - @_SLUD,  
  
                    SLXUAT =  
                        ISNULL(SLXUAT, 0) + @_SLUD  
                WHERE LOT = @LotThuc;  
  
                ----------------------------------------------------------------  
                -- 10.4. LƯU LOT VẬT LÝ THẬT  
                ----------------------------------------------------------------  
                IF NOT EXISTS  
                (  
                    SELECT 1  
                    FROM #STOK  
                    WHERE STT = @_STTUD  
                )  
                BEGIN  
                    INSERT INTO #STOK  
                    (  
                        STT,  
                        LOT,  
                        SOLUONG,  
                        STATUS  
                    )  
        VALUES  
                    (  
                        @_STTUD,  
                        @LotThuc,  
                        @_SLUD,  
                        'OK'  
                    );  
                END;  
            END;  
  
            FETCH NEXT FROM UPDATETMP  
            INTO @_STTUD, @_LOTUD, @_SLUD, @_STUP;  
        END;  
  
        CLOSE UPDATETMP;  
        DEALLOCATE UPDATETMP;  
  
        ----------------------------------------------------------------  
        -- 11. KIỂM TRA LỖI PHÁT SINH TRONG TRANSACTION  
        ----------------------------------------------------------------  
        IF EXISTS
        (
            SELECT 1
            FROM #STNG
        )
        BEGIN
            -- Preserve business diagnostics across rollback. The temp-table writes
            -- are transactional, but the table variable snapshot survives rollback.
            DECLARE @ErrorSnapshot TABLE
            (
                MH NVARCHAR(100),
                LOT NVARCHAR(MAX),
                SLCAN INT,
                SLCO INT,
                SLTHIEU INT,
                STATUS NVARCHAR(500)
            );

            INSERT INTO @ErrorSnapshot(MH, LOT, SLCAN, SLCO, SLTHIEU, STATUS)
            SELECT MH, LOT, SLCAN, SLCO, SLTHIEU, STATUS
            FROM #STNG;

            IF @StartedTransaction = 1
                ROLLBACK TRANSACTION;
            ELSE
                ROLLBACK TRANSACTION UspQrcodeUpdateStock2405;

            INSERT INTO #STNG(MH, LOT, SLCAN, SLCO, SLTHIEU, STATUS)
            SELECT MH, LOT, SLCAN, SLCO, SLTHIEU, STATUS
            FROM @ErrorSnapshot;
  
            SELECT  
                STT,  
                LOT,  
                SOLUONG,  
                STATUS  
            FROM #STOK;  
  
            SELECT  
                MH,  
                LOT,  
                SLCAN AS SLC,  
                SLCO AS SLTK,  
                SLTHIEU AS SLT,  
                STATUS  
            FROM #STNG;  
  
            RETURN;  
        END;  
  
        ----------------------------------------------------------------  
        -- 12. LƯU LUUPHIEUGIAOHANG  
        ----------------------------------------------------------------  
        IF EXISTS  
        (  
            SELECT 1  
            FROM #STOK  
            WHERE STATUS = 'OK'  
        )  
        BEGIN  
  
            SET @sql = N'  
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
                    Note  
                )  
                SELECT  
                    tmp.STT,  
                    LEFT(tmp.CUA, 50),  
                    LEFT(tmp.TRUYEN, 50),  
                    LEFT(tmp.MAHANG, 50),  
                    LEFT(tmp.TENHANG, 60),  
                    LEFT(tmp.LOT, 500),  
                    LEFT(tmp.DV, 50),  
                    tmp.SOLUONG,  
                    tmp.NGAYGIAO,  
                    LEFT(tmp.GIOGIAO, 50),  
                    ''OK'',  
                    LEFT(tmp.TTPHIEU, 50),  
                    LEFT(@NHAMAY, 60),  
                    LEFT(@GIOGIAOFCC, 50),  
                    LEFT(tmp.HOP, 50),  
                    LEFT(tmp.STATUSDOC, 50),  
                    LEFT(tmp.Note, 250)  
                FROM [' + @TMPTABLE + N'] tmp  
                WHERE tmp.STT IN  
                (  
                    SELECT STT  
                    FROM #STOK  
                    WHERE STATUS = ''OK''  
                );  
            ';  
  
            EXEC sp_executesql  
                @sql,  
                N'@NHAMAY NVARCHAR(250),  
                  @GIOGIAOFCC NVARCHAR(250)',  
                @NHAMAY,  
                @GIOGIAOFCC;  
  
            ----------------------------------------------------------------  
            -- 13. LƯU LUUDOCQRCODE  
            ----------------------------------------------------------------  
            SET @sql = N'  
                INSERT INTO LUUDOCQRCODE  
                (  
                    LOTFCC,  
                    MAHANGFCC,  
                    SLTEMFCC,  
                    LOTHVN,  
                    MAHANGHVN,  
                    SLTEMHVN,  
                    STATUS,  
                    MAFCC,  
                    STT,  
                    KETQUA,  
                    NGAYXUAT,  
                    GIOXUAT,  
                    NHAMAY  
                )  
                SELECT  
                    LEFT(DQ.LOTFCC, 500),  
                    LEFT(DQ.MAHANGFCC, 60),  
                   DQ.SLTEMFCC,  
                    LEFT(DQ.LOTHVN, 500),  
                    LEFT(DQ.MAHANGHVN, 60),  
                    DQ.SLTEMHVN,  
                    DQ.STATUS,  
                    LEFT(DQ.MAFCC, 50),  
                    DQ.STT,  
                    LEFT(DQ.KETQUA, 30),  
                    @NgayGiao,  
                    LEFT(@GIOGIAOFCC, 30),  
                    LEFT(@NHAMAY, 60)  
                FROM [' + @DOCQRTABLE + N'] DQ  
                INNER JOIN [' + @TMPTABLE + N'] PG  
                    ON PG.STT =  
                       TRY_CAST(DQ.GIO AS INT)  
                WHERE PG.STT IN  
                (  
                    SELECT STT  
                    FROM #STOK  
                    WHERE STATUS = ''OK''  
                )  
                  AND ISNULL(DQ.GIO, '''') <> '''';  
  
                INSERT INTO LUUDOCQRCODE  
                (  
                    LOTFCC,  
                    MAHANGFCC,  
                    SLTEMFCC,  
                    LOTHVN,  
                    MAHANGHVN,  
                    SLTEMHVN,  
                    STATUS,  
                    MAFCC,  
                    STT,  
                    KETQUA,  
                    NGAYXUAT,  
                    GIOXUAT,  
                    NHAMAY  
                )  
                SELECT  
                    LEFT(DQ.LOTFCC, 500),  
                    LEFT(DQ.MAHANGFCC, 60),  
                    DQ.SLTEMFCC,  
                    LEFT(DQ.LOTHVN, 500),  
                    LEFT(DQ.MAHANGHVN, 60),  
                    DQ.SLTEMHVN,  
                    DQ.STATUS,  
                    LEFT(DQ.MAFCC, 50),  
                    DQ.STT,  
                    LEFT(DQ.KETQUA, 30),  
                    @NgayGiao,  
                    LEFT(@GIOGIAOFCC, 30),  
                    LEFT(@NHAMAY, 60)  
                FROM [' + @DOCQRTABLE + N'] DQ  
                INNER JOIN [' + @TMPTABLE + N'] PG  
                    ON RTRIM(DQ.MAHANGFCC) =  
                       RTRIM(PG.MAHANG)  
                WHERE PG.STT IN  
                (  
                    SELECT STT  
                    FROM #STOK  
                    WHERE STATUS = ''OK''  
                )  
                  AND ISNULL(DQ.GIO, '''') = '''';  
            ';  
  
            EXEC sp_executesql  
                @sql,  
                N'@NgayGiao SMALLDATETIME,  
                  @GIOGIAOFCC NVARCHAR(250),  
                  @NHAMAY NVARCHAR(250)',  
                @NgayGiao,  
                @GIOGIAOFCC,  
                @NHAMAY;  
  
            ----------------------------------------------------------------  
            -- 14. UPDATE TMP STATUS  
            ----------------------------------------------------------------  
            SET @sql = N'  
                UPDATE [' + @TMPTABLE + N']  
                SET STATUS = ''OK''  
                WHERE STT IN  
                (  
                    SELECT STT  
                    FROM #STOK  
                    WHERE STATUS = ''OK''  
                );  
            ';  
  
            EXEC sp_executesql @sql;  
  
            ----------------------------------------------------------------  
            -- 15. DELETE DOCQRCODE ĐÃ XỬ LÝ  
            ----------------------------------------------------------------  
            SET @sql = N'  
                DELETE DQ  
                FROM [' + @DOCQRTABLE + N'] DQ  
                INNER JOIN [' + @TMPTABLE + N'] PG  
                    ON PG.STT =  
                       TRY_CAST(DQ.GIO AS INT)  
                WHERE PG.STT IN  
                (  
                    SELECT STT  
                    FROM #STOK  
                    WHERE STATUS = ''OK''  
                )  
                  AND ISNULL(DQ.GIO, '''') <> '''';  
  
                DELETE DQ  
                FROM [' + @DOCQRTABLE + N'] DQ  
                INNER JOIN [' + @TMPTABLE + N'] PG  
                    ON RTRIM(DQ.MAHANGFCC) =  
                       RTRIM(PG.MAHANG)  
                WHERE PG.STT IN  
                (  
                    SELECT STT  
                    FROM #STOK  
                    WHERE STATUS = ''OK''  
                )  
                  AND ISNULL(DQ.GIO, '''') = '''';  
            ';  
  
            EXEC sp_executesql @sql;  
  
            ----------------------------------------------------------------
            -- 16. ORDER TABLE DELIVERY STATE
            ----------------------------------------------------------------
            -- Deliberately do not update Purchase_Order_HTN/YMVN here.
            -- The application finalizes each successful TMP row inside the
            -- same UnitOfWork by building OrderLineKey from PO_NO + MAHANG
            -- + NGAYGIAO/GIOGIAO and calling OrderTableSql.MarkDelivered.
            -- Keeping this update in one place avoids a second, weaker key
            -- interpretation in SQL (especially legacy hour-only GIOGIAO).
  
  
        END;  
  
        ----------------------------------------------------------------  
        -- 18. COMMIT  
        ----------------------------------------------------------------  
        -- Only commit a transaction opened by this procedure. If a caller owns
        -- the transaction, leave final commit/rollback to the caller.
        IF @StartedTransaction = 1
            COMMIT TRANSACTION;
  
    END TRY  
    BEGIN CATCH  
  
        IF @@TRANCOUNT > 0
        BEGIN
            IF @StartedTransaction = 1
                ROLLBACK TRANSACTION;
            ELSE IF XACT_STATE() = 1 AND @SavepointCreated = 1
                ROLLBACK TRANSACTION UspQrcodeUpdateStock2405;
            ELSE IF XACT_STATE() = -1
                THROW; -- the caller must roll back the doomed outer transaction
        END;  
  
        IF CURSOR_STATUS('local', 'LOT_IN_PGH') >= 0  
        BEGIN  
            CLOSE LOT_IN_PGH;  
            DEALLOCATE LOT_IN_PGH;  
        END;  
  
        IF CURSOR_STATUS('local', 'PGH') >= 0  
        BEGIN  
            CLOSE PGH;  
            DEALLOCATE PGH;  
        END;  
  
        IF CURSOR_STATUS('local', 'UPDATETMP') >= 0  
        BEGIN  
            CLOSE UPDATETMP;  
            DEALLOCATE UPDATETMP;  
        END;  
  
        INSERT INTO #STNG  
        (  
            MH,  
            LOT,  
            SLCAN,  
            SLCO,  
            SLTHIEU,  
            STATUS  
        )  
        VALUES  
        (  
            N'SYSTEM ERROR',  
            N'Line ' +  
                CAST(ERROR_LINE() AS NVARCHAR(10)),  
            0,  
            0,  
            0,  
            LEFT  
            (  
                N'Lỗi dòng ' +  
                CAST(ERROR_LINE() AS NVARCHAR(10)) +  
                N': ' +  
                ERROR_MESSAGE(),  
                500  
            )  
        );  
  
    END CATCH;  
  
    --------------------------------------------------------------------  
    -- 19. TRẢ KẾT QUẢ  
    --------------------------------------------------------------------  
    SELECT  
        STT,  
        LOT,  
        SOLUONG,  
        STATUS  
    FROM #STOK;  
  
    SELECT  
        MH,  
        LOT,  
        SLCAN AS SLC,  
        SLCO AS SLTK,  
        SLTHIEU AS SLT,  
        STATUS  
    FROM #STNG;  
  
END;  
