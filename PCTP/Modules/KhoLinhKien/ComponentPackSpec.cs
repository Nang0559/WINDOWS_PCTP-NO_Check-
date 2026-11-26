using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using OfficeOpenXml;
using PCTP.Framework.UiLayout;
using PCTP.Infrastructure.Security;
using PCTP.Modules.KhoLinhKien.UI;
using PCTP.Security;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>
    /// Quy cách đóng gói linh kiện (thay B20ItemQuyCach): một dòng cho mỗi Site + Nhà cung cấp + Mã vật tư.
    /// SupplierCode rỗng = áp dụng cho mọi nhà cung cấp; dòng đúng nhà cung cấp được ưu tiên hơn dòng rỗng.
    /// Khi phát hành PO, mỗi dòng hàng được chia thành các LOT/tem có số lượng = PackQty, tem cuối giữ phần dư (migration 061).
    /// </summary>
    public sealed class ComponentPackSpecRow
    {
        public string SiteCode { get; set; }
        public string SupplierCode { get; set; }
        public string PartNo { get; set; }
        public decimal PackQty { get; set; }
        public decimal? MaxCloseQty { get; set; }
        public bool IsActive { get; set; }

        public ComponentPackSpecRow() { IsActive = true; }
    }

    public interface IComponentPackSpecRepository
    {
        DataTable List(string siteCode);
        /// <summary>Ghi tất cả dòng trong một transaction (hoặc tất cả hoặc không dòng nào).</summary>
        void Upsert(IReadOnlyList<ComponentPackSpecRow> rows, string actor);
    }

    public sealed class ComponentPackSpecRepository : IComponentPackSpecRepository
    {
        private readonly string _connectionString;

        public ComponentPackSpecRepository(string connectionString = null)
        {
            _connectionString = connectionString
                ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable List(string siteCode)
        {
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(
                "SELECT PackSpecId, SiteCode, SupplierCode, PartNo, PackQty, MaxCloseQty, IsActive, " +
                "       CreatedBy, COALESCE(UpdatedAtUtc, CreatedAtUtc) AS LastUpdateUtc, UpdatedBy " +
                "FROM MES.FVN_ComponentPackSpec WHERE SiteCode = @Site ORDER BY PartNo, SupplierCode", cn) { CommandTimeout = 60 })
            {
                cmd.Parameters.Add("@Site", SqlDbType.NVarChar, 30).Value = siteCode;
                using (var da = new SqlDataAdapter(cmd)) da.Fill(table);
            }
            return table;
        }

        public void Upsert(IReadOnlyList<ComponentPackSpecRow> rows, string actor)
        {
            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    foreach (var r in rows)
                    {
                        using (var cmd = new SqlCommand("MES.usp_FVN_ComponentPackSpec_Upsert", cn, tx))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.CommandTimeout = 60;
                            cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = r.SiteCode;
                            cmd.Parameters.Add("@SupplierCode", SqlDbType.NVarChar, 60).Value = r.SupplierCode ?? string.Empty;
                            cmd.Parameters.Add("@PartNo", SqlDbType.NVarChar, 100).Value = r.PartNo;
                            var qty = cmd.Parameters.Add("@PackQty", SqlDbType.Decimal);
                            qty.Precision = 18; qty.Scale = 3; qty.Value = r.PackQty;
                            var max = cmd.Parameters.Add("@MaxCloseQty", SqlDbType.Decimal);
                            max.Precision = 18; max.Scale = 3;
                            max.Value = r.MaxCloseQty.HasValue ? (object)r.MaxCloseQty.Value : DBNull.Value;
                            cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = r.IsActive;
                            cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                            cmd.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                }
            }
        }
    }

    public sealed class ComponentPackSpecService
    {
        private const string Function = ComponentPoStatus.Function;
        private readonly IComponentPackSpecRepository _repository;

        public ComponentPackSpecService(IComponentPackSpecRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public DataTable List(string siteCode)
        {
            string site = NormalizeSite(siteCode);
            AuthorizationGuard.DemandActionAndScope(Function, "VIEW", "SITE", site, site);
            return _repository.List(site);
        }

        /// <summary>Tạo/sửa/vô hiệu nhiều quy cách. Cần quyền CREATE theo từng Site; SQL kiểm lại mã vật tư trong danh mục IFS.</summary>
        public int Save(IReadOnlyList<ComponentPackSpecRow> rows)
        {
            string actor = AuthorizationGuard.ActorCode;
            if (rows == null || rows.Count == 0) throw new ArgumentException("Không có dòng quy cách nào để lưu.");

            foreach (var r in rows)
            {
                r.SiteCode = NormalizeSite(r.SiteCode);
                r.SupplierCode = (r.SupplierCode ?? string.Empty).Trim();
                r.PartNo = (r.PartNo ?? string.Empty).Trim();
                if (r.PartNo.Length == 0) throw new ArgumentException("Thiếu PartNo.");
                if (r.SupplierCode.Length > 60 || r.PartNo.Length > 100)
                    throw new ArgumentException("Độ dài mã vượt giới hạn (PartNo " + r.PartNo + ").");
                if (r.PackQty <= 0) throw new ArgumentException("PackQty phải lớn hơn 0 (PartNo " + r.PartNo + ").");
                if (r.MaxCloseQty.HasValue && r.MaxCloseQty.Value <= 0)
                    throw new ArgumentException("MaxCloseQty phải lớn hơn 0 (PartNo " + r.PartNo + ").");
            }

            var dup = rows.GroupBy(r => (r.SiteCode + "|" + r.SupplierCode + "|" + r.PartNo).ToUpperInvariant())
                .FirstOrDefault(g => g.Count() > 1);
            if (dup != null) throw new ArgumentException("Trùng khóa Site|Nhà cung cấp|PartNo: " + dup.Key);

            foreach (var bySite in rows.GroupBy(r => r.SiteCode))
            {
                AuthorizationGuard.DemandActionAndScope(Function, "CREATE", "SITE", bySite.Key, bySite.Key);
                // Liệt kê đủ các mã chưa có trong IFS (thủ tục SQL chỉ báo từng mã một).
                var active = bySite.Where(r => r.IsActive).Select(r => r.PartNo).ToList();
                if (active.Count > 0)
                {
                    IReadOnlyList<string> missing = new ComponentPurchaseOrderRepository().FindMissingParts(bySite.Key, active);
                    if (missing.Count > 0)
                        throw new InvalidOperationException("Site " + bySite.Key + ": " + missing.Count +
                            " mã chưa có trong danh mục IFS:\r\n" + string.Join(", ", missing.Take(20)) +
                            (missing.Count > 20 ? ", ..." : string.Empty));
                }
            }

            _repository.Upsert(rows, actor);
            return rows.Count;
        }

        private static string NormalizeSite(string siteCode)
        {
            string site = (siteCode ?? string.Empty).Trim().ToUpperInvariant();
            if (site.Length == 0) throw new ArgumentException("Thiếu SiteCode.");
            return site;
        }
    }

    /// <summary>
    /// Excel quy cách đóng gói. Cột: PartNo (hoặc ItemCode), PackQty (hoặc Quantity) là bắt buộc;
    /// SupplierCode (hoặc CustomerCode), MaxCloseQty, SiteCode, IsActive là tùy chọn. Tên cột như B20ItemQuyCach nên
    /// có thể dán trực tiếp dữ liệu xuất từ bảng đó; thiếu SiteCode thì dùng Site của màn hình.
    /// </summary>
    public static class ComponentPackSpecExcel
    {
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            { "partno", "PartNo" }, { "itemcode", "PartNo" }, { "item", "PartNo" },
            { "packqty", "PackQty" }, { "quantity", "PackQty" }, { "qty", "PackQty" },
            { "suppliercode", "SupplierCode" }, { "customercode", "SupplierCode" },
            { "maxcloseqty", "MaxCloseQty" },
            { "sitecode", "SiteCode" },
            { "isactive", "IsActive" }
        };

        /// <summary>Phân tích số theo máy hiện tại rồi theo Invariant (Regional Settings mỗi máy một khác).</summary>
        public static bool TryParseDecimal(string text, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            return decimal.TryParse(t, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
                || decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        public static List<ComponentPackSpecRow> Read(string path, string defaultSite)
        {
            using (var package = new ExcelPackage(new FileInfo(path)))
            {
                ExcelWorksheet ws = package.Workbook.Worksheets.FirstOrDefault(
                                        w => string.Equals(w.Name, "QuyCach", StringComparison.OrdinalIgnoreCase))
                                    ?? package.Workbook.Worksheets.FirstOrDefault();
                if (ws == null || ws.Dimension == null) throw new InvalidOperationException("File Excel không có dữ liệu.");

                var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int c = 1; c <= ws.Dimension.End.Column; c++)
                {
                    string h = (ws.Cells[1, c].Text ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty);
                    string canonical;
                    if (h.Length > 0 && Aliases.TryGetValue(h, out canonical) && !col.ContainsKey(canonical)) col[canonical] = c;
                }
                foreach (var need in new[] { "PartNo", "PackQty" })
                    if (!col.ContainsKey(need))
                        throw new InvalidOperationException("Thiếu cột bắt buộc: " + need + " (hoặc " + (need == "PartNo" ? "ItemCode" : "Quantity") + ").");

                Func<int, string, string> text = (r, key) =>
                    col.ContainsKey(key) ? (ws.Cells[r, col[key]].Text ?? string.Empty).Trim() : string.Empty;

                var result = new List<ComponentPackSpecRow>();
                for (int r = 2; r <= ws.Dimension.End.Row; r++)
                {
                    string part = text(r, "PartNo");
                    string qtyText = text(r, "PackQty");
                    if (part.Length == 0 && qtyText.Length == 0) continue;

                    decimal qty;
                    object qtyCell = ws.Cells[r, col["PackQty"]].Value;
                    if (qtyCell is double) qty = Convert.ToDecimal((double)qtyCell);
                    else if (!TryParseDecimal(qtyText, out qty))
                        throw new InvalidOperationException("PackQty không hợp lệ tại dòng " + r + ".");

                    decimal? max = null;
                    string maxText = text(r, "MaxCloseQty");
                    if (maxText.Length > 0)
                    {
                        decimal m;
                        object maxCell = ws.Cells[r, col["MaxCloseQty"]].Value;
                        if (maxCell is double) m = Convert.ToDecimal((double)maxCell);
                        else if (!TryParseDecimal(maxText, out m))
                            throw new InvalidOperationException("MaxCloseQty không hợp lệ tại dòng " + r + ".");
                        max = m;
                    }

                    bool active = true;
                    string activeText = text(r, "IsActive").ToLowerInvariant();
                    if (activeText == "0" || activeText == "false" || activeText == "no" || activeText == "n") active = false;

                    string site = text(r, "SiteCode");
                    result.Add(new ComponentPackSpecRow
                    {
                        SiteCode = site.Length == 0 ? defaultSite : site,
                        SupplierCode = text(r, "SupplierCode"),
                        PartNo = part,
                        PackQty = qty,
                        MaxCloseQty = max,
                        IsActive = active
                    });
                }
                if (result.Count == 0) throw new InvalidOperationException("File Excel không có dòng quy cách nào.");
                return result;
            }
        }

        public static void WriteTemplate(string path, string site)
        {
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("QuyCach");
                string[] headers = { "SiteCode", "SupplierCode", "PartNo", "PackQty", "MaxCloseQty", "IsActive" };
                for (int i = 0; i < headers.Length; i++) ws.Cells[1, i + 1].Value = headers[i];
                ws.Cells[2, 1].Value = site;
                ws.Cells[2, 2].Value = "100016";
                ws.Cells[2, 3].Value = "23123-KPH-9002";
                ws.Cells[2, 4].Value = 2400;
                ws.Cells[2, 5].Value = 2400;
                ws.Cells[2, 6].Value = 1;
                ws.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;
                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                package.SaveAs(new FileInfo(path));
            }
        }
    }

    /// <summary>Nút "Quy cách đóng gói…" trên màn hình Phiếu đặt hàng linh kiện.</summary>
    public static class ComponentPackSpecAction
    {
        public static UiScreenAction Create()
        {
            return new UiScreenAction("Quy cách đóng gói…", 150,
                () =>
                {
                    var session = UserSession.Current;
                    return session != null && session.HasPermission(ComponentPoStatus.Function, "VIEW");
                },
                (owner, site) =>
                {
                    using (var form = new FormComponentPackSpec(site)) form.ShowDialog(owner);
                    return null;   // không tải lại lưới PO: màn hình quy cách không đổi dữ liệu PO
                });
        }
    }
}
