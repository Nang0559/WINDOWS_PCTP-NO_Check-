using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Modules.MesPlanning
{
    public sealed class ProductionTraceRepository : IProductionTraceRepository
    {
        private readonly string _connectionString;

        public ProductionTraceRepository(string connectionString = null)
        {
            _connectionString = connectionString ??
                ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable GetTrace(string lotNo, string direction, string siteCode)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand("MES.usp_FVN_ComponentLot_Trace", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 90;
                command.Parameters.Add("@LotNo", SqlDbType.NVarChar, 120).Value = lotNo.Trim();
                command.Parameters.Add("@Direction", SqlDbType.VarChar, 12).Value = direction;
                command.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value =
                    string.IsNullOrWhiteSpace(siteCode) ? (object)DBNull.Value : siteCode.Trim();
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(command))
                    adapter.Fill(table);
                return table;
            }
        }
    }
}
