using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using PCTP.Infrastructure.Security;

namespace PCTP.Modules.MesPlanning
{
    /// <summary>Kết quả một lần nạp "Dòng chảy linh kiện": lưới theo PO/LotNo + số liệu tổng hợp cho 6 thẻ chặng.</summary>
    public sealed class ComponentFlowSnapshot
    {
        public DataTable Grid { get; set; }
        public DataRow Summary { get; set; }
    }

    public interface IComponentFlowRepository
    {
        DataTable GetGrid(string siteCode, DateTime? fromDate, DateTime? toDate);
        DataTable GetSummary(string siteCode, DateTime? fromDate, DateTime? toDate);
    }

    public interface IComponentFlowService
    {
        ComponentFlowSnapshot GetSnapshot(string siteCode, DateTime? fromDate, DateTime? toDate);
    }

    public sealed class ComponentFlowRepository : IComponentFlowRepository
    {
        private readonly string _connectionString;

        public ComponentFlowRepository(string connectionString = null)
        {
            _connectionString = connectionString ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable GetGrid(string siteCode, DateTime? fromDate, DateTime? toDate)
        {
            return Run("MES.usp_FVN_ComponentFlow_Grid", siteCode, fromDate, toDate);
        }

        public DataTable GetSummary(string siteCode, DateTime? fromDate, DateTime? toDate)
        {
            return Run("MES.usp_FVN_ComponentFlow_Summary", siteCode, fromDate, toDate);
        }

        private DataTable Run(string procedure, string siteCode, DateTime? fromDate, DateTime? toDate)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(procedure, cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 90;
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = siteCode;
                cmd.Parameters.Add("@FromDate", SqlDbType.Date).Value = (object)fromDate?.Date ?? DBNull.Value;
                cmd.Parameters.Add("@ToDate", SqlDbType.Date).Value = (object)toDate?.Date ?? DBNull.Value;
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }
    }

    /// <summary>
    /// Ranh giới chỉ đọc của "MES CONTROL TOWER - Dòng chảy linh kiện". Cần WAREHOUSE.COMPONENT_STOCK/VIEW và phạm vi
    /// dữ liệu theo Site (cùng quy tắc với tồn kho linh kiện PART). Không có thao tác ghi / chuyển trạng thái.
    /// </summary>
    public sealed class ComponentFlowService : IComponentFlowService
    {
        public const string Function = "WAREHOUSE.COMPONENT_STOCK";
        private readonly IComponentFlowRepository _repository;

        public ComponentFlowService(IComponentFlowRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public ComponentFlowSnapshot GetSnapshot(string siteCode, DateTime? fromDate, DateTime? toDate)
        {
            string site = (siteCode ?? string.Empty).Trim().ToUpperInvariant();
            if (site.Length == 0) throw new ArgumentException("SiteCode là bắt buộc.");
            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
                throw new ArgumentException("Từ ngày không được lớn hơn đến ngày.");

            AuthorizationGuard.Demand(Function, "VIEW");
            AuthorizationGuard.DemandScope(Function, "SITE", site, site);

            DataTable grid = _repository.GetGrid(site, fromDate, toDate);
            DataTable summary = _repository.GetSummary(site, fromDate, toDate);
            return new ComponentFlowSnapshot
            {
                Grid = grid,
                Summary = summary != null && summary.Rows.Count > 0 ? summary.Rows[0] : null
            };
        }
    }

    public static class MesComponentFlowComposition
    {
        public static IComponentFlowService Create()
        {
            return new ComponentFlowService(new ComponentFlowRepository());
        }
    }
}