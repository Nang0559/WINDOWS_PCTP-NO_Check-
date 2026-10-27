/*
  dbo.Split - definition exported from DB57 (docs/SQL.sql.txt, object "Split").
  Source docs/SQL.sql.txt SHA-256: 595aafa76194aa239710acd085645283e381899d4669a2141c5841fb01292380
  Body is verbatim; only "CREATE" became "CREATE OR ALTER" so the CI deployment can be re-run.
  It replaces the former STRING_SPLIT-based test shim (05_TestDependencyShim.sql), which returned a column
  named [value] and does not guarantee token order. The production function returns [strval] in input order.
  Disposable CI database only: never apply to DB57/DB14 from this repository.
*/
CREATE OR ALTER FUNCTION [dbo].[Split]
(
 @SearchString NVARCHAR (max),
 @Separator NVARCHAR (5)
)
 
RETURNS @strtable TABLE (strval NVARCHAR (max))
AS
 
BEGIN
 DECLARE @SeparatorLength INT
 SET @SeparatorLength = DATALENGTH(@Separator) / 2
 
 IF (@SeparatorLength = 0)
  RETURN
 
 WHILE 1=1
 BEGIN
  IF (CHARINDEX(@Separator,@SearchString) = 0)
  BEGIN
   INSERT INTO @strtable
   VALUES (@SearchString);
   BREAK;
  END
 
  INSERT INTO @strtable
  VALUES ( LEFT(@SearchString, CHARINDEX(@Separator,@SearchString) - 1))
 
  SET @SearchString = RIGHT(@SearchString, DATALENGTH(@SearchString)/2 - CHARINDEX(@Separator,@SearchString) - @SeparatorLength + 1)
 END
 RETURN
END
GO
