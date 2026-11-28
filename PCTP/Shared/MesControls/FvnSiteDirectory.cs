using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using PCTP.Framework.Documents;
using PCTP.Security;

namespace PCTP.Shared.MesControls
{
    /// <summary>
    /// Nguồn Site chuẩn cho mọi màn hình MES / Kho linh kiện.
    /// Danh sách lấy từ <c>dbo.vFVN_Site</c> (hợp nhất <c>dbo.FVN_PCTPSite</c> với <c>IFS.FVN_Site</c>) và chỉ giữ Site có
    /// <c>SiteStatus = 'OK'</c>: đã được cho phép, đang hoạt động và còn trong IFS. Tên Site hiển thị là tên từ IFS.
    /// Với một chức năng, danh sách còn được giới hạn theo phạm vi dữ liệu của tài khoản qua <see cref="DocumentSiteResolver"/>
    /// (đọc Site ở cả cột SiteCode lẫn ScopeValue của scope SITE và bung scope ALL).
    /// </summary>
    public static class FvnSiteDirectory
    {
        /// <summary>Mã dòng "Tất cả Site" (chỉ có khi form cho phép lọc toàn bộ Site trong phạm vi).</summary>
        public const string AllSiteCode = "*";

        public const string UsableSitesSql =
            "SELECT SiteCode, SiteName FROM dbo.vFVN_Site WHERE SiteStatus = N'OK' ORDER BY SiteCode";

        /// <summary>Bảng nguồn cho lookup: SiteCode (giá trị), SiteName (tên IFS), SiteShort (chữ hiện trong ô chọn).</summary>
        public static DataTable CreateTable()
        {
            var table = new DataTable("Site");
            table.Columns.Add("SiteCode", typeof(string));
            table.Columns.Add("SiteName", typeof(string));
            table.Columns.Add("SiteShort", typeof(string));
            return table;
        }

        /// <summary>
        /// Dựng bảng lookup từ các cặp (mã, tên). <paramref name="allowedCodes"/> = null nghĩa là không giới hạn;
        /// ngược lại chỉ giữ mã có trong danh sách (không phân biệt hoa thường). Thứ tự theo mã Site.
        /// </summary>
        public static DataTable Build(IEnumerable<KeyValuePair<string, string>> sites, IEnumerable<string> allowedCodes, bool includeAll)
        {
            HashSet<string> allowed = allowedCodes == null
                ? null
                : new HashSet<string>(allowedCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()),
                    StringComparer.OrdinalIgnoreCase);
            var table = CreateTable();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in (sites ?? Enumerable.Empty<KeyValuePair<string, string>>())
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .OrderBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                string code = pair.Key.Trim();
                if (allowed != null && !allowed.Contains(code)) continue;
                if (!seen.Add(code)) continue;
                table.Rows.Add(code, string.IsNullOrWhiteSpace(pair.Value) ? string.Empty : pair.Value.Trim(), code);
            }
            if (includeAll && table.Rows.Count > 0)
            {
                DataRow all = table.NewRow();
                all["SiteCode"] = AllSiteCode;
                all["SiteName"] = "Tất cả Site trong phạm vi";
                all["SiteShort"] = "Tất cả";
                table.Rows.InsertAt(all, 0);
            }
            table.AcceptChanges();
            return table;
        }

        /// <summary>Đọc mọi Site dùng được (SiteStatus = OK) từ <c>dbo.vFVN_Site</c>: cặp (SiteCode, SiteName IFS).</summary>
        public static IList<KeyValuePair<string, string>> LoadUsableSites()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["FVN_PCTP"] == null
                ? null
                : ConfigurationManager.ConnectionStrings["FVN_PCTP"].ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");

            var list = new List<KeyValuePair<string, string>>();
            using (var cn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(UsableSitesSql, cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                        list.Add(new KeyValuePair<string, string>(
                            Convert.ToString(rd["SiteCode"]),
                            rd["SiteName"] == DBNull.Value ? string.Empty : Convert.ToString(rd["SiteName"])));
            }
            return list;
        }

        /// <summary>
        /// Bảng lookup cho một chức năng: Site dùng được (vFVN_Site) ∩ Site tài khoản được xem
        /// (<see cref="DocumentSiteResolver"/> + <see cref="UserSession.HasDataScope"/>). Ném lỗi khi chưa đăng nhập / lỗi SQL.
        /// </summary>
        public static DataTable ForFunction(string functionCode, bool includeAll)
        {
            UserSession session = UserSession.Current;
            if (session == null) throw new InvalidOperationException("Chưa đăng nhập.");
            IList<KeyValuePair<string, string>> usable = LoadUsableSites();
            IReadOnlyList<string> allowed = DocumentSiteResolver.Candidates(
                session, functionCode, () => usable.Select(x => x.Key).ToList());
            return Build(usable, allowed, includeAll);
        }

        /// <summary>Như <see cref="ForFunction"/> nhưng Site phải nằm trong phạm vi của TẤT CẢ các chức năng (màn hình dùng nhiều quyền).</summary>
        public static DataTable ForFunctions(IEnumerable<string> functionCodes, bool includeAll)
        {
            UserSession session = UserSession.Current;
            if (session == null) throw new InvalidOperationException("Chưa đăng nhập.");
            string[] codes = (functionCodes ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (codes.Length == 0) throw new ArgumentException("Cần ít nhất một mã chức năng.", "functionCodes");
            IList<KeyValuePair<string, string>> usable = LoadUsableSites();
            IEnumerable<string> allowed = null;
            foreach (string function in codes)
            {
                IReadOnlyList<string> sites = DocumentSiteResolver.Candidates(
                    session, function, () => usable.Select(x => x.Key).ToList());
                allowed = allowed == null
                    ? (IEnumerable<string>)sites
                    : allowed.Intersect(sites, StringComparer.OrdinalIgnoreCase).ToList();
            }
            return Build(usable, allowed, includeAll);
        }

        /// <summary>Bảng lookup cho danh sách mã Site mà Application Service đã lọc quyền sẵn.</summary>
        public static DataTable ForCodes(IEnumerable<string> authorizedCodes, bool includeAll)
        {
            return Build(LoadUsableSites(), authorizedCodes ?? Enumerable.Empty<string>(), includeAll);
        }
    }
}
