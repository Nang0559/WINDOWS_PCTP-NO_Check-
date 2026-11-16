/*
    YMVN (100002) doc QR: loi "Invalid column name 'SOPHIEU'".
    Code (DocQRRepository.InsertFCC / KiemTraTrungTemTong) luu phan dem cua LOT FCC vao cot SOPHIEU
    de chan bat trung tem, nhung bang YMVN_DOCQRCODE tren DB57 chua co cot nay.

    Script idempotent: chay nhieu lan an toan. Chi THEM cot, khong doi/xoa du lieu.
    Chay tren DB57 (moi truong test) truoc khi bat QR YMVN.
*/
IF OBJECT_ID(N'dbo.YMVN_DOCQRCODE', N'U') IS NULL
    THROW 51301, N'Khong co bang dbo.YMVN_DOCQRCODE tren DB nay.', 1;
GO
IF COL_LENGTH(N'dbo.YMVN_DOCQRCODE', N'SOPHIEU') IS NULL
    ALTER TABLE dbo.YMVN_DOCQRCODE ADD SOPHIEU nvarchar(50) NULL;
GO
SELECT c.name, TYPE_NAME(c.system_type_id) AS type_name, c.max_length, c.is_nullable
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(N'dbo.YMVN_DOCQRCODE') AND c.name IN (N'SOPHIEU', N'Gear');
GO
