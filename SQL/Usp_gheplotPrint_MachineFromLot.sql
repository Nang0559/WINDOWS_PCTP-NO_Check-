/*
    Usp_gheplotPrint — @MACHINE lấy từ dữ liệu LOT truyền vào.

    @MACHINE = ký tự thứ 14-16 (3 ký tự) của LOTNO, ví dụ:
        LOT 26103000840200861700  ->  @MACHINE = N'086'
    Khớp với app: LotCodeHelper.GetGhepLotMachineCode (PCTP/Shared/Common/LotCodeHelper.cs).

    - @MACHINE = NULL (mặc định): không lọc, in toàn bộ TMPLOTGHEP (in lại lần trước).
    - @MACHINE có giá trị: chỉ lấy các dòng TMPLOTGHEP có LOT (LOT đầu tiên của chuỗi ghép)
      mang mã máy đó. Việc lọc dựa trên CHÍNH LOT, không phụ thuộc cột MachineName.

    Lỗi trước đây: app luôn truyền @MACHINE nhưng SP đã comment tham số này nên SQL báo
    "@MACHINE is not a parameter for procedure Usp_gheplotPrint".

    Thay đổi khác so với bản cũ: @_LOT NVARCHAR(25) -> NVARCHAR(50). Chuỗi "LOT20-SL" dài 25 ký tự khi
    SL 4 chữ số và 26+ ký tự khi SL 5 chữ số; NVARCHAR(25) cắt cụt làm sai số lượng ghép.
*/
CREATE OR ALTER PROC [dbo].[Usp_gheplotPrint]
    @MACHINE          NVARCHAR(50) = NULL,
    @_Id              INT = NULL,
    @_DocDate         NVARCHAR(16) = '',
    @_ReceiptItemCode NVARCHAR(24) = '',
    @_LOTTMP          NVARCHAR(600) = ''
AS
BEGIN
    SET NOCOUNT ON;
    SET FMTONLY OFF;

    SET @MACHINE = NULLIF(LTRIM(RTRIM(@MACHINE)), N'');

    IF OBJECT_ID('TempDb..#CtTmp') IS NOT NULL DROP TABLE #CtTmp
    SELECT TOP 0 ItemCode AS Model, ItemCode AS ShiftCode, CAST(0 AS NUMERIC(18,0)) AS Quantity9,
        itemcode AS DocDate, ItemCode, ItemName, ItemName AS ItemLotCode, EmployeeCode,
        CAST(N'' AS NVARCHAR(10)) AS Group1, DeptName, CAST(N'' AS NVARCHAR(256)) AS QRCODE
    INTO #CtTmp
    FROM dbo.B00CtTmp

    EXECUTE dbo.usp_sys_DefaultTable '#CtTmp'

    DECLARE MY_CURSOR CURSOR LOCAL STATIC READ_ONLY FORWARD_ONLY FOR
        SELECT DISTINCT LOT, MAHANG
        FROM TMPLOTGHEP
        WHERE @MACHINE IS NULL
           OR SUBSTRING(LTRIM(RTRIM(LOT)), 14, 3) = @MACHINE

    OPEN MY_CURSOR
    FETCH NEXT FROM MY_CURSOR INTO @_LOTTMP, @_ReceiptItemCode
    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @nsx AS NVARCHAR(6) = '', @_d AS NVARCHAR(2) = '', @_m AS NVARCHAR(2) = '', @_y AS NVARCHAR(2) = '',
                @_ItemName AS NVARCHAR(50), @_Model AS NVARCHAR(25),
                @_ItemLotCodeTP NVARCHAR(25), @_SL INT = 0,
                @_ShiftCode AS INT, @_QRCODE NVARCHAR(256), @_LOT AS NVARCHAR(50)

        DECLARE TT_CURSOR CURSOR LOCAL STATIC READ_ONLY FORWARD_ONLY FOR
            SELECT * FROM Split(@_LOTTMP, ',')

        OPEN TT_CURSOR
        FETCH NEXT FROM TT_CURSOR INTO @_LOT
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @_SL = @_SL + CAST(RTRIM(SUBSTRING(@_Lot, CHARINDEX('-',@_Lot)+1, LEN(@_Lot)-CHARINDEX('-',@_Lot,1))) AS INT)
            FETCH NEXT FROM TT_CURSOR INTO @_LOT
        END
        CLOSE TT_CURSOR
        DEALLOCATE TT_CURSOR

        SELECT @_ItemName = Name, @_Model = model FROM B20Item WHERE Code = @_ReceiptItemCode

        SET @nsx = LEFT(@_LOTTMP, 6)
        SET @_d = RIGHT(@nsx, 2)
        SET @_m = SUBSTRING(@nsx, 3, 2)
        SELECT @_y = LEFT(@nsx, 2)
        SET @_DocDate = @_d + '/' + @_m + '/' + @_y
        SET @_ShiftCode = SUBSTRING(@_LOTTMP, 12, 1)
        SET @_QRCODE = LTRIM(RTRIM(@_LOTTMP)) + ':' + LTRIM(RTRIM(@_ReceiptItemCode)) + ':' + LTRIM(RTRIM(@_DocDate)) + ':' + CAST(@_SL AS NVARCHAR(10))

        INSERT INTO #CtTmp (Model, ShiftCode, Quantity9, DocDate, ItemCode, ItemName, ItemLotCode, EmployeeCode, Group1, DeptName, QRCODE)
        VALUES (@_Model, @_ShiftCode, @_SL, @_DocDate, @_ReceiptItemCode, @_ItemName, @_LOTTMP, '', '', '', @_QRCODE)

        FETCH NEXT FROM MY_CURSOR INTO @_LOTTMP, @_ReceiptItemCode
    END
    CLOSE MY_CURSOR
    DEALLOCATE MY_CURSOR

    SELECT * FROM #CtTmp
END
GO
