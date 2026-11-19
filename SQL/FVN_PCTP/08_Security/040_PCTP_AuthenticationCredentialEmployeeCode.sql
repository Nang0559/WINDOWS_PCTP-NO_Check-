/*
 Migration 040: return employee identity with authentication credentials.
 Apply after migration 039. SQL Server 2016 compatible.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME()<>N'FVN_PCTP'
    THROW 54000,'Migration 040 may run only in FVN_PCTP.',1;

IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL
    THROW 54001,'MES migration registry is missing.',1;

IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='039')
    THROW 54002,'Migration 039 must be applied before 040.',1;

IF EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='040')
    RETURN;

IF OBJECT_ID(N'SEC.usp_Account_GetCredential',N'P') IS NULL
    THROW 54003,'SEC.usp_Account_GetCredential is missing. Apply migration 037 first.',1;

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC(N'
    ALTER PROCEDURE SEC.usp_Account_GetCredential
        @UserName nvarchar(80)
    AS
    BEGIN
        SET NOCOUNT ON;

        SELECT
            UserId,
            UserName,
            DisplayName,
            EmployeeCode,
            PasswordSalt,
            PasswordHash,
            PasswordIterations,
            IsActive,
            MustChangePassword,
            FailedLoginCount,
            LockedUntilUtc
        FROM SEC.UserAccount
        WHERE UserNameNormalized = UPPER(LTRIM(RTRIM(@UserName)));
    END');

    INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
    VALUES('040',N'Include EmployeeCode in authentication credential profile');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
