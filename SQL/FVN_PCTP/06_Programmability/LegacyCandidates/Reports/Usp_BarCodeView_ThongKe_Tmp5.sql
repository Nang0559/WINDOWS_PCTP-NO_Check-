/*
    Usp_BarCodeView_ThongKe_Tmp5 - tem TÁCH LOT / IN LẠI TEM GỐC cho khách KHÔNG phải YMVN.
    Cơ chế LOT đồng nhất với Usp_BarCodeView_ThongKe_Tmp5_YMVN:
      - TMPLOTTACH.LOT đã là LOT đầy đủ = LOT gốc + 4 ký tự SL đệm.
      - Dùng nguyên LOT làm ItemLotCode; KHÔNG cắt 4 ký tự rồi ghép lại.
      - QR luôn có cấu trúc: LOT_FULL:ITEM_CODE:DD/MM/YY:SL.
      - Ký tự 12 của LOT là Ca; ký tự 13 (Gear) không được dùng làm Ca ở luồng MP/SP.
      - Kiểm tra LOT suffix phải khớp SL trước khi trả dữ liệu report.
    Giữ nguyên contract #CtTmp và tên/các tham số procedure cho report GHEPLOT.
*/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROC [dbo].[Usp_BarCodeView_ThongKe_Tmp5]
    @_Id INT = NULL,
    @_DocDate NVARCHAR(16) = N'',
    @_ReceiptItemCode NVARCHAR(24) = N'',
    @_LOTTMP NVARCHAR(600) = N'',
    @_ShiftCode NVARCHAR(24) = N'',
    @_MachinesCode NVARCHAR(24) = N''
AS
BEGIN
    SET NOCOUNT ON;

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

    DECLARE
        @_Numrow INT,
        @_SL NUMERIC(18,0),
        @_STT NUMERIC(18,0),
        @_MH NVARCHAR(50),
        @_SLText NVARCHAR(20),
        @_SLPadded NVARCHAR(4),
        @_LotFull NVARCHAR(600),
        @_LotSuffix NVARCHAR(4),
        @_nam NVARCHAR(4),
        @_nsx NVARCHAR(6),
        @_QRCODE NVARCHAR(256),
        @_model NVARCHAR(25),
        @_Name NVARCHAR(60),
        @_ItemLotCodeTP1 NVARCHAR(50);

    /*
       Validate toàn bộ batch trước khi thay đổi flag.
       LOT chuẩn của TMPLOTTACH là 23 ký tự LOT gốc + 4 ký tự SL.
       SL được giới hạn 1..9999 vì hậu tố LOT có đúng 4 chữ số.
    */
    IF EXISTS
    (
        SELECT 1
        FROM dbo.TMPLOTTACH
        WHERE ISNULL(flag, 0) = 0
          AND
          (
              SL IS NULL
              OR SL <= 0
              OR SL > 9999
              OR LOT IS NULL
              OR LEN(LTRIM(RTRIM(LOT))) < 27
              OR RIGHT(LTRIM(RTRIM(LOT)), 4)
                    <> RIGHT(N'0000' + CONVERT(NVARCHAR(20), TRY_CONVERT(BIGINT, SL)), 4)
              OR TRY_CONVERT(INT, SUBSTRING(LTRIM(RTRIM(LOT)), 7, 5)) IS NULL
              OR LEFT(LTRIM(RTRIM(LOT)), 6) LIKE N'%[^0-9]%'
          )
    )
    BEGIN
        THROW 50201,
            N'TMPLOTTACH có LOT/SL không hợp lệ. LOT phải có đủ LOT gốc + 4 số SL, hậu tố phải khớp SL (1..9999).',
            1;
    END;

    /* Chặn lỗi mã hàng trước khi bắt đầu đánh dấu flag. */
    IF EXISTS
    (
        SELECT 1
        FROM dbo.TMPLOTTACH AS T
        WHERE ISNULL(T.flag, 0) = 0
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.B20item AS I
              WHERE I.id = TRY_CONVERT(INT, SUBSTRING(LTRIM(RTRIM(T.LOT)), 7, 5))
          )
    )
    BEGIN
        THROW 50202,
            N'Không tìm thấy mã hàng tương ứng trong B20item cho một hoặc nhiều LOT của TMPLOTTACH.',
            1;
    END;

    SELECT @_Numrow = COUNT(*)
    FROM dbo.TMPLOTTACH
    WHERE ISNULL(flag, 0) = 0;

    WHILE @_Numrow > 0
    BEGIN
        /* Lấy LOT FULL nguyên vẹn giống cơ chế YMVN. */
        SELECT TOP 1
            @_MH = MAHANG,
            @_LotFull = LTRIM(RTRIM(LOT)),
            @_SL = SL,
            @_STT = STT
        FROM dbo.TMPLOTTACH
        WHERE ISNULL(flag, 0) = 0
        ORDER BY STT;

        IF @_LotFull IS NULL OR @_SL IS NULL OR @_STT IS NULL
            THROW 50203, N'Không lấy được dòng LOT hợp lệ từ TMPLOTTACH.', 1;

        SET @_SLText = CONVERT(NVARCHAR(20), CONVERT(BIGINT, @_SL));
        SET @_SLPadded = RIGHT(N'0000' + @_SLText, 4);

        /* Không cắt/ghép LOT: giữ nguyên giá trị đầy đủ đã lưu trong TMPLOTTACH. */
        SET @_ItemLotCodeTP1 = @_LotFull;

        IF LEN(@_ItemLotCodeTP1) > 50
            THROW 50204, N'LOT đầy đủ vượt quá độ dài ItemLotCode (50 ký tự) của report GHEPLOT.', 1;

        SET @_Id = TRY_CONVERT(INT, SUBSTRING(@_LotFull, 7, 5));
        IF @_Id IS NULL
            THROW 50205, N'Không phân tích được ID mã hàng từ LOT.', 1;

        /* MP/SP: ký tự 12 là Ca. Không đọc ký tự 13 làm Gear như YMVN. */
        SET @_ShiftCode = SUBSTRING(@_LotFull, 12, 1);

        SET @_nsx = LEFT(@_LotFull, 6);
        SET @_DocDate =
            RIGHT(@_nsx, 2) + N'/'
            + SUBSTRING(@_nsx, 3, 2) + N'/'
            + LEFT(@_nsx, 2);

        SET @_ReceiptItemCode = NULL;
        SET @_Name = NULL;
        SET @_model = NULL;

        SELECT
            @_ReceiptItemCode = code,
            @_Name = name,
            @_model = model
        FROM dbo.B20item
        WHERE id = @_Id;

        IF @_ReceiptItemCode IS NULL
            THROW 50206, N'Không tìm thấy ItemCode trong B20item.', 1;

        /*
           QR đủ 4 phần, số lượng dùng dạng số nguyên không có khoảng trắng
           do STR() tạo ra.
        */
        SET @_QRCODE =
            @_ItemLotCodeTP1 + N':'
            + LTRIM(RTRIM(@_ReceiptItemCode)) + N':'
            + LTRIM(RTRIM(@_DocDate)) + N':'
            + @_SLText;

        IF @_QRCODE IS NULL
           OR LEN(@_QRCODE) - LEN(REPLACE(@_QRCODE, N':', N'')) <> 3
           OR RIGHT(@_QRCODE, 1) = N':'
           OR RIGHT(@_QRCODE, LEN(@_SLText)) <> @_SLText
        BEGIN
            THROW 50207, N'QR tạo ra không đủ 4 trường hoặc số lượng QR không khớp SL.', 1;
        END;

        INSERT INTO #CtTmp
        (
            Model, ShiftCode, Quantity9, DocDate, ItemCode, ItemName,
            ItemLotCode, EmployeeCode, Group1, DeptName, QRCODE
        )
        VALUES
        (
            @_model, @_ShiftCode, @_SL, @_DocDate, @_ReceiptItemCode, @_Name,
            @_ItemLotCodeTP1, N'', CONVERT(NVARCHAR(10), @_Numrow), N'', @_QRCODE
        );

        /* Đánh dấu đúng dòng đã lấy; MAHANG + STT là khóa logic của batch hiện tại. */
        UPDATE dbo.TMPLOTTACH
        SET flag = 1
        WHERE STT = @_STT
          AND ISNULL(MAHANG, N'') = ISNULL(@_MH, N'');

        IF @@ROWCOUNT <> 1
            THROW 50208, N'Không đánh dấu được duy nhất dòng TMPLOTTACH đang xử lý.', 1;

        SET @_Numrow = @_Numrow - 1;
    END;

    SELECT *
    FROM #CtTmp
    ORDER BY ItemLotCode;

    /*
       Giữ tương thích với luồng cũ: reset flag sau khi lấy dữ liệu report.
       Lưu ý: TMPLOTTACH hiện là bảng dùng chung; C# đang DELETE toàn bảng
       trước khi insert. Không chạy đồng thời nhiều phiên in trên cùng DB.
       Muốn hỗ trợ in song song cần bổ sung BatchId/SessionId ở cả C# và SQL.
    */
    UPDATE dbo.TMPLOTTACH
    SET flag = 0;
END
GO
