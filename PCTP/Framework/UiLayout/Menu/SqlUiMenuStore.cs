using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Framework.UiLayout
{
    public interface IUiMenuStore
    {
        IList<UiMenuProfileInfo> ListProfiles(long actorUserId);
        IList<UiMenuRole> ListRoles(long actorUserId);
        UiMenuProfile Load(long actorUserId, string profileCode);
        void CreateProfile(long actorUserId, string actorCode, string profileCode, string displayName);
        /// <summary>Returns the new ConfigVersion. Throws <see cref="UiConfigConflictException"/> on a version mismatch.</summary>
        int Save(long actorUserId, string actorCode, UiMenuProfile profile, int expectedConfigVersion);
        /// <summary>Nodes of the profile that applies to the user (role profile, else DEFAULT). Empty = standard menu.</summary>
        IList<UiMenuNode> LoadRuntime(long actorUserId);
    }

    /// <summary>Calls migration 059 procedures. Not a security boundary: every designer procedure re-checks RBAC.</summary>
    public sealed class SqlUiMenuStore : IUiMenuStore
    {
        private const int ConfigVersionConflict = 59131;
        private readonly string _connectionString;

        public SqlUiMenuStore(string connectionString = null)
        {
            _connectionString = connectionString ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public IList<UiMenuProfileInfo> ListProfiles(long actorUserId)
        {
            var ds = Fill("MES.usp_FVN_UiMenuProfile_List", p => p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId);
            return ds.Tables[0].Rows.Cast<DataRow>().Select(r => new UiMenuProfileInfo
            {
                ProfileCode = (string)r["ProfileCode"],
                DisplayName = (string)r["DisplayName"],
                ConfigVersion = (int)r["ConfigVersion"],
                NodeCount = (int)r["NodeCount"],
                RoleCount = (int)r["RoleCount"]
            }).ToList();
        }

        public IList<UiMenuRole> ListRoles(long actorUserId)
        {
            var ds = Fill("MES.usp_FVN_UiMenuProfile_ListRoles", p => p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId);
            return ds.Tables[0].Rows.Cast<DataRow>().Select(r => new UiMenuRole
            {
                RoleCode = (string)r["RoleCode"],
                RoleName = (string)r["RoleName"]
            }).ToList();
        }

        public UiMenuProfile Load(long actorUserId, string profileCode)
        {
            var ds = Fill("MES.usp_FVN_UiMenuProfile_Load", p =>
            {
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                p.Add("@ProfileCode", SqlDbType.VarChar, 40).Value = profileCode ?? string.Empty;
            });
            var head = ds.Tables[0].Rows[0];
            return new UiMenuProfile
            {
                ProfileCode = (string)head["ProfileCode"],
                DisplayName = (string)head["DisplayName"],
                ConfigVersion = (int)head["ConfigVersion"],
                Nodes = ToNodes(ds.Tables[1]).ToList(),
                RoleCodes = ds.Tables[2].Rows.Cast<DataRow>().Select(r => (string)r["RoleCode"]).ToList()
            };
        }

        public void CreateProfile(long actorUserId, string actorCode, string profileCode, string displayName)
        {
            Fill("MES.usp_FVN_UiMenuProfile_Create", p =>
            {
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                p.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actorCode ?? string.Empty;
                p.Add("@ProfileCode", SqlDbType.VarChar, 40).Value = profileCode ?? string.Empty;
                p.Add("@DisplayName", SqlDbType.NVarChar, 120).Value = displayName ?? string.Empty;
            });
        }

        public int Save(long actorUserId, string actorCode, UiMenuProfile profile, int expectedConfigVersion)
        {
            string nodes = JsonConvert.SerializeObject(profile.Nodes.Select(n => new
            {
                n.NodeId,
                n.ParentNodeId,
                n.NodeType,
                n.ItemKey,
                n.Caption,
                n.SequenceNo
            }));
            string roles = JsonConvert.SerializeObject(profile.RoleCodes ?? new List<string>());
            try
            {
                var ds = Fill("MES.usp_FVN_UiMenuProfile_Save", p =>
                {
                    p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                    p.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actorCode ?? string.Empty;
                    p.Add("@ProfileCode", SqlDbType.VarChar, 40).Value = profile.ProfileCode;
                    p.Add("@ExpectedConfigVersion", SqlDbType.Int).Value = expectedConfigVersion;
                    p.Add("@NodesJson", SqlDbType.NVarChar, -1).Value = nodes;
                    p.Add("@RolesJson", SqlDbType.NVarChar, -1).Value = roles;
                });
                return (int)ds.Tables[0].Rows[0][0];
            }
            catch (SqlException ex) when (ex.Number == ConfigVersionConflict)
            {
                throw new UiConfigConflictException(ex.Message, ex);
            }
        }

        public IList<UiMenuNode> LoadRuntime(long actorUserId)
        {
            var ds = Fill("MES.usp_FVN_UiMenuProfile_Runtime", p => p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId);
            return ds.Tables.Count == 0 ? new List<UiMenuNode>() : ToNodes(ds.Tables[0]).ToList();
        }

        private static IEnumerable<UiMenuNode> ToNodes(DataTable table)
        {
            return table.Rows.Cast<DataRow>().Select(r => new UiMenuNode
            {
                NodeId = (int)r["NodeId"],
                ParentNodeId = r["ParentNodeId"] == DBNull.Value ? (int?)null : (int)r["ParentNodeId"],
                NodeType = (string)r["NodeType"],
                ItemKey = r["ItemKey"] == DBNull.Value ? null : (string)r["ItemKey"],
                Caption = r["Caption"] == DBNull.Value ? null : (string)r["Caption"],
                SequenceNo = (int)r["SequenceNo"]
            });
        }

        private DataSet Fill(string procedure, Action<SqlParameterCollection> parameters)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(procedure, cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;
                parameters(cmd.Parameters);
                var ds = new DataSet();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(ds);
                return ds;
            }
        }
    }
}
