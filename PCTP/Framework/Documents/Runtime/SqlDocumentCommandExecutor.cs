using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Framework.Documents
{
    /// <summary>Executes the registered stored procedure with only the declared, typed parameters.</summary>
    public sealed class SqlDocumentCommandExecutor : IDocumentCommandExecutor
    {
        private readonly string _connectionString;

        public SqlDocumentCommandExecutor(string connectionString = null)
        {
            _connectionString = connectionString ??
                ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable Execute(DocumentCommandDefinition command,
            IReadOnlyList<KeyValuePair<DocumentParameterDefinition, object>> values)
        {
            if (command == null) throw new ArgumentNullException("command");
            if (values == null) throw new ArgumentNullException("values");
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(command.ProcedureName, cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;
                foreach (var pair in values)
                {
                    var definition = pair.Key;
                    var parameter = cmd.Parameters.Add("@" + definition.Name, definition.DbType);
                    if (definition.Size > 0) parameter.Size = definition.Size;
                    if (definition.DbType == SqlDbType.Decimal)
                    {
                        parameter.Precision = definition.Precision;
                        parameter.Scale = definition.Scale;
                    }
                    parameter.Value = pair.Value;
                }
                var table = new DataTable("CommandResult");
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }
    }
}
