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
using DevExpress.XtraPrinting.BarCode;
using PCTP.Framework.UiLayout;
using PCTP.Infrastructure.Security;
using PCTP.Security;

namespace PCTP.Modules.KhoLinhKien
{
    public interface IComponentPoLabelRepository
    {
        DataTable GetLabels(long purchaseOrderId, bool onlyUnprinted);
        void LogPrinted(long purchaseOrderId, string actionCode, string reason, string actor, IReadOnlyList<long> lotIds);
    }

    public sealed class ComponentPoLabelRepository : IComponentPoLabelRepository
    {
        private readonly string _connectionString;

        public ComponentPoLabelRepository(string connectionString = null)
        {
            _connectionString = connectionString
                ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable GetLabels(long purchaseOrderId, bool onlyUnprinted)
        {
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ComponentPO_LabelData", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 })
            {
                cmd.Parameters.Add("@PurchaseOrderId", SqlDbType.BigInt).Value = purchaseOrderId;
                cmd.Parameters.Add("@OnlyUnprinted", SqlDbType.Bit).Value = onlyUnprinted;
                using (var da = new SqlDataAdapter(cmd)) da.Fill(table);
            }
            return table;
        }

        public void LogPrinted(long purchaseOrderId, string actionCode, string reason, string actor, IReadOnlyList<long> lotIds)
        {
            var json = new StringBuilder("[");
            for (int i = 0; i < lotIds.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append(lotIds[i].ToString(CultureInfo.InvariantCulture));
            }
            json.Append(']');
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ComponentPO_LabelLog", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 })
            {
                cmd.Parameters.Add("@PurchaseOrderId", SqlDbType.BigInt).Value = purchaseOrderId;
                cmd.Parameters.Add("@ActionCode", SqlDbType.VarChar, 20).Value = actionCode;
                cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 300).Value =
                    string.IsNullOrWhiteSpace(reason) ? (object)DBNull.Value : reason;
                cmd.Parameters.Add("@RequestedBy", SqlDbType.NVarChar, 100).Value = actor;
                cmd.Parameters.Add("@LotIdsJson", SqlDbType.NVarChar, -1).Value = json.ToString();
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }

    public sealed class ComponentPoLabelService
    {
        private const string Function = ComponentPoStatus.Function;
        private readonly IComponentPoLabelRepository _repository;

        public ComponentPoLabelService(IComponentPoLabelRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>In tem cần quyền CONFIRM theo Site của PO. reprint = false chỉ lấy tem chưa in lần đầu.</summary>
        public DataTable GetLabels(long purchaseOrderId, bool reprint)
        {
            Demand(purchaseOrderId);
            return _repository.GetLabels(purchaseOrderId, !reprint);
        }

        public void LogPrinted(long purchaseOrderId, bool reprint, string reason, IReadOnlyList<long> lotIds)
        {
            Demand(purchaseOrderId);
            if (reprint && string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Bắt buộc nhập lý do in lại.");
            _repository.LogPrinted(purchaseOrderId, reprint ? "REPRINT" : "INITIAL_PRINT", reason, AuthorizationGuard.ActorCode, lotIds);
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

    /// <summary>
    /// Tờ tem QR A4 ngang, 4 cột x 4 hàng = 16 tem/trang (mỗi ô khoảng 72 x 50 mm). Mỗi tem một LOT/gói:
    /// ngày phát hành, Stt (số thứ tự gói trong dòng PO), mã vật tư, tên, số dòng PO, nhà cung cấp, số lượng và mã QR
    /// (QrPayload của LOT) thay cho mã vạch 2D cũ.
    /// </summary>
    public static class ComponentPoLabelSheet
    {
        public const int Columns = 4;
        public const int Rows = 4;
        public const int PerPage = Columns * Rows;
        private const int Margin = 20;   // 1/100 inch (~5 mm)

        public static int PageCount(int labels) { return (labels + PerPage - 1) / PerPage; }

        /// <summary>Gửi bảng tem tới máy in đã chọn trong <paramref name="dialog"/>. Trả false nếu người dùng hủy hộp thoại in.</summary>
        public static bool Print(IWin32Window owner, DataTable labels, string title)
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
                DateTime printedAt = DateTime.Now;
                document.PrintPage += (s, e) =>
                {
                    RectangleF area = e.MarginBounds;
                    float cellW = area.Width / Columns;
                    float cellH = area.Height / Rows;
                    for (int slot = 0; slot < PerPage && index < labels.Rows.Count; slot++, index++)
                    {
                        var cell = new RectangleF(area.Left + (slot % Columns) * cellW, area.Top + (slot / Columns) * cellH, cellW, cellH);
                        DrawLabel(e.Graphics, cell, labels.Rows[index], printedAt);
                    }
                    e.HasMorePages = index < labels.Rows.Count;
                };
                document.Print();
                return true;
            }
        }

        private static void DrawLabel(Graphics g, RectangleF cell, DataRow row, DateTime printedAt)
        {
            const float pad = 3f;
            var box = new RectangleF(cell.Left + pad, cell.Top + pad, cell.Width - 2 * pad, cell.Height - 2 * pad);
            float rowH = box.Height * 0.115f;

            string date = Convert.ToDateTime(row["LotIssueDate"]).ToString("dd-MMM", CultureInfo.InvariantCulture);
            string qty = Convert.ToDecimal(row["IssuedQty"]).ToString("#,##0.###", CultureInfo.InvariantCulture);
            string supplierName = Convert.ToString(row["SupplierName"]);
            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            var left = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

            using (var pen = new Pen(Color.Black, 1f))
            using (var small = new Font("Arial", 7f, FontStyle.Regular))
            using (var bold = new Font("Arial", 8f, FontStyle.Bold))
            using (var big = new Font("Arial", 13f, FontStyle.Bold))
            using (var lotFont = new Font("Arial", 8.5f, FontStyle.Bold))
            {
                g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);

                // Hàng 1: Date | Stt | Time
                float y = box.Top;
                float third = box.Width / 3f;
                g.DrawString("Date: " + date, bold, Brushes.Black, new RectangleF(box.Left, y, third, rowH), center);
                g.DrawString("Stt: " + Convert.ToString(row["Seq"], CultureInfo.InvariantCulture), bold, Brushes.Black, new RectangleF(box.Left + third, y, third, rowH), center);
                g.DrawString("Time: " + printedAt.Hour.ToString(CultureInfo.InvariantCulture) + "h", bold, Brushes.Black, new RectangleF(box.Left + 2 * third, y, third, rowH), center);
                g.DrawLine(pen, box.Left, y + rowH, box.Right, y + rowH);
                g.DrawLine(pen, box.Left + third, y, box.Left + third, y + rowH);
                g.DrawLine(pen, box.Left + 2 * third, y, box.Left + 2 * third, y + rowH);

                // Hàng 2-3: PartNo, PartName
                y += rowH;
                g.DrawString("PartNo: " + Convert.ToString(row["PartNo"]), bold, Brushes.Black, new RectangleF(box.Left + 2, y, box.Width - 4, rowH), left);
                y += rowH;
                g.DrawLine(pen, box.Left, y, box.Right, y);
                g.DrawString("PartName: " + Convert.ToString(row["PartDescription"]), bold, Brushes.Black, new RectangleF(box.Left + 2, y, box.Width - 4, rowH), left);
                y += rowH;
                g.DrawLine(pen, box.Left, y, box.Right, y);

                // Hàng 4-5: DeptName/ INVOICE + số dòng PO
                g.DrawString("DeptName/ INVOICE", small, Brushes.Black, new RectangleF(box.Left, y, box.Width, rowH * 0.7f), center);
                y += rowH * 0.7f;
                g.DrawString(Convert.ToString(row["LineNo"], CultureInfo.InvariantCulture), bold, Brushes.Black, new RectangleF(box.Left, y, box.Width, rowH * 0.9f), center);
                y += rowH * 0.9f;
                g.DrawLine(pen, box.Left, y, box.Right, y);

                // Hàng 6: Maker | Quantity
                float makerH = rowH * 1.7f;
                float half = box.Width / 2f;
                g.DrawString("Maker:", small, Brushes.Black, new RectangleF(box.Left + 2, y, half * 0.4f, makerH * 0.5f), left);
                g.DrawString(Convert.ToString(row["SupplierCode"]), bold, Brushes.Black, new RectangleF(box.Left + half * 0.4f, y, half * 0.6f, makerH * 0.5f), left);
                g.DrawString(supplierName, bold, Brushes.Black, new RectangleF(box.Left, y + makerH * 0.5f, half, makerH * 0.5f), center);
                g.DrawString("Quantity", small, Brushes.Black, new RectangleF(box.Left + half, y, half, makerH * 0.4f), center);
                g.DrawString(qty, big, Brushes.Black, new RectangleF(box.Left + half, y + makerH * 0.4f, half, makerH * 0.6f), center);
                g.DrawLine(pen, box.Left + half, y, box.Left + half, y + makerH);
                y += makerH;
                g.DrawLine(pen, box.Left, y, box.Right, y);

                // Phần dưới: mã QR bên trái, số LOT bên phải
                float qrSize = Math.Min(box.Bottom - y - 4f, box.Width * 0.42f);
                if (qrSize > 20f)
                {
                    using (Bitmap qr = CreateQr(Convert.ToString(row["QrPayload"])))
                    {
                        GraphicsState state = g.Save();
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(qr, box.Left + 4f, y + 2f, qrSize, qrSize);
                        g.Restore(state);
                    }
                    var lotArea = new RectangleF(box.Left + qrSize + 10f, y + 2f, box.Right - box.Left - qrSize - 14f, qrSize);
                    g.DrawString(Convert.ToString(row["LotNo"]), lotFont, Brushes.Black, lotArea, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
            }
        }

        /// <summary>Bitmap mã QR (cùng bộ sinh QR của DevExpress như tem MES). Mức sửa lỗi M để quét tốt khi in nhỏ.</summary>
        internal static Bitmap CreateQr(string payload)
        {
            var bitmap = new Bitmap(300, 300);
            using (var control = new BarCodeControl())
            {
                control.Size = new Size(300, 300);
                control.AutoModule = true;
                control.Text = payload;
                control.Symbology = new QRCodeGenerator
                {
                    CompactionMode = QRCodeCompactionMode.Byte,
                    ErrorCorrectionLevel = QRCodeErrorCorrectionLevel.M,
                    Version = QRCodeVersion.AutoVersion
                };
                control.CreateControl();
                control.DrawToBitmap(bitmap, new Rectangle(0, 0, 300, 300));
            }
            return bitmap;
        }
    }

    public static class ComponentPoLabelFlow
    {
        private static readonly int[] PrintableStatuses =
            { ComponentPoStatus.Released, ComponentPoStatus.PartiallyReceived, ComponentPoStatus.Received };

        /// <summary>In lần đầu: PO đã phát hành đến đã nhận đủ. In lại (rách / không đọc được mã vạch): thêm cả PO đã đóng.</summary>
        public static bool CanPrint(UiScreenRowContext row, bool reprint)
        {
            int status;
            if (row == null || !row.HasRow || !ComponentPoStatus.TryFromName(row.GetText("Status"), out status)) return false;
            return PrintableStatuses.Contains(status) || (reprint && status == ComponentPoStatus.Closed);
        }

        public static string Run(IWin32Window owner, UiScreenRowContext row, bool reprint)
        {
            if (row == null || !row.HasRow) throw new InvalidOperationException("Chưa chọn phiếu đặt hàng nào.");
            object idValue = row.GetValue("PurchaseOrderId");
            if (idValue == null) throw new InvalidOperationException("Không đọc được Id của phiếu đặt hàng.");
            long id = Convert.ToInt64(idValue, CultureInfo.InvariantCulture);
            string poNo = row.GetText("PurchaseOrderNo") ?? id.ToString(CultureInfo.InvariantCulture);

            var service = new ComponentPoLabelService(new ComponentPoLabelRepository());
            DataTable labels = service.GetLabels(id, reprint);
            if (labels.Rows.Count == 0)
                return reprint
                    ? "PO " + poNo + " chưa có tem (LOT) nào để in."
                    : "PO " + poNo + ": mọi tem đã được in lần đầu. Dùng \"In lại tem\" nếu cần in lại.";

            string reason = null;
            if (reprint)
            {
                // Chỉ in lại các tem được chọn (rách / không đọc được), không bắt in lại cả PO.
                labels = ComponentLabelPicker.Pick(owner, labels, "Chọn tem cần in lại - PO " + poNo, new[]
                {
                    new KeyValuePair<string, string>("LineNo", "Dòng"),
                    new KeyValuePair<string, string>("Seq", "Stt"),
                    new KeyValuePair<string, string>("LotNo", "Số lô"),
                    new KeyValuePair<string, string>("PartNo", "Mã vật tư"),
                    new KeyValuePair<string, string>("IssuedQty", "Số lượng")
                });
                if (labels == null || labels.Rows.Count == 0) return null;
                reason = XtraInputBox.Show("Nhập lý do in lại tem (bắt buộc):", "In lại tem " + poNo, string.Empty);
                if (string.IsNullOrWhiteSpace(reason)) return null;
                reason = reason.Trim();
            }
            else
            {
                string ask = "In " + labels.Rows.Count + " tem QR của PO " + poNo + " (" + ComponentPoLabelSheet.PageCount(labels.Rows.Count) +
                             " trang A4, " + ComponentPoLabelSheet.PerPage + " tem/trang)?";
                if (XtraMessageBox.Show(owner, ask, "In tem PO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return null;
            }

            if (!ComponentPoLabelSheet.Print(owner, labels, "Tem PO " + poNo)) return null;

            var ids = labels.Rows.Cast<DataRow>().Select(r => Convert.ToInt64(r["ComponentLotId"], CultureInfo.InvariantCulture)).ToList();
            try
            {
                service.LogPrinted(id, reprint, reason, ids);
            }
            catch (Exception ex)
            {
                // Tem đã ra máy in nhưng chưa ghi lịch sử: nếu im lặng, lần sau "In tem" sẽ in lại cùng các tem này.
                throw new InvalidOperationException("Đã gửi " + labels.Rows.Count + " tem tới máy in nhưng KHÔNG ghi được lịch sử in: " +
                    ex.Message + "\r\nKhông bấm In tem lần nữa (tem sẽ in trùng). Báo quản trị để ghi nhận hoặc dùng \"In lại tem\" khi cần.", ex);
            }
            return "Đã gửi " + labels.Rows.Count + " tem của PO " + poNo + " tới máy in (" + (reprint ? "in lại" : "in lần đầu") + ").";
        }
    }

    /// <summary>Nút "In tem" / "In lại tem" trên màn hình Phiếu đặt hàng linh kiện.</summary>
    public static class ComponentPoLabelActions
    {
        public static IList<UiScreenAction> Create()
        {
            return new List<UiScreenAction>
            {
                Step("In tem", 90, false), Step("In lại tem", 100, true),
                PalletStep("In tem palet", 115, false), PalletStep("In lại tem palet", 135, true)
            };
        }

        private static UiScreenAction Step(string caption, int width, bool reprint)
        {
            return new UiScreenAction(caption, width, "PurchaseOrderId",
                () =>
                {
                    var session = UserSession.Current;
                    return session != null && session.HasPermission(ComponentPoStatus.Function, "CONFIRM");
                },
                row => ComponentPoLabelFlow.CanPrint(row, reprint),
                (owner, site, row) => ComponentPoLabelFlow.Run(owner, row, reprint));
        }

        private static UiScreenAction PalletStep(string caption, int width, bool reprint)
        {
            return new UiScreenAction(caption, width, "PurchaseOrderId",
                () =>
                {
                    var session = UserSession.Current;
                    return session != null && session.HasPermission(ComponentPoStatus.Function, "CONFIRM");
                },
                row => ComponentPoLabelFlow.CanPrint(row, reprint),
                (owner, site, row) => ComponentPalletLabelFlow.Run(owner, row, reprint));
        }
    }
}
