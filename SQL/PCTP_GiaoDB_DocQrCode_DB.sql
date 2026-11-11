/*
  PCTP_GiaoDB_DocQrCode_DB.sql
  Tạo bảng ghi QR riêng cho phiên GIAO DB (giao đặc biệt) của HVN (100001).

  Quy tắc: Giao DB lấy đơn hàng từ TMPPHIEUGIAOHANGDB nên phiên đọc QR cũng ghi vào
  bảng riêng DOCQRCODE_DB, không dùng chung DOCQRCODE của giao thường.
  Cấu trúc cột sao chép y hệt DOCQRCODE (có hay không bắn tem khách đều dùng chung cột
  LOTFCC/LOTHVN...: Giao DB chỉ bắn tem FCC thì cột HVN để trống).

  Script idempotent: chạy lại nhiều lần an toàn. Không đụng dữ liệu DOCQRCODE.
  Đồng thời cần deploy lại Usp_Qrcode_Update_Stock2405.sql và Usp_Qrcode_Take_Lot2405.sql
  (đã thêm TMPPHIEUGIAOHANGDB / DOCQRCODE_DB vào whitelist tên bảng).
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.DOCQRCODE_DB', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DOCQRCODE_DB](
        [LOTFCC]     [nvarchar](500) NULL,
        [MAHANGFCC]  [nvarchar](60)  NULL,
        [SLTEMFCC]   [smallint]      NULL,
        [LOTHVN]     [nvarchar](500) NULL,
        [MAHANGHVN]  [nvarchar](60)  NULL,
        [SLTEMHVN]   [smallint]      NULL,
        [STATUS]     [smallint]      NULL,
        [MAFCC]      [nvarchar](50)  NULL,
        [STT]        [smallint]      NULL,
        [KETQUA]     [nvarchar](30)  NULL,
        [CUA]        [nvarchar](10)  NULL,
        [TRUYEN]     [nvarchar](50)  NULL,
        [GIO]        [nvarchar](50)  NULL,
        [STTBAN]     [int]           NULL,
        [SUALOTHVN]  [nchar](30)     NULL,
        [TGLUU]      [timestamp]     NULL,
        [FindTem]    [nvarchar](50)  NULL
    ) ON [PRIMARY];
END
ELSE
    PRINT N'dbo.DOCQRCODE_DB đã tồn tại - bỏ qua.';

-- Kiểm tra cột đủ như DOCQRCODE (cảnh báo nếu DOCQRCODE ở DB này có thêm cột).
IF OBJECT_ID(N'dbo.DOCQRCODE', N'U') IS NOT NULL
BEGIN
    DECLARE @missing nvarchar(max) = N'';
    SELECT @missing = @missing + c.name + N' '
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.DOCQRCODE')
      AND NOT EXISTS (SELECT 1 FROM sys.columns d
                      WHERE d.object_id = OBJECT_ID(N'dbo.DOCQRCODE_DB') AND d.name = c.name);
    IF LEN(@missing) > 0
        THROW 53001, N'DOCQRCODE_DB thiếu cột so với DOCQRCODE - thêm cột còn thiếu trước khi dùng.', 1;
END
GO
