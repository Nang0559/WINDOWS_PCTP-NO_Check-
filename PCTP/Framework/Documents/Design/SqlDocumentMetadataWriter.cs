using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Newtonsoft.Json;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Calls the layer-3 procedures of migration 051. It sends presentation columns only; the procedures re-check RBAC,
    /// ConfigVersion, update-only semantics and write the audit row, so this class is not the security boundary.
    /// </summary>
    public sealed class SqlDocumentMetadataWriter : IDocumentMetadataWriter
    {
        private const int ConfigVersionConflict = 55131;
        private readonly string _connectionString;

        public SqlDocumentMetadataWriter(string connectionString = null)
        {
            _connectionString = connectionString ??
                ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DocumentDesignSaveResult Save(long actorUserId, string actorCode, DocumentMetadata draft, int expectedConfigVersion)
        {
            if (draft == null) throw new ArgumentNullException("draft");
            string fields = JsonConvert.SerializeObject(draft.Fields.Select(f => new
            {
                f.RegionCode, f.FieldCode, f.Caption, f.SequenceNo, f.Width, f.DisplayFormat, f.IsVisible
            }));
            string commands = JsonConvert.SerializeObject(draft.Commands.Select(c => new
            {
                c.CommandCode, c.Caption, c.SequenceNo, c.IsVisible
            }));
            string layouts = JsonConvert.SerializeObject(draft.Layouts.Select(l => new
            {
                l.LayoutCode, l.Caption, l.SequenceNo, l.IsVisible
            }));
            return ExecuteScalarResult("MES.usp_FVN_DocumentMetadata_Save", p =>
            {
                p.Add("@ModuleCode", SqlDbType.VarChar, 80).Value = draft.ModuleCode;
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                p.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actorCode ?? string.Empty;
                p.Add("@ExpectedConfigVersion", SqlDbType.Int).Value = expectedConfigVersion;
                p.Add("@FieldsJson", SqlDbType.NVarChar, -1).Value = fields;
                p.Add("@CommandsJson", SqlDbType.NVarChar, -1).Value = commands;
                p.Add("@LayoutsJson", SqlDbType.NVarChar, -1).Value = layouts;
            });
        }

        public DocumentDesignSaveResult Provision(long actorUserId, string actorCode, DocumentMetadata seed)
        {
            if (seed == null) throw new ArgumentNullException("seed");
            string fields = JsonConvert.SerializeObject(seed.Fields.Select(f => new
            {
                f.RegionCode, f.FieldCode, f.EditorCode, f.Caption, f.SequenceNo, f.Width, f.DisplayFormat, f.IsVisible
            }));
            string commands = JsonConvert.SerializeObject(seed.Commands.Select(c => new
            {
                c.CommandCode, c.Caption, c.SequenceNo, c.IsVisible
            }));
            return ExecuteScalarResult("MES.usp_FVN_DocumentMetadata_Provision", p =>
            {
                p.Add("@ModuleCode", SqlDbType.VarChar, 80).Value = seed.ModuleCode;
                p.Add("@HeaderView", SqlDbType.NVarChar, 128).Value = seed.HeaderView;
                p.Add("@DetailView", SqlDbType.NVarChar, 128).Value = seed.DetailView;
                p.Add("@WorkflowCode", SqlDbType.VarChar, 80).Value = seed.WorkflowCode;
                p.Add("@DisplayName", SqlDbType.NVarChar, 160).Value = seed.DisplayName;
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                p.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actorCode ?? string.Empty;
                p.Add("@FieldsJson", SqlDbType.NVarChar, -1).Value = fields;
                p.Add("@CommandsJson", SqlDbType.NVarChar, -1).Value = commands;
            });
        }

        public DataTable ListAudit(long actorUserId, string moduleCode, int top)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_DocumentMetadata_ListAudit", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;
                cmd.Parameters.Add("@ModuleCode", SqlDbType.VarChar, 80).Value = moduleCode;
                cmd.Parameters.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                cmd.Parameters.Add("@Top", SqlDbType.Int).Value = top;
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }

        private DocumentDesignSaveResult ExecuteScalarResult(string procedure, Action<SqlParameterCollection> parameters)
        {
            try
            {
                using (var cn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(procedure, cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 60;
                    parameters(cmd.Parameters);
                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) throw new InvalidOperationException("Procedure " + procedure + " returned no result.");
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
