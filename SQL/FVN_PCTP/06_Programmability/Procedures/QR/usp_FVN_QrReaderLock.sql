/* usp_FVN_QrReaderLock_* / usp_FVN_QrReaderMachine_*  (renamed from Usp_QrReader*; see 00K).
   Part 1 = tables + base procedures, Part 2 = MP/SP ScanMode revision (CREATE OR ALTER overrides part 1).
   Run only in FVN_PCTP. Idempotent. SQL Server 2016. C# strings must be deployed in the same release. */
IF DB_NAME() <> N'FVN_PCTP' THROW 51410, 'Run only in database FVN_PCTP.', 1;
GO
/*
    Khoá đọc QR theo KHÁCH HÀNG + đăng ký máy được phép đọc (thay cho cờ toàn cục tbl_QR_MAY_DOCQR.TT = 1).
    DEPLOYMENT SCRIPT - chạy trên DB FVN_PCTP . Idempotent: chạy lại an toàn.
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

/* Legacy seed from tbl_QR_MAY_DOCQR removed: that table does not exist in FVN_PCTP. Register machines with
   INSERT dbo.FVN_QrReaderMachine (CustomerNo, ClientIp, MachineName) VALUES (N'*', N'<ip>', N'<pc>'); */
GO

/* WhoAmI: IP mà SERVER thấy cho kết nối hiện tại (form dùng để "Đăng ký máy này"). */
CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderMachine_WhoAmI
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CONVERT(NVARCHAR(64), CONNECTIONPROPERTY('client_net_address')) AS ClientIp;
END;
GO

/* Acquire: lấy / giữ / tiếp quản khoá. @Force = 1 chỉ dùng cho thao tác "Chiếm quyền" có xác nhận.
   Định danh = IP server đọc; @MachineName/@UserName chỉ để hiển thị. */
CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderLock_Acquire
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
CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderLock_Heartbeat
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
CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderLock_Release
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
CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderLock_List
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
CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderLock_ForceRelease
    @CustomerNo NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE dbo.FVN_QrReaderLock WHERE CustomerNo = @CustomerNo;
END;
GO

/* ---- Quản lý đăng ký máy đọc QR (màn hình "Quản lý máy đọc QR" trong Main_APP) ---- */
CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderMachine_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CustomerNo, ClientIp, MachineName, IsActive, Note, CreatedAt
    FROM dbo.FVN_QrReaderMachine
    ORDER BY CASE WHEN CustomerNo = N'*' THEN 0 ELSE 1 END, CustomerNo, ClientIp;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderMachine_Upsert
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

CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderMachine_Remove
    @CustomerNo NVARCHAR(50),
    @ClientIp   NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE dbo.FVN_QrReaderMachine WHERE CustomerNo = @CustomerNo AND ClientIp = @ClientIp;
END;
GO

GO
/* ===== Part 2 ===== */
/*
  MIGRATION: độc quyền máy đọc QR theo (CustomerNo, ScanMode).
  Chỉ có hai chế độ hợp lệ: MP và SP.
  Chạy SAU this file part 1 trên database FVN_PCTP.
  Dữ liệu đăng ký/khoá cũ được gán MP để giữ tương thích; SP cần đăng ký riêng.
*/
SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'dbo.FVN_QrReaderLock', N'ScanMode') IS NULL
    ALTER TABLE dbo.FVN_QrReaderLock ADD ScanMode NVARCHAR(2) NOT NULL
        CONSTRAINT DF_FVN_QrReaderLock_ScanMode DEFAULT (N'MP') WITH VALUES;
GO
DECLARE @sql NVARCHAR(MAX), @lockPk SYSNAME;
-- Chỉ đổi khoá chính khi khoá hiện tại CHƯA có cột ScanMode (chạy lại script không đụng bảng đang dùng).
IF NOT EXISTS (SELECT 1
               FROM sys.indexes i
               JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
               JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               WHERE i.object_id = OBJECT_ID(N'dbo.FVN_QrReaderLock') AND i.is_primary_key = 1 AND c.name = N'ScanMode')
BEGIN
    SELECT @lockPk = kc.name
    FROM sys.key_constraints kc
    WHERE kc.parent_object_id = OBJECT_ID(N'dbo.FVN_QrReaderLock') AND kc.type = 'PK';
    IF @lockPk IS NOT NULL
    BEGIN
        -- EXEC(...) không cho gọi hàm (QUOTENAME) trong ngoặc => dựng câu lệnh vào biến trước.
        SET @sql = N'ALTER TABLE dbo.FVN_QrReaderLock DROP CONSTRAINT ' + QUOTENAME(@lockPk) + N';';
        EXEC (@sql);
    END;
    ALTER TABLE dbo.FVN_QrReaderLock
        ADD CONSTRAINT PK_FVN_QrReaderLock PRIMARY KEY (CustomerNo, ScanMode);
END;
GO

IF COL_LENGTH(N'dbo.FVN_QrReaderMachine', N'ScanMode') IS NULL
    ALTER TABLE dbo.FVN_QrReaderMachine ADD ScanMode NVARCHAR(2) NOT NULL
        CONSTRAINT DF_FVN_QrReaderMachine_ScanMode DEFAULT (N'MP') WITH VALUES;
GO
DECLARE @sql NVARCHAR(MAX), @machinePk SYSNAME;
-- Chỉ đổi khoá chính khi khoá hiện tại CHƯA có cột ScanMode (chạy lại script không đụng bảng đang dùng).
IF NOT EXISTS (SELECT 1
               FROM sys.indexes i
               JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
               JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               WHERE i.object_id = OBJECT_ID(N'dbo.FVN_QrReaderMachine') AND i.is_primary_key = 1 AND c.name = N'ScanMode')
BEGIN
    SELECT @machinePk = kc.name
    FROM sys.key_constraints kc
    WHERE kc.parent_object_id = OBJECT_ID(N'dbo.FVN_QrReaderMachine') AND kc.type = 'PK';
    IF @machinePk IS NOT NULL
    BEGIN
        -- EXEC(...) không cho gọi hàm (QUOTENAME) trong ngoặc => dựng câu lệnh vào biến trước.
        SET @sql = N'ALTER TABLE dbo.FVN_QrReaderMachine DROP CONSTRAINT ' + QUOTENAME(@machinePk) + N';';
        EXEC (@sql);
    END;
    ALTER TABLE dbo.FVN_QrReaderMachine
        ADD CONSTRAINT PK_FVN_QrReaderMachine PRIMARY KEY (CustomerNo, ClientIp, ScanMode);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderLock_Acquire
    @CustomerNo NVARCHAR(50),
    @ScanMode NVARCHAR(2) = N'MP',
    @MachineName NVARCHAR(100) = NULL,
    @UserName NVARCHAR(100) = NULL,
    @TtlSeconds INT = 120,
    @Force BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @mode NVARCHAR(2) = UPPER(LTRIM(RTRIM(ISNULL(@ScanMode, N'MP'))));
    IF @mode NOT IN (N'MP', N'SP') THROW 51001, N'ScanMode chỉ nhận MP hoặc SP.', 1;

    DECLARE @ip NVARCHAR(64) = CONVERT(NVARCHAR(64), CONNECTIONPROPERTY('client_net_address'));
    DECLARE @now DATETIME2(0) = SYSDATETIME();
    DECLARE @owner NVARCHAR(64), @hb DATETIME2(0);
    DECLARE @registered BIT = CASE WHEN @ip IS NOT NULL AND EXISTS
        (SELECT 1 FROM dbo.FVN_QrReaderMachine
         WHERE IsActive = 1 AND ClientIp = @ip AND ScanMode = @mode
           AND CustomerNo IN (@CustomerNo, N'*')) THEN 1 ELSE 0 END;

    IF @registered = 0
    BEGIN
        SELECT @CustomerNo AS CustomerNo, @mode AS ScanMode,
               l.ClientIp AS OwnerClientIp, l.MachineName AS OwnerMachine,
               l.UserName AS OwnerUser, l.LockedAt, l.HeartbeatAt,
               CAST(0 AS BIT) AS Acquired, CAST(0 AS BIT) AS IsMine,
               CAST(CASE WHEN l.HeartbeatAt < DATEADD(SECOND, -@TtlSeconds, SYSDATETIME()) THEN 1 ELSE 0 END AS BIT) AS IsStale,
               CAST(0 AS BIT) AS Registered
        FROM (SELECT 1 AS x) d
        LEFT JOIN dbo.FVN_QrReaderLock l
          ON l.CustomerNo = @CustomerNo AND l.ScanMode = @mode;
        RETURN;
    END;

    BEGIN TRANSACTION;
    SELECT @owner = ClientIp, @hb = HeartbeatAt
    FROM dbo.FVN_QrReaderLock WITH (UPDLOCK, HOLDLOCK)
    WHERE CustomerNo = @CustomerNo AND ScanMode = @mode;

    IF @owner IS NULL
        INSERT dbo.FVN_QrReaderLock (CustomerNo, ScanMode, ClientIp, MachineName, UserName, LockedAt, HeartbeatAt, History)
        VALUES (@CustomerNo, @mode, @ip, @MachineName, @UserName, @now, @now,
                LEFT(CONCAT(N'-> ', @ip, N' : ', CONVERT(NVARCHAR(19), @now, 120)), 200));
    ELSE IF @owner = @ip
        UPDATE dbo.FVN_QrReaderLock
        SET HeartbeatAt = @now, MachineName = ISNULL(@MachineName, MachineName),
            UserName = ISNULL(@UserName, UserName)
        WHERE CustomerNo = @CustomerNo AND ScanMode = @mode;
    ELSE IF @Force = 1 OR @hb < DATEADD(SECOND, -@TtlSeconds, @now)
        UPDATE dbo.FVN_QrReaderLock
        SET ClientIp = @ip, MachineName = @MachineName, UserName = @UserName,
            LockedAt = @now, HeartbeatAt = @now,
            History = LEFT(CONCAT(@owner, N' --> ', @ip,
                CASE WHEN @Force = 1 THEN N' (chiếm quyền)' ELSE N' (hết hạn)' END,
                N' : ', CONVERT(NVARCHAR(19), @now, 120)), 200)
        WHERE CustomerNo = @CustomerNo AND ScanMode = @mode;
    COMMIT TRANSACTION;

    SELECT CustomerNo, ScanMode, ClientIp AS OwnerClientIp, MachineName AS OwnerMachine,
           UserName AS OwnerUser, LockedAt, HeartbeatAt,
           CAST(CASE WHEN ClientIp = @ip THEN 1 ELSE 0 END AS BIT) AS Acquired,
           CAST(CASE WHEN ClientIp = @ip THEN 1 ELSE 0 END AS BIT) AS IsMine,
           CAST(CASE WHEN HeartbeatAt < DATEADD(SECOND, -@TtlSeconds, SYSDATETIME()) THEN 1 ELSE 0 END AS BIT) AS IsStale,
           CAST(1 AS BIT) AS Registered
    FROM dbo.FVN_QrReaderLock
    WHERE CustomerNo = @CustomerNo AND ScanMode = @mode;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderLock_Heartbeat
    @CustomerNo NVARCHAR(50), @ScanMode NVARCHAR(2) = N'MP'
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @mode NVARCHAR(2) = UPPER(LTRIM(RTRIM(ISNULL(@ScanMode, N'MP'))));
    IF @mode NOT IN (N'MP', N'SP') THROW 51002, N'ScanMode chỉ nhận MP hoặc SP.', 1;
    DECLARE @ip NVARCHAR(64) = CONVERT(NVARCHAR(64), CONNECTIONPROPERTY('client_net_address'));

    IF @ip IS NULL OR NOT EXISTS
       (SELECT 1 FROM dbo.FVN_QrReaderMachine
        WHERE IsActive = 1 AND ClientIp = @ip AND ScanMode = @mode
          AND CustomerNo IN (@CustomerNo, N'*'))
    BEGIN
        IF @ip IS NOT NULL
            DELETE dbo.FVN_QrReaderLock
            WHERE CustomerNo = @CustomerNo AND ScanMode = @mode AND ClientIp = @ip;
        SELECT CAST(0 AS BIT) AS Owned;
        RETURN;
    END;

    UPDATE dbo.FVN_QrReaderLock SET HeartbeatAt = SYSDATETIME()
    WHERE CustomerNo = @CustomerNo AND ScanMode = @mode AND ClientIp = @ip;
    SELECT CAST(CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS BIT) AS Owned;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderLock_Release
    @CustomerNo NVARCHAR(50), @ScanMode NVARCHAR(2) = N'MP'
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @mode NVARCHAR(2) = UPPER(LTRIM(RTRIM(ISNULL(@ScanMode, N'MP'))));
    IF @mode NOT IN (N'MP', N'SP') THROW 51003, N'ScanMode chỉ nhận MP hoặc SP.', 1;
    DECLARE @ip NVARCHAR(64) = CONVERT(NVARCHAR(64), CONNECTIONPROPERTY('client_net_address'));
    IF @ip IS NULL RETURN;
    DELETE dbo.FVN_QrReaderLock
    WHERE CustomerNo = @CustomerNo AND ScanMode = @mode AND ClientIp = @ip;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderLock_List
    @TtlSeconds INT = 120
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ip NVARCHAR(64) = CONVERT(NVARCHAR(64), CONNECTIONPROPERTY('client_net_address'));
    SELECT CustomerNo, ScanMode, ClientIp AS OwnerClientIp, MachineName AS OwnerMachine,
           UserName AS OwnerUser, LockedAt, HeartbeatAt,
           CAST(CASE WHEN HeartbeatAt < DATEADD(SECOND, -@TtlSeconds, SYSDATETIME()) THEN 1 ELSE 0 END AS BIT) AS IsStale,
           CAST(CASE WHEN ClientIp = @ip THEN 1 ELSE 0 END AS BIT) AS IsMine
    FROM dbo.FVN_QrReaderLock
    ORDER BY CustomerNo, ScanMode;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderLock_ForceRelease
    @CustomerNo NVARCHAR(50), @ScanMode NVARCHAR(2) = N'MP'
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @mode NVARCHAR(2) = UPPER(LTRIM(RTRIM(ISNULL(@ScanMode, N'MP'))));
    IF @mode NOT IN (N'MP', N'SP') THROW 51004, N'ScanMode chỉ nhận MP hoặc SP.', 1;
    DELETE dbo.FVN_QrReaderLock WHERE CustomerNo = @CustomerNo AND ScanMode = @mode;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderMachine_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CustomerNo, ScanMode, ClientIp, MachineName, IsActive, Note, CreatedAt
    FROM dbo.FVN_QrReaderMachine
    ORDER BY CASE WHEN CustomerNo = N'*' THEN 0 ELSE 1 END, CustomerNo, ScanMode, ClientIp;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderMachine_Upsert
    @CustomerNo NVARCHAR(50), @ClientIp NVARCHAR(64), @ScanMode NVARCHAR(2) = N'MP',
    @MachineName NVARCHAR(100) = NULL,
    @IsActive BIT = 1, @Note NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @mode NVARCHAR(2) = UPPER(LTRIM(RTRIM(ISNULL(@ScanMode, N'MP'))));
    IF @mode NOT IN (N'MP', N'SP') THROW 51005, N'ScanMode chỉ nhận MP hoặc SP.', 1;
    BEGIN TRANSACTION;
    UPDATE dbo.FVN_QrReaderMachine WITH (UPDLOCK, HOLDLOCK)
    SET IsActive = @IsActive, MachineName = ISNULL(@MachineName, MachineName),
        Note = ISNULL(@Note, Note)
    WHERE CustomerNo = @CustomerNo AND ClientIp = @ClientIp AND ScanMode = @mode;
    IF @@ROWCOUNT = 0
        INSERT dbo.FVN_QrReaderMachine (CustomerNo, ScanMode, ClientIp, MachineName, IsActive, Note)
        VALUES (@CustomerNo, @mode, @ClientIp, @MachineName, @IsActive, @Note);
    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderMachine_Remove
    @CustomerNo NVARCHAR(50), @ClientIp NVARCHAR(64), @ScanMode NVARCHAR(2) = N'MP'
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @mode NVARCHAR(2) = UPPER(LTRIM(RTRIM(ISNULL(@ScanMode, N'MP'))));
    IF @mode NOT IN (N'MP', N'SP') THROW 51006, N'ScanMode chỉ nhận MP hoặc SP.', 1;
    DELETE dbo.FVN_QrReaderMachine
    WHERE CustomerNo = @CustomerNo AND ScanMode = @mode AND ClientIp = @ClientIp;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FVN_QrReaderMachine_CanReadAnyMode
    @CustomerNo NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ip NVARCHAR(64) = CONVERT(NVARCHAR(64), CONNECTIONPROPERTY('client_net_address'));
    SELECT CAST(CASE WHEN @ip IS NOT NULL AND EXISTS
        (SELECT 1 FROM dbo.FVN_QrReaderMachine
         WHERE IsActive = 1 AND ClientIp = @ip
           AND CustomerNo IN (@CustomerNo, N'*')
           AND ScanMode IN (N'MP', N'SP'))
        THEN 1 ELSE 0 END AS BIT) AS Registered;
END;
GO
