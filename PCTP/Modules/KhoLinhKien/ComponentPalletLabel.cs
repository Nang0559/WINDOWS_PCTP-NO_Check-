using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using PCTP.Framework.UiLayout;
using PCTP.Infrastructure.Security;
using PCTP.Security;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>
    /// Tem palet: gom các tem nhỏ của một dòng PO theo SL / palet (MaxCloseQty của quy cách đóng gói).
    /// QR palet = "FVN2|&lt;PalletNo&gt;"; quét ở màn hình nhận hàng sẽ nhận toàn bộ tem nhỏ còn lại của palet.
    /// </summary>
    public static class ComponentPalletQr
    {
        public const string Prefix = "FVN2|";

        public static bool IsPalletPayload(string payload)
        {
            return payload != null && payload.TrimStart().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
        }
    }

    public interface IComponentPalletRepository
    {
        DataTable GetPallets(long purchaseOrderId, bool onlyUnprinted, string actor);
        void LogPrinted(long purchaseOrderId, string actionCode, string reason, string actor, IReadOnlyList<long> palletIds);
        /// <summary>Các tem nhỏ của một QR palet (cột ComponentLotId, LotNo, LotQrPayload, IssuedQty, ReceivedQty, Status, PalletNo).</summary>
        DataTable GetPalletLots(string siteCode, string palletQr);
    }

    public sealed class ComponentPalletRepository : IComponentPalletRepository
    {
        private readonly string _connectionString;

        public ComponentPalletRepository(string connectionString = null)
        {
            _connectionString = connectionString
                ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable GetPallets(long purchaseOrderId, bool onlyUnprinted, string actor)
        {
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ComponentPO_PalletData", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 })
            {
                cmd.Parameters.Add("@PurchaseOrderId", SqlDbType.BigInt).Value = purchaseOrderId;
                cmd.Parameters.Add("@OnlyUnprinted", SqlDbType.Bit).Value = onlyUnprinted;
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                using (var da = new SqlDataAdapter(cmd)) da.Fill(table);
            }
            return table;
        }

        public void LogPrinted(long purchaseOrderId, string actionCode, string reason, string actor, IReadOnlyList<long> palletIds)
        {
            var json = new StringBuilder("[");
            for (int i = 0; i < palletIds.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append(palletIds[i].ToString(CultureInfo.InvariantCulture));
            }
            json.Append(']');
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ComponentPO_PalletLabelLog", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 })
            {
                cmd.Parameters.Add("@PurchaseOrderId", SqlDbType.BigInt).Value = purchaseOrderId;
                cmd.Parameters.Add("@ActionCode", SqlDbType.VarChar, 20).Value = actionCode;
                cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 300).Value =
                    string.IsNullOrWhiteSpace(reason) ? (object)DBNull.Value : reason;
                cmd.Parameters.Add("@RequestedBy", SqlDbType.NVarChar, 100).Value = actor;
                cmd.Parameters.Add("@PalletIdsJson", SqlDbType.NVarChar, -1).Value = json.ToString();
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public DataTable GetPalletLots(string siteCode, string palletQr)
        {
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ComponentPallet_Lots", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 })
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = siteCode;
                cmd.Parameters.Add("@QrPayload", SqlDbType.NVarChar, 500).Value = palletQr;
                using (var da = new SqlDataAdapter(cmd)) da.Fill(table);
            }
            return table;
        }
    }

    public sealed class ComponentPalletLabelService
    {
        private const string Function = ComponentPoStatus.Function;
        private readonly IComponentPalletRepository _repository;

        public ComponentPalletLabelService(IComponentPalletRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>Cần quyền CONFIRM theo Site của PO (như in tem nhỏ). Lần đầu gọi sẽ tạo palet cho các tem chưa có palet.</summary>
        public DataTable GetPallets(long purchaseOrderId, bool reprint)
        {
            Demand(purchaseOrderId);
            return _repository.GetPallets(purchaseOrderId, !reprint, AuthorizationGuard.ActorCode);
        }

        public void LogPrinted(long purchaseOrderId, bool reprint, string reason, IReadOnlyList<long> palletIds)
        {
            Demand(purchaseOrderId);
            if (reprint && string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Bắt buộc nhập lý do in lại.");
            _repository.LogPrinted(purchaseOrderId, reprint ? "REPRINT" : "INITIAL_PRINT", reason, AuthorizationGuard.ActorCode, palletIds);
        }

        private static void Demand(long purchaseOrderId)
        {
            if (purchaseOrderId <= 0) throw new ArgumentException("Yêu cầu mua hàng không hợp lệ.");
            string site = new ComponentPurchaseOrderRepository().GetSiteCode(purchaseOrderId);
            if (string.IsNullOrWhiteSpace(site))
                throw new UnauthorizedAccessException("Không xác định được Site của yêu cầu mua hàng.");
            AuthorizationGuard.DemandActionAndScope(Function, "CONFIRM", "SITE", site, site);
        }
    }

    /// <summary>Tờ tem palet A4 ngang, 2 cột x 2 hàng = 4 tem/trang (mỗi tem dán được lên palet, QR lớn để quét xa).</summary>
    public static class ComponentPalletLabelSheet
    {
        public const int Columns = 2;
        public const int Rows = 2;
        public const int PerPage = Columns * Rows;
        private const int Margin = 20;

        public static int PageCount(int labels) { return (labels + PerPage - 1) / PerPage; }

        public static bool Print(IWin32Window owner, DataTable pallets, string title)
        {
            using (var document = new PrintDocument { DocumentName = title })
            using (var dialog = new PrintDialog { Document = document, UseEXDialog = true })
            {
                document.DefaultPageSettings.Landscape = true;
                document.DefaultPageSettings.Margins = new Margins(Margin, Margin, Margin, Margin);
                foreach (PaperSize size in document.PrinterSettings.PaperSizes)
                    if (size.Kind == PaperKind.A4) { document.DefaultPageSettings.PaperSize = size; break; }
                if (dialog.ShowDialog(owner) != DialogResult.OK) return false;

                int index = 0;
                document.PrintPage += (s, e) =>
                {
                    RectangleF area = e.MarginBounds;
                    float cellW = area.Width / Columns;
                    float cellH = area.Height / Rows;
                    for (int slot = 0; slot < PerPage && index < pallets.Rows.Count; slot++, index++)
                    {
                        var cell = new RectangleF(area.Left + (slot % Columns) * cellW, area.Top + (slot / Columns) * cellH, cellW, cellH);
                        DrawPallet(e.Graphics, cell, pallets.Rows[index]);
                    }
                    e.HasMorePages = index < pallets.Rows.Count;
                };
                document.Print();
                return true;
            }
        }

        private static void DrawPallet(Graphics g, RectangleF cell, DataRow row)
        {
            const float pad = 6f;
            var box = new RectangleF(cell.Left + pad, cell.Top + pad, cell.Width - 2 * pad, cell.Height - 2 * pad);
            var left = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            string qty = Convert.ToDecimal(row["TotalQty"], CultureInfo.InvariantCulture).ToString("#,##0.###", CultureInfo.InvariantCulture);
            string date = row["LotIssueDate"] == DBNull.Value ? string.Empty
                : Convert.ToDateTime(row["LotIssueDate"], CultureInfo.InvariantCulture).ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture);
            float rowH = box.Height * 0.095f;

            using (var pen = new Pen(Color.Black, 1.5f))
            using (var small = new Font("Arial", 8f, FontStyle.Regular))
            using (var bold = new Font("Arial", 10f, FontStyle.Bold))
            using (var title = new Font("Arial", 16f, FontStyle.Bold))
            using (var big = new Font("Arial", 26f, FontStyle.Bold))
            using (var codeFont = new Font("Arial", 9f, FontStyle.Bold))
            {
                g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
                float y = box.Top;
                g.DrawString("PALLET  " + Convert.ToString(row["PalletSeq"], CultureInfo.InvariantCulture) + " / " + Convert.ToString(row["PalletCount"], CultureInfo.InvariantCulture),
                    title, Brushes.Black, new RectangleF(box.Left + 4, y, box.Width * 0.6f, rowH * 1.4f), left);
                g.DrawString(date, bold, Brushes.Black, new RectangleF(box.Left + box.Width * 0.6f, y, box.Width * 0.4f - 4, rowH * 1.4f), center);
                y += rowH * 1.4f; g.DrawLine(pen, box.Left, y, box.Right, y);

                g.DrawString("PO: " + Convert.ToString(row["PurchaseOrderNo"]) + "    Line: " + Convert.ToString(row["LineNo"], CultureInfo.InvariantCulture),
                    bold, Brushes.Black, new RectangleF(box.Left + 4, y, box.Width - 8, rowH), left);
                y += rowH;
                g.DrawString("PartNo: " + Convert.ToString(row["PartNo"]), bold, Brushes.Black, new RectangleF(box.Left + 4, y, box.Width - 8, rowH), left);
                y += rowH;
                g.DrawString(Convert.ToString(row["PartDescription"]), small, Brushes.Black, new RectangleF(box.Left + 4, y, box.Width - 8, rowH), left);
                y += rowH;
                g.DrawString("Maker: " + Convert.ToString(row["SupplierCode"]) + " " + Convert.ToString(row["SupplierName"]),
                    small, Brushes.Black, new RectangleF(box.Left + 4, y, box.Width - 8, rowH), left);
                y += rowH; g.DrawLine(pen, box.Left, y, box.Right, y);

                float bottom = box.Bottom - 4f;
                float qrSize = Math.Min(bottom - y - 4f, box.Width * 0.46f);
                if (qrSize > 30f)
                {
                    using (Bitmap qr = ComponentPoLabelSheet.CreateQr(Convert.ToString(row["QrPayload"])))
                    {
                        GraphicsState state = g.Save();
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(qr, box.Left + 6f, y + 2f, qrSize, qrSize);
                        g.Restore(state);
                    }
                    float tx = box.Left + qrSize + 14f, tw = box.Right - tx - 4f;
                    g.DrawString("Quantity", small, Brushes.Black, new RectangleF(tx, y + 2f, tw, rowH), center);
                    g.DrawString(qty, big, Brushes.Black, new RectangleF(tx, y + rowH, tw, rowH * 2.2f), center);
                    g.DrawString(Convert.ToString(row["LotCount"], CultureInfo.InvariantCulture) + " tem nhỏ",
                        bold, Brushes.Black, new RectangleF(tx, y + rowH * 3.3f, tw, rowH), center);
                    g.DrawString(Convert.ToString(row["PalletNo"]), codeFont, Brushes.Black, new RectangleF(tx, y + rowH * 4.4f, tw, rowH * 1.4f), center);
                }
            }
        }
    }

    public static class ComponentPalletLabelFlow
    {
        public static string Run(IWin32Window owner, UiScreenRowContext row, bool reprint)
        {
            if (row == null || !row.HasRow) throw new InvalidOperationException("Chưa chọn phiếu đặt hàng nào.");
            object idValue = row.GetValue("PurchaseOrderId");
            if (idValue == null) throw new InvalidOperationException("Không đọc được Id của phiếu đặt hàng.");
            long id = Convert.ToInt64(idValue, CultureInfo.InvariantCulture);
            string poNo = row.GetText("PurchaseOrderNo") ?? id.ToString(CultureInfo.InvariantCulture);

            var service = new ComponentPalletLabelService(new ComponentPalletRepository());
            DataTable pallets = service.GetPallets(id, reprint);
            if (pallets.Rows.Count == 0)
                return reprint
                    ? "PO " + poNo + " chưa có tem palet nào để in."
                    : "PO " + poNo + ": mọi tem palet đã được in lần đầu. Dùng \"In lại tem palet\" nếu cần in lại.";

            string reason = null;
            if (reprint)
            {
                pallets = ComponentLabelPicker.Pick(owner, pallets, "Chọn tem palet cần in lại - PO " + poNo, new[]
                {
                    new KeyValuePair<string, string>("LineNo", "Dòng"),
                    new KeyValuePair<string, string>("PalletNo", "Mã palet"),
                    new KeyValuePair<string, string>("PartNo", "Mã vật tư"),
                    new KeyValuePair<string, string>("TotalQty", "Số lượng"),
                    new KeyValuePair<string, string>("LotCount", "Số tem nhỏ")
                });
                if (pallets == null || pallets.Rows.Count == 0) return null;
                reason = XtraInputBox.Show("Nhập lý do in lại tem palet (rách / không đọc được mã vạch...):", "In lại tem palet " + poNo, string.Empty);
                if (string.IsNullOrWhiteSpace(reason)) return null;
                reason = reason.Trim();
            }
            else
            {
                string ask = "In " + pallets.Rows.Count + " tem palet của PO " + poNo + " (" + ComponentPalletLabelSheet.PageCount(pallets.Rows.Count) +
                             " trang A4, " + ComponentPalletLabelSheet.PerPage + " tem/trang)?";
                if (XtraMessageBox.Show(owner, ask, "In tem palet", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return null;
            }

            if (!ComponentPalletLabelSheet.Print(owner, pallets, "Tem palet " + poNo)) return null;

            var ids = pallets.Rows.Cast<DataRow>().Select(r => Convert.ToInt64(r["ComponentPalletId"], CultureInfo.InvariantCulture)).ToList();
            try
            {
                service.LogPrinted(id, reprint, reason, ids);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Đã gửi " + pallets.Rows.Count + " tem palet tới máy in nhưng KHÔNG ghi được lịch sử in: " +
                    ex.Message + "\r\nKhông bấm In tem palet lần nữa (tem sẽ in trùng). Báo quản trị để ghi nhận hoặc dùng \"In lại tem palet\" khi cần.", ex);
            }
            return "Đã gửi " + pallets.Rows.Count + " tem palet của PO " + poNo + " tới máy in (" + (reprint ? "in lại" : "in lần đầu") + ").";
        }
    }
}
