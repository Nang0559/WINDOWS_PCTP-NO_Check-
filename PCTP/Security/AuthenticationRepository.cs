using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Security
{
    public sealed class AuthenticationRepository
    {
        private readonly string _connectionString;
        public AuthenticationRepository(string connectionString = null)
        {
            _connectionString = connectionString ??
                ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Không tìm thấy connection string FVN_PCTP.");
        }

        public bool HasUsers()
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("SEC.usp_Account_GetBootstrapState", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cn.Open();
                return Convert.ToBoolean(cmd.ExecuteScalar());
            }
        }

        public DataRow GetCredential(string userName)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("SEC.usp_Account_GetCredential", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@UserName", SqlDbType.NVarChar, 80).Value = userName;
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table.Rows.Count == 0 ? null : table.Rows[0];
            }
        }

        public void RecordLogin(long? userId, string userName, bool success, string failureCode)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("SEC.usp_Account_RecordLogin", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@UserId", SqlDbType.BigInt).Value = (object)userId ?? DBNull.Value;
                cmd.Parameters.Add("@UserName", SqlDbType.NVarChar, 80).Value = (object)(userName ?? string.Empty);
                cmd.Parameters.Add("@IsSuccess", SqlDbType.Bit).Value = success;
                cmd.Parameters.Add("@MachineName", SqlDbType.NVarChar, 128).Value = Environment.MachineName;
                cmd.Parameters.Add("@ClientVersion", SqlDbType.NVarChar, 40).Value =
                    typeof(AuthenticationRepository).Assembly.GetName().Version.ToString();
                cmd.Parameters.Add("@FailureCode", SqlDbType.VarChar, 30).Value = (object)failureCode ?? DBNull.Value;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public UserSession LoadSession(DataRow credential)
        {
            long userId = Convert.ToInt64(credential["UserId"]);
            var permissions = new System.Collections.Generic.List<UserPermission>();
            var scopes = new System.Collections.Generic.List<UserDataScope>();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("SEC.usp_Account_GetAccessProfile", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userId;
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    var ds = new DataSet();
                    adapter.Fill(ds);
                    if (ds.Tables.Count > 0)
                        foreach (DataRow row in ds.Tables[0].Rows)
                            permissions.Add(new UserPermission {
                                FunctionCode = Convert.ToString(row["FunctionCode"]),
                                ActionCode = Convert.ToString(row["ActionCode"]),
                                IsAllowed = Convert.ToBoolean(row["IsAllowed"])
                            });
                    if (ds.Tables.Count > 1)
                        foreach (DataRow row in ds.Tables[1].Rows)
                            scopes.Add(new UserDataScope {
                                ScopeSource = Convert.ToString(row["ScopeSource"]),
                                FunctionCode = row["FunctionCode"] == DBNull.Value ? null : Convert.ToString(row["FunctionCode"]),
                                ScopeType = Convert.ToString(row["ScopeType"]),
                                ScopeValue = Convert.ToString(row["ScopeValue"]),
                                SiteCode = row["SiteCode"] == DBNull.Value ? null : Convert.ToString(row["SiteCode"]),
                                IsAllowed = Convert.ToBoolean(row["IsAllowed"])
                            });
                }
            }
            return new UserSession(userId, Convert.ToString(credential["UserName"]),
                Convert.ToString(credential["DisplayName"]),
                credential["EmployeeCode"] == DBNull.Value ? null : Convert.ToString(credential["EmployeeCode"]),
                Convert.ToBoolean(credential["MustChangePassword"]), permissions, scopes);
        }

        public long InitializeFirstAdmin(string userName, string displayName, byte[] salt, byte[] hash, int iterations)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("SEC.usp_Account_InitializeFirstAdmin", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@UserName", SqlDbType.NVarChar, 80).Value = userName;
                cmd.Parameters.Add("@DisplayName", SqlDbType.NVarChar, 160).Value = displayName;
                cmd.Parameters.Add("@PasswordSalt", SqlDbType.VarBinary, 32).Value = salt;
                cmd.Parameters.Add("@PasswordHash", SqlDbType.VarBinary, 32).Value = hash;
                cmd.Parameters.Add("@PasswordIterations", SqlDbType.Int).Value = iterations;
                cmd.Parameters.Add("@MachineName", SqlDbType.NVarChar, 128).Value = Environment.MachineName;
                cn.Open();
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        public void ChangePassword(long userId, byte[] currentHash, byte[] newSalt, byte[] newHash, int iterations, bool firstPassword)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("SEC.usp_Account_ChangePassword", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userId;
                cmd.Parameters.Add("@CurrentPasswordHash", SqlDbType.VarBinary, 32).Value = (object)currentHash ?? DBNull.Value;
                cmd.Parameters.Add("@NewPasswordSalt", SqlDbType.VarBinary, 32).Value = newSalt;
                cmd.Parameters.Add("@NewPasswordHash", SqlDbType.VarBinary, 32).Value = newHash;
                cmd.Parameters.Add("@PasswordIterations", SqlDbType.Int).Value = iterations;
                cmd.Parameters.Add("@IsFirstPassword", SqlDbType.Bit).Value = firstPassword;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}