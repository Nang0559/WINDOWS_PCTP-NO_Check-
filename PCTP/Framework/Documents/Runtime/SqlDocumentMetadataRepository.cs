using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Framework.Documents
{
    /// <summary>Read-only, fully parameterized loader for layer-2 metadata (migration 039 tables).</summary>
    public sealed class SqlDocumentMetadataRepository : IDocumentMetadataRepository
    {
        private readonly string _connectionString;

        public SqlDocumentMetadataRepository(string connectionString = null)
        {
            _connectionString = connectionString ??
                ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DocumentMetadata Load(string moduleCode)
        {
            if (string.IsNullOrWhiteSpace(moduleCode)) throw new ArgumentException("moduleCode is required.", "moduleCode");
            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                DocumentMetadata metadata = null;
                using (var cmd = new SqlCommand(
                    "SELECT ModuleCode,HeaderView,DetailView,WorkflowCode,DisplayName,ConfigVersion " +
                    "FROM MES.FVN_DocumentDefinition WHERE ModuleCode=@ModuleCode AND IsActive=1", cn))
                {
                    cmd.Parameters.Add("@ModuleCode", SqlDbType.VarChar, 80).Value = moduleCode.Trim();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) return null;
                        metadata = new DocumentMetadata
                        {
                            ModuleCode = rd.GetString(0),
                            HeaderView = rd.GetString(1),
                            DetailView = rd.GetString(2),
                            WorkflowCode = rd.GetString(3),
                            DisplayName = rd.GetString(4),
                            ConfigVersion = rd.GetInt32(5)
                        };
                    }
                }
                using (var cmd = new SqlCommand(
                    "SELECT FieldCode,RegionCode,Caption,SequenceNo,Width,DisplayFormat,EditorCode,IsVisible,IsReadOnly,IsRequired,LookupCode " +
                    "FROM MES.FVN_DocumentField WHERE ModuleCode=@ModuleCode ORDER BY RegionCode,SequenceNo", cn))
                {
                    cmd.Parameters.Add("@ModuleCode", SqlDbType.VarChar, 80).Value = moduleCode.Trim();
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                            metadata.Fields.Add(new DocumentFieldMetadata
                            {
                                FieldCode = rd.GetString(0),
                                RegionCode = rd.GetString(1),
                                Caption = rd.GetString(2),
                                SequenceNo = rd.GetInt32(3),
                                Width = rd.IsDBNull(4) ? (int?)null : rd.GetInt32(4),
                                DisplayFormat = rd.IsDBNull(5) ? null : rd.GetString(5),
                                EditorCode = rd.GetString(6),
                                IsVisible = rd.GetBoolean(7),
                                IsReadOnly = rd.GetBoolean(8),
                                IsRequired = rd.GetBoolean(9),
                                LookupCode = rd.IsDBNull(10) ? null : rd.GetString(10)
                            });
                }
                using (var cmd = new SqlCommand(
                    "SELECT CommandCode,Caption,SequenceNo,IsVisible,RequiredStatusCode " +
                    "FROM MES.FVN_DocumentCommand WHERE ModuleCode=@ModuleCode ORDER BY SequenceNo", cn))
                {
                    cmd.Parameters.Add("@ModuleCode", SqlDbType.VarChar, 80).Value = moduleCode.Trim();
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                            metadata.Commands.Add(new DocumentCommandMetadata
                            {
                                CommandCode = rd.GetString(0),
                                Caption = rd.GetString(1),
                                SequenceNo = rd.GetInt32(2),
                                IsVisible = rd.GetBoolean(3),
                                RequiredStatusCode = rd.IsDBNull(4) ? (int?)null : rd.GetInt32(4)
                            });
                }
                // LayoutJson is intentionally not read: v1 consumes only caption/visibility of HEADER and DETAIL.
                using (var cmd = new SqlCommand(
                    "SELECT LayoutCode,RegionCode,Caption,SequenceNo,IsVisible " +
                    "FROM MES.FVN_DocumentLayout WHERE ModuleCode=@ModuleCode ORDER BY SequenceNo", cn))
                {
                    cmd.Parameters.Add("@ModuleCode", SqlDbType.VarChar, 80).Value = moduleCode.Trim();
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                            metadata.Layouts.Add(new DocumentLayoutMetadata
                            {
                                LayoutCode = rd.GetString(0),
                                RegionCode = rd.GetString(1),
                                Caption = rd.IsDBNull(2) ? null : rd.GetString(2),
                                SequenceNo = rd.GetInt32(3),
                                IsVisible = rd.GetBoolean(4)
                            });
                }
                return metadata;
            }
        }
    }
}
