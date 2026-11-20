using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Reads only the registered views and registered columns, always filtered by the authorized Site.
    /// View, column and key names come from the trusted definition; values are always parameters.
    /// </summary>
    public sealed class SqlDocumentDataProvider : IDocumentDataProvider
    {
        private const int MaxRows = 5000;
        private readonly IDocumentRegistry _registry;
        private readonly IDocumentAuthorizer _authorizer;
        private readonly string _connectionString;

        public SqlDocumentDataProvider(IDocumentRegistry registry, IDocumentAuthorizer authorizer, string connectionString = null)
        {
            if (registry == null) throw new ArgumentNullException("registry");
            if (authorizer == null) throw new ArgumentNullException("authorizer");
            _registry = registry;
            _authorizer = authorizer;
            _connectionString = connectionString ??
                ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable ListHeaders(string moduleCode, string siteCode, int top)
        {
            var document = Demand(moduleCode, siteCode);
            int limit = Math.Max(1, Math.Min(MaxRows, top));
            string sql = "SELECT TOP (@Top) " + Columns(document, DocumentRegion.Header) +
                " FROM " + document.HeaderView +
                " WHERE " + DocumentIdentifiers.Bracket(document.SiteField) + "=@Site" +
                " ORDER BY " + DocumentIdentifiers.Bracket(document.KeyField) + " DESC";
            return Fill(sql, siteCode, null, limit);
        }

        public DataTable GetHeader(string moduleCode, string siteCode, object key)
        {
            var document = Demand(moduleCode, siteCode);
            RequireKey(key);
            return Fill(ByKey(document, document.HeaderView, DocumentRegion.Header), siteCode, key, null);
        }

        public DataTable GetDetail(string moduleCode, string siteCode, object key)
        {
            var document = Demand(moduleCode, siteCode);
            RequireKey(key);
            return Fill(ByKey(document, document.DetailView, DocumentRegion.Detail), siteCode, key, null);
        }

        private DocumentFormDefinition Demand(string moduleCode, string siteCode)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            var document = _registry.GetRequired(moduleCode);
            _authorizer.DemandView(document, siteCode.Trim());
            return document;
        }

        private static void RequireKey(object key)
        {
            if (key == null || key == DBNull.Value) throw new ArgumentException("Khóa chứng từ là bắt buộc.", "key");
        }

        private static string Columns(DocumentFormDefinition document, DocumentRegion region)
        {
            return string.Join(",", document.FieldsOf(region).Select(f => DocumentIdentifiers.Bracket(f.FieldCode)));
        }

        private static string ByKey(DocumentFormDefinition document, string view, DocumentRegion region)
        {
            return "SELECT " + Columns(document, region) + " FROM " + view +
                " WHERE " + DocumentIdentifiers.Bracket(document.SiteField) + "=@Site" +
                " AND " + DocumentIdentifiers.Bracket(document.KeyField) + "=@Key";
        }

        private DataTable Fill(string sql, string siteCode, object key, int? top)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.CommandTimeout = 60;
                cmd.Parameters.Add("@Site", SqlDbType.NVarChar, 30).Value = siteCode.Trim();
                if (key != null) cmd.Parameters.AddWithValue("@Key", key);
                if (top.HasValue) cmd.Parameters.Add("@Top", SqlDbType.Int).Value = top.Value;
                var table = new DataTable("Document");
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }
    }
}
