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
using DevExpress.XtraReports.UI;
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

    /// <summary>Chuẩn bị dữ liệu cho báo cáo tem: thêm các cột chữ đã định dạng (ngày, giờ, số lượng...) để bố cục trong Report Designer chỉ cần gắn [Cột].</summary>
    public static class ComponentPoLabelData
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static DataTable PrepareCarton(DataTable source, DateTime printedAt)
        {
            DataTable t = source.Copy();
            foreach (string c in new[] { "DateText", "SeqText", "TimeText", "QtyText" }) t.Columns.Add(c, typeof(string));
            foreach (DataRow r in t.Rows)
            {
                r["DateText"] = DateText(r["LotIssueDate"], "dd-MMM");
                r["SeqText"] = Convert.ToString(r["Seq"], Inv);
                r["TimeText"] = printedAt.Hour.ToString(Inv) + "h";
                r["QtyText"] = QtyText(r["IssuedQty"]);
            }
            return t;
        }

        public static DataTable PreparePallet(DataTable source)
        {
            DataTable t = source.Copy();
            foreach (string c in new[] { "PalletText", "DateText", "QtyText", "SeqText" }) t.Columns.Add(c, typeof(string));
            foreach (DataRow r in t.Rows)
            {
                string seq = Convert.ToString(r["PalletSeq"], Inv) + " / " + Convert.ToString(r["PalletCount"], Inv);
                r["SeqText"] = seq;
                r["PalletText"] = "PALLET " + seq;
                r["DateText"] = DateText(r["LotIssueDate"], "dd-MMM-yyyy");
                r["QtyText"] = QtyText(r["TotalQty"]);
            }
            return t;
        }

        private static string DateText(object value, string format)
        {
            return value == null || value == DBNull.Value ? string.Empty : Convert.ToDateTime(value, Inv).ToString(format, Inv);
        }

        private static string QtyText(object value)
        {
            return value == null || value == DBNull.Value ? string.Empty : Convert.ToDecimal(value, Inv).ToString("#,##0.###", Inv);
        }
    }

    /// <summary>In báo cáo tem (XtraReport) ra máy in người dùng chọn. Trả false nếu hủy hộp thoại in.</summary>
    internal static class ComponentLabelReportPrinter
    {
        public static bool Print(IWin32Window owner, XtraReport report, string title)
        {
            using (report)
            using (var dialog = new PrintDialog { PrinterSettings = new PrinterSettings(), UseEXDialog = true })
            {
                report.DisplayName = title;
                if (dialog.ShowDialog(owner) != DialogResult.OK) return false;
                using (var tool = new ReportPrintTool(report))
                    tool.Print(dialog.PrinterSettings.PrinterName);
                return true;
            }
        }
    }

    /// <summary>
    /// Tem thùng linh kiện: ComponentCartonLabelReport (A4 ngang, 4 cột x 4 hàng = 16 tem/trang, mỗi ô khoảng 72 x 47 mm).
    /// Bố cục chỉnh trong Report Designer; mỗi tem một LOT: Date | Stt | Time, PartNo, PartName, Maker | Quantity,
    /// mã QR (QrPayload của LOT), Quantity / DeptName-INVOICE và số LOT ở dòng cuối.
    /// </summary>
    public static class ComponentPoLabelSheet
    {
        public const int Columns = 4;
        public const int Rows = 4;
        public const int PerPage = Columns * Rows;

        public static int PageCount(int labels) { return (labels + PerPage - 1) / PerPage; }

        public static bool Print(IWin32Window owner, DataTable labels, string title)
        {
            var report = new Reports.ComponentCartonLabelReport { DataSource = ComponentPoLabelData.PrepareCarton(labels, DateTime.Now) };
            return ComponentLabelReportPrinter.Print(owner, report, title);
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
