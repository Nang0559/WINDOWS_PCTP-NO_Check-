/*
 Migration 041: SEC.usp_Admin_AssignUserRole must be idempotent.
 Bug fixed: removing SYSTEM_ADMIN from a user who does NOT hold it raised
 53727 "Cannot remove the last active system administrator." whenever only one
 active administrator existed (the guard never checked the target user).
 Apply after migration 040. SQL Server 2016 compatible.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME()<>N'FVN_PCTP'
    THROW 54100,'Migration 041 may run only in FVN_PCTP.',1;

IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL
    THROW 54101,'MES migration registry is missing.',1;

IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='040')
    THROW 54102,'Migration 040 must be applied before 041.',1;

IF EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='041')
    RETURN;

IF OBJECT_ID(N'SEC.usp_Admin_AssignUserRole',N'P') IS NULL
    THROW 54103,'SEC.usp_Admin_AssignUserRole is missing. Apply migration 037 first.',1;

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC(N'
    ALTER PROCEDURE SEC.usp_Admin_AssignUserRole
        @UserId bigint,@RoleId int,@IsAssigned bit,@ActorUserId bigint
    AS
    BEGIN
        SET NOCOUNT ON; SET XACT_ABORT ON;
        BEGIN TRANSACTION;
        DECLARE @Changed int=0;

        IF @IsAssigned=1
        BEGIN
            IF NOT EXISTS(SELECT 1 FROM SEC.UserRole WHERE UserId=@UserId AND RoleId=@RoleId)
            BEGIN
                INSERT SEC.UserRole(UserId,RoleId,AssignedByUserId) VALUES(@UserId,@RoleId,@ActorUserId);
                SET @Changed=1;
            END;
        END
        ELSE IF EXISTS(SELECT 1 FROM SEC.UserRole WHERE UserId=@UserId AND RoleId=@RoleId)
        BEGIN
            -- Guard only when the target really holds SYSTEM_ADMIN and is itself an active administrator.
            IF EXISTS(SELECT 1 FROM SEC.Role WHERE RoleId=@RoleId AND RoleCode=''SYSTEM_ADMIN'')
               AND EXISTS(SELECT 1 FROM SEC.UserRole ur JOIN SEC.UserAccount u ON u.UserId=ur.UserId
                          WHERE ur.UserId=@UserId AND ur.RoleId=@RoleId AND u.IsActive=1)
               AND (SELECT COUNT(*) FROM SEC.UserRole ur JOIN SEC.UserAccount u ON u.UserId=ur.UserId
                    WHERE ur.RoleId=@RoleId AND u.IsActive=1)<=1
                THROW 53727,''Cannot remove the last active system administrator.'',1;
            DELETE SEC.UserRole WHERE UserId=@UserId AND RoleId=@RoleId;
            SET @Changed=1;
        END;

        IF @Changed=1
            INSERT SEC.AdminAudit(ActorUserId,EventCode,TargetType,TargetId,AfterJson)
            VALUES(@ActorUserId,''USER_ROLE_CHANGED'',''USER'',CONVERT(nvarchar(100),@UserId),
                N''{"roleId":''+CONVERT(nvarchar(20),@RoleId)+N'',"assigned":''+CASE WHEN @IsAssigned=1 THEN N''true'' ELSE N''false'' END+N''}'');
        COMMIT TRANSACTION;
    END');

    INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
    VALUES('041',N'Make SEC.usp_Admin_AssignUserRole idempotent; last-admin guard checks the target user');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
