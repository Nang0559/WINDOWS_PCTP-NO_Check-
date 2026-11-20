using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Reads <see cref="DocumentFormDefinition.StatusField"/> from the header view for the document a command targets.
    /// Convention: the command declares one Input parameter whose SourceFieldCode is the document key field;
    /// without it the status cannot be located and the gate fails closed.
    /// </summary>
    public sealed class SqlDocumentStatusReader : IDocumentStatusReader
    {
        private readonly string _connectionString;

        public SqlDocumentStatusReader(string connectionString = null)
        {
            _connectionString = connectionString ??
                ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public int ReadStatus(DocumentFormDefinition document, DocumentCommandDefinition command, string siteCode,
            IDictionary<string, object> input)
        {
            if (document.StatusField == null)
                throw new InvalidOperationException("Document " + document.ModuleCode + " has no registered status field.");
            var keyParameter = command.Parameters.FirstOrDefault(p => p.Source == DocumentParameterSource.Input &&
                string.Equals(p.SourceFieldCode, document.KeyField, StringComparison.OrdinalIgnoreCase));
            object key = null;
            if (keyParameter != null && input != null)
                input.TryGetValue(keyParameter.Name, out key);
            if (key == null || key == DBNull.Value)
                throw new InvalidOperationException("Cannot locate the document key for command " + command.CommandCode + ".");

            string sql = "SELECT " + DocumentIdentifiers.Bracket(document.StatusField) + " FROM " + document.HeaderView +
                " WHERE " + DocumentIdentifiers.Bracket(document.SiteField) + "=@Site AND " +
                DocumentIdentifiers.Bracket(document.KeyField) + "=@Key";
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add("@Site", SqlDbType.NVarChar, 30).Value = siteCode;
                cmd.Parameters.AddWithValue("@Key", key);
                cn.Open();
                object value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("Document not found or has no status.");
                return Convert.ToInt32(value);
            }
        }
    }
}
