using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using PCTP.Infrastructure.Security;

namespace PCTP.Modules.MesPlanning
{
    /// <summary>
    /// Khai báo chuyền sản xuất của một Site: chuyền gốc lấy từ IFS (IFS.FVN_ProductionLine, đồng bộ PRODUCTION_LINE),
    /// PCTP chỉ gán thêm mã chuyền LOT (3 ký tự) và mã bộ phận (4 ký tự) vào MES.FVN_ProductionLotCodeMap (migration 067).
    /// </summary>
    public interface IProductionLineSetupRepository
    {
        DataTable List(string siteCode);
        void Save(string siteCode, string planLineCode, string lotLineCode, string departmentCode, bool isActive, string actor);
    }

    public sealed class ProductionLineSetupRepository : IProductionLineSetupRepository
    {
        private readonly string _connectionString;

        public ProductionLineSetupRepository(string connectionString = null)
        {
            _connectionString = connectionString ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable List(string siteCode)
        {
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(@"
SELECT SiteCode, ProductionLineCode, Description, CalendarId, TaktTime, DailyOperatingMinutes,
       ExistsInIfs, IsKnownSite, LotLineCode, DepartmentCode, IsMapped, MapIsActive, SyncedAtUtc
FROM MES.vFVN_ProductionLineSetup
WHERE SiteCode = @Site
ORDER BY ProductionLineCode;", cn) { CommandTimeout = 60 })
            {
                cmd.Parameters.Add("@Site", SqlDbType.NVarChar, 30).Value = siteCode;
                using (var da = new SqlDataAdapter(cmd)) da.Fill(table);
            }
            return table;
        }

        public void Save(string siteCode, string planLineCode, string lotLineCode, string departmentCode, bool isActive, string actor)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ProductionLotCodeMap_Save", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 })
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = siteCode;
                cmd.Parameters.Add("@PlanProductionLineCode", SqlDbType.NVarChar, 30).Value = planLineCode;
                cmd.Parameters.Add("@LotLineCode", SqlDbType.VarChar, 3).Value = lotLineCode ?? string.Empty;
                cmd.Parameters.Add("@DepartmentCode", SqlDbType.VarChar, 4).Value = departmentCode ?? string.Empty;
                cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }

    public sealed class ProductionLineSetupService
    {
        private const string Function = "MES.PRODUCTION_LINE_SETUP";
        private readonly IProductionLineSetupRepository _repository;

        public ProductionLineSetupService(IProductionLineSetupRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public DataTable List(string siteCode)
        {
            string site = NormalizeSite(siteCode);
            AuthorizationGuard.DemandScope(Function, "SITE", site, site);
            return _repository.List(site);
        }

        /// <summary>Gán / sửa / ngừng một chuyền. Cần CREATE trên MES.PLAN_UPLOAD theo Site; SQL kiểm lại chuyền có trong IFS và ràng buộc chuyền LOT - bộ phận.</summary>
        public void Save(string siteCode, string planLineCode, string lotLineCode, string departmentCode, bool isActive)
        {
            string site = NormalizeSite(siteCode);
            string actor = AuthorizationGuard.ActorCode;
            AuthorizationGuard.DemandActionAndScope(Function, "CREATE", "SITE", site, site);

            string line = (planLineCode ?? string.Empty).Trim();
            string lot = (lotLineCode ?? string.Empty).Trim();
            string dept = (departmentCode ?? string.Empty).Trim();
            if (line.Length == 0) throw new ArgumentException("Thiếu mã chuyền IFS.");
            if (isActive && lot.Length != 3) throw new ArgumentException("Mã chuyền LOT phải đúng 3 ký tự.");
            if (isActive && dept.Length != 4) throw new ArgumentException("Mã bộ phận phải đúng 4 ký tự.");
            _repository.Save(site, line, lot, dept, isActive, actor);
        }

        private static string NormalizeSite(string siteCode)
        {
            string site = (siteCode ?? string.Empty).Trim().ToUpperInvariant();
            if (site.Length == 0) throw new ArgumentException("Thiếu SiteCode.");
            return site;
        }
    }
}
