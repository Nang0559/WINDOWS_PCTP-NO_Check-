
SET ANSI_NULLS ON;
GO

SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[Usp_BarCodeView_ThongKe_Tmp5]
    @_Id INT = NULL,
    @_DocDate NVARCHAR(16) = N'',
    @_ReceiptItemCode NVARCHAR(24) = N'',
    @_LOTTMP NVARCHAR(600) = N'',
    @_ShiftCode NVARCHAR(24) = N'',
    @_MachinesCode NVARCHAR(24) = N''
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY

        /* =========================================================
           1. TẠO BẢNG KẾT QUẢ - GIỮ NGUYÊN CONTRACT CỦA REPORT
           ========================================================= */

        IF OBJECT_ID('tempdb..#CtTmp') IS NOT NULL
            DROP TABLE #CtTmp;

        SELECT TOP 0
            ItemCode AS Model,
            ItemCode AS ShiftCode,
            CAST(0 AS NUMERIC(18,0)) AS Quantity9,
            ItemCode AS DocDate,
            ItemCode,
            ItemName,
            CAST(N'' AS NVARCHAR(50)) AS ItemLotCode,
            EmployeeCode,
            CAST(N'' AS NVARCHAR(10)) AS Group1,
            DeptName,
            CAST(N'' AS NVARCHAR(256)) AS QRCODE
        INTO #CtTmp
        FROM dbo.B00CtTmp;

        EXECUTE dbo.usp_sys_DefaultTable '#CtTmp';


        /* =========================================================
           2. CHỤP DANH SÁCH LOT CẦN IN
           Không sửa flag trong TMPLOTTACH.
           ========================================================= */

        IF OBJECT_ID('tempdb..#Work') IS NOT NULL
            DROP TABLE #Work;

        SELECT
            ROW_NUMBER() OVER (ORDER BY STT) AS RowNo,
            STT,
            LTRIM(RTRIM(LOT)) AS LotFull,
            LTRIM(RTRIM(MAHANG)) AS MaHang,
            TRY_CONVERT(NUMERIC(18,0), SL) AS SL
        INTO #Work
        FROM dbo.TMPLOTTACH
        WHERE ISNULL(flag, 0) = 0;

        DECLARE
            @TotalRows INT,
            @CurrentRow INT = 1,

            @STT NUMERIC(18,0),
            @MH NVARCHAR(50),
            @LotFull NVARCHAR(600),
            @LotBase NVARCHAR(600),

            @SL NUMERIC(18,0),
            @SLText NVARCHAR(20),
            @SLPadded NVARCHAR(4),

            @LotLength INT,
            @LotSuffix NVARCHAR(4),

            @nam NVARCHAR(4),
            @nsx NVARCHAR(6),

            @QRCODE NVARCHAR(256),
            @model NVARCHAR(25),
            @_Name NVARCHAR(60),

            @ItemLotCodeTP1 NVARCHAR(50),
            @Group1 NVARCHAR(10),

            @ErrorMessage NVARCHAR(2048);

        SELECT @TotalRows = COUNT(*)
        FROM #Work;

        IF @TotalRows = 0
        BEGIN
            SELECT * FROM #CtTmp;
            RETURN;
        END;


        /* =========================================================
           3. XỬ LÝ TỪNG LOT
           ========================================================= */

        WHILE @CurrentRow <= @TotalRows
        BEGIN

            SELECT
                @STT = STT,
                @MH = MaHang,
                @LotFull = LotFull,
                @SL = SL
            FROM #Work
            WHERE RowNo = @CurrentRow;


            /* -----------------------------------------------------
               3.1. Kiểm tra số lượng
               ----------------------------------------------------- */

            IF @SL IS NULL OR @SL <= 0 OR @SL > 9999
            BEGIN
                SET @ErrorMessage =
                    N'Số lượng không hợp lệ tại STT '
                    + CONVERT(NVARCHAR(20), @STT)
                    + N'. Giá trị SL phải từ 1 đến 9999.';

                THROW 50001, @ErrorMessage, 1;
            END;

            SET @SLText = CONVERT(NVARCHAR(20), CONVERT(BIGINT, @SL));

            SET @SLPadded =
                RIGHT(N'0000' + @SLText, 4);


            /* -----------------------------------------------------
               3.2. Kiểm tra LOT
               LOT trong TMPLOTTACH phải có dạng:

               LOT GỐC + 4 SỐ LƯỢNG ĐỆM

               Ví dụ:
               LOT GỐC + 0005
               LOT GỐC + 0030
               ----------------------------------------------------- */

            IF @LotFull IS NULL OR LEN(@LotFull) <= 4
            BEGIN
                SET @ErrorMessage =
                    N'LOT rỗng hoặc quá ngắn tại STT '
                    + CONVERT(NVARCHAR(20), @STT);

                THROW 50002, @ErrorMessage, 1;
            END;

            SET @LotLength = LEN(@LotFull);

            SET @LotSuffix = RIGHT(@LotFull, 4);

            IF @LotSuffix <> @SLPadded
            BEGIN
                SET @ErrorMessage =
                    N'LOT không khớp số lượng tại STT '
                    + CONVERT(NVARCHAR(20), @STT)
                    + N'. LOT=' + @LotFull
                    + N', 4 ký tự cuối=' + @LotSuffix
                    + N', SL yêu cầu=' + @SLPadded;

                THROW 50003, @ErrorMessage, 1;
            END;


            /* -----------------------------------------------------
               3.3. Tách LOT gốc khỏi 4 số lượng đệm
               ----------------------------------------------------- */

            SET @LotBase =
                LEFT(@LotFull, @LotLength - 4);

            SET @_LOTTMP = LTRIM(RTRIM(@LotBase));

            IF LEN(@_LOTTMP) = 0
            BEGIN
                SET @ErrorMessage =
                    N'LOT gốc rỗng tại STT '
                    + CONVERT(NVARCHAR(20), @STT);

                THROW 50004, @ErrorMessage, 1;
            END;


            /* -----------------------------------------------------
               3.4. Tạo LOT hoàn chỉnh
               Giữ tương thích LOT hiện tại và LOT ngắn cũ.
               ----------------------------------------------------- */

            IF LEN(@_LOTTMP) >= 23
            BEGIN

                SET @ItemLotCodeTP1 =
                    @_LOTTMP + @SLPadded;

                SET @_Id =
                    TRY_CONVERT(INT, SUBSTRING(@_LOTTMP, 7, 5));

                SET @_ShiftCode =
                    SUBSTRING(@_LOTTMP, 12, 1);

            END
            ELSE
            BEGIN

                SET @ItemLotCodeTP1 =
                    @_LOTTMP + @SLText;

                SET @_Id =
                    TRY_CONVERT(INT, SUBSTRING(@_LOTTMP, 7, 5));

                SET @_ShiftCode =
                    RIGHT(@_LOTTMP, 1);

            END;


            /* -----------------------------------------------------
               3.5. Kiểm tra ID sản phẩm
               ----------------------------------------------------- */

            IF @_Id IS NULL
            BEGIN
                SET @ErrorMessage =
                    N'Không lấy được ID sản phẩm từ LOT tại STT '
                    + CONVERT(NVARCHAR(20), @STT)
                    + N'. LOT=' + @_LOTTMP;

                THROW 50005, @ErrorMessage, 1;
            END;


            /* -----------------------------------------------------
               3.6. Kiểm tra độ dài ItemLotCodeTP1
               ----------------------------------------------------- */

            IF LEN(@ItemLotCodeTP1) > 50
            BEGIN
                SET @ErrorMessage =
                    N'ItemLotCode vượt quá 50 ký tự tại STT '
                    + CONVERT(NVARCHAR(20), @STT);

                THROW 50006, @ErrorMessage, 1;
            END;


            /* -----------------------------------------------------
               3.7. Tạo ngày sản xuất
               LOT bắt đầu bằng YYMMDD.
               ----------------------------------------------------- */

            SET @nsx = LEFT(@_LOTTMP, 6);

            IF LEN(@nsx) <> 6 OR @nsx LIKE N'%[^0-9]%'
            BEGIN
                SET @ErrorMessage =
                    N'Ngày sản xuất trong LOT không hợp lệ tại STT '
                    + CONVERT(NVARCHAR(20), @STT)
                    + N'. LOT=' + @_LOTTMP;

                THROW 50007, @ErrorMessage, 1;
            END;

            SET @_DocDate =
                RIGHT(@nsx, 2)
                + N'/'
                + SUBSTRING(@nsx, 3, 2)
                + N'/'
                + LEFT(@nsx, 2);


            /* -----------------------------------------------------
               3.8. Lấy thông tin sản phẩm
               ----------------------------------------------------- */

            SET @_ReceiptItemCode = NULL;
            SET @_Name = NULL;
            SET @model = NULL;

            SELECT
                @_ReceiptItemCode = code,
                @_Name = name,
                @model = model
            FROM dbo.B20item
            WHERE id = @_Id;

            IF @_ReceiptItemCode IS NULL
            BEGIN
                SET @ErrorMessage =
                    N'Không tìm thấy mã hàng trong B20item. ID='
                    + CONVERT(NVARCHAR(20), @_Id)
                    + N', LOT=' + @_LOTTMP;

                THROW 50008, @ErrorMessage, 1;
            END;


            /* -----------------------------------------------------
               3.9. TẠO QR CODE - BẮT BUỘC ĐỦ 4 PHẦN

               LOT:SẢN_PHẨM:NGÀY:SỐ_LƯỢNG

               Ví dụ:
               LOT0005:ABC123:12/11/26:5
               ----------------------------------------------------- */

            SET @QRCODE =
                LTRIM(RTRIM(@ItemLotCodeTP1))
                + N':'
                + LTRIM(RTRIM(@_ReceiptItemCode))
                + N':'
                + LTRIM(RTRIM(@_DocDate))
                + N':'
                + @SLText;


            /* -----------------------------------------------------
               3.10. KIỂM TRA QR TRƯỚC KHI TRẢ REPORT
               ----------------------------------------------------- */

            IF @QRCODE IS NULL
                THROW 50009, N'Không tạo được chuỗi QR.', 1;

            IF LEN(@QRCODE) - LEN(REPLACE(@QRCODE, N':', N'')) <> 3
                THROW 50010, N'QR không đủ 4 phần dữ liệu.', 1;

            IF RIGHT(@QRCODE, 1) = N':'
                THROW 50011, N'QR thiếu trường số lượng cuối.', 1;

            IF RIGHT(@QRCODE, LEN(@SLText)) <> @SLText
                THROW 50012, N'Số lượng QR không khớp số lượng LOT.', 1;


            /* -----------------------------------------------------
               3.11. Đưa dữ liệu vào bảng report
               ----------------------------------------------------- */

            SET @Group1 = CONVERT(NVARCHAR(10), @TotalRows);

            INSERT INTO #CtTmp
            (
                Model,
                ShiftCode,
                Quantity9,
                DocDate,
                ItemCode,
                ItemName,
                ItemLotCode,
                EmployeeCode,
                Group1,
                DeptName,
                QRCODE
            )
            VALUES
            (
                @model,
                @_ShiftCode,
                @SL,
                @_DocDate,
                @_ReceiptItemCode,
                @_Name,
                @ItemLotCodeTP1,
                N'',
                @Group1,
                N'',
                @QRCODE
            );


            SET @CurrentRow = @CurrentRow + 1;

        END;


        /* =========================================================
           4. TRẢ KẾT QUẢ CHO REPORT
           ========================================================= */

        SELECT *
        FROM #CtTmp
        ORDER BY ItemLotCode;


    END TRY
    BEGIN CATCH

        DECLARE @CatchMessage NVARCHAR(2048);

        SET @CatchMessage =
            N'Usp_BarCodeView_ThongKe_Tmp5: '
            + ERROR_MESSAGE();

        THROW 50099, @CatchMessage, 1;

    END CATCH;

END;
GO
