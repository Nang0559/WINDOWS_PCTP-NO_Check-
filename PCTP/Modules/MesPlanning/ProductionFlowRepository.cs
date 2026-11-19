using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
namespace PCTP.Modules.MesPlanning
{
    public sealed class ProductionFlowRepository : IProductionFlowRepository
    {
        private readonly string _connectionString;
        public ProductionFlowRepository(string connectionString = null)
        {
            _connectionString = connectionString ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public long GetChangeVersion()
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(
                "SELECT ChangeVersion FROM MES.FVN_ProductionFlowChangeVersion WHERE ChangeVersionId=1;", cn))
            {
                cmd.CommandTimeout = 5;
                cn.Open();
                object value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("Chưa cài migration 049 cho change-version MES.");
                return Convert.ToInt64(value);
            }
        }

        public DataTable GetDashboard(string siteCode, DateTime? fromDate, DateTime? toDate, string departmentScopeXml)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ProductionFlow_Dashboard", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 90;
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = siteCode;
                cmd.Parameters.Add("@FromDate", SqlDbType.Date).Value = (object)fromDate?.Date ?? DBNull.Value;
                cmd.Parameters.Add("@ToDate", SqlDbType.Date).Value = (object)toDate?.Date ?? DBNull.Value;
                cmd.Parameters.Add("@DepartmentScopeXml", SqlDbType.Xml).Value = departmentScopeXml;
                var table = new DataTable("ProductionFlow");
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }
    }
}