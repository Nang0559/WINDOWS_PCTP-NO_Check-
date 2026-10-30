-- READ-ONLY. Tìm LOT trong STOCKTP bị lưu dạng số khoa học (vd 2.61021E+19) do import từ Excel.
-- STOCKTP.LOT là nvarchar(50): chuỗi "2.61021E+19" đã nằm sẵn trong DB, báo cáo chỉ hiển thị lại đúng giá trị đó.
-- Chạy trên DB đang cần kiểm tra (DB57 trước, DB14 chỉ đọc). Không UPDATE/DELETE gì ở file này.
SELECT  LOT, PART, NAME, MODEL, NGAYSX, CASX, SLSX, SLNHAP, SLXUAT, SLCONLAI, SATUS
FROM    dbo.STOCKTP
WHERE   LOT LIKE '%[0-9].[0-9]%E+[0-9]%' OR LOT LIKE '[0-9]E+[0-9]%'
ORDER BY LOT;

-- Gợi ý đối chiếu LOT thật (chỉ để người có thẩm quyền xem xét; KHÔNG tự động sửa):
-- LOT thật 20 ký tự thường bắt đầu bằng 4 chữ số đầu của LOT lỗi; so thêm PART + NGAYSX + CASX + SLSX.
SELECT  s.LOT AS LotLoi, s.PART, s.NGAYSX, s.CASX, s.SLSX, sl.LotNo AS LotCoTheKhop
FROM    dbo.STOCKTP AS s
LEFT JOIN dbo.SlotLot AS sl
       ON sl.ItemCode = s.PART
      AND LEN(sl.LotNo) > 13
      AND sl.LotNo LIKE LEFT(REPLACE(s.LOT, '.', ''), 5) + '%'
WHERE   s.LOT LIKE '%[0-9].[0-9]%E+[0-9]%' OR s.LOT LIKE '[0-9]E+[0-9]%';
