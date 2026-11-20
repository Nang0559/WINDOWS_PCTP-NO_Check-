using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Reads only the registered views and registered columns, always filtered by the authorized Site and,
    /// when the document registers a DepartmentField, by the user's DEPARTMENT data scope.
    /// SQL text is produced by <see cref="DocumentSqlBuilder"/> (trusted identifiers, parameterized values).
    /// </summary>
    public sealed class SqlDocumentDataProvider : IDocumentDataProvider
    {
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
            return ListHeaders(moduleCode, siteCode, DocumentListQuery.Latest(top)).Rows;
        }

        public DocumentPage ListHeaders(string moduleCode, string siteCode, DocumentListQuery query)
        {
            if (query == null) throw new ArgumentNullException("query");
            DocumentDataScope scope;
            var document = Demand(moduleCode, siteCode, out scope);
            siteCode = siteCode.Trim();
            var rows = DocumentSqlBuilder.BuildList(document, query, scope, false);
            var count = DocumentSqlBuilder.BuildList(document, query, scope, true);
            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                var page = new DocumentPage { PageIndex = query.PageIndex, PageSize = query.PageSize };
                using (var cmd = Command(cn, count, siteCode))
                    page.TotalCount = Convert.ToInt32(cmd.ExecuteScalar());
                page.Rows = Fill(Command(cn, rows, siteCode));
                return page;
            }
        }

        public DataTable GetHeader(string moduleCode, string siteCode, object key)
        {
            DocumentDataScope scope;
            var document = Demand(moduleCode, siteCode, out scope);
            return Fill(DocumentSqlBuilder.BuildHeaderByKey(document, key, scope), siteCode.Trim());
        }

        public DataTable GetDetail(string moduleCode, string siteCode, object key)
        {
            DocumentDataScope scope;
            var document = Demand(moduleCode, siteCode, out scope);
            return Fill(DocumentSqlBuilder.BuildDetailByKey(document, key, scope), siteCode.Trim());
        }

        private DocumentFormDefinition Demand(string moduleCode, string siteCode, out DocumentDataScope scope)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            var document = _registry.GetRequired(moduleCode);
            _authorizer.DemandView(document, siteCode.Trim());
            scope = _authorizer.GetDataScope(document, siteCode.Trim());
            if (scope == null) throw new UnauthorizedAccessException("Không xác định được phạm vi dữ liệu.");
            return document;
        }

        private DataTable Fill(DocumentSqlStatement statement, string siteCode)
        {
            using (var cn = new SqlConnection(_connectionString))
                return Fill(Command(cn, statement, siteCode));
        }

        private static DataTable Fill(SqlCommand cmd)
        {
            using (cmd)
            {
                var table = new DataTable("Document");
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }

        private static SqlCommand Command(SqlConnection cn, DocumentSqlStatement statement, string siteCode)
        {
            var cmd = new SqlCommand(statement.Sql, cn) { CommandTimeout = 60 };
            foreach (var p in statement.Parameters)
            {
                if (p.Name == DocumentSqlBuilder.SiteParameter)
                    cmd.Parameters.Add("@" + p.Name, SqlDbType.NVarChar, 30).Value = siteCode;
                else if (p.DbType == SqlDbType.Variant)
                    cmd.Parameters.AddWithValue("@" + p.Name, p.Value); // document key: CLR type decides the SQL type
                else
                {
                    var parameter = p.DbType == SqlDbType.Decimal
                        ? cmd.Parameters.Add("@" + p.Name, SqlDbType.Decimal)
                        : p.Size > 0 ? cmd.Parameters.Add("@" + p.Name, p.DbType, p.Size)
                                     : cmd.Parameters.Add("@" + p.Name, p.DbType);
                    if (p.DbType == SqlDbType.Decimal) { parameter.Precision = p.Precision; parameter.Scale = p.Scale; }
                    parameter.Value = p.Value ?? DBNull.Value;
                }
            }
            return cmd;
        }
    }
}
