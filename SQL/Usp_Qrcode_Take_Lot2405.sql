USE [B7R2_FCC]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROC [dbo].[Usp_Qrcode_Take_Lot2405]
    @_MaFCC NVARCHAR(50)=N'',
    @_STTP INT=0,
    @_DeM INT=0,
    @_SLGIAO INT=0,
    @DOCQRTABLE NVARCHAR(128)=N'DOCQRCODE',
    @TMPTABLE NVARCHAR(128)=N'TMPPHIEUGIAOHANG'
AS
BEGIN
 SET NOCOUNT ON;
 SET XACT_ABORT ON;
 IF @DOCQRTABLE IS NULL OR @DOCQRTABLE NOT IN(N'DOCQRCODE',N'DOCQRCODE_SP',N'DOCQRCODE_100003')
  THROW 51101,N'Tên bảng DOCQRTABLE không hợp lệ.',1;
 IF @TMPTABLE IS NULL OR @TMPTABLE NOT IN(N'TMPPHIEUGIAOHANG',N'TMPPHIEUGIAOHANG_SP',N'TMPPHIEUGIAOHANG_100003')
  THROW 51102,N'Tên bảng TMPTABLE không hợp lệ.',1;
 IF NULLIF(LTRIM(RTRIM(@_MaFCC)),N'') IS NULL OR @_STTP<=0 OR @_SLGIAO<=0
  THROW 51103,N'Thông tin mã hàng, STT hoặc số lượng giao không hợp lệ.',1;

 DECLARE @OwnTran bit=0,@Savepoint bit=0,@sql nvarchar(max),@path nvarchar(max),
         @chosenCount int=0,@affected int=0;
 BEGIN TRY
  IF @@TRANCOUNT=0
  BEGIN
   BEGIN TRANSACTION;
   SET @OwnTran=1;
  END
  ELSE
  BEGIN
   SAVE TRANSACTION PctpTakeLot2405;
   SET @Savepoint=1;
  END;

  SET @sql=N'
   IF NOT EXISTS(SELECT 1 FROM dbo.'+QUOTENAME(@TMPTABLE)+N' WITH(UPDLOCK,HOLDLOCK)
    WHERE STT=@stt AND MAHANG=@ma AND SOLUONG=@qty AND ISNULL(LOT,N'''')=N'''')
    THROW 51104,N''Dòng phiếu không còn hợp lệ hoặc đã được xử lý.'',1;';
  EXEC sys.sp_executesql @sql,N'@stt int,@ma nvarchar(50),@qty int',@_STTP,@_MaFCC,@_SLGIAO;

  CREATE TABLE #QRData(STT int NOT NULL PRIMARY KEY,LOTFCC nvarchar(56),SLTEMFCC int NOT NULL,SLTEMHVN int NOT NULL);
  SET @sql=N'INSERT #QRData(STT,LOTFCC,SLTEMFCC,SLTEMHVN)
   SELECT STT,LOTFCC,ISNULL(SLTEMFCC,0),ISNULL(SLTEMHVN,0)
   FROM dbo.'+QUOTENAME(@DOCQRTABLE)+N' WITH(UPDLOCK,HOLDLOCK)
   WHERE MAHANGFCC=@ma AND ISNULL(KETQUA,N'''')<>N''DG'' AND ISNULL(SLTEMFCC,0)>0
     AND NULLIF(LTRIM(RTRIM(LOTFCC)),N'''') IS NOT NULL;';
  EXEC sys.sp_executesql @sql,N'@ma nvarchar(50)',@_MaFCC;

  CREATE TABLE #DP(Qty int NOT NULL PRIMARY KEY,Path nvarchar(max) NOT NULL);
  CREATE TABLE #DPPrev(Qty int NOT NULL PRIMARY KEY,Path nvarchar(max) NOT NULL);
  INSERT #DP VALUES(0,N',');
  DECLARE @qrStt int,@qrQty int,@qrLot nvarchar(56),@qrHvn int;
  DECLARE QrCursor CURSOR LOCAL FAST_FORWARD FOR SELECT STT,SLTEMFCC,LOTFCC,SLTEMHVN FROM #QRData ORDER BY STT;
  OPEN QrCursor;
  FETCH NEXT FROM QrCursor INTO @qrStt,@qrQty,@qrLot,@qrHvn;
  WHILE @@FETCH_STATUS=0
  BEGIN
   IF @qrQty>0 AND @qrQty<=@_SLGIAO
   BEGIN
    TRUNCATE TABLE #DPPrev;
    INSERT #DPPrev SELECT Qty,Path FROM #DP;
    INSERT #DP(Qty,Path)
    SELECT p.Qty+@qrQty,p.Path+CONVERT(nvarchar(20),@qrStt)+N','
    FROM #DPPrev p
    WHERE p.Qty+@qrQty<=@_SLGIAO
      AND NOT EXISTS(SELECT 1 FROM #DP d WHERE d.Qty=p.Qty+@qrQty);
    IF EXISTS(SELECT 1 FROM #DP WHERE Qty=@_SLGIAO) BREAK;
   END;
   FETCH NEXT FROM QrCursor INTO @qrStt,@qrQty,@qrLot,@qrHvn;
  END;
  CLOSE QrCursor; DEALLOCATE QrCursor;
  SELECT @path=Path FROM #DP WHERE Qty=@_SLGIAO;
  IF @path IS NULL
  BEGIN
   IF @OwnTran=1 COMMIT TRANSACTION;
   SELECT CAST(NULL AS nvarchar(56)) AS LOTFCC,CAST(0 AS int) AS FCC,CAST(0 AS int) AS HVN WHERE 1=0;
   RETURN;
  END;

  CREATE TABLE #Chosen(STT int NOT NULL PRIMARY KEY);
  INSERT #Chosen SELECT DISTINCT TRY_CONVERT(int,value) FROM STRING_SPLIT(@path,N',')
   WHERE NULLIF(value,N'') IS NOT NULL AND TRY_CONVERT(int,value) IS NOT NULL;
  SELECT @chosenCount=COUNT(*) FROM #Chosen;
  IF @chosenCount=0 THROW 51105,N'Danh sách QR phân bổ rỗng.',1;

  SELECT TOP 0 LOTFCC,SLTEMFCC,SLTEMHVN INTO #CtTmp FROM dbo.DOCQRCODE;
  DECLARE @lot nvarchar(56),@fcc int,@hvn int,@pos int,@next int,@token nvarchar(56),
          @hyphen int,@lotPart nvarchar(30),@qtyPart int;
  DECLARE ChosenCursor CURSOR LOCAL FAST_FORWARD FOR
   SELECT q.LOTFCC,q.SLTEMFCC,q.SLTEMHVN FROM #QRData q JOIN #Chosen c ON c.STT=q.STT ORDER BY q.STT;
  OPEN ChosenCursor;
  FETCH NEXT FROM ChosenCursor INTO @lot,@fcc,@hvn;
  WHILE @@FETCH_STATUS=0
  BEGIN
   IF CHARINDEX(N',',ISNULL(@lot,N''))>0
   BEGIN
    SET @pos=1;
    WHILE @pos<=LEN(ISNULL(@lot,N''))
    BEGIN
     SET @next=CHARINDEX(N',',@lot,@pos); IF @next=0 SET @next=LEN(@lot)+1;
     SET @token=LTRIM(RTRIM(SUBSTRING(@lot,@pos,@next-@pos)));
     SET @hyphen=CHARINDEX(N'-',@token);
     IF @hyphen>1 AND @hyphen<LEN(@token)
     BEGIN
      SET @lotPart=LEFT(@token,@hyphen-1);
      SET @qtyPart=TRY_CONVERT(int,SUBSTRING(@token,@hyphen+1,20));
      IF @qtyPart>0
      BEGIN
       IF EXISTS(SELECT 1 FROM #CtTmp WHERE LOTFCC=@lotPart)
        UPDATE #CtTmp SET SLTEMFCC=SLTEMFCC+@qtyPart,SLTEMHVN=SLTEMHVN+@qtyPart WHERE LOTFCC=@lotPart;
       ELSE INSERT #CtTmp(LOTFCC,SLTEMFCC,SLTEMHVN) VALUES(@lotPart,@qtyPart,@qtyPart);
      END;
     END;
     SET @pos=@next+1;
    END;
   END
   ELSE
   BEGIN
    IF EXISTS(SELECT 1 FROM #CtTmp WHERE LOTFCC=RTRIM(@lot))
     UPDATE #CtTmp SET SLTEMFCC=SLTEMFCC+@fcc,SLTEMHVN=SLTEMHVN+@hvn WHERE LOTFCC=RTRIM(@lot);
    ELSE INSERT #CtTmp(LOTFCC,SLTEMFCC,SLTEMHVN) VALUES(RTRIM(@lot),@fcc,@hvn);
   END;
   FETCH NEXT FROM ChosenCursor INTO @lot,@fcc,@hvn;
  END;
  CLOSE ChosenCursor; DEALLOCATE ChosenCursor;

  -- LOT payload must reconcile to the same exact quantity before any QR is marked DG.
  DECLARE @LotTotal bigint;
  SELECT @LotTotal=SUM(CONVERT(bigint,SLTEMFCC)) FROM #CtTmp;
  IF @LotTotal IS NULL OR @LotTotal<>CONVERT(bigint,@_SLGIAO)
   THROW 51108,N'Tổng số lượng LOT sau khi phân rã không khớp số lượng dòng phiếu; hủy cấp phát.',1;

  SET @sql=N'UPDATE q SET KETQUA=N''DG'',STTBAN=@stt,GIO=@stt
    FROM dbo.'+QUOTENAME(@DOCQRTABLE)+N' q JOIN #Chosen c ON c.STT=q.STT
    WHERE q.MAHANGFCC=@ma AND ISNULL(q.KETQUA,N'''')<>N''DG'';
    SET @n=@@ROWCOUNT;';
  EXEC sys.sp_executesql @sql,N'@stt int,@ma nvarchar(50),@n int OUTPUT',@_STTP,@_MaFCC,@affected OUTPUT;
  IF @affected<>@chosenCount
  BEGIN
   DECLARE @claimError nvarchar(2048)=CONCAT(N'QR đã bị tiến trình khác nhận; expected=',@chosenCount,N', actual=',@affected,N'.');
   THROW 51106,@claimError,1;
  END;

  SET @sql=N'UPDATE dbo.'+QUOTENAME(@TMPTABLE)+N'
    SET TTPHIEU=N''Đã Thống Kê QR'',STATUSDOC=N''OK''
    WHERE STT=@stt AND MAHANG=@ma AND SOLUONG=@qty AND ISNULL(LOT,N'''')=N'''';
    SELECT @n=@@ROWCOUNT;';
  SET @affected=0;
  EXEC sys.sp_executesql @sql,N'@stt int,@ma nvarchar(50),@qty int,@n int OUTPUT',@_STTP,@_MaFCC,@_SLGIAO,@affected OUTPUT;
  IF @affected<>1 THROW 51107,N'Dòng phiếu thay đổi trong lúc cấp phát; hủy toàn bộ.',1;

  SELECT LOTFCC,SUM(SLTEMFCC) AS FCC,SUM(SLTEMHVN) AS HVN FROM #CtTmp GROUP BY LOTFCC ORDER BY LOTFCC;
  IF @OwnTran=1 COMMIT TRANSACTION;
 END TRY
 BEGIN CATCH
  IF CURSOR_STATUS('local','QrCursor')>=0 BEGIN CLOSE QrCursor; DEALLOCATE QrCursor; END;
  IF CURSOR_STATUS('local','ChosenCursor')>=0 BEGIN CLOSE ChosenCursor; DEALLOCATE ChosenCursor; END;
  IF @OwnTran=1 AND XACT_STATE()<>0 ROLLBACK TRANSACTION;
  ELSE IF @Savepoint=1 AND XACT_STATE()=1 ROLLBACK TRANSACTION PctpTakeLot2405;
  THROW;
 END CATCH;
END
GO
