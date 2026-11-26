using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using PCTP.Framework.UiLayout;
using PCTP.Security;
using PCTP.Shared.UiMd;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>
    /// "Upload Excel PO…" flow shared by the legacy "Kho linh kiện PART" form and the layout-driven
    /// "Phiếu đặt hàng linh kiện" screen: pick file → read sheet PO → confirm → import as DRAFT in one transaction.
    /// The service re-checks CREATE and the Site scope of every row, and SQL checks again; nothing here is a gate.
    /// </summary>
    public static class ComponentPoUploadFlow
    {
        public const string Title = "Upload yêu cầu mua hàng";

        /// <summary>Returns a status line, or null when the user cancelled. Throws on a failed read or import.</summary>
        public static string Run(IWin32Window owner, IComponentPurchaseOrderService po)
        {
            if (po == null) throw new ArgumentNullException("po");

            string path;
            using (var dlg = new OpenFileDialog
            {
                Filter = "Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm",
                Title = "Chọn file yêu cầu mua hàng linh kiện"
            })
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return null;
                path = dlg.FileName;
            }

            List<ComponentPoImportRow> rows;
            string summary;
            if (ComponentPoBravoExcelReader.IsBravoWorkbook(path))
            {
                // File xuất từ Bravo: sheet header + sheet detail (chuẩn header/detail của phiếu đặt hàng).
                List<BravoPoDocument> docs = ComponentPoBravoExcelReader.Read(path);
                rows = ComponentPoBravoExcelReader.ToImportRows(docs);
                summary = DescribeBravo(docs);
            }
            else
            {
                IList<string> sheets = ComponentPoExcelReader.GetSheetNames(path);
                string sheet = sheets.FirstOrDefault(n => string.Equals(n, "PO", StringComparison.OrdinalIgnoreCase)) ?? sheets.FirstOrDefault();
                if (sheet == null) throw new InvalidOperationException("File Excel không có sheet nào.");

                rows = ComponentPoExcelReader.Read(path, sheet);
                int orders = rows.GroupBy(r => new { r.SiteCode, r.PurchaseOrderNo }).Count();
                string sites = string.Join(", ", rows
                    .Select(r => (r.SiteCode ?? string.Empty).Trim().ToUpperInvariant())
                    .Where(x => x.Length > 0).Distinct().OrderBy(x => x, StringComparer.Ordinal));
                summary = "Sheet \"" + sheet + "\": " + orders + " PO, " + rows.Count + " dòng hàng" +
                    (sites.Length > 0 ? " (Site: " + sites + ")" : string.Empty) + ".";
            }

            // Thủ tục upload chỉ báo "một PartNo không có trong danh mục IFS" mà không nêu mã nào: kiểm tra trước để liệt kê.
            foreach (var bySite in rows.GroupBy(r => (r.SiteCode ?? string.Empty).Trim().ToUpperInvariant()))
            {
                IReadOnlyList<string> missing = po.FindMissingParts(bySite.Key, bySite.Select(r => r.PartNo));
                if (missing.Count > 0)
                    throw new InvalidOperationException("Site " + bySite.Key + ": " + missing.Count +
                        " mã linh kiện chưa có trong danh mục IFS nên không thể upload:\r\n" +
                        string.Join(", ", missing.Take(20)) + (missing.Count > 20 ? ", ..." : string.Empty));
            }

            string message = summary + "\r\n\r\n" +
                "Upload tạo PO ở trạng thái Nháp (hoặc thay thế PO Nháp cùng số). PO đã Kiểm tra/Phát hành không bị ghi đè. " +
                "Toàn bộ file chạy trong một transaction.\r\n\r\nTiếp tục upload?";
            if (XtraMessageBox.Show(owner, message, Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return null;

            int count = po.Import(rows, Path.GetFileName(path));
            return "Đã upload " + count + " PO (" + rows.Count + " dòng) ở trạng thái Nháp.";
        }

        private static string DescribeBravo(IList<BravoPoDocument> docs)
        {
            var lines = new List<string>();
            foreach (BravoPoDocument d in docs)
            {
                string range = d.MinNeedBy.HasValue
                    ? d.MinNeedBy.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + " → " +
                      d.MaxNeedBy.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                    : "-";
                lines.Add("PO " + d.PurchaseOrderNo + " | NCC " + d.SupplierCode + " | Site " + d.SiteCode +
                          " | ngày đặt " + d.OrderDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + "\r\n" +
                          d.Lines.Count + " dòng, " + d.PartCount + " mã, tổng SL " +
                          d.TotalQty.ToString("#,##0.###", CultureInfo.CurrentCulture) + ", ngày cần " + range +
                          (string.IsNullOrWhiteSpace(d.Description) ? string.Empty : "\r\nMô tả: " + d.Description.Trim()));
            }
            return "File Bravo (header + detail):\r\n" + string.Join("\r\n\r\n", lines);
        }
    }

    /// <summary>Toolbar action for the "Phiếu đặt hàng linh kiện" layout screen.</summary>
    public static class ComponentPoUploadAction
    {
        public static UiScreenAction Create()
        {
            return new UiScreenAction("Upload Excel PO…", 140,
                () =>
                {
                    var session = UserSession.Current;
                    return session != null && session.HasPermission(ComponentPoStatus.Function, "CREATE");
                },
                (owner, site) => ComponentPoUploadFlow.Run(owner, new ComponentPurchaseOrderService(
                    new ComponentPurchaseOrderRepository(),
                    new WorkflowTransitionService(new ComponentPoWorkflowRepository()))));
        }
    }
}
