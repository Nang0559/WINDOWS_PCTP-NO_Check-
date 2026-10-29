-- g6-delivery-hvn-multilot: Remaining must be 0 after cleanup.
SELECT
    (SELECT COUNT(*) FROM dbo.LUUDOCQRCODE WHERE MAHANGFCC LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%')
  + (SELECT COUNT(*) FROM dbo.DOCQRCODE WHERE MAHANGFCC LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%')
  + (SELECT COUNT(*) FROM dbo.LUUPHIEUGIAOHANG WHERE MAHANG LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%' OR CUA = @Prefix)
  + (SELECT COUNT(*) FROM dbo.TMPPHIEUGIAOHANG WHERE Note LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%' OR CUA = @Prefix OR MAHANG LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%')
  + (SELECT COUNT(*) FROM dbo.STOCKTP WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%')
  + (SELECT COUNT(*) FROM dbo.B20Item WHERE Code LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS Remaining;
