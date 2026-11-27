/*
 Migration 074: dedicated function permission for MES production-line LOT mapping.
 The setup UI already exists; this migration registers its own function code and
 carries forward the existing MES.PLAN_UPLOAD VIEW/CREATE grants and scopes so
 current planners do not unexpectedly lose access. Role admins can then tune it
 independently in the account/role permission screen.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 54500, 'Migration 045 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'SEC.FunctionCatalog', N'U') IS NULL
   OR OBJECT_ID(N'SEC.RoleFunctionPermission', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 54501, 'RBAC objects or MES migration registry are missing. Apply security migrations 037+ first.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '074')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM SEC.FunctionCatalog WHERE FunctionCode = 'MES.PRODUCTION_LINE_SETUP')
        INSERT SEC.FunctionCatalog(FunctionCode, ModuleCode, FunctionName, ParentFunctionCode, SortOrder, IsActive)
        VALUES('MES.PRODUCTION_LINE_SETUP', 'MES', N'Khai báo chuyền sản xuất theo Site', 'MES.PLAN_UPLOAD', 65, 1);
    ELSE
        UPDATE SEC.FunctionCatalog
           SET ModuleCode = 'MES',
               FunctionName = N'Khai báo chuyền sản xuất theo Site',
               ParentFunctionCode = 'MES.PLAN_UPLOAD',
               IsActive = 1
         WHERE FunctionCode = 'MES.PRODUCTION_LINE_SETUP';

    /* SYSTEM_ADMIN receives explicit grants for this newly added catalog function. */
    INSERT SEC.RoleFunctionPermission(RoleId, FunctionCode, ActionCode, IsAllowed)
    SELECT r.RoleId, 'MES.PRODUCTION_LINE_SETUP', a.ActionCode, 1
    FROM SEC.Role r
    CROSS JOIN SEC.ActionCatalog a
    WHERE r.RoleCode = 'SYSTEM_ADMIN'
      AND NOT EXISTS
      (
          SELECT 1 FROM SEC.RoleFunctionPermission p
          WHERE p.RoleId = r.RoleId
            AND p.FunctionCode = 'MES.PRODUCTION_LINE_SETUP'
            AND p.ActionCode = a.ActionCode
      );

    /* Carry forward only the two operations used by this setup form/service. */
    INSERT SEC.RoleFunctionPermission(RoleId, FunctionCode, ActionCode, IsAllowed)
    SELECT p.RoleId, 'MES.PRODUCTION_LINE_SETUP', p.ActionCode, p.IsAllowed
    FROM SEC.RoleFunctionPermission p
    WHERE p.FunctionCode = 'MES.PLAN_UPLOAD'
      AND p.ActionCode IN ('VIEW', 'CREATE')
      AND NOT EXISTS
      (
          SELECT 1 FROM SEC.RoleFunctionPermission x
          WHERE x.RoleId = p.RoleId
            AND x.FunctionCode = 'MES.PRODUCTION_LINE_SETUP'
            AND x.ActionCode = p.ActionCode
      );

    /* Preserve existing role site scopes for planner roles. */
    INSERT SEC.RoleDataScope(RoleId, FunctionCode, ScopeType, ScopeValue, SiteCode, IsAllowed)
    SELECT s.RoleId, 'MES.PRODUCTION_LINE_SETUP', s.ScopeType, s.ScopeValue, s.SiteCode, s.IsAllowed
    FROM SEC.RoleDataScope s
    WHERE s.FunctionCode = 'MES.PLAN_UPLOAD'
      AND NOT EXISTS
      (
          SELECT 1 FROM SEC.RoleDataScope x
          WHERE x.RoleId = s.RoleId
            AND x.FunctionCode = 'MES.PRODUCTION_LINE_SETUP'
            AND x.ScopeType = s.ScopeType
            AND x.ScopeValue = s.ScopeValue
            AND ISNULL(x.SiteCode, N'') = ISNULL(s.SiteCode, N'')
      );

    /* Preserve explicit user site scopes where the account had a PLAN_UPLOAD policy. */
    INSERT SEC.UserDataScope(UserId, FunctionCode, ScopeType, ScopeValue, SiteCode, IsAllowed)
    SELECT s.UserId, 'MES.PRODUCTION_LINE_SETUP', s.ScopeType, s.ScopeValue, s.SiteCode, s.IsAllowed
    FROM SEC.UserDataScope s
    WHERE s.FunctionCode = 'MES.PLAN_UPLOAD'
      AND NOT EXISTS
      (
          SELECT 1 FROM SEC.UserDataScope x
          WHERE x.UserId = s.UserId
            AND x.FunctionCode = 'MES.PRODUCTION_LINE_SETUP'
            AND x.ScopeType = s.ScopeType
            AND x.ScopeValue = s.ScopeValue
            AND ISNULL(x.SiteCode, N'') = ISNULL(s.SiteCode, N'')
      );

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES('074', N'MES production line setup dedicated function permission');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT FunctionCode, FunctionName
FROM SEC.FunctionCatalog
WHERE FunctionCode = 'MES.PRODUCTION_LINE_SETUP';

SELECT r.RoleCode, p.ActionCode, p.IsAllowed
FROM SEC.RoleFunctionPermission p
JOIN SEC.Role r ON r.RoleId = p.RoleId
WHERE p.FunctionCode = 'MES.PRODUCTION_LINE_SETUP'
ORDER BY r.RoleCode, p.ActionCode;
GO
