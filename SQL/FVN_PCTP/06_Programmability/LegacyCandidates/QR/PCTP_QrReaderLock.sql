/*
    Khoá đọc QR theo KHÁCH HÀNG + đăng ký máy được phép đọc (thay cho cờ toàn cục tbl_QR_MAY_DOCQR.TT = 1).
    DEPLOYMENT SCRIPT - chạy trên DB B7R2_FCC (cùng DB với tbl_QR_MAY_DOCQR). Idempotent: chạy lại an toàn.
    Chạy thử trên DB57/bản sao trước, không chạy thẳng production khi đang giao hàng.

    ĐỊNH DANH MÁY = IP do SQL SERVER tự đọc: CONNECTIONPROPERTY('client_net_address').
      - Client KHÔNG thể khai báo/giả mạo IP qua tham số: các SP Acquire/Heartbeat/Release không nhận @ClientIp.
      - Tên máy (@MachineName) chỉ để HIỂN THỊ / ghi chú, không dùng để phân quyền.
      - Máy phải có IP tĩnh hoặc DHCP reservation (đổi IP => phải đăng ký lại).
      - Nếu SQL Server cài cùng máy với app, giá trị là N'<local machine>' (shared memory).
      - Nếu server không xác định được IP (NULL) => KHÔNG ai giành được khoá (fail-closed).
      - Lưu ý: đây là kiểm soát vận hành, chưa phải bảo mật tuyệt đối — ai có chuỗi kết nối chung vẫn gọi SP
        được từ máy khác (sẽ bị coi theo IP máy đó). Muốn chặn hẳn: tài khoản SQL / Windows Auth riêng cho từng máy.

    Quy tắc:
      - Chỉ IP ĐÃ ĐĂNG KÝ (dbo.FVN_QrReaderMachine) mới giành được khoá. Máy chỉ xem mở form trước
        cũng không thể chiếm chỗ máy cắm súng. CustomerNo = N'*' nghĩa là IP đó được đọc MỌI khách,
        kể cả khách thêm sau này vào CustomerTableConfig (không sửa DB/code khi thêm khách 4, 5...).
      - Mỗi CustomerNo chỉ có TỐI ĐA 1 máy giữ quyền đọc QR (PRIMARY KEY CustomerNo => DB bảo đảm).
      - Máy khác mở cùng khách => không giành được khoá => chỉ XEM.
      - Khách khác nhau => khoá độc lập => nhiều máy đọc song song.
      - Tắt/xoá đăng ký của máy đang giữ khoá => máy đó mất quyền ngay ở lần heartbeat / lần quét kế tiếp.
      - Máy giữ khoá gửi heartbeat; quá @TtlSeconds không heartbeat (treo máy, rút mạng, kill app)
        => khoá coi như hết hạn, máy khác (đã đăng ký) được tiếp quản.
      - Mọi so sánh thời gian dùng SYSDATETIME() của SQL Server (không dùng giờ máy client).
*/
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.FVN_QrReaderLock', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_QrReaderLock
    (
        CustomerNo  NVARCHAR(50)  NOT NULL CONSTRAINT PK_FVN_QrReaderLock PRIMARY KEY,
        ClientIp    NVARCHAR(64)  NOT NULL,   -- định danh chủ khoá (server tự đọc)
        MachineName NVARCHAR(100) NULL,       -- chỉ để hiển thị
        UserName    NVARCHAR(100) NULL,
        LockedAt    DATETIME2(0)  NOT NULL CONSTRAINT DF_FVN_QrReaderLock_LockedAt DEFAULT (SYSDATETIME()),
        HeartbeatAt DATETIME2(0)  NOT NULL CONSTRAINT DF_FVN_QrReaderLock_Heartbeat DEFAULT (SYSDATETIME()),
        History     NVARCHAR(200) NULL
    );
END;
GO

/* Đăng ký IP được phép đọc QR. (CustomerNo, ClientIp) duy nhất; CustomerNo = N'*' => mọi khách. */
IF OBJECT_ID(N'dbo.FVN_QrReaderMachine', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_QrReaderMachine
    (
        CustomerNo  NVARCHAR(50)  NOT NULL,
        ClientIp    NVARCHAR(64)  NOT NULL,
        MachineName NVARCHAR(100) NULL,       -- chỉ để hiển thị / ghi chú
        IsActive    BIT           NOT NULL CONSTRAINT DF_FVN_QrReaderMachine_Active DEFAULT (1),
        Note        NVARCHAR(200) NULL,
        CreatedAt   DATETIME2(0)  NOT NULL CONSTRAINT DF_FVN_QrReaderMachine_Created DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_FVN_QrReaderMachine PRIMARY KEY (CustomerNo, ClientIp)
    );
END;
GO

/* Nạp ban đầu (best-effort): máy đang TT=1 ở bảng cũ, NẾU đang có kết nối tới server này thì lấy IP của nó
   từ sys.dm_exec_connections (cần quyền VIEW SERVER STATE) và đăng ký '*' => hành vi giống trước nâng cấp.
   Không resolve được thì bỏ qua: đăng ký bằng màn hình "Quản lý máy đọc QR" (nút "Đăng ký máy này")
   trên chính máy cắm súng, hoặc INSERT tay:
     INSERT dbo.FVN_QrReaderMachine (CustomerNo, ClientIp, MachineName) VALUES (N'*', N'192.168.1.50', N'PC-SUNG-01'); */
BEGIN TRY
    IF OBJECT_ID(N'dbo.tbl_QR_MAY_DOCQR', N'U') IS NOT NULL
    BEGIN
        INSERT dbo.FVN_QrReaderMachine (CustomerNo, ClientIp, MachineName, Note)
        SELECT DISTINCT N'*', CONVERT(NVARCHAR(64), c.client_net_address), t.TenMay,
               N'seed từ tbl_QR_MAY_DOCQR (TT=1)'
        FROM dbo.tbl_QR_MAY_DOCQR t
        JOIN sys.dm_exec_sessions s ON s.host_name = t.TenMay
        JOIN sys.dm_exec_connections c ON c.session_id = s.session_id
        WHERE t.TT = 1
          AND c.client_net_address IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM dbo.FVN_QrReaderMachine r
                          WHERE r.CustomerNo = N'*' AND r.ClientIp = CONVERT(NVARCHAR(64), c.client_net_address));
    END;
END TRY
BEGIN CATCH
    PRINT N'Bỏ qua seed từ tbl_QR_MAY_DOCQR: ' + ERROR_MESSAGE();
END CATCH;
GO

/* WhoAmI: IP mà SERVER thấy cho kết nối hiện tại (form dùng để "Đăng ký máy này"). */
CREATE OR ALTER PROCEDURE dbo.Usp_QrReaderMachine_WhoAmI
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CONVERT(NVARCHAR(64), CONNECTIONPROPERTY('client_net_address')) AS ClientIp;
END;
GO

/* Acquire: lấy / giữ / tiếp quản khoá. @Force = 1 chỉ dùng cho thao tác "Chiếm quyền" có xác nhận.
   Định danh = IP server đọc; @MachineName/@UserName chỉ để hiển thị. */
CREATE OR ALTER PROCEDURE dbo.Usp_QrReaderLock_Acquire
    @CustomerNo  NVARCHAR(50),
    @MachineName NVARCHAR(100) = NULL,
    @UserName    NVARCHAR(100) = NULL,
    @TtlSeconds  INT = 120,
    @Force       BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ip NVARCHAR(64) = CONVERT(NVARCHAR(64), CONNECTIONPROPERTY('client_net_address'));
    DECLARE @now DATETIME2(0) = SYSDATETIME();
    DECLARE @owner NVARCHAR(64), @hb DATETIME2(0);

    DECLARE @registered BIT = CASE WHEN @ip IS NOT NULL AND EXISTS
        (SELECT 1 FROM dbo.FVN_QrReaderMachine
         WHERE IsActive = 1 AND ClientIp = @ip AND CustomerNo IN (@CustomerNo, N'*'))
        THEN 1 ELSE 0 END;

    -- IP không xác định hoặc chưa đăng ký: không đụng vào khoá, chỉ trả về ai đang giữ để hiển thị.
    IF @registered = 0
    BEGIN
        SELECT @CustomerNo AS CustomerNo, l.ClientIp AS OwnerClientIp, l.MachineName AS OwnerMachine,
               l.UserName AS OwnerUser, l.LockedAt, l.HeartbeatAt,
               CAST(0 AS BIT) AS Acquired, CAST(0 AS BIT) AS IsMine,
               CAST(CASE WHEN l.HeartbeatAt < DATEADD(SECOND, -@TtlSeconds, SYSDATETIME()) THEN 1 ELSE 0 END AS BIT) AS IsStale,
               CAST(0 AS BIT) AS Registered
        FROM (SELECT 1 AS x) d
        LEFT JOIN dbo.FVN_QrReaderLock l ON l.CustomerNo = @CustomerNo;
        RETURN;
    END;

    BEGIN TRANSACTION;

    -- UPDLOCK + HOLDLOCK: khoá cả khoảng khoá của key chưa tồn tại => 2 máy cùng acquire
    -- một lúc sẽ bị tuần tự hoá, không thể cùng INSERT / cùng thắng.
    SELECT @owner = ClientIp, @hb = HeartbeatAt
    FROM dbo.FVN_QrReaderLock WITH (UPDLOCK, HOLDLOCK)
    WHERE CustomerNo = @CustomerNo;

    IF @owner IS NULL
    BEGIN
        INSERT dbo.FVN_QrReaderLock (CustomerNo, ClientIp, MachineName, UserName, LockedAt, HeartbeatAt, History)
        VALUES (@CustomerNo, @ip, @MachineName, @UserName, @now, @now,
                LEFT(CONCAT(N'-> ', @ip, N' : ', CONVERT(NVARCHAR(19), @now, 120)), 200));
    END
    ELSE IF @owner = @ip
    BEGIN
        UPDATE dbo.FVN_QrReaderLock
        SET HeartbeatAt = @now,
            MachineName = ISNULL(@MachineName, MachineName),
            UserName    = ISNULL(@UserName, UserName)
        WHERE CustomerNo = @CustomerNo;
    END
    ELSE IF @Force = 1 OR @hb < DATEADD(SECOND, -@TtlSeconds, @now)
    BEGIN
        UPDATE dbo.FVN_QrReaderLock
        SET ClientIp    = @ip,
            MachineName = @MachineName,
            UserName    = @UserName,
            LockedAt    = @now,
            HeartbeatAt = @now,
            History     = LEFT(CONCAT(@owner, N' --> ', @ip,
                                      CASE WHEN @Force = 1 THEN N' (chiếm quyền)' ELSE N' (hết hạn)' END,
                                      N' : ', CONVERT(NVARCHAR(19), @now, 120)), 200)
        WHERE CustomerNo = @CustomerNo;
    END;

    COMMIT TRANSACTION;

    SELECT CustomerNo, ClientIp AS OwnerClientIp, MachineName AS OwnerMachine, UserName AS OwnerUser,
           LockedAt, HeartbeatAt,
           CAST(CASE WHEN ClientIp = @ip THEN 1 ELSE 0 END AS BIT) AS Acquired,
           CAST(CASE WHEN ClientIp = @ip THEN 1 ELSE 0 END AS BIT) AS IsMine,
           CAST(CASE WHEN HeartbeatAt < DATEADD(SECOND, -@TtlSeconds, SYSDATETIME()) THEN 1 ELSE 0 END AS BIT) AS IsStale,
           CAST(1 AS BIT) AS Registered
    FROM dbo.FVN_QrReaderLock
    WHERE CustomerNo = @CustomerNo;
END;
GO

/* Heartbeat: trả 1 nếu IP này vẫn là chủ khoá VÀ vẫn còn đăng ký hiệu lực; ngược lại trả 0.
   Máy bị tắt/xoá đăng ký (hoặc khoá bị admin nhả) sẽ mất quyền ở lần heartbeat / lần quét kế tiếp. */
CREATE OR ALTER PROCEDURE dbo.Usp_QrReaderLock_Heartbeat
    @CustomerNo NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ip NVARCHAR(64) = CONVERT(NVARCHAR(64), CONNECTIONPROPERTY('client_net_address'));

    IF @ip IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.FVN_QrReaderMachine
                                  WHERE IsActive = 1 AND ClientIp = @ip AND CustomerNo IN (@CustomerNo, N'*'))
    BEGIN
        IF @ip IS NOT NULL
            DELETE dbo.FVN_QrReaderLock WHERE CustomerNo = @CustomerNo AND ClientIp = @ip;
        SELECT CAST(0 AS BIT) AS Owned;
        RETURN;
    END;

    UPDATE dbo.FVN_QrReaderLock
    SET HeartbeatAt = SYSDATETIME()
    WHERE CustomerNo = @CustomerNo AND ClientIp = @ip;

    SELECT CAST(CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS BIT) AS Owned;
END;
GO

/* Release: chỉ chủ khoá (đúng IP) mới nhả được (tránh máy xem đóng form làm mất khoá của máy đang đọc). */
CREATE OR ALTER PROCEDURE dbo.Usp_QrReaderLock_Release
    @CustomerNo NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ip NVARCHAR(64) = CONVERT(NVARCHAR(64), CONNECTIONPROPERTY('client_net_address'));
    IF @ip IS NULL RETURN;

    DELETE dbo.FVN_QrReaderLock
    WHERE CustomerNo = @CustomerNo AND ClientIp = @ip;
END;
GO

/* List: trạng thái tất cả khoá (Dashboard / Ribbon / form quản lý). IsMine tính ở server. */
CREATE OR ALTER PROCEDURE dbo.Usp_QrReaderLock_List
    @TtlSeconds INT = 120
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ip NVARCHAR(64) = CONVERT(NVARCHAR(64), CONNECTIONPROPERTY('client_net_address'));

    SELECT CustomerNo, ClientIp AS OwnerClientIp, MachineName AS OwnerMachine, UserName AS OwnerUser,
           LockedAt, HeartbeatAt,
           CAST(CASE WHEN HeartbeatAt < DATEADD(SECOND, -@TtlSeconds, SYSDATETIME()) THEN 1 ELSE 0 END AS BIT) AS IsStale,
           CAST(CASE WHEN ClientIp = @ip THEN 1 ELSE 0 END AS BIT) AS IsMine
    FROM dbo.FVN_QrReaderLock
    ORDER BY CustomerNo;
END;
GO

/* ForceRelease: admin nhả khoá của một khách (máy treo / quên đóng form). Máy đang giữ mất quyền ở lần quét kế tiếp. */
CREATE OR ALTER PROCEDURE dbo.Usp_QrReaderLock_ForceRelease
    @CustomerNo NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE dbo.FVN_QrReaderLock WHERE CustomerNo = @CustomerNo;
END;
GO

/* ---- Quản lý đăng ký máy đọc QR (màn hình "Quản lý máy đọc QR" trong Main_APP) ---- */
CREATE OR ALTER PROCEDURE dbo.Usp_QrReaderMachine_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CustomerNo, ClientIp, MachineName, IsActive, Note, CreatedAt
    FROM dbo.FVN_QrReaderMachine
    ORDER BY CASE WHEN CustomerNo = N'*' THEN 0 ELSE 1 END, CustomerNo, ClientIp;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Usp_QrReaderMachine_Upsert
    @CustomerNo  NVARCHAR(50),
    @ClientIp    NVARCHAR(64),
    @MachineName NVARCHAR(100) = NULL,
    @IsActive    BIT = 1,
    @Note        NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    UPDATE dbo.FVN_QrReaderMachine WITH (UPDLOCK, HOLDLOCK)
    SET IsActive    = @IsActive,
        MachineName = ISNULL(@MachineName, MachineName),
        Note        = ISNULL(@Note, Note)
    WHERE CustomerNo = @CustomerNo AND ClientIp = @ClientIp;

    IF @@ROWCOUNT = 0
        INSERT dbo.FVN_QrReaderMachine (CustomerNo, ClientIp, MachineName, IsActive, Note)
        VALUES (@CustomerNo, @ClientIp, @MachineName, @IsActive, @Note);

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Usp_QrReaderMachine_Remove
    @CustomerNo NVARCHAR(50),
    @ClientIp   NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE dbo.FVN_QrReaderMachine WHERE CustomerNo = @CustomerNo AND ClientIp = @ClientIp;
END;
GO
