using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Framework.Documents
{
    public sealed class SqlDocumentMenuStore : IDocumentMenuStore, IDocumentMenuRuntimeStore
    {
        private const int ConfigVersionConflict = 55131;
        private readonly string _connectionString;

        public SqlDocumentMenuStore(string connectionString = null)
        {
            _connectionString = connectionString ??
                ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public IReadOnlyList<DocumentMenuEntry> ListConfigured()
        {
            var list = new List<DocumentMenuEntry>();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_UiScreenMenu_Runtime", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 30;
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                        list.Add(new DocumentMenuEntry
                        {
                            ModuleCode = rd.GetString(rd.GetOrdinal("ModuleCode")),
                            MenuGroup = rd.GetString(rd.GetOrdinal("MenuGroup")),
                            MenuText = rd.GetString(rd.GetOrdinal("MenuText")),
                            SequenceNo = rd.GetInt32(rd.GetOrdinal("SequenceNo")),
                            IsActive = rd.GetBoolean(rd.GetOrdinal("IsActive"))
                        });
            }
            return list;
        }

        public IReadOnlyList<DocumentMenuEntry> List(long actorUserId)
        {
            var list = new List<DocumentMenuEntry>();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_UiScreenMenu_List", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;
                cmd.Parameters.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                        list.Add(new DocumentMenuEntry
                        {
                            ModuleCode = rd.GetString(rd.GetOrdinal("ModuleCode")),
                            DisplayName = rd.IsDBNull(rd.GetOrdinal("DisplayName")) ? null : rd.GetString(rd.GetOrdinal("DisplayName")),
                            MenuGroup = rd.IsDBNull(rd.GetOrdinal("MenuGroup")) ? null : rd.GetString(rd.GetOrdinal("MenuGroup")),
                            MenuText = rd.IsDBNull(rd.GetOrdinal("MenuText")) ? null : rd.GetString(rd.GetOrdinal("MenuText")),
                            SequenceNo = rd.IsDBNull(rd.GetOrdinal("SequenceNo")) ? 100 : rd.GetInt32(rd.GetOrdinal("SequenceNo")),
                            IsActive = !rd.IsDBNull(rd.GetOrdinal("IsActive")) && rd.GetBoolean(rd.GetOrdinal("IsActive")),
                            ConfigVersion = rd.GetInt32(rd.GetOrdinal("ConfigVersion"))
                        });
            }
            return list;
        }

        public DocumentDesignSaveResult Save(long actorUserId, string actorCode, DocumentMenuEntry entry, int expectedConfigVersion)
        {
            if (entry == null) throw new ArgumentNullException("entry");
            try
            {
                using (var cn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand("MES.usp_FVN_UiScreenMenu_Save", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 60;
                    cmd.Parameters.Add("@ModuleCode", SqlDbType.VarChar, 80).Value = entry.ModuleCode;
                    cmd.Parameters.Add("@MenuGroup", SqlDbType.VarChar, 20).Value = entry.MenuGroup;
                    cmd.Parameters.Add("@MenuText", SqlDbType.NVarChar, 160).Value = entry.MenuText;
                    cmd.Parameters.Add("@SequenceNo", SqlDbType.Int).Value = entry.SequenceNo;
                    cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = entry.IsActive;
                    cmd.Parameters.Add("@ExpectedConfigVersion", SqlDbType.Int).Value = expectedConfigVersion;
                    cmd.Parameters.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                    cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actorCode ?? string.Empty;
                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) throw new InvalidOperationException("Procedure UiScreenMenu_Save returned no result.");
                        return new DocumentDesignSaveResult { ConfigVersion = rd.GetInt32(0), ChangedCount = rd.GetInt32(1) };
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == ConfigVersionConflict)
            {
                throw new DocumentConfigConflictException(ex.Message, ex);
            }
        }
    }
}