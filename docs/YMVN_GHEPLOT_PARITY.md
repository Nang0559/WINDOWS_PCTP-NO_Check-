# YMVN (100002) ghép LOT — tương đương legacy `Usp_Qrcode_gheplotYM1`

## Vì sao cần
Màn hình YMVN hiện hành (HVN_PGH, `CoGear = true`) gọi `Usp_Qrcode_gheplot` (contract HVN/IFS) nên không ra
thông tin ghép như form legacy `GIAOHANGYMN`, vốn gọi `Usp_Qrcode_gheplotYM1`. YM1 không tham số và hard-code
`YMVN_TMPPHIEUGIAOHANG`, trong khi luồng hiện hành ghi `TMPPHIEUGIAOHANG_100002`, và chuỗi Gear nằm ở cột
`GEAR` (còn `TTPHIEU` là ORDER_NO, sau đó là `DG`).

## Thay đổi
- `SQL/Usp_Qrcode_gheplotYMVN.sql`: USP mới `@TMPTABLE`, cùng thuật toán YM1, không ghi `STOCKTP.SLCONLAITMP`.
- `PhieuService.LoadGhepLot()`: khi `Delivery.CoGear` => `LoadGhepLotYMVN(tenBan)`; loại SP trả lưới rỗng.
  HVN, HTN (100003) và các khách khác không đổi đường gọi.

## Triển khai (DB57 trước)
1. Chạy `SQL/Usp_Qrcode_gheplotYMVN.sql`.
2. Chạy bản build mới của app.

## Kiểm tra tương đương (chỉ chạy trên DB test — YM1 ghi STOCKTP.SLCONLAITMP)
Nạp cùng dữ liệu vào `YMVN_TMPPHIEUGIAOHANG` (cột `TTPHIEU` = chuỗi Gear) rồi:
```sql
CREATE TABLE #A (MH nvarchar(56), GG nvarchar(50), LG nvarchar(1000), GearName nvarchar(1));
CREATE TABLE #B (MH nvarchar(56), GG nvarchar(50), LG nvarchar(1000), GearName nvarchar(1));
INSERT #A EXEC dbo.Usp_Qrcode_gheplotYM1;
INSERT #B EXEC dbo.Usp_Qrcode_gheplotYMVN @TMPTABLE = N'YMVN_TMPPHIEUGIAOHANG';
SELECT 'chỉ có ở YM1' AS src, * FROM (SELECT * FROM #A EXCEPT SELECT * FROM #B) x
UNION ALL
SELECT 'chỉ có ở YMVN', * FROM (SELECT * FROM #B EXCEPT SELECT * FROM #A) y;   -- kỳ vọng: 0 dòng
```

## Khác biệt có chủ ý so với YM1
- `MinCloseQty = 0` coi như không có quy cách (YM1 chia cho 0).
- Gear không có trong `B20Gear` => không cấp phát (YM1 dùng nhầm Gear của dòng trước).
- Thứ tự duyệt cố định: MAHANG, NGAYGIAO, Gear, GIOGIAO (YM1 phụ thuộc kế hoạch thực thi).
- Bỏ `LEFT JOIN B20ItemQuyCach` (không dùng cột nào, chỉ có thể nhân đôi dòng tồn).

## Chưa làm
- In tem ghép lot YMVN (`Usp_gheplotPrintYM1` + `GHEPLOT_YMVN` + cột `GearName`): luồng in hiện hành qua
  `TMPLOTGHEP`/`Usp_gheplotPrint` chưa mang giờ dạng `HH:mm` và Gear.
- `Usp_Qrcode_gheplotYM2` (đếm lot cần ghép, phụ thuộc `ups_YMVN_GHEPLOT_SLIP` không có trong export DB57).
