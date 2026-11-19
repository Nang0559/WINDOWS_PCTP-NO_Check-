using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using PCTP.Security;

namespace PCTP.Security
{
    public sealed class SecurityAdministrationRepository
    {
        private readonly string _connectionString;
        public SecurityAdministrationRepository()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Không tìm thấy connection string FVN_PCTP.");
        }

        private static void Demand(string functionCode, string actionCode)
        {
            (UserSession.Current ?? throw new UnauthorizedAccessException("Chưa đăng nhập."))
                .DemandPermission(functionCode, actionCode);
        }

        private DataTable Query(string procedure, Action<SqlParameterCollection> parameters = null)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(procedure, cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;
                if (parameters != null) parameters(cmd.Parameters);
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }

        private void Execute(string procedure, Action<SqlParameterCollection> parameters)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(procedure, cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;
                parameters(cmd.Parameters);
                cn.Open(); cmd.ExecuteNonQuery();
            }
        }

        public DataTable ListUsers() { Demand("ADMIN.USER_MANAGEMENT", "VIEW"); return Query("SEC.usp_Admin_ListUsers"); }
        public DataTable ListRoles() { Demand("ADMIN.ROLE_MANAGEMENT", "VIEW"); return Query("SEC.usp_Admin_ListRoles"); }
        public DataTable ListPermissions(int roleId)
        {
            Demand("ADMIN.ROLE_MANAGEMENT", "VIEW");
            return Query("SEC.usp_Admin_ListFunctionCatalog", p => p.Add("@RoleId", SqlDbType.Int).Value = roleId);
        }
        public DataTable ListRoleScopes(int roleId)
        {
            Demand("ADMIN.ROLE_MANAGEMENT", "VIEW");
            return Query("SEC.usp_Admin_ListRoleDataScopes", p => p.Add("@RoleId", SqlDbType.Int).Value = roleId);
        }
        public DataTable ListUserRoles(long userId)
        {
            Demand("ADMIN.USER_MANAGEMENT", "VIEW");
            return Query("SEC.usp_Admin_ListUserRoles", p => p.Add("@UserId", SqlDbType.BigInt).Value = userId);
        }
        public DataTable ListAudit() { Demand("ADMIN.AUDIT", "VIEW"); return Query("SEC.usp_Admin_ListAudit"); }

        public void CreateUser(string userName, string displayName, string employeeCode, string email, string password, long actorId)
        {
            Demand("ADMIN.USER_MANAGEMENT", "ADMIN");
            PasswordHasher.ValidatePolicy(password);
            var salt = PasswordHasher.CreateSalt();
            var hash = PasswordHasher.Hash(password, salt);
            Query("SEC.usp_Admin_CreateUser", p =>
            {
                p.Add("@UserName", SqlDbType.NVarChar, 80).Value = userName;
                p.Add("@DisplayName", SqlDbType.NVarChar, 160).Value = displayName;
                p.Add("@EmployeeCode", SqlDbType.NVarChar, 50).Value = (object)employeeCode ?? DBNull.Value;
                p.Add("@Email", SqlDbType.NVarChar, 254).Value = (object)email ?? DBNull.Value;
                p.Add("@PasswordSalt", SqlDbType.VarBinary, 32).Value = salt;
                p.Add("@PasswordHash", SqlDbType.VarBinary, 32).Value = hash;
                p.Add("@PasswordIterations", SqlDbType.Int).Value = PasswordHasher.DefaultIterations;
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorId;
            });
        }

        public int CreateRole(string roleCode, string roleName, string description, long actorId)
        {
            Demand("ADMIN.ROLE_MANAGEMENT", "ADMIN");
            var table = Query("SEC.usp_Admin_CreateRole", p =>
            {
                p.Add("@RoleCode", SqlDbType.VarChar, 50).Value = roleCode;
                p.Add("@RoleName", SqlDbType.NVarChar, 120).Value = roleName;
                p.Add("@Description", SqlDbType.NVarChar, 300).Value = (object)description ?? DBNull.Value;
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorId;
            });
            return Convert.ToInt32(table.Rows[0]["RoleId"]);
        }

        public void SaveRolePermission(int roleId, string functionCode, string actionCode, bool allowed, long actorId)
        {
            Demand("ADMIN.ROLE_MANAGEMENT", "ADMIN");
            Execute("SEC.usp_Admin_SaveRolePermission", p =>
            {
                p.Add("@RoleId", SqlDbType.Int).Value = roleId;
                p.Add("@FunctionCode", SqlDbType.VarChar, 100).Value = functionCode;
                p.Add("@ActionCode", SqlDbType.VarChar, 20).Value = actionCode;
                p.Add("@IsAllowed", SqlDbType.Bit).Value = allowed;
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorId;
            });
        }

        public void AssignUserRole(long userId, int roleId, bool assigned, long actorId)
        {
            Demand("ADMIN.USER_MANAGEMENT", "ADMIN");
            Execute("SEC.usp_Admin_AssignUserRole", p =>
            {
                p.Add("@UserId", SqlDbType.BigInt).Value = userId;
                p.Add("@RoleId", SqlDbType.Int).Value = roleId;
                p.Add("@IsAssigned", SqlDbType.Bit).Value = assigned;
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorId;
            });
        }

        public void SetUserActive(long userId, bool active, long actorId)
        {
            Demand("ADMIN.USER_MANAGEMENT", "ADMIN");
            Execute("SEC.usp_Admin_SetUserActive", p =>
            {
                p.Add("@UserId", SqlDbType.BigInt).Value = userId;
                p.Add("@IsActive", SqlDbType.Bit).Value = active;
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorId;
            });
        }

        public void ResetPassword(long userId, string password, long actorId)
        {
            Demand("ADMIN.USER_MANAGEMENT", "ADMIN");
            PasswordHasher.ValidatePolicy(password);
            var salt = PasswordHasher.CreateSalt();
            var hash = PasswordHasher.Hash(password, salt);
            Execute("SEC.usp_Admin_ResetPassword", p =>
            {
                p.Add("@UserId", SqlDbType.BigInt).Value = userId;
                p.Add("@PasswordSalt", SqlDbType.VarBinary, 32).Value = salt;
                p.Add("@PasswordHash", SqlDbType.VarBinary, 32).Value = hash;
                p.Add("@PasswordIterations", SqlDbType.Int).Value = PasswordHasher.DefaultIterations;
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorId;
            });
        }

        public void SaveRoleScope(int roleId, string functionCode, string scopeType, string scopeValue, string siteCode, bool allowed, long actorId)
        {
            Demand("ADMIN.ROLE_MANAGEMENT", "ADMIN");
            Execute("SEC.usp_Admin_SaveRoleDataScope", p =>
            {
                p.Add("@RoleId", SqlDbType.Int).Value = roleId;
                p.Add("@FunctionCode", SqlDbType.VarChar, 100).Value = (object)functionCode ?? DBNull.Value;
                p.Add("@ScopeType", SqlDbType.VarChar, 20).Value = scopeType;
                p.Add("@ScopeValue", SqlDbType.NVarChar, 100).Value = scopeValue;
                p.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = (object)siteCode ?? DBNull.Value;
                p.Add("@IsAllowed", SqlDbType.Bit).Value = allowed;
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorId;
            });
        }
    }
}