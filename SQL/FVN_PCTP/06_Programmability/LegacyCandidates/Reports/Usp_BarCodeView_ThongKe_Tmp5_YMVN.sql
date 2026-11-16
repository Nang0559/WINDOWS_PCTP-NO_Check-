/*
    Usp_BarCodeView_ThongKe_Tmp5_YMVN - dữ liệu tem TÁCH LOT / IN LẠI TEM GỐC của YMVN (report GHEPLOT_YMVN).
    Nguồn: bảng TMPLOTTACH (flag = 0). LOT trong TMPLOTTACH là LOT đầy đủ (LOT gốc + 4 số SL), dùng nguyên.

    Thay đổi so với bản cũ:
      - Gear: tra B20Gear (Code -> Name) thay vì hard-code 1..5. B20Gear: 0='', 1=A, 2=B, 3=C, 4=D, 5=E, 6=F.
      - TRY_CAST ký tự 13 để LOT lạ không làm SP lỗi chuyển kiểu.
      - ORDER BY STT cho select top 1 để thứ tự in ổn định.
    Cột Model của #CtTmp mang chữ Gear (report bind [Model] vào ô Gear); ShiftCode = ký tự 12 của LOT.
*/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROC [dbo].[Usp_BarCodeView_ThongKe_Tmp5_YMVN]
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
        SELECT TOP 1 @_MH = MAHANG, @_LOTTMP = lot, @_SL = SL, @_STT = STT
        FROM TMPLOTTACH WHERE flag = 0 ORDER BY STT

        DECLARE @gear AS int, @nam AS nvarchar(4) = '', @_dat AS nvarchar(10) = '', @QRCODE AS nvarchar(256) = '',
                @model AS nvarchar(25) = '', @_Name AS nvarchar(60) = '', @nsx nvarchar(6) = '',
                @_ItemLotCodeTP1 NVARCHAR(50)

        SET @_ItemLotCodeTP1 = LTRIM(RTRIM(@_LOTTMP))
        SET @_Id = CAST(SUBSTRING(@_LOTTMP, 7, 5) AS int)
        SET @gear = TRY_CAST(SUBSTRING(@_LOTTMP, 13, 1) AS int)      -- ký tự 13 = B20Gear.Code
        SET @_ShiftCode = SUBSTRING(@_LOTTMP, 12, 1)                 -- ký tự 12 = Ca

        SET @nsx = LEFT(@_LOTTMP, 6)
        SELECT @nam = YEAR(GETDATE())
        SET @_DocDate = RIGHT(@nsx, 2) + '/' + SUBSTRING(@nsx, 3, 2) + '/' + LEFT(@nsx, 2)

        SELECT @_ReceiptItemCode = code, @_Name = name FROM B20item WHERE id = @_Id

        SET @model = ''
        SELECT @model = LTRIM(RTRIM(Name)) FROM B20Gear WHERE Code = @gear
        SET @model = ISNULL(@model, '')                              -- Code 0 / không có = không phân loại Gear

        SET @QRCODE = LTRIM(RTRIM(@_LOTTMP)) + ':' + LTRIM(RTRIM(@_ReceiptItemCode)) + ':' + LTRIM(RTRIM(@_DocDate)) + ':' + LTRIM(RTRIM(STR(@_SL)))
        INSERT INTO #CtTmp (Model, ShiftCode, Quantity9, DocDate, ItemCode, ItemName, ItemLotCode, EmployeeCode, Group1, DeptName, QRCODE)
        VALUES (@model, @_ShiftCode, @_SL, @_DocDate, @_ReceiptItemCode, @_Name, @_ItemLotCodeTP1, '', @_Numrow, '', @QRCODE)
        UPDATE TMPLOTTACH SET flag = 1 WHERE lot = @_LOTTMP AND STT = @_STT
        SET @_Numrow = @_Numrow - 1
    END
    SELECT * FROM #CtTmp
    UPDATE TMPLOTTACH SET flag = 0
END
GO
