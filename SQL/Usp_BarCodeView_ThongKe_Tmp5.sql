/*
    Usp_BarCodeView_ThongKe_Tmp5 - dữ liệu tem TÁCH LOT / IN LẠI TEM GỐC cho khách KHÔNG phải YMVN (report GHEPLOT).
    Nguồn: TMPLOTTACH (flag = 0). LOT trong TMPLOTTACH = LOT 23 ký tự + 4 số SL đệm 0 (27 ký tự);
    SP cắt 4 ký tự cuối rồi ghép lại SL đệm 0. Ca = ký tự 12 của LOT (ký tự 13 là Gear, KHÔNG phải Ca).

    Thay đổi so với bản cũ:
      - ORDER BY STT cho các select top 1 (thứ tự in ổn định, hai select luôn cùng một dòng).
      - Đánh dấu flag theo MAHANG của chính dòng TMPLOTTACH (@_MH) + STT, thay vì mã từ B20item
        (lệch hoa/thường hoặc khoảng trắng sẽ làm dòng không được đánh dấu -> in lặp tem).
      - Phía C# phải ghi LOT đủ 4 số SL cuối (cả khi in lại tem gốc); nhánh LEN < 23 chỉ còn là tương thích cũ.
*/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROC [dbo].[Usp_BarCodeView_ThongKe_Tmp5]
    @_Id INT = NULL,
    @_DocDate nvarchar(16) = '',
    @_ReceiptItemCode NVARCHAR(24) = '',
    @_LOTTMP NVARCHAR(600) = '',
    @_ShiftCode NVARCHAR(24) = '',
    @_MachinesCode NVARCHAR(24) = N''
AS
BEGIN
    SET NOCOUNT ON;
    IF OBJECT_ID('TempDb..#CtTmp') IS NOT NULL DROP TABLE #CtTmp
    SELECT TOP 0 ItemCode AS Model, ItemCode AS ShiftCode, CAST(0 AS NUMERIC(18,0)) AS Quantity9,
        itemcode AS DocDate, ItemCode, ItemName, CAST(N'' AS NVARCHAR(50)) AS ItemLotCode, EmployeeCode, CAST(N'' AS NVARCHAR(10)) AS Group1,
        DeptName, CAST(N'' AS nvarchar(256)) AS QRCODE
    INTO #CtTmp
    FROM dbo.B00CtTmp

    EXECUTE dbo.usp_sys_DefaultTable '#CtTmp'
    DECLARE @_Numrow INT, @_SL NUMERIC(18,0), @_STT NUMERIC(18,0), @_MH NVARCHAR(50)
    SELECT @_Numrow = COUNT(*) FROM TMPLOTTACH WHERE flag = 0

    WHILE @_Numrow > 0
    BEGIN
        -- Lấy một dòng duy nhất theo STT; cắt 4 ký tự cuối (SL đệm 0) để ra LOT 23 ký tự
        SELECT TOP 1 @_MH = MAHANG, @_LOTTMP = SUBSTRING(lot, 0, LEN(lot) - 3), @_SL = SL, @_STT = STT
        FROM TMPLOTTACH WHERE flag = 0 ORDER BY STT

        DECLARE @nam AS nvarchar(4) = '', @_dat AS nvarchar(10) = '', @QRCODE AS nvarchar(256) = '', @model AS nvarchar(25) = '',
                @_Name AS nvarchar(60) = '', @nsx nvarchar(6) = '', @_ItemLotCodeTP1 NVARCHAR(50)

        IF LEN(@_LOTTMP) >= 23
        BEGIN
            SET @_ItemLotCodeTP1 = LTRIM(RTRIM(@_LOTTMP)) + CASE LEN(LTRIM(RTRIM(STR(@_SL))))
                WHEN 1 THEN '000' + LTRIM(RTRIM(STR(@_SL)))
                WHEN 2 THEN '00' + LTRIM(RTRIM(STR(@_SL)))
                WHEN 3 THEN '0' + LTRIM(RTRIM(STR(@_SL)))
                WHEN 4 THEN LTRIM(RTRIM(STR(@_SL)))
                END
            SET @_Id = CAST(SUBSTRING(@_LOTTMP, 7, 5) AS int)
            SET @_ShiftCode = SUBSTRING(@_LOTTMP, 12, 1)             -- ký tự 12 = Ca
        END
        ELSE
        BEGIN
            -- Tương thích dữ liệu cũ (LOT ngắn)
            SET @_ItemLotCodeTP1 = LTRIM(RTRIM(@_LOTTMP)) + LTRIM(RTRIM(STR(@_SL)))
            SET @_Id = SUBSTRING(@_LOTTMP, 7, 5)
            SET @_ShiftCode = RIGHT(LTRIM(RTRIM(@_LOTTMP)), 1)
        END

        SET @nsx = LEFT(@_LOTTMP, 6)
        SELECT @nam = YEAR(GETDATE())
        SET @_DocDate = RIGHT(@nsx, 2) + '/' + SUBSTRING(@nsx, 3, 2) + '/' + LEFT(@nsx, 2)

        SELECT @_ReceiptItemCode = code, @_Name = name, @model = model FROM B20item WHERE id = @_Id
        SET @QRCODE = LTRIM(RTRIM(@_ItemLotCodeTP1)) + ':' + LTRIM(RTRIM(@_ReceiptItemCode)) + ':' + LTRIM(RTRIM(@_DocDate)) + ':' + LTRIM(RTRIM(STR(@_SL)))
        INSERT INTO #CtTmp (Model, ShiftCode, Quantity9, DocDate, ItemCode, ItemName, ItemLotCode, EmployeeCode, Group1, DeptName, QRCODE)
        VALUES (@model, @_ShiftCode, @_SL, @_DocDate, @_ReceiptItemCode, @_Name, @_ItemLotCodeTP1, '', @_Numrow, '', @QRCODE)
        UPDATE TMPLOTTACH SET flag = 1 WHERE MAHANG = @_MH AND STT = @_STT
        SET @_Numrow = @_Numrow - 1
    END
    SELECT * FROM #CtTmp
    UPDATE TMPLOTTACH SET flag = 0
END
GO
