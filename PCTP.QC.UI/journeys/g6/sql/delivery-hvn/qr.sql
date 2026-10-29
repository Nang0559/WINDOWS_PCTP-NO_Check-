-- g6-delivery-hvn: QR strings to scan/type for this run (read-only).
--   tem thung (shipping, 4 parts): LOT28:ITEM:dd/MM/yyyy:QTY   (LOT28 = LOT20 + counter(4) + qty(4))
-- ASSUMPTION to confirm with ONE manual scan: Usp_Qrcode_Update_Stock2405 matches the 28-char LOT back to STOCKTP.LOT (LOT20)
-- and to TMPPHIEUGIAOHANG.LOT (LOT20-qty). If the screen rejects it, send the screen message and the seed is adjusted.
SELECT N'TEM_THUNG_XUAT_KHO' AS Kind,
       RTRIM(s.LOT) + N'0001' + RIGHT(N'0000' + CAST(s.SLNHAP AS nvarchar(4)), 4) + N':' + RTRIM(s.PART) + N':'
         + CONVERT(nvarchar(10), s.NGAYSX, 103) + N':' + CAST(s.SLNHAP AS nvarchar(10)) AS Qr
FROM dbo.STOCKTP s WHERE s.PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%'
UNION ALL
SELECT N'TMP_STT_CAN_GIAO', CAST(t.STT AS nvarchar(10)) + N' / ' + RTRIM(t.LOT)
FROM dbo.TMPPHIEUGIAOHANG t WHERE t.Note LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%';
