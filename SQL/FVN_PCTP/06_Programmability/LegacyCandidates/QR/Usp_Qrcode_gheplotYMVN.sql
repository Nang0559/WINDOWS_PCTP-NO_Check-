USE [B7R2_FCC]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
/*
    Compatibility wrapper only.
    All LOT allocation logic, including YMVN Gear mapping, lives in dbo.Usp_Qrcode_gheplot.
    Gear text uses B20Gear.Name (A..F); the 13th LOT character uses B20Gear.Code (0..6).
    Keep this wrapper temporarily for older deployed callers during rollout.
*/
CREATE OR ALTER PROC dbo.Usp_Qrcode_gheplotYMVN
    @TMPTABLE nvarchar(100) = N'TMPPHIEUGIAOHANG_100002',
    @EXPECTEDTMPTABLE nvarchar(100) = N'TMPPHIEUGIAOHANG_100002',
    @VIEWTABLEPREFIX nvarchar(100) = N'TMPPHIEUGIAOHANGView_100002'
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.Usp_Qrcode_gheplot
        @TMPTABLE = @TMPTABLE,
        @USEGEAR = 1,
        @EXPECTEDTMPTABLE = @EXPECTEDTMPTABLE,
        @VIEWTABLEPREFIX = @VIEWTABLEPREFIX;
END
GO
