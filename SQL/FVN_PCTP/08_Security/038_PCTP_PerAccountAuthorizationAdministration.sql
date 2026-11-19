/*
 Migration 038: per-account function overrides and data-scope administration.
 SQL Server 2016 compatible. Apply after migration 037.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'FVN_PCTP' THROW 53800,'Migration 038 may run only in FVN_PCTP.',1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL THROW 53801,'MES migration registry is missing.',1;
IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='037')
 THROW 53802,'Migration 037 must be applied before 038.',1;
IF EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='038') RETURN;

BEGIN TRY
 BEGIN TRANSACTION;

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_ListUserFunctionOverrides @UserId bigint
 AS
 BEGIN
  SET NOCOUNT ON;
  ;WITH RoleGrant AS(
   SELECT p.FunctionCode,p.ActionCode,MIN(CONVERT(int,p.IsAllowed)) IsAllowed
   FROM SEC.UserRole ur JOIN SEC.Role r ON r.RoleId=ur.RoleId AND r.IsActive=1
   JOIN SEC.RoleFunctionPermission p ON p.RoleId=r.RoleId
   WHERE ur.UserId=@UserId GROUP BY p.FunctionCode,p.ActionCode
  )
  SELECT f.FunctionCode,f.ModuleCode,f.FunctionName,a.ActionCode,a.ActionName,
   CAST(o.Effect AS varchar(5)) Effect,
   CASE WHEN o.Effect=''ALLOW'' THEN CAST(1 AS bit)
        WHEN o.Effect=''DENY'' THEN CAST(0 AS bit) ELSE NULL END IsAllowed,
   CAST(ISNULL(r.IsAllowed,0) AS bit) RoleIsAllowed,
   CASE WHEN o.Effect=''ALLOW'' THEN CAST(1 AS bit)
        WHEN o.Effect=''DENY'' THEN CAST(0 AS bit)
        ELSE CAST(ISNULL(r.IsAllowed,0) AS bit) END EffectiveIsAllowed
  FROM SEC.FunctionCatalog f CROSS JOIN SEC.ActionCatalog a
  LEFT JOIN RoleGrant r ON r.FunctionCode=f.FunctionCode AND r.ActionCode=a.ActionCode
  LEFT JOIN SEC.UserFunctionOverride o
   ON o.UserId=@UserId AND o.FunctionCode=f.FunctionCode AND o.ActionCode=a.ActionCode
  WHERE f.IsActive=1
  ORDER BY f.SortOrder,f.ModuleCode,f.FunctionName,a.SortOrder;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_SaveUserFunctionOverride
  @UserId bigint,@FunctionCode varchar(100),@ActionCode varchar(20),
  @Effect varchar(5)=NULL,@ActorUserId bigint
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF @Effect IS NOT NULL AND @Effect NOT IN(''ALLOW'',''DENY'')
   THROW 53810,''Override must be ALLOW, DENY or NULL (inherit role).'',1;
  IF NOT EXISTS(SELECT 1 FROM SEC.UserAccount WHERE UserId=@UserId)
   THROW 53811,''Target account does not exist.'',1;
  IF NOT EXISTS(SELECT 1 FROM SEC.FunctionCatalog WHERE FunctionCode=@FunctionCode AND IsActive=1)
   THROW 53812,''Function code is invalid or inactive.'',1;
  IF NOT EXISTS(SELECT 1 FROM SEC.ActionCatalog WHERE ActionCode=@ActionCode)
   THROW 53813,''Action code is invalid.'',1;
  IF EXISTS(SELECT 1 FROM SEC.UserRole ur JOIN SEC.Role r ON r.RoleId=ur.RoleId
    WHERE ur.UserId=@UserId AND r.RoleCode=''SYSTEM_ADMIN'') AND @Effect=''DENY''
   THROW 53814,''Cannot deny permissions of a SYSTEM_ADMIN account.'',1;
  BEGIN TRANSACTION;
  DECLARE @Before nvarchar(max);
  SELECT @Before=(SELECT Effect FROM SEC.UserFunctionOverride
   WHERE UserId=@UserId AND FunctionCode=@FunctionCode AND ActionCode=@ActionCode FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  IF @Effect IS NULL
   DELETE SEC.UserFunctionOverride WHERE UserId=@UserId AND FunctionCode=@FunctionCode AND ActionCode=@ActionCode;
  ELSE IF EXISTS(SELECT 1 FROM SEC.UserFunctionOverride
    WHERE UserId=@UserId AND FunctionCode=@FunctionCode AND ActionCode=@ActionCode)
   UPDATE SEC.UserFunctionOverride SET Effect=@Effect,UpdatedAtUtc=SYSUTCDATETIME(),UpdatedByUserId=@ActorUserId
    WHERE UserId=@UserId AND FunctionCode=@FunctionCode AND ActionCode=@ActionCode;
  ELSE
   INSERT SEC.UserFunctionOverride(UserId,FunctionCode,ActionCode,Effect,UpdatedByUserId)
    VALUES(@UserId,@FunctionCode,@ActionCode,@Effect,@ActorUserId);
  INSERT SEC.AdminAudit(ActorUserId,EventCode,TargetType,TargetId,BeforeJson,AfterJson)
   VALUES(@ActorUserId,''USER_PERMISSION_OVERRIDE'',''USER'',CONVERT(nvarchar(100),@UserId),
    @Before,(SELECT @FunctionCode FunctionCode,@ActionCode ActionCode,@Effect Effect FOR JSON PATH,WITHOUT_ARRAY_WRAPPER));
  COMMIT TRANSACTION;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_ListUserDataScopes @UserId bigint
 AS
 BEGIN
  SET NOCOUNT ON;
  SELECT UserDataScopeId,FunctionCode,ScopeType,ScopeValue,SiteCode,IsAllowed
  FROM SEC.UserDataScope WHERE UserId=@UserId
  ORDER BY FunctionCode,ScopeType,ScopeValue,SiteCode;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_SaveUserDataScope
  @UserId bigint,@FunctionCode varchar(100)=NULL,@ScopeType varchar(20),
  @ScopeValue nvarchar(100),@SiteCode nvarchar(30)=NULL,@IsAllowed bit,@ActorUserId bigint
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF NOT EXISTS(SELECT 1 FROM SEC.UserAccount WHERE UserId=@UserId)
   THROW 53820,''Target account does not exist.'',1;
  IF @ScopeType NOT IN(''ALL'',''SITE'',''DEPARTMENT'',''WAREHOUSE'',''LINE'',''PROCESS'',''OWN'')
   THROW 53821,''Invalid data-scope type.'',1;
  IF @ScopeType=''ALL'' AND @ScopeValue<>N''*''
   THROW 53822,''ALL scope must use *.'',1;
  IF @ScopeType<>''ALL'' AND NULLIF(LTRIM(RTRIM(@ScopeValue)),N'''') IS NULL
   THROW 53823,''Scope value is required.'',1;
  IF @FunctionCode IS NOT NULL AND NOT EXISTS(SELECT 1 FROM SEC.FunctionCatalog
    WHERE FunctionCode=@FunctionCode AND IsActive=1)
   THROW 53824,''Function code is invalid or inactive.'',1;
  IF EXISTS(SELECT 1 FROM SEC.UserRole ur JOIN SEC.Role r ON r.RoleId=ur.RoleId
    WHERE ur.UserId=@UserId AND r.RoleCode=''SYSTEM_ADMIN'')
   THROW 53825,''Direct data scopes of a SYSTEM_ADMIN account cannot be changed.'',1;
  IF @ScopeType=''SITE'' AND NOT EXISTS(SELECT 1 FROM dbo.FVN_PCTPSite
    WHERE SiteCode=@ScopeValue AND IsActive=1)
   THROW 53826,''Site scope must reference an active PCTP site.'',1;
  BEGIN TRANSACTION;
  INSERT SEC.UserDataScope(UserId,FunctionCode,ScopeType,ScopeValue,SiteCode,IsAllowed)
   VALUES(@UserId,@FunctionCode,@ScopeType,@ScopeValue,@SiteCode,@IsAllowed);
  INSERT SEC.AdminAudit(ActorUserId,EventCode,TargetType,TargetId,AfterJson)
   VALUES(@ActorUserId,''USER_DATA_SCOPE_ADDED'',''USER'',CONVERT(nvarchar(100),@UserId),
    (SELECT @FunctionCode FunctionCode,@ScopeType ScopeType,@ScopeValue ScopeValue,
      @SiteCode SiteCode,@IsAllowed IsAllowed FOR JSON PATH,WITHOUT_ARRAY_WRAPPER));
  COMMIT TRANSACTION;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_DeleteUserDataScope @UserDataScopeId bigint,@ActorUserId bigint
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  DECLARE @UserId bigint,@Before nvarchar(max);
  SELECT @UserId=UserId,@Before=(SELECT UserId,FunctionCode,ScopeType,ScopeValue,SiteCode,IsAllowed
    FROM SEC.UserDataScope WHERE UserDataScopeId=@UserDataScopeId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)
   FROM SEC.UserDataScope WHERE UserDataScopeId=@UserDataScopeId;
  IF @UserId IS NULL THROW 53830,''User data scope does not exist.'',1;
  IF EXISTS(SELECT 1 FROM SEC.UserRole ur JOIN SEC.Role r ON r.RoleId=ur.RoleId
    WHERE ur.UserId=@UserId AND r.RoleCode=''SYSTEM_ADMIN'')
   THROW 53831,''Cannot change direct data scopes of SYSTEM_ADMIN through this procedure.'',1;
  BEGIN TRANSACTION;
  DELETE SEC.UserDataScope WHERE UserDataScopeId=@UserDataScopeId;
  INSERT SEC.AdminAudit(ActorUserId,EventCode,TargetType,TargetId,BeforeJson)
   VALUES(@ActorUserId,''USER_DATA_SCOPE_REMOVED'',''USER'',CONVERT(nvarchar(100),@UserId),@Before);
  COMMIT TRANSACTION;
 END');

 INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
 VALUES('038',N'Per-account function overrides and data-scope administration');

 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
