CREATE OR ALTER PROC [dbo].[Usp_Qrcode_Update_Stock_SP]
    @GIOGIAOFCC NVARCHAR(250) = N'',
    @NHAMAY NVARCHAR(250) = N'',
    @TMPTABLE NVARCHAR(128) = N'TMPPHIEUGIAOHANG',
    @DOCQRTABLE NVARCHAR(128) = N'DOCQRCODE'
AS
BEGIN
    SET NOCOUNT ON;

    /*
      Compatibility wrapper:
      - All stock, delivery-ledger, QR archive, TMP status and QR cleanup work
        is implemented by Usp_Qrcode_Update_Stock2405.
      - FIFO_SP is the only SP-specific behavior: resolve one eligible physical
        LOT by SP/MaHang and quantity, then continue through the common pipeline.
      - SlotLot is intentionally not updated here. PhieuKhoRepository applies
        the shared stock movement service inside the caller UnitOfWork.
    */
    EXEC dbo.Usp_Qrcode_Update_Stock2405
        @GIOGIAOFCC = @GIOGIAOFCC,
        @NHAMAY = @NHAMAY,
        @TMPTABLE = @TMPTABLE,
        @DOCQRTABLE = @DOCQRTABLE,
        @LOTRESOLUTIONMODE = N'FIFO_SP';
END;
