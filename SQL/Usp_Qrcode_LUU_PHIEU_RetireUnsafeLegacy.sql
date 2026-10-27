/*
  Usp_Qrcode_LUU_PHIEU_RetireUnsafeLegacy.sql

  Muc dich:
    - Ngung an toan proc legacy co UPDATE LUUPHIEUGIAOHANG thieu WHERE va DELETE DOCQRCODE
      theo KETQUA tren toan bang.
    - Khong tu dong chay tren production.
    - Mac dinh chi preflight; chi thay proc thanh THROW khi @ConfirmRetire = 1.

  QUAN TRONG:
    1. Chay preflight tren DB TEST truoc.
    2. Tim caller trong source code / config / job va xac nhan khong con duong goi hop le.
    3. Backup va duyet thay doi truoc khi set @ConfirmRetire = 1.
    4. Khong chay tren production. Script tu choi server co ten chua BRAVO.
*/
SET NOCOUNT ON;

IF DB_NAME() <> N'B7R2_FCC'
    THROW 51015, N'Sai database. Script nay chi danh cho B7R2_FCC tren DB test.', 1;

-- Allowlist theo dia chi IP thuc cua SQL Server, khong chi dua vao ten alias/instance.
-- QC-CNC\\BRV chua du de ket luan la DB57; phai xac nhan local_net_address = 192.168.200.57.
DECLARE @SqlServerIp nvarchar(48) = CONVERT(nvarchar(48), CONNECTIONPROPERTY('local_net_address'));

IF @SqlServerIp IS NULL OR @SqlServerIp <> N'192.168.200.57'
    THROW 51015, N'Chan deploy: chi cho phep SQL Server co local_net_address = 192.168.200.57 (DB test DB57).', 1;

IF UPPER(COALESCE(CONVERT(nvarchar(256), SERVERPROPERTY('ServerName')), @@SERVERNAME)) LIKE N'%BRAVO%'
    THROW 51015, N'Chan deploy: server BRAVO duoc xem la production. Khong duoc retire proc tai day.', 1;

DECLARE @ConfirmRetire bit = 0; -- Chi doi thanh 1 sau khi da backup va xac nhan caller.

DECLARE @ProcedureId int = OBJECT_ID(N'dbo.Usp_Qrcode_LUU_PHIEU', N'P');

IF @ProcedureId IS NULL
BEGIN
    PRINT N'Khong tim thay dbo.Usp_Qrcode_LUU_PHIEU; khong co thay doi.';
    RETURN;
END;

SELECT
    @@SERVERNAME AS ServerName,
    DB_NAME() AS DatabaseName,
    s.name AS SchemaName,
    o.name AS ProcedureName,
    o.modify_date AS LastModified,
    N'PRECHECK ONLY - chua thay doi procedure' AS ScriptMode
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
WHERE o.object_id = @ProcedureId;

-- Tham chieu bang van ban trong module SQL, bo qua chinh procedure.
SELECT
    s.name AS SchemaName,
    o.name AS CallerObject,
    o.type_desc AS CallerType,
    o.modify_date
FROM sys.sql_modules AS m
JOIN sys.objects AS o ON o.object_id = m.object_id
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
WHERE o.object_id <> @ProcedureId
  AND m.definition LIKE N'%Usp_Qrcode_LUU_PHIEU%'
ORDER BY o.type_desc, s.name, o.name;

-- SQL Agent caller. Neu khong co quyen doc msdb, can kiem tra bang tai khoan DBA.
BEGIN TRY
    SELECT
        j.name AS JobName,
        j.enabled AS JobEnabled,
        st.step_id AS StepId,
        st.step_name AS StepName,
        st.database_name AS StepDatabase,
        st.command AS StepCommand
    FROM msdb.dbo.sysjobsteps AS st
    JOIN msdb.dbo.sysjobs AS j ON j.job_id = st.job_id
    WHERE st.command LIKE N'%Usp_Qrcode_LUU_PHIEU%'
    ORDER BY j.name, st.step_id;
END TRY
BEGIN CATCH
    PRINT N'Khong doc duoc SQL Agent metadata; DBA phai xac minh job truoc khi retire.';
    THROW;
END CATCH;

IF @ConfirmRetire = 0
BEGIN
    PRINT N'PRECHECK ONLY. Khong co DDL nao duoc thuc thi.';
    PRINT N'Neu da backup va xac nhan khong con caller, doi @ConfirmRetire = 1 va chay lai tren DB TEST.';
    RETURN;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.sql_modules AS m
    JOIN sys.objects AS o ON o.object_id = m.object_id
    WHERE o.object_id <> @ProcedureId
      AND m.definition LIKE N'%Usp_Qrcode_LUU_PHIEU%'
)
    THROW 51015, N'Van con module SQL tham chieu Usp_Qrcode_LUU_PHIEU. Khong retire.', 1;

IF EXISTS
(
    SELECT 1
    FROM msdb.dbo.sysjobsteps AS st
    WHERE st.command LIKE N'%Usp_Qrcode_LUU_PHIEU%'
)
    THROW 51015, N'Van con SQL Agent job step tham chieu Usp_Qrcode_LUU_PHIEU. Khong retire.', 1;

-- Keep the legacy signature so any overlooked caller receives a clear error instead of data damage.
EXEC sys.sp_executesql N'
ALTER PROCEDURE dbo.Usp_Qrcode_LUU_PHIEU
    @GIOGIAOFCC nvarchar(50) = N''''
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51015, N''dbo.Usp_Qrcode_LUU_PHIEU da bi vo hieu hoa vi logic legacy co nguy co ghi de toan bo LUUPHIEUGIAOHANG va xoa QR khong gioi han. Hay chuyen sang luong luu phieu duoc kiem soat.'', 1;
END;
';

PRINT N'Da retire proc legacy tren DB test. Hay kiem tra ung dung nhan loi ro rang va chuyen caller sang luong luu chuan.';
