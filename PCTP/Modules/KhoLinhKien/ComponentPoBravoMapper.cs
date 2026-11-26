using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>
    /// Một dòng thô của sheet Bravo: số dòng trong Excel + giá trị ô theo tên trường (dòng 2 của sheet,
    /// ví dụ CustomerCode, ItemCode, Quantity9). Không phụ thuộc EPPlus để kiểm thử được.
    /// </summary>
    public sealed class BravoRawRow
    {
        public BravoRawRow(int sheetRow, IDictionary<string, object> cells)
        {
            SheetRow = sheetRow;
            Cells = new Dictionary<string, object>(cells ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase);
        }

        public int SheetRow { get; private set; }
        public Dictionary<string, object> Cells { get; private set; }

        public bool IsEmpty
        {
            get { return Cells.Values.All(v => v == null || string.IsNullOrWhiteSpace(Convert.ToString(v, CultureInfo.InvariantCulture))); }
        }

        public object Get(string key)
        {
            object v;
            return Cells.TryGetValue(key, out v) ? v : null;
        }
    }

    public sealed class BravoPoLine
    {
        public int LineNo { get; set; }
        public string PartNo { get; set; }
        public decimal Quantity { get; set; }
        public DateTime NeedByDate { get; set; }
        public int SheetRow { get; set; }
    }

    /// <summary>Một phiếu đặt hàng = 1 dòng header + các dòng detail cùng NCC.</summary>
    public sealed class BravoPoDocument
    {
        public BravoPoDocument() { Lines = new List<BravoPoLine>(); }

        public string PurchaseOrderNo { get; set; }
        public string SiteCode { get; set; }
        public string SupplierCode { get; set; }
        public DateTime OrderDate { get; set; }
        public string Description { get; set; }
        public string EmployeeCode { get; set; }
        public List<BravoPoLine> Lines { get; private set; }

        public decimal TotalQty { get { return Lines.Sum(l => l.Quantity); } }
        public int PartCount { get { return Lines.Select(l => l.PartNo).Distinct(StringComparer.OrdinalIgnoreCase).Count(); } }
        public DateTime? MinNeedBy { get { return Lines.Count == 0 ? (DateTime?)null : Lines.Min(l => l.NeedByDate); } }
        public DateTime? MaxNeedBy { get { return Lines.Count == 0 ? (DateTime?)null : Lines.Max(l => l.NeedByDate); } }

        /// <summary>Ghi chú lưu vào PO: Mô tả (Bravo) + người lập (Bravo).</summary>
        public string Remarks
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Description)) parts.Add(Description.Trim());
                if (!string.IsNullOrWhiteSpace(EmployeeCode)) parts.Add("Lập: " + EmployeeCode.Trim());
                return string.Join(" | ", parts);
            }
        }
    }

    /// <summary>
    /// Chuẩn hóa file xuất từ Bravo (phiếu đặt hàng) thành PO header + detail.
    /// Header (vB30BizDoc_Edit): CustomerCode, SiteCode, [EmployeeCode, Description].
    /// Detail (vB30BizDocDetail_Edit): ItemCode, RequestDate, Quantity9, CustomerCode.
    /// File không có số PO, ngày đặt, số dòng: số PO và ngày đặt lấy từ tên file (hoặc giờ ghi file),
    /// số dòng đánh theo thứ tự trong file; mỗi (mã hàng, ngày yêu cầu) là một dòng PO với NeedByDate = ngày yêu cầu.
    /// </summary>
    public static class BravoPoMapper
    {
        public const string HeaderSheetName = "vB30BizDoc_Edit";
        public const string DetailSheetName = "vB30BizDocDetail_Edit";

        public static readonly string[] HeaderRequired = { "CustomerCode", "SiteCode" };
        public static readonly string[] DetailRequired = { "ItemCode", "RequestDate", "Quantity9", "CustomerCode" };

        private const int MaxPoNoLength = 60;
        private const int MaxPartNoLength = 100;
        private const int MaxSupplierLength = 60;
        private const int MaxSiteLength = 30;

        // Không dùng culture của máy: 13/10/2026 phải ra 13 tháng 10 trên mọi Regional Settings.
        private static readonly string[] DateFormats =
        {
            "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "d.M.yyyy",
            "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyyMMdd"
        };

        private static readonly Regex FileStamp = new Regex(
            @"(?<y>\d{4})-(?<mo>\d{2})-(?<d>\d{2})[_\s-]+(?<h>\d{2})-(?<mi>\d{2})-(?<s>\d{2})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static List<BravoPoDocument> Build(IList<BravoRawRow> headerRows, IList<BravoRawRow> detailRows,
            string fileName, DateTime fileTime)
        {
            if (headerRows == null) throw new ArgumentNullException("headerRows");
            if (detailRows == null) throw new ArgumentNullException("detailRows");

            DateTime stamp = ParseFileStamp(fileName) ?? fileTime;
            string baseNo = "BRV-" + stamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            var headers = headerRows.Where(r => !r.IsEmpty).ToList();
            if (headers.Count == 0)
                throw new InvalidOperationException("Sheet header (" + HeaderSheetName + ") không có dòng dữ liệu.");

            var docs = new List<BravoPoDocument>();
            var byCustomer = new Dictionary<string, BravoPoDocument>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in headers)
            {
                string supplier = AsText(h.Get("CustomerCode"));
                string site = (AsText(h.Get("SiteCode")) ?? string.Empty).ToUpperInvariant();
                if (string.IsNullOrEmpty(supplier))
                    throw new InvalidOperationException("Header dòng " + h.SheetRow + ": thiếu CustomerCode (NCC).");
                if (string.IsNullOrEmpty(site))
                    throw new InvalidOperationException("Header dòng " + h.SheetRow + ": thiếu SiteCode.");
                if (supplier.Length > MaxSupplierLength || site.Length > MaxSiteLength)
                    throw new InvalidOperationException("Header dòng " + h.SheetRow + ": CustomerCode/SiteCode vượt độ dài cho phép.");
                if (byCustomer.ContainsKey(supplier))
                    throw new InvalidOperationException("Header có nhiều hơn một dòng cho NCC " + supplier +
                        " (dòng " + h.SheetRow + "); không xác định được detail thuộc dòng nào.");

                var doc = new BravoPoDocument
                {
                    SiteCode = site,
                    SupplierCode = supplier,
                    OrderDate = stamp.Date,
                    Description = AsText(h.Get("Description")),
                    EmployeeCode = AsText(h.Get("EmployeeCode"))
                };
                byCustomer[supplier] = doc;
                docs.Add(doc);
            }

            foreach (var doc in docs)
                doc.PurchaseOrderNo = docs.Count == 1 ? baseNo : baseNo + "-" + doc.SupplierCode;
            foreach (var doc in docs)
                if (doc.PurchaseOrderNo.Length > MaxPoNoLength)
                    throw new InvalidOperationException("Số PO sinh ra quá dài (" + doc.PurchaseOrderNo + ").");

            foreach (var d in detailRows.Where(r => !r.IsEmpty))
            {
                string supplier = AsText(d.Get("CustomerCode"));
                BravoPoDocument doc;
                if (string.IsNullOrEmpty(supplier) && docs.Count == 1) doc = docs[0];
                else if (string.IsNullOrEmpty(supplier))
                    throw new InvalidOperationException("Detail dòng " + d.SheetRow + ": thiếu CustomerCode, không biết thuộc header nào.");
                else if (!byCustomer.TryGetValue(supplier, out doc))
                    throw new InvalidOperationException("Detail dòng " + d.SheetRow + ": NCC " + supplier + " không có trong sheet header.");

                string part = AsText(d.Get("ItemCode"));
                if (string.IsNullOrEmpty(part))
                    throw new InvalidOperationException("Detail dòng " + d.SheetRow + ": thiếu ItemCode.");
                if (part.Length > MaxPartNoLength)
                    throw new InvalidOperationException("Detail dòng " + d.SheetRow + ": ItemCode vượt " + MaxPartNoLength + " ký tự.");

                DateTime needBy;
                if (!TryParseDate(d.Get("RequestDate"), out needBy))
                    throw new InvalidOperationException("Detail dòng " + d.SheetRow + ": RequestDate không hợp lệ (" +
                        Convert.ToString(d.Get("RequestDate"), CultureInfo.InvariantCulture) + "), cần dd/MM/yyyy.");

                decimal qty;
                if (!TryParseQuantity(d.Get("Quantity9"), out qty))
                    throw new InvalidOperationException("Detail dòng " + d.SheetRow + ": Quantity9 không hợp lệ (" +
                        Convert.ToString(d.Get("Quantity9"), CultureInfo.InvariantCulture) + ").");
                if (qty <= 0)
                    throw new InvalidOperationException("Detail dòng " + d.SheetRow + ": Quantity9 phải lớn hơn 0.");

                doc.Lines.Add(new BravoPoLine
                {
                    LineNo = doc.Lines.Count + 1,
                    PartNo = part,
                    Quantity = qty,
                    NeedByDate = needBy.Date,
                    SheetRow = d.SheetRow
                });
            }

            foreach (var doc in docs)
                if (doc.Lines.Count == 0)
                    throw new InvalidOperationException("NCC " + doc.SupplierCode + " có header nhưng không có dòng detail nào.");

            return docs;
        }

        /// <summary>Lấy yyyy-MM-dd_HH-mm-ss từ tên file kiểu Bravo_dat_hang_2026-09-29_12-28-52.xlsx.</summary>
        public static DateTime? ParseFileStamp(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            Match m = FileStamp.Match(fileName);
            if (!m.Success) return null;
            try
            {
                return new DateTime(
                    int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture),
                    int.Parse(m.Groups["mo"].Value, CultureInfo.InvariantCulture),
                    int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture),
                    int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture),
                    int.Parse(m.Groups["mi"].Value, CultureInfo.InvariantCulture),
                    int.Parse(m.Groups["s"].Value, CultureInfo.InvariantCulture));
            }
            catch (ArgumentOutOfRangeException) { return null; }
        }

        /// <summary>Ô ngày: DateTime / số OADate / chuỗi theo định dạng cố định (ngày trước tháng), không theo culture máy.</summary>
        public static bool TryParseDate(object value, out DateTime date)
        {
            date = default(DateTime);
            if (value == null) return false;
            if (value is DateTime) { date = ((DateTime)value).Date; return true; }
            if (value is double)
            {
                double oa = (double)value;
                if (oa < 1 || oa > 2958465) return false;
                date = DateTime.FromOADate(oa).Date;
                return true;
            }
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return TryParseDateText(text, out date);
        }

        public static bool TryParseDateText(string text, out DateTime date)
        {
            date = default(DateTime);
            if (string.IsNullOrWhiteSpace(text)) return false;
            DateTime parsed;
            if (!DateTime.TryParseExact(text.Trim(), DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out parsed))
                return false;
            date = parsed.Date;
            return true;
        }

        public static bool TryParseQuantity(object value, out decimal qty)
        {
            qty = 0m;
            if (value == null) return false;
            if (value is decimal) { qty = (decimal)value; return true; }
            if (value is double)
            {
                double d = (double)value;
                if (double.IsNaN(d) || double.IsInfinity(d) || Math.Abs(d) > 1e12) return false;
                qty = Convert.ToDecimal(d);
                return true;
            }
            if (value is int || value is long) { qty = Convert.ToDecimal(value, CultureInfo.InvariantCulture); return true; }
            return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Number, CultureInfo.InvariantCulture, out qty);
        }

        /// <summary>Mã có thể nằm trong ô số (100016 → "100016"); bỏ phần ".0" do double.</summary>
        public static string AsText(object value)
        {
            if (value == null) return null;
            if (value is double)
            {
                double d = (double)value;
                if (d == Math.Floor(d) && Math.Abs(d) < 1e15)
                    return ((long)d).ToString(CultureInfo.InvariantCulture);
                return d.ToString("R", CultureInfo.InvariantCulture);
            }
            string s = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (s == null) return null;
            s = s.Trim();
            return s.Length == 0 ? null : s;
        }
    }
}
