using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Framework.Documents
{
    /// <summary>Reads sys.columns / sys.parameters for registered objects; names are parameters, never concatenated.</summary>
    public sealed class SqlDocumentSchemaProbe : IDocumentSchemaProbe
    {
        private readonly string _connectionString;

        public SqlDocumentSchemaProbe(string connectionString = null)
        {
            _connectionString = connectionString ??
                ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public IReadOnlyList<DocumentSqlColumn> GetViewColumns(string bracketedViewName)
        {
            return Read(bracketedViewName, "IsView",
                "SELECT c.name, t.name, c.precision, c.scale FROM sys.columns c " +
                "JOIN sys.types t ON t.user_type_id = c.user_type_id " +
                "WHERE c.object_id = OBJECT_ID(@Object) ORDER BY c.column_id");
        }

        public IReadOnlyList<DocumentSqlColumn> GetProcedureParameters(string bracketedProcedureName)
        {
            return Read(bracketedProcedureName, "IsProcedure",
                "SELECT p.name, t.name, p.precision, p.scale FROM sys.parameters p " +
                "JOIN sys.types t ON t.user_type_id = p.user_type_id " +
                "WHERE p.object_id = OBJECT_ID(@Object) AND p.parameter_id > 0 ORDER BY p.parameter_id");
        }

        private IReadOnlyList<DocumentSqlColumn> Read(string objectName, string kindProperty, string sql)
        {
            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var check = new SqlCommand(
                    "SELECT CASE WHEN OBJECTPROPERTY(OBJECT_ID(@Object), '" + kindProperty + "') = 1 THEN 1 ELSE 0 END", cn))
                {
                    check.Parameters.Add("@Object", SqlDbType.NVarChar, 300).Value = objectName;
                    if (Convert.ToInt32(check.ExecuteScalar()) != 1) return null;
                }
                var result = new List<DocumentSqlColumn>();
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@Object", SqlDbType.NVarChar, 300).Value = objectName;
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                            result.Add(new DocumentSqlColumn
                            {
                                Name = rd.GetString(0).TrimStart('@'),
                                TypeName = rd.GetString(1),
                                Precision = rd.GetByte(2),
                                Scale = rd.GetByte(3)
                            });
                }
                return result;
            }
        }
    }
}
