using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using PCTP.Framework.UiLayout;
using PCTP.Infrastructure.Security;
using PCTP.Modules.KhoLinhKien;
using PCTP.Security;
using PCTP.Shared.Labels;

namespace PCTP.Modules.MesPlanning
{
    /// <summary>Loại tem in từ một lệnh sản xuất (khớp CK_MES_FVN_ProductionOrderLabel_Kind, migration 065).</summary>
    public enum ProductionLabelKind { Carton, Pallet }

    /// <summary>Ngữ cảnh một lệnh cho việc in tem (MES.usp_FVN_ProductionOrder_LabelContext).</summary>
    public sealed class ProductionOrderLabelInfo
    {
        public long ProductionOrderId { get; set; }
        public string SiteCode { get; set; }
        public string OrderNo { get; set; }
        public string DepartmentCode { get; set; }
        public string ProductPartNo { get; set; }
        public string Status { get; set; }
        public int PlannedQty { get; set; }
        public int CartonLabelQty { get; set; }
        public int PalletLabelQty { get; set; }
        public int? QtyPerCarton { get; set; }
        public int? QtyPerPallet { get; set; }

        public int LabeledQty(ProductionLabelKind kind) { return kind == ProductionLabelKind.Carton ? CartonLabelQty : PalletLabelQty; }
        public int RemainingQty(ProductionLabelKind kind) { return Math.Max(0, PlannedQty - LabeledQty(kind)); }
        public int? QtyPerPack(ProductionLabelKind kind) { return kind == ProductionLabelKind.Carton ? QtyPerCarton : QtyPerPallet; }
    }

    public interface IProductionOrderLabelRepository
    {
        ProductionOrderLabelInfo GetContext(long productionOrderId);
        DataTable Build(long productionOrderId, string labelKind, string actor, int? qty, int? qtyPerPack);
        DataTable GetLabels(long productionOrderId, string labelKind, bool onlyUnprinted);
        void LogPrinted(long productionOrderId, string actionCode, string reason, string actor, IReadOnlyList<long> labelIds);
    }

    public sealed class ProductionOrderLabelRepository : IProductionOrderLabelRepository
    {
        private readonly string _connectionString;

        public ProductionOrderLabelRepository(string connectionString = null)
        {
            _connectionString = connectionString ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public ProductionOrderLabelInfo GetContext(long productionOrderId)
        {
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ProductionOrder_LabelContext", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 })
            {
                cmd.Parameters.Add("@ProductionOrderId", SqlDbType.BigInt).Value = productionOrderId;
                using (var da = new SqlDataAdapter(cmd)) da.Fill(table);
            }
            if (table.Rows.Count == 0) return null;
            DataRow r = table.Rows[0];
            return new ProductionOrderLabelInfo
            {
                ProductionOrderId = Convert.ToInt64(r["ProductionOrderId"], CultureInfo.InvariantCulture),
                SiteCode = Convert.ToString(r["SiteCode"], CultureInfo.InvariantCulture),
                OrderNo = Convert.ToString(r["OrderNo"], CultureInfo.InvariantCulture),
                DepartmentCode = Convert.ToString(r["DepartmentCode"], CultureInfo.InvariantCulture),
                ProductPartNo = Convert.ToString(r["ProductPartNo"], CultureInfo.InvariantCulture),
                Status = Convert.ToString(r["Status"], CultureInfo.InvariantCulture),
                PlannedQty = Convert.ToInt32(r["PlannedQty"], CultureInfo.InvariantCulture),
                CartonLabelQty = Convert.ToInt32(r["CartonLabelQty"], CultureInfo.InvariantCulture),
                PalletLabelQty = Convert.ToInt32(r["PalletLabelQty"], CultureInfo.InvariantCulture),
                QtyPerCarton = NullableInt(r["QtyPerCarton"]),
                QtyPerPallet = NullableInt(r["QtyPerPallet"])
            };
        }

        public DataTable Build(long productionOrderId, string labelKind, string actor, int? qty, int? qtyPerPack)
        {
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ProductionOrder_LabelBuild", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 })
            {
                cmd.Parameters.Add("@ProductionOrderId", SqlDbType.BigInt).Value = productionOrderId;
                cmd.Parameters.Add("@LabelKind", SqlDbType.VarChar, 10).Value = labelKind;
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                cmd.Parameters.Add("@Qty", SqlDbType.Int).Value = qty.HasValue ? (object)qty.Value : DBNull.Value;
                cmd.Parameters.Add("@QtyPerPack", SqlDbType.Int).Value = qtyPerPack.HasValue ? (object)qtyPerPack.Value : DBNull.Value;
                using (var da = new SqlDataAdapter(cmd)) da.Fill(table);
            }
            return table;
        }

        public DataTable GetLabels(long productionOrderId, string labelKind, bool onlyUnprinted)
        {
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ProductionOrder_LabelData", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 })
            {
                cmd.Parameters.Add("@ProductionOrderId", SqlDbType.BigInt).Value = productionOrderId;
                cmd.Parameters.Add("@LabelKind", SqlDbType.VarChar, 10).Value = labelKind;
                cmd.Parameters.Add("@OnlyUnprinted", SqlDbType.Bit).Value = onlyUnprinted;
                using (var da = new SqlDataAdapter(cmd)) da.Fill(table);
            }
            return table;
        }

        public void LogPrinted(long productionOrderId, string actionCode, string reason, string actor, IReadOnlyList<long> labelIds)
        {
            var json = new StringBuilder("[");
            for (int i = 0; i < labelIds.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append(labelIds[i].ToString(CultureInfo.InvariantCulture));
            }
            json.Append(']');
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ProductionOrder_LabelLog", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 })
            {
                cmd.Parameters.Add("@ProductionOrderId", SqlDbType.BigInt).Value = productionOrderId;
                cmd.Parameters.Add("@ActionCode", SqlDbType.VarChar, 20).Value = actionCode;
                cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 300).Value =
                    string.IsNullOrWhiteSpace(reason) ? (object)DBNull.Value : reason;
                cmd.Parameters.Add("@RequestedBy", SqlDbType.NVarChar, 100).Value = actor;
                cmd.Parameters.Add("@LabelIdsJson", SqlDbType.NVarChar, -1).Value = json.ToString();
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private static int? NullableInt(object value)
        {
            return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Ranh giới ứng dụng cho in tem lệnh sản xuất: kiểm quyền CONFIRM theo Site và bộ phận của lệnh (như các thao tác thực thi khác
    /// của MES.PRODUCTION_EXECUTION); SQL (065) kiểm lại trạng thái lệnh, SL tối đa và ghi lịch sử in trong transaction.
    /// </summary>
    public sealed class ProductionOrderLabelService
    {
        private readonly IProductionOrderLabelRepository _repository;

        public ProductionOrderLabelService(IProductionOrderLabelRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public ProductionOrderLabelInfo GetContext(long productionOrderId)
        {
            ProductionOrderLabelInfo info = Load(productionOrderId);
            Demand(info);
            return info;
        }

        /// <summary>Tạo phần tem còn thiếu (SL chưa có tem của loại đó). Trả về số tem tạo mới và SL được gắn tem.</summary>
        public DataTable Build(long productionOrderId, ProductionLabelKind kind, int? qty, int? qtyPerPack)
        {
            Demand(Load(productionOrderId));
            return _repository.Build(productionOrderId, KindCode(kind), AuthorizationGuard.ActorCode, qty, qtyPerPack);
        }

        /// <summary>reprint = false chỉ lấy tem chưa in lần đầu.</summary>
        public DataTable GetLabels(long productionOrderId, ProductionLabelKind kind, bool reprint)
        {
            Demand(Load(productionOrderId));
            return _repository.GetLabels(productionOrderId, KindCode(kind), !reprint);
        }

        public void LogPrinted(long productionOrderId, bool reprint, string reason, IReadOnlyList<long> labelIds)
        {
            Demand(Load(productionOrderId));
            if (reprint && string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Bắt buộc nhập lý do in lại.");
            _repository.LogPrinted(productionOrderId, reprint ? "REPRINT" : "INITIAL_PRINT", reason, AuthorizationGuard.ActorCode, labelIds);
        }

        public static string KindCode(ProductionLabelKind kind) { return kind == ProductionLabelKind.Carton ? "CARTON" : "PALLET"; }

        private ProductionOrderLabelInfo Load(long productionOrderId)
        {
            if (productionOrderId <= 0) throw new ArgumentException("Lệnh sản xuất không hợp lệ.");
            ProductionOrderLabelInfo info = _repository.GetContext(productionOrderId);
            if (info == null) throw new InvalidOperationException("Không tìm thấy lệnh sản xuất.");
            return info;
        }

        private static void Demand(ProductionOrderLabelInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.SiteCode))
                throw new UnauthorizedAccessException("Không xác định được Site của lệnh sản xuất.");
            if (string.IsNullOrWhiteSpace(info.DepartmentCode))
                throw new UnauthorizedAccessException("Lệnh sản xuất chưa được gán bộ phận.");
            AuthorizationGuard.DemandActionAndScope(ProductionOrderStatus.Function, "CONFIRM", "SITE", info.SiteCode, info.SiteCode);
            AuthorizationGuard.DemandActionAndScope(ProductionOrderStatus.Function, "CONFIRM", "DEPARTMENT", info.DepartmentCode, info.SiteCode);
        }
    }

    /// <summary>Xem trước + in một loại tem của lệnh (A4 ngang, 1 tem / trang); chỉ trả true khi người dùng thật sự bấm In.</summary>
    public static class ProductionLabelSheet
    {
        public const int PerPage = 1;

        public static bool Print(IWin32Window owner, ProductionLabelKind kind, DataTable labels, string title)
        {
            LabelDefinition def = kind == ProductionLabelKind.Carton ? ProductionLabelCatalog.Carton : ProductionLabelCatalog.Pallet;
            return LabelReportService.PreviewAndPrint(owner, def, labels, title);
        }
    }

    /// <summary>
    /// Luồng "In tem thùng" / "In tem palet" (+ in lại) của một lệnh sản xuất. In lần đầu: tạo phần tem còn thiếu theo quy cách đóng gói
    /// → in các tem chưa in → ghi lịch sử INITIAL_PRINT. In lại: chọn tem, bắt buộc lý do → ghi REPRINT.
    /// </summary>
    public static class ProductionOrderLabelFlow
    {
        public static bool CanPrint(UiScreenRowContext row, bool reprint)
        {
            int status;
            if (row == null || !row.HasRow || !ProductionOrderStatus.TryFromName(row.GetText("Status"), out status)) return false;
            return reprint ? ProductionOrderStatus.CanReprintLabels(status) : ProductionOrderStatus.CanBuildLabels(status);
        }

        public static string Run(IWin32Window owner, UiScreenRowContext row, ProductionLabelKind kind, bool reprint)
        {
            if (row == null || !row.HasRow) throw new InvalidOperationException("Chưa chọn lệnh sản xuất nào.");
            object idValue = row.GetValue("ProductionOrderId");
            if (idValue == null) throw new InvalidOperationException("Không đọc được Id của lệnh sản xuất.");
            long id = Convert.ToInt64(idValue, CultureInfo.InvariantCulture);
            string orderNo = row.GetText("OrderNo") ?? id.ToString(CultureInfo.InvariantCulture);
            string kindText = kind == ProductionLabelKind.Carton ? "tem thùng" : "tem palet";

            var service = new ProductionOrderLabelService(new ProductionOrderLabelRepository());
            string reason = null;
            DataTable labels;

            if (reprint)
            {
                labels = service.GetLabels(id, kind, true);
                if (labels.Rows.Count == 0) return "Lệnh " + orderNo + " chưa có " + kindText + " nào để in lại.";
                labels = ComponentLabelPicker.Pick(owner, labels, "Chọn " + kindText + " cần in lại - lệnh " + orderNo, new[]
                {
                    new KeyValuePair<string, string>("Seq", "STT"),
                    new KeyValuePair<string, string>("LabelNo", "Số tem"),
                    new KeyValuePair<string, string>("Qty", "Số lượng"),
                    new KeyValuePair<string, string>("PalletCode", "Mã palet")
                });
                if (labels == null || labels.Rows.Count == 0) return null;
                reason = XtraInputBox.Show("Nhập lý do in lại " + kindText + " (rách / không đọc được mã...):", "In lại " + kindText + " " + orderNo, string.Empty);
                if (string.IsNullOrWhiteSpace(reason)) return null;
                reason = reason.Trim();
            }
            else
            {
                ProductionOrderLabelInfo info = service.GetContext(id);
                int remaining = info.RemainingQty(kind);
                if (remaining > 0)
                {
                    int? perPack = info.QtyPerPack(kind);
                    if (!perPack.HasValue)
                    {
                        string text = XtraInputBox.Show("Chưa khai báo quy cách đóng gói của " + info.ProductPartNo + " (MES.FVN_ProductionPackSpec).\r\n" +
                            "Nhập SL / " + (kind == ProductionLabelKind.Carton ? "thùng" : "palet") + " cho lần in này:", "Quy cách " + kindText, string.Empty);
                        int parsed;
                        if (string.IsNullOrWhiteSpace(text)) return null;
                        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) || parsed < 1 || parsed > 9999)
                            throw new InvalidOperationException("SL / gói phải là số nguyên từ 1 đến 9999.");
                        perPack = parsed;
                    }
                    int count = (remaining + perPack.Value - 1) / perPack.Value;
                    string ask = "Lệnh " + orderNo + " (" + info.ProductPartNo + "), SL lệnh " + info.PlannedQty.ToString("#,##0", CultureInfo.InvariantCulture) +
                        ", đã có " + kindText + " cho " + info.LabeledQty(kind).ToString("#,##0", CultureInfo.InvariantCulture) + ".\r\n" +
                        "Tạo " + count + " " + kindText + " cho " + remaining.ToString("#,##0", CultureInfo.InvariantCulture) + " sản phẩm còn lại (" +
                        perPack.Value + " / gói) rồi in?";
                    if (XtraMessageBox.Show(owner, ask, "In " + kindText, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return null;
                    service.Build(id, kind, null, perPack.Value);
                }
                labels = service.GetLabels(id, kind, false);
                if (labels.Rows.Count == 0)
                    return "Lệnh " + orderNo + ": mọi " + kindText + " đã được in lần đầu. Dùng \"In lại\" nếu cần in lại.";
            }

            if (!ProductionLabelSheet.Print(owner, kind, labels, (kind == ProductionLabelKind.Carton ? "Tem thùng " : "Tem palet ") + orderNo)) return null;

            var ids = labels.Rows.Cast<DataRow>().Select(r => Convert.ToInt64(r["ProductionOrderLabelId"], CultureInfo.InvariantCulture)).ToList();
            try
            {
                service.LogPrinted(id, reprint, reason, ids);
            }
            catch (Exception ex)
            {
                // Tem đã ra máy in nhưng chưa ghi lịch sử: im lặng thì lần sau "In" sẽ in trùng.
                throw new InvalidOperationException("Đã gửi " + labels.Rows.Count + " " + kindText + " tới máy in nhưng KHÔNG ghi được lịch sử in: " +
                    ex.Message + "\r\nKhông bấm In lần nữa (tem sẽ in trùng). Báo quản trị để ghi nhận hoặc dùng \"In lại\" khi cần.", ex);
            }
            return "Đã gửi " + labels.Rows.Count + " " + kindText + " của lệnh " + orderNo + " tới máy in (" + (reprint ? "in lại" : "in lần đầu") + ").";
        }
    }

    /// <summary>Bốn nút trên màn hình Lệnh sản xuất: In tem thùng / In lại tem thùng / In tem palet / In lại tem palet.</summary>
    public static class ProductionOrderLabelActions
    {
        public static IList<UiScreenAction> Create()
        {
            return new List<UiScreenAction>
            {
                Step("In tem thùng", 105, ProductionLabelKind.Carton, false),
                Step("In lại tem thùng", 125, ProductionLabelKind.Carton, true),
                Step("In tem palet", 100, ProductionLabelKind.Pallet, false),
                Step("In lại tem palet", 120, ProductionLabelKind.Pallet, true)
            };
        }

        private static UiScreenAction Step(string caption, int width, ProductionLabelKind kind, bool reprint)
        {
            return new UiScreenAction(caption, width, "ProductionOrderId",
                () =>
                {
                    var session = UserSession.Current;
                    return session != null && session.HasPermission(ProductionOrderStatus.Function, "CONFIRM");
                },
                row => ProductionOrderLabelFlow.CanPrint(row, reprint),
                (owner, site, row) => ProductionOrderLabelFlow.Run(owner, row, kind, reprint));
        }
    }
}
