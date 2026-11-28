using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using PCTP.Infrastructure.Security;

namespace PCTP.Modules.IfsSync
{
    public interface ISiteSetupRepository
    {
        /// <summary>Mọi Site: có trong IFS và/hoặc trong danh sách cho phép dbo.FVN_PCTPSite (dbo.vFVN_Site).</summary>
        DataTable List();

        /// <summary>Số chuyền / bộ phận / mapping đang dùng của Site, để cảnh báo trước khi tạm ngưng.</summary>
        DataTable GetUsage(string siteCode);

        /// <summary>Cho phép / bật / tạm ngưng / đổi tên một Site qua MES.usp_FVN_PCTPSite_Save.</summary>
        void Save(string siteCode, string siteName, bool isActive);
    }

    /// <summary>
    /// Quản lý danh sách Site PCTP được phép dùng (dbo.FVN_PCTPSite). Tên Site lấy từ IFS; Site chỉ được cho phép khi đã đồng bộ từ IFS.
    /// Quyền MES.SITE_SETUP (VIEW để xem, EDIT để thay đổi). Mọi thay đổi đi qua procedure có kiểm tra và audit, không xóa Site.
    /// </summary>
    public sealed class SiteSetupRepository : ISiteSetupRepository
    {
        public const string FunctionCode = "MES.SITE_SETUP";

        private readonly string _connectionString;

        public SiteSetupRepository(string connectionString = null)
        {
            _connectionString = connectionString
                ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable List()
        {
            AuthorizationGuard.Demand(FunctionCode, "VIEW");
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(@"
SELECT v.SiteCode, v.SiteName, fs.SiteName AS IfsSiteName, v.IsAllowListed, v.IsActive, v.IsPresentInIfs, v.SiteStatus, v.IfsSyncedAtUtc
FROM dbo.vFVN_Site v
LEFT JOIN IFS.FVN_Site fs ON fs.SiteCode = v.SiteCode
ORDER BY v.SiteCode;", cn) { CommandTimeout = 60 })
            using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
            return table;
        }

        public DataTable GetUsage(string siteCode)
        {
            AuthorizationGuard.Demand(FunctionCode, "VIEW");
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(@"
SELECT (SELECT COUNT(*) FROM dbo.FVN_ProductionLine WHERE SiteCode = @SiteCode AND IsActive = 1) AS ActiveLines,
       (SELECT COUNT(*) FROM dbo.FVN_ProductionDepartment WHERE SiteCode = @SiteCode AND IsActive = 1) AS ActiveDepartments,
       (SELECT COUNT(*) FROM MES.FVN_ProductionLotCodeMap WHERE SiteCode = @SiteCode AND IsActive = 1) AS ActiveLotCodeMaps;", cn))
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = RequireSite(siteCode);
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
            }
            return table;
        }

        public void Save(string siteCode, string siteName, bool isActive)
        {
            AuthorizationGuard.Demand(FunctionCode, "EDIT");
            string actor = AuthorizationGuard.ActorCode;
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_PCTPSite_Save", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 })
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = RequireSite(siteCode);
                cmd.Parameters.Add("@SiteName", SqlDbType.NVarChar, 120).Value =
                    string.IsNullOrWhiteSpace(siteName) ? (object)DBNull.Value : siteName.Trim();
                cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                try
                {
                    cn.Open();
                    cmd.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {
                    // THROW tuỳ biến của MES dùng mã 57xxx và có thông điệp nghiệp vụ.
                    if (ex.Number >= 50000 && ex.Number <= 59999) throw new InvalidOperationException(ex.Message, ex);
                    throw;
                }
            }
        }

        private static string RequireSite(string siteCode)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            return siteCode.Trim().ToUpperInvariant();
        }
    }
}
