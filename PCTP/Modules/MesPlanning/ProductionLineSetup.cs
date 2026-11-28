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
    /// Từ migration 081 hai mã này chỉ được CHỌN trong danh sách đã khai báo (Mã chuyền 5 ↔ 3, Work Center ↔ mã bộ phận, Liên kết Work Center); SQL từ chối mã khác.
    /// </summary>
    public interface IProductionLineSetupRepository
    {
        DataTable List(string siteCode);

        /// <summary>Mã chuyền LOT (3 ký tự) đã khai báo cho chuyền IFS (màn Mã chuyền 5 ↔ 3 + Liên kết Work Center). Chỉ các giá trị này được chọn.</summary>
        DataTable ListLotLineChoices(string siteCode, string ifsProductionLine);

        /// <summary>Bộ phận (4 ký tự) đã liên kết Work Center thuộc chuyền LOT đã chọn (màn Liên kết Work Center). Chỉ các giá trị này được chọn.</summary>
        DataTable ListDepartmentChoices(string siteCode, string ifsProductionLine, string lotLineCode);

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
SELECT SiteCode, SiteName, SiteExistsInIfs, SiteIsActive,
       ProductionLineCode, Description, CalendarId, TaktTime, DailyOperatingMinutes,
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

        public DataTable ListLotLineChoices(string siteCode, string ifsProductionLine)
        {
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(@"
SELECT LineCode, LineName
FROM dbo.FVN_ProductionLine
WHERE SiteCode = @Site AND IsActive = 1 AND IfsProductionLineCode = @IfsLine
ORDER BY LineCode;", cn) { CommandTimeout = 60 })
            {
                cmd.Parameters.Add("@Site", SqlDbType.NVarChar, 30).Value = siteCode;
                cmd.Parameters.Add("@IfsLine", SqlDbType.NVarChar, 30).Value = ifsProductionLine;
                using (var da = new SqlDataAdapter(cmd)) da.Fill(table);
            }
            return table;
        }

        public DataTable ListDepartmentChoices(string siteCode, string ifsProductionLine, string lotLineCode)
        {
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(@"
SELECT d.DepartmentCode, d.DepartmentName, d.WorkCenterNo
FROM dbo.FVN_ProductionDepartment AS d
LEFT JOIN IFS.FVN_WorkCenter AS w ON w.SiteCode = d.SiteCode AND w.WorkCenterNo = d.WorkCenterNo
WHERE d.SiteCode = @Site AND d.IsActive = 1 AND d.LineCode = @LotLine AND d.WorkCenterNo IS NOT NULL
  AND (NULLIF(LTRIM(RTRIM(w.IfsProductionLine)), N'') IS NULL OR LTRIM(RTRIM(w.IfsProductionLine)) = @IfsLine)
ORDER BY d.DepartmentCode;", cn) { CommandTimeout = 60 })
            {
                cmd.Parameters.Add("@Site", SqlDbType.NVarChar, 30).Value = siteCode;
                cmd.Parameters.Add("@IfsLine", SqlDbType.NVarChar, 30).Value = ifsProductionLine;
                cmd.Parameters.Add("@LotLine", SqlDbType.VarChar, 3).Value = lotLineCode;
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

        public DataTable ListLotLineChoices(string siteCode, string ifsProductionLine)
        {
            string site = NormalizeSite(siteCode);
            AuthorizationGuard.DemandScope(Function, "SITE", site, site);
            return _repository.ListLotLineChoices(site, (ifsProductionLine ?? string.Empty).Trim());
        }

        public DataTable ListDepartmentChoices(string siteCode, string ifsProductionLine, string lotLineCode)
        {
            string site = NormalizeSite(siteCode);
            AuthorizationGuard.DemandScope(Function, "SITE", site, site);
            return _repository.ListDepartmentChoices(site, (ifsProductionLine ?? string.Empty).Trim(), (lotLineCode ?? string.Empty).Trim());
        }

        /// <summary>Gán / sửa / ngừng mapping IFS line → LOT line. Quyền CREATE + SITE thuộc MES.PRODUCTION_LINE_SETUP; SQL xác thực IFS site/line và quan hệ LOT line - department.</summary>
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
