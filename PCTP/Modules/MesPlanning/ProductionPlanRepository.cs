using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
namespace PCTP.Modules.MesPlanning
{
    public sealed class ProductionPlanRepository : IProductionPlanRepository
    {
        private readonly string _connectionString;
        public ProductionPlanRepository(string connectionString = null)
        {
            _connectionString = connectionString ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString)) throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }
        public DataTable GetPlanLines(string siteCode)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ProductionPlan_List", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value =
                    string.IsNullOrWhiteSpace(siteCode) ? (object)DBNull.Value : siteCode.Trim();
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }

        public IList<string> GetActiveSites()
        {
            var sites = new List<string>();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("SELECT SiteCode FROM dbo.FVN_PCTPSite WHERE IsActive=1 ORDER BY SiteCode", cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read()) sites.Add(rd.GetString(0));
            }
            return sites;
        }

        public DataTable GetProductionLines(string siteCode)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(@"
SELECT m.PlanProductionLineCode AS LineCode, m.LotLineCode, m.DepartmentCode, pl.LineName
FROM MES.FVN_ProductionLotCodeMap m
LEFT JOIN dbo.FVN_ProductionLine pl ON pl.SiteCode=m.SiteCode AND pl.LineCode=m.LotLineCode
WHERE m.SiteCode=@SiteCode AND m.IsActive=1
ORDER BY m.PlanProductionLineCode;", cn))
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = (siteCode ?? string.Empty).Trim();
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }

        public void ImportRows(IList<ProductionPlanImportRow> rows, string sourceFileName, string createdBy)
        {
            if (rows == null || rows.Count == 0) throw new ArgumentException("Không có dòng kế hoạch để upload.");
            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        foreach (var row in rows)
                        {
                            using (var cmd = new SqlCommand("MES.usp_FVN_ProductionPlan_ImportRow", cn, tx))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.CommandTimeout = 60;
                                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = row.SiteCode;
                                cmd.Parameters.Add("@PlanNo", SqlDbType.NVarChar, 80).Value = row.PlanNo;
                                cmd.Parameters.Add("@PlanDate", SqlDbType.Date).Value = row.PlanDate.Date;
                                cmd.Parameters.Add("@LineNo", SqlDbType.Int).Value = row.LineNo;
                                cmd.Parameters.Add("@ShiftCode", SqlDbType.NVarChar, 20).Value = row.ShiftCode;
                                cmd.Parameters.Add("@ProductionLineCode", SqlDbType.NVarChar, 30).Value = row.ProductionLineCode;
                                cmd.Parameters.Add("@ProductPartNo", SqlDbType.NVarChar, 100).Value = row.ProductPartNo;
                                var qty = cmd.Parameters.Add("@PlannedQty", SqlDbType.Decimal); qty.Precision = 19; qty.Scale = 6; qty.Value = row.PlannedQty;
                                cmd.Parameters.Add("@UomCode", SqlDbType.NVarChar, 40).Value = row.UomCode;
                                cmd.Parameters.Add("@PlannedStartUtc", SqlDbType.DateTime2).Value = (object)row.PlannedStart ?? DBNull.Value;
                                cmd.Parameters.Add("@PlannedEndUtc", SqlDbType.DateTime2).Value = (object)row.PlannedEnd ?? DBNull.Value;
                                cmd.Parameters.Add("@SourceFileName", SqlDbType.NVarChar, 260).Value = (object)sourceFileName ?? DBNull.Value;
                                cmd.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = createdBy;
                                cmd.ExecuteScalar();
                            }
                        }
                        tx.Commit();
                    }
                    catch { try { tx.Rollback(); } catch { } throw; }
                }
            }
        }
    }
}