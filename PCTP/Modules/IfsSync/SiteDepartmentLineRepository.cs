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

        /// <summary>Chuyền IFS đang hoạt động của Site kèm mã chuyền LOT đã gắn (nếu có).</summary>
        DataTable GetIfsProductionLines(string siteCode);

        /// <summary>Mã chuyền LOT 3 ký tự tự sinh / đã gắn cho một chuyền IFS (chỉ xem trước, không ghi).</summary>
        string SuggestLotLineCode(string siteCode, string ifsProductionLine);

        /// <summary>Bảng đối chiếu mã chuyền 5 ký tự (chuẩn tham chiếu) ↔ 3 ký tự (dùng trong LOT) của Site.</summary>
        DataTable GetLineCodeCrossRef(string siteCode);

        /// <summary>
        /// Sinh cặp mã 5 / 3 ký tự. family (2-3 ký tự) = dãy máy đánh số; body (4 ký tự) = máy đơn lẻ; đúng một trong hai.
        /// preview = true: chỉ xem trước, không ghi. Trả về một dòng: LineCode5, LineCode3, FamilyCode, SeqNo.
        /// </summary>
        DataTable GenerateLineCode(string siteCode, string family, string body, string description,
            string ifsProductionLine, string lineCode3Override, bool preview);

        /// <summary>
        /// Liên kết Work Center. departmentCode / lineCode để trống = tự lấy từ IFS (WorkCenterNo đệm 0 thành 4 ký tự;
        /// mã chuyền LOT sinh từ chuyền IFS). Tên bộ phận / chuyền lấy từ IFS khi để trống.
        /// </summary>
        void Link(string siteCode, string workCenterNo, string departmentCode, string lineCode,
            string lineName, string departmentName, string ifsProductionLine);

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

        /// <summary>IFS WorkCenterNo đệm '0' phía trước thành 4 ký tự (19A → 019A). Null nếu dài hơn 4 hoặc có ký tự ngoài A-Z / 0-9 (ví dụ 1100E).</summary>
        public static string DepartmentCodeFromWorkCenter(string workCenterNo)
        {
            string n = (workCenterNo ?? string.Empty).Trim().ToUpperInvariant();
            if (n.Length == 0 || n.Length > 4) return null;
            foreach (char c in n)
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z'))) return null;
            return n.PadLeft(4, '0');
        }

        public DataTable GetLineCodeCrossRef(string siteCode)
        {
            string site = RequireSite(siteCode);
            AuthorizationGuard.DemandScope(FunctionCode, "SITE", site, site);
            return Query(@"
SELECT SiteCode, LineCode5, LineCode3, FamilyCode, SeqNo, Description, IfsProductionLineCode, IfsProductionLineDescription,
       LotLineName, LotLineStatus, IsLegacySeed, CreatedAtUtc, CreatedBy
FROM MES.vw_FVN_LineCodeCrossRef
WHERE SiteCode = @SiteCode
ORDER BY LineCode5;", site);
        }

        public DataTable GenerateLineCode(string siteCode, string family, string body, string description,
            string ifsProductionLine, string lineCode3Override, bool preview)
        {
            string site = RequireSite(siteCode);
            string actor = preview
                ? null
                : AuthorizationGuard.DemandActionAndScope(FunctionCode, "EDIT", "SITE", site, site).ActorCode;
            if (preview) AuthorizationGuard.DemandScope(FunctionCode, "SITE", site, site);

            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_LineCodeCrossRef_Generate", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                cmd.Parameters.Add("@FamilyCode", SqlDbType.VarChar, 3).Value = NullIfBlank(family);
                cmd.Parameters.Add("@Body", SqlDbType.VarChar, 4).Value = NullIfBlank(body);
                cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 200).Value = NullIfBlank(description);
                cmd.Parameters.Add("@IfsProductionLine", SqlDbType.NVarChar, 30).Value = NullIfBlank(ifsProductionLine);
                cmd.Parameters.Add("@LineCode3Override", SqlDbType.VarChar, 3).Value = NullIfBlank(lineCode3Override);
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = (object)actor ?? DBNull.Value;
                cmd.Parameters.Add("@Preview", SqlDbType.Bit).Value = preview;
                var table = new DataTable();
                try
                {
                    using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                }
                catch (SqlException ex)
                {
                    if (ex.Number >= 50000 && ex.Number <= 59999)
                        throw new InvalidOperationException(ex.Message, ex);
                    throw;
                }
                return table;
            }
        }

        /// <summary>Gợi ý 4 ký tự thân mã cho máy đơn lẻ từ mô tả: 2 chữ đầu của từ thứ nhất + 2 chữ đầu của từ thứ hai (bỏ 2W / 4W). Chỉ là gợi ý, người dùng sửa được.</summary>
        public static string SuggestBody(string description)
        {
            var words = new System.Collections.Generic.List<string>();
            foreach (string w in System.Text.RegularExpressions.Regex.Split((description ?? string.Empty).ToUpperInvariant(), "[^A-Z0-9]+"))
            {
                if (w.Length == 0 || w == "2W" || w == "4W") continue;
                words.Add(w);
            }
            if (words.Count == 0) return string.Empty;
            string a = words[0], b = words.Count > 1 ? words[1] : string.Empty;
            string body = b.Length == 0 ? a : (a.Length >= 2 ? a.Substring(0, 2) : a) + (b.Length >= 2 ? b.Substring(0, 2) : b);
            if (body.Length < 4 && words.Count > 2) body += words[2].Substring(0, 1);
            return body.Length > 4 ? body.Substring(0, 4) : body;
        }

        public DataTable GetIfsProductionLines(string siteCode)
        {
            string site = RequireSite(siteCode);
            AuthorizationGuard.DemandScope(FunctionCode, "SITE", site, site);
            return Query(@"
SELECT l.ProductionLineCode, l.Description,
       COALESCE(x.LineCode, m.LotLineCode) AS LotLineCode
FROM IFS.FVN_ProductionLine AS l
OUTER APPLY (SELECT TOP (1) p.LineCode FROM dbo.FVN_ProductionLine AS p
             WHERE p.SiteCode = l.SiteCode AND p.IfsProductionLineCode = l.ProductionLineCode) AS x
LEFT JOIN MES.FVN_ProductionLotCodeMap AS m
       ON m.SiteCode = l.SiteCode AND m.PlanProductionLineCode = l.ProductionLineCode AND m.IsActive = 1
WHERE l.SiteCode = @SiteCode AND l.IsActive = 1
ORDER BY l.ProductionLineCode;", site);
        }

        public string SuggestLotLineCode(string siteCode, string ifsProductionLine)
        {
            string site = RequireSite(siteCode);
            AuthorizationGuard.DemandScope(FunctionCode, "SITE", site, site);
            if (string.IsNullOrWhiteSpace(ifsProductionLine)) return null;

            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_LotLineCode_Resolve", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                cmd.Parameters.Add("@IfsProductionLine", SqlDbType.NVarChar, 30).Value = ifsProductionLine.Trim();
                SqlParameter result = cmd.Parameters.Add("@LotLineCode", SqlDbType.VarChar, 3);
                result.Direction = ParameterDirection.Output;
                Execute(cn, cmd);
                return result.Value == null || result.Value == DBNull.Value ? null : Convert.ToString(result.Value).Trim();
            }
        }

        public void Link(string siteCode, string workCenterNo, string departmentCode, string lineCode,
            string lineName, string departmentName, string ifsProductionLine)
        {
            string site = RequireSite(siteCode);
            string actor = AuthorizationGuard.DemandActionAndScope(FunctionCode, "EDIT", "SITE", site, site).ActorCode;

            if (string.IsNullOrWhiteSpace(workCenterNo))
                throw new ArgumentException("Chọn Work Center IFS.", "workCenterNo");
            string dept = (departmentCode ?? string.Empty).Trim();
            string line = (lineCode ?? string.Empty).Trim();
            // Để trống = tự lấy từ IFS: bộ phận = WorkCenterNo đệm 0, chuyền LOT = tự sinh từ chuyền IFS (SQL cấp trong transaction).
            if (dept.Length == 0) dept = DepartmentCodeFromWorkCenter(workCenterNo) ?? string.Empty;
            if (dept.Length == 0)
                throw new ArgumentException("Work Center " + workCenterNo.Trim() + " không đổi được thành mã bộ phận 4 ký tự (dài hơn 4 hoặc có ký tự đặc biệt); nhập mã bộ phận.", "departmentCode");
            if (dept.Length != 4)
                throw new ArgumentException("Mã Department phải đúng 4 ký tự (một phần của LOT 20 ký tự).", "departmentCode");
            if (line.Length != 0 && line.Length != 3)
                throw new ArgumentException("Mã Line phải đúng 3 ký tự (một phần của LOT 20 ký tự).", "lineCode");
            if (line.Length != 3 && string.IsNullOrWhiteSpace(ifsProductionLine))
                throw new ArgumentException("Chọn chuyền IFS để hệ thống tự sinh mã chuyền LOT.", "ifsProductionLine");

            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_SiteDepartmentLine_Link", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                cmd.Parameters.Add("@WorkCenterNo", SqlDbType.NVarChar, 30).Value = workCenterNo.Trim();
                cmd.Parameters.Add("@DepartmentCode", SqlDbType.VarChar, 4).Value = dept;
                cmd.Parameters.Add("@LineCode", SqlDbType.VarChar, 3).Value = line.Length == 0 ? (object)DBNull.Value : line;
                cmd.Parameters.Add("@LineName", SqlDbType.NVarChar, 120).Value = NullIfBlank(lineName);
                cmd.Parameters.Add("@DepartmentName", SqlDbType.NVarChar, 120).Value = NullIfBlank(departmentName);
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                cmd.Parameters.Add("@IfsProductionLine", SqlDbType.NVarChar, 30).Value = NullIfBlank(ifsProductionLine);
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
