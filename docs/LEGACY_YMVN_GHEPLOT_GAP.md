# Legacy YMVN ghép LOT — schema drift được khoanh vùng

## Kết quả đối chiếu SQL2 DB57

SQL2 column inventory ghi nhận `dbo.TMPLOTGHEPTEST` chỉ có:

- `LOT`
- `MAHANG`
- `GIOXUAT`
- `Flag`

Trong khi `PCTP/Modules/GiaoHangKhach/YMVN/GIAOHANGYMN.cs`,
`CMD_INGHEPLOT_Click` ghi thêm `GearName` và gọi
`Usp_gheplotPrintYM1`.

SQL2 module catalog không có `Usp_gheplotPrintYM1`. Nó có
`Usp_gheplotPrint` và `Usp_Qrcode_gheplotYM1`, nhưng hai procedure này
không phải cùng contract:
- `Usp_gheplotPrint` đọc `TMPLOTGHEP` theo `MachineName` và trả dataset
  tương thích các field của report `GHEPLOT_YMVN`.
- `Usp_Qrcode_gheplotYM1` tự đọc `YMVN_TMPPHIEUGIAOHANG`, tính LOT từ
  `STOCKTP`, và trả bảng `#TMPGHEPLOT` với shape khác.

Không được thêm `GearName` vào DB57 chỉ để làm cho câu INSERT cũ chạy,
và cũng không được thay tên procedure một cách máy móc vì hai contract khác nhau.

## Phân loại và hành động

Đây là legacy form path cần retirement hoặc migration có kiểm soát; không được xem
là schema DB57 thiếu cột. Rà soát entrypoint hiện tại không tìm thấy đường mở form
`GIAOHANGYMN` trong luồng YMVN đang dùng. Gap được ghi trong
`SQL/QC_Schema/tools/known_column_gaps.txt` để scanner không nhầm nó với lỗi
schema của runtime flow hiện hành.

Nếu form được tái kích hoạt, phải migrate sang contract đang có trên DB57
(`TMPLOTGHEP` + `MachineName` + `Usp_gheplotPrint`) hoặc thiết kế lại theo
`Usp_Qrcode_gheplotYM1`, sau đó bổ sung SQL integration test cho đúng report
dataset trước khi gỡ exception này.
