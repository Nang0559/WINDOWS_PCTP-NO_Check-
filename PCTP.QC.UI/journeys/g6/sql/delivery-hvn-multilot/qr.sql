-- g6-delivery-hvn-multilot: QR strings to scan/type for this run (read-only): one carton QR per lot.
--   tem thung (shipping, 4 parts): LOT28:ITEM:dd/MM/yyyy:QTY   (LOT28 = LOT20 + counter(4) + qty(4))
-- Qty comes from the pending line itself ('LOT20-qty' element of TMPPHIEUGIAOHANG.LOT), so it always matches the seed.
-- Scan every carton QR of a line before confirming that line. Expected stock after all 3 lines: SLXUAT = ExpectedIssued,
-- SLCONLAI = SLNHAP - ExpectedIssued.
SELECT N'TEM_THUNG_XUAT_KHO' AS Kind,
       t.STT AS Stt,
       RTRIM(s.LOT) + N'0001' + RIGHT(N'0000' + CAST(x.Qty AS nvarchar(4)), 4) + N':' + RTRIM(s.PART) + N':'
         + CONVERT(nvarchar(10), s.NGAYSX, 103) + N':' + CAST(x.Qty AS nvarchar(10)) AS Qr,
       x.Qty AS ExpectedIssued,
       s.SLNHAP - x.Qty AS ExpectedRemaining
FROM dbo.STOCKTP s
JOIN dbo.TMPPHIEUGIAOHANG t
  ON t.MAHANG = s.PART
 AND t.Note LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%'
 AND t.LOT LIKE N'%' + RTRIM(s.LOT) + N'-%'
CROSS APPLY (SELECT CHARINDEX(RTRIM(s.LOT) + N'-', t.LOT) AS P) p
CROSS APPLY (SELECT TRY_CAST(SUBSTRING(t.LOT, p.P + 21,
                    CHARINDEX(N',', t.LOT + N',', p.P) - (p.P + 21)) AS int) AS Qty) x
WHERE s.PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%'
ORDER BY t.STT, s.NGAYSX DESC;
