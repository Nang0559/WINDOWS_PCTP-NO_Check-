-- g6-receive: the QR strings to scan/type for this run (read-only). Formats come from QRCodeParser:
--   tem tong  (receiving, 6 parts): LOT28:ITEM:dd/MM/yyyy:QTY:SoPhieuTong:MaPhieu
--   tem thung (shipping,  4 parts): LOT28:ITEM:dd/MM/yyyy:QTY
-- SoPhieuTong / MaPhieu are harness placeholders (prefix-based) so the rows stay traceable.
SELECT N'TEM_TONG_NHAP_KHO' AS Kind,
       RTRIM(a.ItemLotCodeTP) + N':' + RTRIM(a.ReceiptItemCode) + N':' + CONVERT(nvarchar(10), a.DocDate, 103) + N':'
         + CAST(CAST(a.ReceiptQuantity AS int) AS nvarchar(10)) + N':' + @Prefix + N'_T1:' + @Prefix + N'_P1' AS Qr
FROM dbo.B30AccDoc a WHERE a.ReceiptItemCode LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%'
UNION ALL
SELECT N'TEM_THUNG_XUAT_KHO',
       RTRIM(a.ItemLotCodeTP) + N':' + RTRIM(a.ReceiptItemCode) + N':' + CONVERT(nvarchar(10), a.DocDate, 103) + N':'
         + CAST(CAST(a.ReceiptQuantity AS int) AS nvarchar(10))
FROM dbo.B30AccDoc a WHERE a.ReceiptItemCode LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%';
