/*
  PCTP_GiaoDB_RepairDeliveredLines.sql
  Sửa DỮ LIỆU CŨ: dòng Giao DB đã giao (có trong LUUPHIEUGIAOHANG) nhưng FVN_Purchase_Order_DB_CT vẫn STATUS='NG'
  (trước bản vá, Cập nhật kho không ghi 'OK' về bảng này nên lần Upload/nạp lại sau dựng lại dòng thành NG, LOT trống).

  - Chỉ chạy cho MỘT ngày (@Ngay). Mặc định @Apply = 0: chỉ XEM các cặp sẽ được ghép.
  - Ghép theo mã hàng + cửa + truyền + số lượng, mỗi dòng đã giao ghép đúng MỘT dòng chi tiết chưa giao (theo thứ tự).
  - Chỉ lấy dòng LUU có GIOGIAOFCC = '(GIAO DB)'. Nếu bản ghi của bạn đã bị chuẩn hóa GIOGIAOFCC sang giờ thì
    xem kết quả preview rồi mới nới điều kiện, tránh ghép nhầm với giao hàng thường cùng mã hàng.
  - Chạy trên DB test trước. Chỉ sửa 1 bảng (FVN_Purchase_Order_DB_CT), trong một transaction.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Ngay  date = '2026-11-11';   -- <== đổi ngày cần sửa
DECLARE @Apply bit  = 0;              -- 0 = chỉ xem, 1 = ghi

IF OBJECT_ID('tempdb..#Khop') IS NOT NULL DROP TABLE #Khop;

;WITH L AS (
    SELECT l.MAHANG, RTRIM(l.CUA) AS CUA, RTRIM(l.TRUYEN) AS TRUYEN, l.SOLUONG, l.LOT, l.NGAYGIAO, l.GIOGIAOFCC,
           rn = ROW_NUMBER() OVER (PARTITION BY l.MAHANG, RTRIM(l.CUA), RTRIM(l.TRUYEN), l.SOLUONG ORDER BY l.NGAYGIAO, l.STT)
    FROM dbo.LUUPHIEUGIAOHANG l
    WHERE CAST(l.NGAYGIAO AS date) = @Ngay
      AND l.STATUS = 'OK'
      AND ISNULL(l.LOT, '') <> ''
      AND l.GIOGIAOFCC = N'(GIAO DB)'
), D AS (
    SELECT d.ID, d.MAHANG, RTRIM(d.CUA) AS CUA, RTRIM(d.TRUYEN) AS TRUYEN, d.SOLUONG,
           rn = ROW_NUMBER() OVER (PARTITION BY d.MAHANG, RTRIM(d.CUA), RTRIM(d.TRUYEN), d.SOLUONG ORDER BY d.ID)
    FROM dbo.FVN_Purchase_Order_DB_CT d
    INNER JOIN dbo.FVN_Purchase_Order_DB_HD h ON h.IDP = d.IDP
    WHERE CAST(h.NGAYLAP AS date) = @Ngay
      AND ISNULL(d.STATUS, 'NG') = 'NG'
)
SELECT D.ID, L.MAHANG, L.CUA, L.TRUYEN, L.SOLUONG, L.LOT, L.NGAYGIAO, L.GIOGIAOFCC
INTO #Khop
FROM L
INNER JOIN D ON D.MAHANG = L.MAHANG AND D.CUA = L.CUA AND D.TRUYEN = L.TRUYEN
            AND D.SOLUONG = L.SOLUONG AND D.rn = L.rn;

SELECT * FROM #Khop ORDER BY ID;   -- XEM TRƯỚC

IF @Apply = 1
BEGIN
    BEGIN TRANSACTION;

    UPDATE d
    SET d.STATUS = 'OK',
        d.LOT = LEFT(k.LOT, 100),
        d.NGAYGIAO = k.NGAYGIAO,
        d.GGFCC = LEFT(k.GIOGIAOFCC, 25)
    FROM dbo.FVN_Purchase_Order_DB_CT d
    INNER JOIN #Khop k ON k.ID = d.ID
    WHERE ISNULL(d.STATUS, 'NG') = 'NG';

    PRINT CONCAT(N'Đã cập nhật ', @@ROWCOUNT, N' dòng.');
    COMMIT TRANSACTION;
END
ELSE PRINT N'Chế độ XEM: chưa ghi gì. Đặt @Apply = 1 để ghi.';
