using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using PCTP.Infrastructure.Security;

namespace PCTP.Modules.IfsSync
{
    public interface ISiteDepartmentLineRepository
    {
        /// <summary>Site → Line → Department → Work Center đã liên kết.</summary>
        DataTable GetLinks(string siteCode);

        /// <summary>Work Center IFS và gợi ý Line/Department (chưa liên kết thì LinkStatus = UNLINKED).</summary>
        DataTable GetCandidates(string siteCode);

        /// <summary>Các dòng LOT code map không còn khớp Line/Department (rỗng = hợp lệ).</summary>
        DataTable GetLotCodeMapIssues(string siteCode);

        /// <summary>Các bất nhất dữ liệu gốc của Site (MES.vw_FVN_MasterDataConsistency), kể cả Work Center ↔ chuyền IFS ↔ mapping.</summary>
        DataTable GetConsistency(string siteCode);

        void Link(string siteCode, string workCenterNo, string departmentCode, string lineCode,
            string lineName, string departmentName);

        void Unlink(string siteCode, string departmentCode);
    }

    /// <summary>
    /// Định nghĩa liên kết Site – Line – Department (Work Center IFS) trong FVN_PCTP.
    /// Mọi thay đổi đi qua procedure có kiểm tra và audit; lớp này chỉ gọi procedure cố định
    /// sau khi kiểm tra quyền SEC + phạm vi Site.
    /// </summary>
    public sealed class SiteDepartmentLineRepository : ISiteDepartmentLineRepository
    {
        public const string FunctionCode = "MES.SITE_DEPARTMENT_LINE";

        private readonly string _connectionString;

        public SiteDepartmentLineRepository(string connectionString = null)
        {
            _connectionString = connectionString
                ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable GetLinks(string siteCode)
        {
            string site = RequireSite(siteCode);
            AuthorizationGuard.DemandScope(FunctionCode, "SITE", site, site);
            return Query(@"
SELECT SiteCode, SiteName, LineCode, LineName, DepartmentCode, DepartmentName, DepartmentIsActive,
       WorkCenterNo, WorkCenterDescription, LinkStatus, LinkedAtUtc, LinkedBy
FROM MES.vw_FVN_SiteDepartmentLine
WHERE SiteCode = @SiteCode
ORDER BY LineCode, DepartmentCode;", site);
        }

        public DataTable GetCandidates(string siteCode)
        {
            string site = RequireSite(siteCode);
            AuthorizationGuard.DemandScope(FunctionCode, "SITE", site, site);
            return Query(@"
SELECT SiteCode, WorkCenterNo, Description, WorkCenterCode, IsPresentInIfs,
       IfsProductionLine, IfsDepartmentNo, SuggestedLineCode, SuggestedDepartmentCode,
       LinkedDepartmentCode, LinkedLineCode, LinkStatus, SiteIsAllowListed
FROM MES.vw_FVN_WorkCenterLinkCandidates
WHERE SiteCode = @SiteCode
ORDER BY WorkCenterNo;", site);
        }

        public DataTable GetLotCodeMapIssues(string siteCode)
        {
            string site = RequireSite(siteCode);
            AuthorizationGuard.DemandScope(FunctionCode, "SITE", site, site);
            return Query(@"
SELECT SiteCode, PlanProductionLineCode, LotLineCode, DepartmentCode, IntegrityStatus
FROM MES.vw_FVN_LotCodeMapIntegrity
WHERE SiteCode = @SiteCode AND IntegrityStatus <> 'OK'
ORDER BY PlanProductionLineCode;", site);
        }

        public DataTable GetConsistency(string siteCode)
        {
            string site = RequireSite(siteCode);
            AuthorizationGuard.DemandScope(FunctionCode, "SITE", site, site);
            return Query(@"
SELECT IssueCode, Severity, SiteCode, ObjectKey, Detail
FROM MES.vw_FVN_MasterDataConsistency
WHERE SiteCode = @SiteCode
ORDER BY CASE Severity WHEN 'ERROR' THEN 0 WHEN 'WARN' THEN 1 ELSE 2 END, IssueCode, ObjectKey;", site);
        }

        public void Link(string siteCode, string workCenterNo, string departmentCode, string lineCode,
            string lineName, string departmentName)
        {
            string site = RequireSite(siteCode);
            string actor = AuthorizationGuard.DemandActionAndScope(FunctionCode, "EDIT", "SITE", site, site).ActorCode;

            if (string.IsNullOrWhiteSpace(workCenterNo))
                throw new ArgumentException("Chọn Work Center IFS.", "workCenterNo");
            string dept = (departmentCode ?? string.Empty).Trim();
            string line = (lineCode ?? string.Empty).Trim();
            if (dept.Length != 4)
                throw new ArgumentException("Mã Department phải đúng 4 ký tự (một phần của LOT 20 ký tự).", "departmentCode");
            if (line.Length != 3)
                throw new ArgumentException("Mã Line phải đúng 3 ký tự (một phần của LOT 20 ký tự).", "lineCode");

            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_SiteDepartmentLine_Link", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                cmd.Parameters.Add("@WorkCenterNo", SqlDbType.NVarChar, 30).Value = workCenterNo.Trim();
                cmd.Parameters.Add("@DepartmentCode", SqlDbType.VarChar, 4).Value = dept;
                cmd.Parameters.Add("@LineCode", SqlDbType.VarChar, 3).Value = line;
                cmd.Parameters.Add("@LineName", SqlDbType.NVarChar, 120).Value = NullIfBlank(lineName);
                cmd.Parameters.Add("@DepartmentName", SqlDbType.NVarChar, 120).Value = NullIfBlank(departmentName);
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                Execute(cn, cmd);
            }
        }

        public void Unlink(string siteCode, string departmentCode)
        {
            string site = RequireSite(siteCode);
            string actor = AuthorizationGuard.DemandActionAndScope(FunctionCode, "EDIT", "SITE", site, site).ActorCode;

            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_SiteDepartmentLine_Unlink", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                cmd.Parameters.Add("@DepartmentCode", SqlDbType.VarChar, 4).Value = (departmentCode ?? string.Empty).Trim();
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                Execute(cn, cmd);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static string RequireSite(string siteCode)
        {
            if (string.IsNullOrWhiteSpace(siteCode))
                throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            return siteCode.Trim();
        }

        private static object NullIfBlank(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private DataTable Query(string sql, string siteCode)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = siteCode;
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }

        private static void Execute(SqlConnection cn, SqlCommand cmd)
        {
            try
            {
                cn.Open();
                cmd.ExecuteNonQuery();
            }
            catch (SqlException ex)
            {
                // THROW tuỳ biến của MES dùng mã 56xxx và có thông điệp nghiệp vụ.
                if (ex.Number >= 50000 && ex.Number <= 59999)
                    throw new InvalidOperationException(ex.Message, ex);
                throw;
            }
        }
    }
}
