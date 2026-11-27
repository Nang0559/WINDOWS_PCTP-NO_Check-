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

    /// <summary>
    /// Phiếu quản lý nhập linh kiện palet: ComponentPalletLabelReport (A4 ngang, 1 phiếu / trang) cùng khung, font và vị trí QR với tem thùng.
    /// Bố cục chỉnh trong Report Designer.
    /// </summary>
    public static class ComponentPalletLabelSheet
    {
        public const int Columns = 1;
        public const int Rows = 1;
        public const int PerPage = Columns * Rows;

        public static int PageCount(int labels) { return (labels + PerPage - 1) / PerPage; }

        public static bool Print(IWin32Window owner, DataTable pallets, string title)
        {
            var report = new Reports.ComponentPalletLabelReport { DataSource = ComponentPoLabelData.PreparePallet(pallets) };
            return ComponentLabelReportPrinter.Print(owner, report, title);
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
