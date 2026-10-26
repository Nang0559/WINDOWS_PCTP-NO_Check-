/*
  TEST-ONLY dependency shim.

  The supplied DB57 schema export contains the tables used by the delivery
  procedure but does not contain the source definition of dbo.Split.
  Therefore CI cannot claim that function's source text was exported from DB57.

  This shim exists only so the exact checked-in Usp_Qrcode_Update_Stock2405.sql
  can execute in the disposable CI database. It is intentionally not a
  production deployment artifact and must never be applied to DB57/DB14.
*/
CREATE OR ALTER FUNCTION [dbo].[Split]
(
    @String nvarchar(max),
    @Delimiter nchar(1)
)
RETURNS TABLE
AS
RETURN
(
    SELECT [value]
    FROM STRING_SPLIT(ISNULL(@String, N''), @Delimiter)
);
GO
