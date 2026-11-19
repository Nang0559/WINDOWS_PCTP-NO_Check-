/*
 Migration 037: PCTP application identity, function RBAC, data scopes and audit.
 SQL Server 2016 compatible. Accounts are application identities; no SQL logins are created.
 Run after MES migration 036. Never seed a default password.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'FVN_PCTP' THROW 53700,'Migration 037 may run only in FVN_PCTP.',1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL THROW 53701,'MES migration registry is missing.',1;
IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='036')
 THROW 53702,'Migration 036 must be applied before 037.',1;
IF EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='037') RETURN;

BEGIN TRY
 BEGIN TRANSACTION;
 IF SCHEMA_ID(N'SEC') IS NULL EXEC(N'CREATE SCHEMA SEC AUTHORIZATION dbo');

 IF OBJECT_ID(N'SEC.UserAccount',N'U') IS NULL
 CREATE TABLE SEC.UserAccount(
  UserId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SEC_UserAccount PRIMARY KEY,
  UserName nvarchar(80) NOT NULL, UserNameNormalized AS UPPER(LTRIM(RTRIM(UserName))) PERSISTED,
  DisplayName nvarchar(160) NOT NULL, EmployeeCode nvarchar(50) NULL, Email nvarchar(254) NULL,
  PasswordSalt varbinary(32) NOT NULL, PasswordHash varbinary(32) NOT NULL,
  PasswordIterations int NOT NULL CONSTRAINT DF_SEC_User_Iterations DEFAULT(210000),
  IsActive bit NOT NULL CONSTRAINT DF_SEC_User_Active DEFAULT(1),
  MustChangePassword bit NOT NULL CONSTRAINT DF_SEC_User_Change DEFAULT(0),
  FailedLoginCount int NOT NULL CONSTRAINT DF_SEC_User_Failed DEFAULT(0),
  LockedUntilUtc datetime2(3) NULL, LastLoginAtUtc datetime2(3) NULL,
  CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SEC_User_Created DEFAULT(SYSUTCDATETIME()),
  CreatedByUserId bigint NULL, UpdatedAtUtc datetime2(3) NULL, UpdatedByUserId bigint NULL,
  RowVersion rowversion NOT NULL,
  CONSTRAINT UQ_SEC_UserAccount_UserName UNIQUE(UserNameNormalized),
  CONSTRAINT CK_SEC_UserAccount_Iterations CHECK(PasswordIterations BETWEEN 100000 AND 1000000),
  CONSTRAINT FK_SEC_UserAccount_CreatedBy FOREIGN KEY(CreatedByUserId) REFERENCES SEC.UserAccount(UserId),
  CONSTRAINT FK_SEC_UserAccount_UpdatedBy FOREIGN KEY(UpdatedByUserId) REFERENCES SEC.UserAccount(UserId)
 );

 IF OBJECT_ID(N'SEC.Role',N'U') IS NULL
 CREATE TABLE SEC.Role(
  RoleId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SEC_Role PRIMARY KEY,
  RoleCode varchar(50) NOT NULL, RoleName nvarchar(120) NOT NULL,
  Description nvarchar(300) NULL, IsSystem bit NOT NULL CONSTRAINT DF_SEC_Role_System DEFAULT(0),
  IsActive bit NOT NULL CONSTRAINT DF_SEC_Role_Active DEFAULT(1),
  CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SEC_Role_Created DEFAULT(SYSUTCDATETIME()),
  CONSTRAINT UQ_SEC_Role_Code UNIQUE(RoleCode)
 );

 IF OBJECT_ID(N'SEC.UserRole',N'U') IS NULL
 CREATE TABLE SEC.UserRole(
  UserId bigint NOT NULL, RoleId int NOT NULL, AssignedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SEC_UserRole_Assigned DEFAULT(SYSUTCDATETIME()),
  AssignedByUserId bigint NULL,
  CONSTRAINT PK_SEC_UserRole PRIMARY KEY(UserId,RoleId),
  CONSTRAINT FK_SEC_UserRole_User FOREIGN KEY(UserId) REFERENCES SEC.UserAccount(UserId),
  CONSTRAINT FK_SEC_UserRole_Role FOREIGN KEY(RoleId) REFERENCES SEC.Role(RoleId),
  CONSTRAINT FK_SEC_UserRole_AssignedBy FOREIGN KEY(AssignedByUserId) REFERENCES SEC.UserAccount(UserId)
 );

 IF OBJECT_ID(N'SEC.FunctionCatalog',N'U') IS NULL
 CREATE TABLE SEC.FunctionCatalog(
  FunctionCode varchar(100) NOT NULL CONSTRAINT PK_SEC_FunctionCatalog PRIMARY KEY,
  ModuleCode varchar(50) NOT NULL, FunctionName nvarchar(160) NOT NULL,
  ParentFunctionCode varchar(100) NULL, SortOrder int NOT NULL CONSTRAINT DF_SEC_Function_Sort DEFAULT(0),
  IsActive bit NOT NULL CONSTRAINT DF_SEC_Function_Active DEFAULT(1),
  CONSTRAINT FK_SEC_Function_Parent FOREIGN KEY(ParentFunctionCode) REFERENCES SEC.FunctionCatalog(FunctionCode)
 );

 IF OBJECT_ID(N'SEC.ActionCatalog',N'U') IS NULL
 CREATE TABLE SEC.ActionCatalog(
  ActionCode varchar(20) NOT NULL CONSTRAINT PK_SEC_ActionCatalog PRIMARY KEY,
  ActionName nvarchar(80) NOT NULL, SortOrder int NOT NULL
 );

 IF OBJECT_ID(N'SEC.RoleFunctionPermission',N'U') IS NULL
 CREATE TABLE SEC.RoleFunctionPermission(
  RoleId int NOT NULL, FunctionCode varchar(100) NOT NULL, ActionCode varchar(20) NOT NULL,
  IsAllowed bit NOT NULL, UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SEC_RFP_Updated DEFAULT(SYSUTCDATETIME()),
  UpdatedByUserId bigint NULL,
  CONSTRAINT PK_SEC_RoleFunctionPermission PRIMARY KEY(RoleId,FunctionCode,ActionCode),
  CONSTRAINT FK_SEC_RFP_Role FOREIGN KEY(RoleId) REFERENCES SEC.Role(RoleId),
  CONSTRAINT FK_SEC_RFP_Function FOREIGN KEY(FunctionCode) REFERENCES SEC.FunctionCatalog(FunctionCode),
  CONSTRAINT FK_SEC_RFP_Action FOREIGN KEY(ActionCode) REFERENCES SEC.ActionCatalog(ActionCode),
  CONSTRAINT FK_SEC_RFP_Actor FOREIGN KEY(UpdatedByUserId) REFERENCES SEC.UserAccount(UserId)
 );

 IF OBJECT_ID(N'SEC.UserFunctionOverride',N'U') IS NULL
 CREATE TABLE SEC.UserFunctionOverride(
  UserId bigint NOT NULL, FunctionCode varchar(100) NOT NULL, ActionCode varchar(20) NOT NULL,
  Effect varchar(5) NOT NULL, UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SEC_UFO_Updated DEFAULT(SYSUTCDATETIME()),
  UpdatedByUserId bigint NULL,
  CONSTRAINT PK_SEC_UserFunctionOverride PRIMARY KEY(UserId,FunctionCode,ActionCode),
  CONSTRAINT CK_SEC_UFO_Effect CHECK(Effect IN('ALLOW','DENY')),
  CONSTRAINT FK_SEC_UFO_User FOREIGN KEY(UserId) REFERENCES SEC.UserAccount(UserId),
  CONSTRAINT FK_SEC_UFO_Function FOREIGN KEY(FunctionCode) REFERENCES SEC.FunctionCatalog(FunctionCode),
  CONSTRAINT FK_SEC_UFO_Action FOREIGN KEY(ActionCode) REFERENCES SEC.ActionCatalog(ActionCode),
  CONSTRAINT FK_SEC_UFO_Actor FOREIGN KEY(UpdatedByUserId) REFERENCES SEC.UserAccount(UserId)
 );

 IF OBJECT_ID(N'SEC.RoleDataScope',N'U') IS NULL
 CREATE TABLE SEC.RoleDataScope(
  RoleDataScopeId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SEC_RoleDataScope PRIMARY KEY,
  RoleId int NOT NULL, FunctionCode varchar(100) NULL,
  ScopeType varchar(20) NOT NULL, ScopeValue nvarchar(100) NOT NULL,
  SiteCode nvarchar(30) NULL, IsAllowed bit NOT NULL CONSTRAINT DF_SEC_RDS_Allowed DEFAULT(1),
  CONSTRAINT FK_SEC_RDS_Role FOREIGN KEY(RoleId) REFERENCES SEC.Role(RoleId),
  CONSTRAINT FK_SEC_RDS_Function FOREIGN KEY(FunctionCode) REFERENCES SEC.FunctionCatalog(FunctionCode),
  CONSTRAINT CK_SEC_RDS_Type CHECK(ScopeType IN('ALL','SITE','DEPARTMENT','WAREHOUSE','LINE','PROCESS','OWN')),
  CONSTRAINT CK_SEC_RDS_All CHECK(ScopeType<>'ALL' OR ScopeValue=N'*')
 );

 IF OBJECT_ID(N'SEC.UserDataScope',N'U') IS NULL
 CREATE TABLE SEC.UserDataScope(
  UserDataScopeId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SEC_UserDataScope PRIMARY KEY,
  UserId bigint NOT NULL, FunctionCode varchar(100) NULL,
  ScopeType varchar(20) NOT NULL, ScopeValue nvarchar(100) NOT NULL,
  SiteCode nvarchar(30) NULL, IsAllowed bit NOT NULL CONSTRAINT DF_SEC_UDS_Allowed DEFAULT(1),
  CONSTRAINT FK_SEC_UDS_User FOREIGN KEY(UserId) REFERENCES SEC.UserAccount(UserId),
  CONSTRAINT FK_SEC_UDS_Function FOREIGN KEY(FunctionCode) REFERENCES SEC.FunctionCatalog(FunctionCode),
  CONSTRAINT CK_SEC_UDS_Type CHECK(ScopeType IN('ALL','SITE','DEPARTMENT','WAREHOUSE','LINE','PROCESS','OWN')),
  CONSTRAINT CK_SEC_UDS_All CHECK(ScopeType<>'ALL' OR ScopeValue=N'*')
 );

 IF OBJECT_ID(N'SEC.LoginAudit',N'U') IS NULL
 CREATE TABLE SEC.LoginAudit(
  LoginAuditId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SEC_LoginAudit PRIMARY KEY,
  UserId bigint NULL, UserName nvarchar(80) NOT NULL, IsSuccess bit NOT NULL,
  OccurredAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SEC_LoginAudit_Date DEFAULT(SYSUTCDATETIME()),
  MachineName nvarchar(128) NULL, ClientVersion nvarchar(40) NULL,
  FailureCode varchar(30) NULL,
  CONSTRAINT FK_SEC_LoginAudit_User FOREIGN KEY(UserId) REFERENCES SEC.UserAccount(UserId)
 );

 IF OBJECT_ID(N'SEC.AdminAudit',N'U') IS NULL
 CREATE TABLE SEC.AdminAudit(
  AdminAuditId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SEC_AdminAudit PRIMARY KEY,
  ActorUserId bigint NULL, EventCode varchar(60) NOT NULL, TargetType varchar(40) NOT NULL,
  TargetId nvarchar(100) NULL, BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NULL,
  OccurredAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SEC_AdminAudit_Date DEFAULT(SYSUTCDATETIME()),
  MachineName nvarchar(128) NULL,
  CONSTRAINT FK_SEC_AdminAudit_Actor FOREIGN KEY(ActorUserId) REFERENCES SEC.UserAccount(UserId)
 );

 IF NOT EXISTS(SELECT 1 FROM SEC.ActionCatalog)
 INSERT SEC.ActionCatalog(ActionCode,ActionName,SortOrder) VALUES
 ('VIEW',N'Xem',10),('CREATE',N'Tạo mới',20),('EDIT',N'Sửa',30),('DELETE',N'Xóa',40),
 ('CONFIRM',N'Xác nhận',50),('APPROVE',N'Duyệt',60),('CANCEL',N'Hủy',70),
 ('EXPORT',N'Xuất dữ liệu',80),('PRINT',N'In',90),('ADMIN',N'Quản trị',100);

 IF NOT EXISTS(SELECT 1 FROM SEC.FunctionCatalog)
 INSERT SEC.FunctionCatalog(FunctionCode,ModuleCode,FunctionName,SortOrder) VALUES
 ('CORE.DASHBOARD','CORE',N'Tổng quan',10),
 ('WAREHOUSE.RECEIPT','WAREHOUSE',N'Nhập kho',20),
 ('WAREHOUSE.STOCK_VIEW','WAREHOUSE',N'Xem tồn kho',30),
 ('WAREHOUSE.STOCK_ADJUST','WAREHOUSE',N'Điều chỉnh tồn LOT',40),
 ('WAREHOUSE.MOVE','WAREHOUSE',N'Điều chuyển nội bộ',50),
 ('MES.PLAN_UPLOAD','MES',N'Upload kế hoạch sản xuất',60),
 ('MES.PRODUCTION_EXECUTION','MES',N'Điều hành sản xuất',70),
 ('MES.COMPONENT_ISSUE','MES',N'Yêu cầu và xuất linh kiện PART → WIP',80),
 ('MES.FINISHED_GOODS_RECEIPT','MES',N'Nhập kho thành phẩm',90),
 ('LOT.SPLIT','LOT',N'Tách LOT và in tem',100),
 ('LOT.MERGE','LOT',N'Ghép LOT',110),
 ('SHIPPING.DISPATCH','SHIPPING',N'Giao hàng',120),
 ('QUALITY.RETURN','QUALITY',N'Xử lý hàng lỗi',130),
 ('REPORT.TRACEABILITY','REPORT',N'Truy xuất LOT và linh kiện',140),
 ('REPORT.STOCK_HISTORY','REPORT',N'Lịch sử kho',150),
 ('REPORT.QUALITY_HISTORY','REPORT',N'Lịch sử QC',160),
 ('MASTERDATA.IFS_SYNC','MASTERDATA',N'Đồng bộ IFS',170),
 ('ADMIN.USER_MANAGEMENT','ADMIN',N'Quản lý tài khoản',900),
 ('ADMIN.ROLE_MANAGEMENT','ADMIN',N'Quản lý vai trò và quyền',910),
 ('ADMIN.AUDIT','ADMIN',N'Nhật ký bảo mật',920),
 ('SYSTEM.FIFO_CONFIG','SYSTEM',N'Cấu hình FIFO',930),
 ('SYSTEM.QR_READER_ADMIN','SYSTEM',N'Quản lý máy đọc QR',940),
 ('SYSTEM.TIME_CONFIG','SYSTEM',N'Đổi thời gian hệ thống',950),
 ('SYSTEM.COMPARE_PART','SYSTEM',N'So sánh mã linh kiện',960),
 ('SYSTEM.IMAGE_CONTROL','SYSTEM',N'Quản lý hình ảnh',970);

 IF NOT EXISTS(SELECT 1 FROM SEC.Role WHERE RoleCode='SYSTEM_ADMIN')
 INSERT SEC.Role(RoleCode,RoleName,Description,IsSystem) VALUES('SYSTEM_ADMIN',N'Quản trị hệ thống',N'Quản trị tài khoản, quyền và phạm vi; không bỏ qua kiểm tra quyền.',1);

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Account_GetBootstrapState
 AS
 BEGIN
  SET NOCOUNT ON;
  SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM SEC.UserAccount) THEN 1 ELSE 0 END) HasUsers;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Account_ChangePassword
  @UserId bigint,@CurrentPasswordHash varbinary(32)=NULL,@NewPasswordSalt varbinary(32),
  @NewPasswordHash varbinary(32),@PasswordIterations int,@IsFirstPassword bit=0
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF DATALENGTH(@NewPasswordSalt)<16 OR DATALENGTH(@NewPasswordHash)<>32 OR @PasswordIterations NOT BETWEEN 100000 AND 1000000
   THROW 53714,''Password credential parameters are invalid.'',1;
  IF @IsFirstPassword=1 AND NOT EXISTS(SELECT 1 FROM SEC.UserAccount
   WHERE UserId=@UserId AND MustChangePassword=1)
   THROW 53715,''First-password flow is not enabled for this account.'',1;
  IF @IsFirstPassword=0 AND NOT EXISTS(SELECT 1 FROM SEC.UserAccount
   WHERE UserId=@UserId AND PasswordHash=@CurrentPasswordHash)
   THROW 53716,''Current password is incorrect.'',1;
  UPDATE SEC.UserAccount SET PasswordSalt=@NewPasswordSalt,PasswordHash=@NewPasswordHash,
   PasswordIterations=@PasswordIterations,MustChangePassword=0,FailedLoginCount=0,LockedUntilUtc=NULL,
   UpdatedAtUtc=SYSUTCDATETIME() WHERE UserId=@UserId AND IsActive=1;
  IF @@ROWCOUNT=0 THROW 53717,''Active account not found.'',1;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Account_GetCredential @UserName nvarchar(80)
 AS
 BEGIN
  SET NOCOUNT ON;
  SELECT UserId,UserName,DisplayName,PasswordSalt,PasswordHash,PasswordIterations,
   IsActive,MustChangePassword,FailedLoginCount,LockedUntilUtc
  FROM SEC.UserAccount WHERE UserNameNormalized=UPPER(LTRIM(RTRIM(@UserName)));
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Account_RecordLogin
  @UserId bigint=NULL,@UserName nvarchar(80),@IsSuccess bit,
  @MachineName nvarchar(128)=NULL,@ClientVersion nvarchar(40)=NULL,@FailureCode varchar(30)=NULL
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  BEGIN TRANSACTION;
  IF @UserId IS NOT NULL
  BEGIN
   IF @IsSuccess=1
    UPDATE SEC.UserAccount SET FailedLoginCount=0,LockedUntilUtc=NULL,LastLoginAtUtc=SYSUTCDATETIME()
     WHERE UserId=@UserId;
   ELSE
    UPDATE SEC.UserAccount SET FailedLoginCount=FailedLoginCount+1,
     LockedUntilUtc=CASE WHEN FailedLoginCount+1>=5 THEN DATEADD(MINUTE,15,SYSUTCDATETIME()) ELSE LockedUntilUtc END
     WHERE UserId=@UserId;
  END;
  INSERT SEC.LoginAudit(UserId,UserName,IsSuccess,MachineName,ClientVersion,FailureCode)
  VALUES(@UserId,LEFT(ISNULL(@UserName,N''),80),@IsSuccess,@MachineName,@ClientVersion,@FailureCode);
  COMMIT;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Account_GetAccessProfile @UserId bigint
 AS
 BEGIN
  SET NOCOUNT ON;
  IF NOT EXISTS(SELECT 1 FROM SEC.UserAccount WHERE UserId=@UserId AND IsActive=1)
   THROW 53710,''Account is inactive or unavailable.'',1;
  ;WITH RoleGrant AS(
   SELECT p.FunctionCode,p.ActionCode,MAX(CONVERT(int,p.IsAllowed)) IsAllowed
   FROM SEC.UserRole ur JOIN SEC.Role r ON r.RoleId=ur.RoleId AND r.IsActive=1
   JOIN SEC.RoleFunctionPermission p ON p.RoleId=r.RoleId
   WHERE ur.UserId=@UserId GROUP BY p.FunctionCode,p.ActionCode
  )
  SELECT f.FunctionCode,a.ActionCode,
   CASE WHEN u.Effect=''DENY'' THEN CAST(0 AS bit)
    WHEN u.Effect=''ALLOW'' THEN CAST(1 AS bit)
    ELSE CAST(ISNULL(r.IsAllowed,0) AS bit) END IsAllowed
  FROM SEC.FunctionCatalog f CROSS JOIN SEC.ActionCatalog a
  LEFT JOIN RoleGrant r ON r.FunctionCode=f.FunctionCode AND r.ActionCode=a.ActionCode
  LEFT JOIN SEC.UserFunctionOverride u ON u.UserId=@UserId AND u.FunctionCode=f.FunctionCode AND u.ActionCode=a.ActionCode
  WHERE f.IsActive=1;
  SELECT ''ROLE'' ScopeSource,CAST(NULL AS bigint) ScopeId,f.FunctionCode,s.ScopeType,s.ScopeValue,s.SiteCode,s.IsAllowed
   FROM SEC.UserRole ur JOIN SEC.Role r ON r.RoleId=ur.RoleId AND r.IsActive=1
   JOIN SEC.RoleDataScope s ON s.RoleId=r.RoleId
   LEFT JOIN SEC.FunctionCatalog f ON f.FunctionCode=s.FunctionCode
   WHERE ur.UserId=@UserId
  UNION ALL
  SELECT ''USER'',s.UserDataScopeId,s.FunctionCode,s.ScopeType,s.ScopeValue,s.SiteCode,s.IsAllowed
   FROM SEC.UserDataScope s WHERE s.UserId=@UserId;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Account_InitializeFirstAdmin
  @UserName nvarchar(80),@DisplayName nvarchar(160),@PasswordSalt varbinary(32),
  @PasswordHash varbinary(32),@PasswordIterations int,@MachineName nvarchar(128)=NULL
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF NULLIF(LTRIM(RTRIM(@UserName)),N'''') IS NULL OR NULLIF(LTRIM(RTRIM(@DisplayName)),N'''') IS NULL
   THROW 53711,''Username and display name are required.'',1;
  IF DATALENGTH(@PasswordSalt)<16 OR DATALENGTH(@PasswordHash)<>32 OR @PasswordIterations NOT BETWEEN 100000 AND 1000000
   THROW 53712,''Password credential parameters are invalid.'',1;
  BEGIN TRY
   BEGIN TRANSACTION;
   IF EXISTS(SELECT 1 FROM SEC.UserAccount WITH(UPDLOCK,HOLDLOCK))
    THROW 53713,''First administrator has already been initialized.'',1;
   DECLARE @RoleId int=(SELECT RoleId FROM SEC.Role WHERE RoleCode=''SYSTEM_ADMIN'');
   INSERT SEC.UserAccount(UserName,DisplayName,PasswordSalt,PasswordHash,PasswordIterations)
   VALUES(LTRIM(RTRIM(@UserName)),LTRIM(RTRIM(@DisplayName)),@PasswordSalt,@PasswordHash,@PasswordIterations);
   DECLARE @UserId bigint=SCOPE_IDENTITY();
   INSERT SEC.UserRole(UserId,RoleId) VALUES(@UserId,@RoleId);
   INSERT SEC.RoleFunctionPermission(RoleId,FunctionCode,ActionCode,IsAllowed)
   SELECT @RoleId,f.FunctionCode,a.ActionCode,1
   FROM SEC.FunctionCatalog f CROSS JOIN SEC.ActionCatalog a
   WHERE NOT EXISTS(SELECT 1 FROM SEC.RoleFunctionPermission p
    WHERE p.RoleId=@RoleId AND p.FunctionCode=f.FunctionCode AND p.ActionCode=a.ActionCode);
   INSERT SEC.RoleDataScope(RoleId,FunctionCode,ScopeType,ScopeValue,IsAllowed)
   VALUES(@RoleId,NULL,''ALL'',N''*'',1);
   INSERT SEC.AdminAudit(ActorUserId,EventCode,TargetType,TargetId,AfterJson,MachineName)
   VALUES(@UserId,''FIRST_ADMIN_INITIALIZED'',''USER'',CONVERT(nvarchar(100),@UserId),
    N''{"role":"SYSTEM_ADMIN","method":"first-run"}'',@MachineName);
   COMMIT;
   SELECT @UserId UserId;
  END TRY
  BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_ListUsers
 AS
 BEGIN
  SET NOCOUNT ON;
  SELECT u.UserId,u.UserName,u.DisplayName,u.EmployeeCode,u.Email,u.IsActive,u.MustChangePassword,
   u.FailedLoginCount,u.LockedUntilUtc,u.LastLoginAtUtc,
   STUFF((SELECT N'', ''+r.RoleName FROM SEC.UserRole ur JOIN SEC.Role r ON r.RoleId=ur.RoleId
    WHERE ur.UserId=u.UserId FOR XML PATH(''''),TYPE).value(''.'',''nvarchar(max)''),1,2,N'''') Roles
  FROM SEC.UserAccount u ORDER BY u.UserName;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_ListRoles
 AS
 BEGIN
  SET NOCOUNT ON;
  SELECT RoleId,RoleCode,RoleName,Description,IsSystem,IsActive FROM SEC.Role ORDER BY RoleName;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_ListFunctionCatalog @RoleId int
 AS
 BEGIN
  SET NOCOUNT ON;
  SELECT f.FunctionCode,f.ModuleCode,f.FunctionName,f.SortOrder,a.ActionCode,a.ActionName,a.SortOrder ActionSort,
   CAST(ISNULL(p.IsAllowed,0) AS bit) IsAllowed
  FROM SEC.FunctionCatalog f CROSS JOIN SEC.ActionCatalog a
  LEFT JOIN SEC.RoleFunctionPermission p ON p.RoleId=@RoleId
   AND p.FunctionCode=f.FunctionCode AND p.ActionCode=a.ActionCode
  ORDER BY f.SortOrder,a.SortOrder;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_ListRoleDataScopes @RoleId int
 AS
 BEGIN
  SET NOCOUNT ON;
  SELECT RoleDataScopeId,RoleId,FunctionCode,ScopeType,ScopeValue,SiteCode,IsAllowed
  FROM SEC.RoleDataScope WHERE RoleId=@RoleId ORDER BY FunctionCode,ScopeType,ScopeValue;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_ListUserRoles @UserId bigint
 AS
 BEGIN
  SET NOCOUNT ON;
  SELECT r.RoleId,r.RoleCode,r.RoleName,CONVERT(bit,CASE WHEN ur.UserId IS NULL THEN 0 ELSE 1 END) IsAssigned
  FROM SEC.Role r LEFT JOIN SEC.UserRole ur ON ur.RoleId=r.RoleId AND ur.UserId=@UserId
  WHERE r.IsActive=1 ORDER BY r.RoleName;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_CreateRole
  @RoleCode varchar(50),@RoleName nvarchar(120),@Description nvarchar(300)=NULL,@ActorUserId bigint
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF EXISTS(SELECT 1 FROM SEC.Role WHERE RoleCode=@RoleCode) THROW 53720,''Role code already exists.'',1;
  INSERT SEC.Role(RoleCode,RoleName,Description) VALUES(@RoleCode,@RoleName,@Description);
  DECLARE @Id int=SCOPE_IDENTITY();
  INSERT SEC.AdminAudit(ActorUserId,EventCode,TargetType,TargetId,AfterJson)
   VALUES(@ActorUserId,''ROLE_CREATED'',''ROLE'',CONVERT(nvarchar(100),@Id),
    N''{"roleCode":"''+STRING_ESCAPE(@RoleCode,''json'')+N''"}'');
  SELECT @Id RoleId;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_SaveRolePermission
  @RoleId int,@FunctionCode varchar(100),@ActionCode varchar(20),@IsAllowed bit,@ActorUserId bigint
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  DECLARE @Before bit=(SELECT IsAllowed FROM SEC.RoleFunctionPermission
   WHERE RoleId=@RoleId AND FunctionCode=@FunctionCode AND ActionCode=@ActionCode);
  MERGE SEC.RoleFunctionPermission AS t
  USING(SELECT @RoleId RoleId,@FunctionCode FunctionCode,@ActionCode ActionCode) s
   ON t.RoleId=s.RoleId AND t.FunctionCode=s.FunctionCode AND t.ActionCode=s.ActionCode
  WHEN MATCHED THEN UPDATE SET IsAllowed=@IsAllowed,UpdatedAtUtc=SYSUTCDATETIME(),UpdatedByUserId=@ActorUserId
  WHEN NOT MATCHED THEN INSERT(RoleId,FunctionCode,ActionCode,IsAllowed,UpdatedByUserId)
   VALUES(@RoleId,@FunctionCode,@ActionCode,@IsAllowed,@ActorUserId);
  INSERT SEC.AdminAudit(ActorUserId,EventCode,TargetType,TargetId,BeforeJson,AfterJson)
  VALUES(@ActorUserId,''ROLE_PERMISSION_CHANGED'',''ROLE'',CONVERT(nvarchar(100),@RoleId),
   N''{"function":"''+@FunctionCode+N''","action":"''+@ActionCode+N''","allowed":''+
    CASE WHEN @Before=1 THEN N''true'' WHEN @Before=0 THEN N''false'' ELSE N''null'' END+N''}'',
   N''{"function":"''+@FunctionCode+N''","action":"''+@ActionCode+N''","allowed":''+
    CASE WHEN @IsAllowed=1 THEN N''true'' ELSE N''false'' END+N''}'');
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_AssignUserRole
  @UserId bigint,@RoleId int,@IsAssigned bit,@ActorUserId bigint
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF @IsAssigned=1
  BEGIN
   IF NOT EXISTS(SELECT 1 FROM SEC.UserRole WHERE UserId=@UserId AND RoleId=@RoleId)
    INSERT SEC.UserRole(UserId,RoleId,AssignedByUserId) VALUES(@UserId,@RoleId,@ActorUserId);
  END
  ELSE DELETE SEC.UserRole WHERE UserId=@UserId AND RoleId=@RoleId;
  INSERT SEC.AdminAudit(ActorUserId,EventCode,TargetType,TargetId,AfterJson)
   VALUES(@ActorUserId,''USER_ROLE_CHANGED'',''USER'',CONVERT(nvarchar(100),@UserId),
    N''{"roleId":''+CONVERT(nvarchar(20),@RoleId)+N'',"assigned":''+CASE WHEN @IsAssigned=1 THEN N''true'' ELSE N''false'' END+N''}'');
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_SetUserActive
  @UserId bigint,@IsActive bit,@ActorUserId bigint
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  UPDATE SEC.UserAccount SET IsActive=@IsActive,UpdatedAtUtc=SYSUTCDATETIME(),UpdatedByUserId=@ActorUserId
  WHERE UserId=@UserId;
  IF @@ROWCOUNT=0 THROW 53721,''User not found.'',1;
  INSERT SEC.AdminAudit(ActorUserId,EventCode,TargetType,TargetId,AfterJson)
   VALUES(@ActorUserId,''USER_ACTIVE_CHANGED'',''USER'',CONVERT(nvarchar(100),@UserId),
    N''{"isActive":''+CASE WHEN @IsActive=1 THEN N''true'' ELSE N''false'' END+N''}'');
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_ResetPassword
  @UserId bigint,@PasswordSalt varbinary(32),@PasswordHash varbinary(32),
  @PasswordIterations int,@ActorUserId bigint
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF DATALENGTH(@PasswordSalt)<16 OR DATALENGTH(@PasswordHash)<>32 OR @PasswordIterations NOT BETWEEN 100000 AND 1000000
   THROW 53722,''Password credential parameters are invalid.'',1;
  UPDATE SEC.UserAccount SET PasswordSalt=@PasswordSalt,PasswordHash=@PasswordHash,
   PasswordIterations=@PasswordIterations,MustChangePassword=1,FailedLoginCount=0,LockedUntilUtc=NULL,
   UpdatedAtUtc=SYSUTCDATETIME(),UpdatedByUserId=@ActorUserId WHERE UserId=@UserId;
  IF @@ROWCOUNT=0 THROW 53723,''User not found.'',1;
  INSERT SEC.AdminAudit(ActorUserId,EventCode,TargetType,TargetId)
   VALUES(@ActorUserId,''USER_PASSWORD_RESET'',''USER'',CONVERT(nvarchar(100),@UserId));
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_CreateUser
  @UserName nvarchar(80),@DisplayName nvarchar(160),@EmployeeCode nvarchar(50)=NULL,
  @Email nvarchar(254)=NULL,@PasswordSalt varbinary(32),@PasswordHash varbinary(32),
  @PasswordIterations int,@ActorUserId bigint
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF DATALENGTH(@PasswordSalt)<16 OR DATALENGTH(@PasswordHash)<>32 OR @PasswordIterations NOT BETWEEN 100000 AND 1000000
   THROW 53724,''Password credential parameters are invalid.'',1;
  INSERT SEC.UserAccount(UserName,DisplayName,EmployeeCode,Email,PasswordSalt,PasswordHash,
   PasswordIterations,MustChangePassword,CreatedByUserId)
  VALUES(LTRIM(RTRIM(@UserName)),LTRIM(RTRIM(@DisplayName)),@EmployeeCode,@Email,
   @PasswordSalt,@PasswordHash,@PasswordIterations,1,@ActorUserId);
  DECLARE @NewUserId bigint=SCOPE_IDENTITY();
  INSERT SEC.AdminAudit(ActorUserId,EventCode,TargetType,TargetId)
   VALUES(@ActorUserId,''USER_CREATED'',''USER'',CONVERT(nvarchar(100),@NewUserId));
  SELECT @NewUserId UserId;
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_SaveRoleDataScope
  @RoleId int,@FunctionCode varchar(100)=NULL,@ScopeType varchar(20),@ScopeValue nvarchar(100),
  @SiteCode nvarchar(30)=NULL,@IsAllowed bit,@ActorUserId bigint
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF @ScopeType NOT IN(''ALL'',''SITE'',''DEPARTMENT'',''WAREHOUSE'',''LINE'',''PROCESS'',''OWN'')
   THROW 53725,''Invalid scope type.'',1;
  IF @ScopeType=''ALL'' AND @ScopeValue<>N''*'' THROW 53726,''ALL scope must use *.'',1;
  IF EXISTS(SELECT 1 FROM SEC.RoleDataScope WHERE RoleId=@RoleId
   AND ISNULL(FunctionCode,'''')=ISNULL(@FunctionCode,'''') AND ScopeType=@ScopeType
   AND ScopeValue=@ScopeValue AND ISNULL(SiteCode,'''')=ISNULL(@SiteCode,''''))
   UPDATE SEC.RoleDataScope SET IsAllowed=@IsAllowed WHERE RoleId=@RoleId
    AND ISNULL(FunctionCode,'''')=ISNULL(@FunctionCode,'''') AND ScopeType=@ScopeType
    AND ScopeValue=@ScopeValue AND ISNULL(SiteCode,'''')=ISNULL(@SiteCode,'''');
  ELSE INSERT SEC.RoleDataScope(RoleId,FunctionCode,ScopeType,ScopeValue,SiteCode,IsAllowed)
   VALUES(@RoleId,@FunctionCode,@ScopeType,@ScopeValue,@SiteCode,@IsAllowed);
  INSERT SEC.AdminAudit(ActorUserId,EventCode,TargetType,TargetId,AfterJson)
   VALUES(@ActorUserId,''ROLE_DATA_SCOPE_CHANGED'',''ROLE'',CONVERT(nvarchar(100),@RoleId),
    N''{"scopeType":"''+@ScopeType+N''","scopeValue":"''+@ScopeValue+N''"}'');
 END');

 EXEC(N'
 CREATE PROCEDURE SEC.usp_Admin_ListAudit @FromUtc datetime2(3)=NULL,@ToUtc datetime2(3)=NULL
 AS
 BEGIN
  SET NOCOUNT ON;
  SELECT AuditId,ActorUserId,EventCode,TargetType,TargetId,BeforeJson,AfterJson,OccurredAtUtc,MachineName,AuditSource
  FROM(
   SELECT AdminAuditId AuditId,ActorUserId,EventCode,TargetType,TargetId,BeforeJson,AfterJson,
    OccurredAtUtc,MachineName,CAST(''ADMIN'' AS varchar(10)) AuditSource
   FROM SEC.AdminAudit WHERE (@FromUtc IS NULL OR OccurredAtUtc>=@FromUtc)
    AND (@ToUtc IS NULL OR OccurredAtUtc<@ToUtc)
   UNION ALL
   SELECT LoginAuditId,UserId,
    CASE WHEN IsSuccess=1 THEN ''LOGIN_SUCCESS'' ELSE ''LOGIN_FAILED'' END,
    ''LOGIN'',UserName,CAST(NULL AS nvarchar(max)),FailureCode,OccurredAtUtc,MachineName,
    CAST(''LOGIN'' AS varchar(10))
   FROM SEC.LoginAudit WHERE (@FromUtc IS NULL OR OccurredAtUtc>=@FromUtc)
    AND (@ToUtc IS NULL OR OccurredAtUtc<@ToUtc)
  ) A
  ORDER BY OccurredAtUtc DESC,AuditId DESC;
 END');

 INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
 VALUES('037',N'PCTP application accounts, function RBAC, data scopes and security audit');
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
GO
