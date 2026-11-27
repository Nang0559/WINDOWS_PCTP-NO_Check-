using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using PCTP.Framework.UiLayout;
using PCTP.Modules.KhoLinhKien;
using PCTP.Security;
using PCTP.Shared.UiMd;

namespace PCTP.Modules.MesPlanning
{
    /// <summary>
    /// Upload Excel kế hoạch sản xuất theo ca: chọn file → đọc sheet → xác nhận → nhập toàn bộ trong một transaction (trạng thái IMPORTED).
    /// Service kiểm lại CREATE và Site từng dòng, SQL kiểm tiếp; ở đây không có cổng chặn nào.
    /// </summary>
    public static class ProductionPlanUploadFlow
    {
        public const string Title = "Upload kế hoạch sản xuất";

        /// <summary>Trả về dòng trạng thái, hoặc null khi người dùng hủy. Ném lỗi khi đọc / nhập thất bại.</summary>
        public static string Run(IWin32Window owner, IProductionPlanService service)
        {
            if (service == null) throw new ArgumentNullException("service");
            string path;
            using (var dlg = new OpenFileDialog { Filter = "Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm", Title = "Chọn file kế hoạch sản xuất theo ca" })
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return null;
                path = dlg.FileName;
            }

            IList<string> sheets = ProductionPlanExcelReader.GetSheetNames(path);
            string sheet = sheets.FirstOrDefault(n => string.Equals(n, "PLAN", StringComparison.OrdinalIgnoreCase)) ?? sheets.FirstOrDefault();
            if (sheet == null) throw new InvalidOperationException("File Excel không có sheet nào.");

            List<ProductionPlanImportRow> rows = ProductionPlanExcelReader.Read(path, sheet);
            if (rows.Count == 0) throw new InvalidOperationException("Sheet \"" + sheet + "\" không có dòng kế hoạch nào.");

            int plans = rows.Select(r => (r.SiteCode ?? string.Empty).Trim() + "|" + (r.PlanNo ?? string.Empty).Trim()).Distinct().Count();
            string sites = string.Join(", ", rows.Select(r => (r.SiteCode ?? string.Empty).Trim().ToUpperInvariant())
                .Where(x => x.Length > 0).Distinct().OrderBy(x => x, StringComparer.Ordinal));
            decimal total = rows.Sum(r => r.PlannedQty);
            string range = rows.Min(r => r.PlanDate).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + " → " +
                           rows.Max(r => r.PlanDate).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            string message = "Sheet \"" + sheet + "\": " + plans + " kế hoạch, " + rows.Count + " dòng, tổng SL " +
                total.ToString("#,##0.###", CultureInfo.CurrentCulture) + ", ngày " + range +
                (sites.Length > 0 ? " (Site: " + sites + ")" : string.Empty) + ".\r\n\r\n" +
                "Upload tạo kế hoạch ở trạng thái Mới upload. Toàn bộ file chạy trong một transaction, một dòng lỗi thì cả file bị hủy.\r\n\r\nTiếp tục upload?";
            if (XtraMessageBox.Show(owner, message, Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return null;

            service.ImportRows(rows, Path.GetFileName(path), null);
            return "Đã upload " + plans + " kế hoạch (" + rows.Count + " dòng) ở trạng thái Mới upload.";
        }
    }

    /// <summary>Nút "Upload Excel kế hoạch…" trên màn hình Kế hoạch sản xuất.</summary>
    public static class ProductionPlanUploadAction
    {
        public static UiScreenAction Create()
        {
            return new UiScreenAction("Upload Excel kế hoạch…", 170,
                () =>
                {
                    var session = UserSession.Current;
                    return session != null && session.HasPermission(ProductionPlanStatus.Function, "CREATE");
                },
                (owner, site) => ProductionPlanUploadFlow.Run(owner, new ProductionPlanService(new ProductionPlanRepository())));
        }
    }

    /// <summary>Nút "Tải template kế hoạch…" cạnh nút upload: lưu file Excel mẫu đúng cột reader yêu cầu.</summary>
    public static class ProductionPlanTemplateAction
    {
        public static UiScreenAction Create()
        {
            return new UiScreenAction("Tải template kế hoạch…", 170,
                () =>
                {
                    var session = UserSession.Current;
                    return session != null && session.HasPermission(ProductionPlanStatus.Function, "CREATE");
                },
                (owner, site) => PCTP.Shared.Helpers.ExcelTemplateFlow.Run(owner, ProductionPlanTemplateDefinition.Create()));
        }
    }

    /// <summary>
    /// Nút workflow của màn hình Kế hoạch sản xuất (Kiểm tra / Chốt kế hoạch / Hủy KH) và Lệnh sản xuất (Phát hành / Bắt đầu / Tạm dừng /
    /// Tiếp tục / Hoàn thành / Đóng lệnh / Hủy lệnh), cùng khuôn với <see cref="ComponentPoWorkflowActions"/>:
    /// chuyển trạng thái nào hợp lệ lấy từ bảng workflow chuẩn; ProductionWorkflowService kiểm lại quyền theo Site / bộ phận và transition,
    /// SQL (036) kiểm lại trạng thái thật trong transaction. Bật / tắt nút chỉ để dễ dùng, không phải cổng chặn.
    /// </summary>
    public static class ProductionWorkflowActions
    {
        public static IList<UiScreenAction> CreatePlanSteps()
        {
            var transitions = new TransitionCache(ProductionPlanStatus.ProcessCode);
            return new List<UiScreenAction>
            {
                PlanStep("Kiểm tra", 90, ProductionPlanStatus.Validated, transitions),
                PlanStep("Chốt kế hoạch", 120, ProductionPlanStatus.Firm, transitions),
                PlanStep("Hủy KH", 80, ProductionPlanStatus.Cancelled, transitions)
            };
        }

        public static IList<UiScreenAction> CreateOrderSteps()
        {
            var transitions = new TransitionCache(ProductionOrderStatus.ProcessCode);
            return new List<UiScreenAction>
            {
                OrderStep("Phát hành", 90, ProductionOrderStatus.Released, transitions),
                OrderStep("Bắt đầu", 85, ProductionOrderStatus.Running, transitions),
                OrderStep("Tạm dừng", 90, ProductionOrderStatus.Paused, transitions),
                OrderStep("Hoàn thành", 95, ProductionOrderStatus.Completed, transitions),
                OrderStep("Đóng lệnh", 90, ProductionOrderStatus.Closed, transitions),
                OrderStep("Hủy lệnh", 85, ProductionOrderStatus.Cancelled, transitions)
            };
        }

        private static UiScreenAction PlanStep(string caption, int width, int toStatus, TransitionCache transitions)
        {
            return new UiScreenAction(caption, width, "ProductionPlanId",
                () => HasPermission(ProductionPlanStatus.Function, ProductionPlanStatus.RequiredAction(toStatus)),
                row => IsEnabled(row, toStatus, transitions, ProductionPlanStatus.TryFromName),
                (owner, site, row) => Run(owner, row, caption, toStatus, true));
        }

        private static UiScreenAction OrderStep(string caption, int width, int toStatus, TransitionCache transitions)
        {
            return new UiScreenAction(caption, width, "ProductionOrderId",
                () => HasPermission(ProductionOrderStatus.Function, ProductionOrderStatus.RequiredAction(toStatus)),
                row => IsEnabled(row, toStatus, transitions, ProductionOrderStatus.TryFromName),
                (owner, site, row) => Run(owner, row, caption, toStatus, false));
        }

        private delegate bool TryStatus(string name, out int code);

        private static bool HasPermission(string function, string action)
        {
            var session = UserSession.Current;
            return session != null && action != null && session.HasPermission(function, action);
        }

        private static bool IsEnabled(UiScreenRowContext row, int toStatus, TransitionCache transitions, TryStatus tryFrom)
        {
            if (row == null || !row.HasRow) return false;
            int from;
            if (!tryFrom(row.GetText("Status"), out from)) return false;
            return transitions.Has(from, toStatus);
        }

        private static string Run(IWin32Window owner, UiScreenRowContext row, string caption, int toStatus, bool plan)
        {
            string keyColumn = plan ? "ProductionPlanId" : "ProductionOrderId";
            string noColumn = plan ? "PlanNo" : "OrderNo";
            string what = plan ? "kế hoạch" : "lệnh";
            if (row == null || !row.HasRow) throw new InvalidOperationException("Chưa chọn " + what + " nào.");
            object idValue = row.GetValue(keyColumn);
            if (idValue == null) throw new InvalidOperationException("Không đọc được Id của " + what + ".");
            long id = Convert.ToInt64(idValue, CultureInfo.InvariantCulture);
            string no = row.GetText(noColumn) ?? id.ToString(CultureInfo.InvariantCulture);

            int from;
            bool known = plan ? ProductionPlanStatus.TryFromName(row.GetText("Status"), out from)
                              : ProductionOrderStatus.TryFromName(row.GetText("Status"), out from);
            if (!known) throw new InvalidOperationException("Không đọc được trạng thái của " + what + " " + no + ".");

            bool needsReason = plan ? ProductionPlanStatus.NeedsReason(from, toStatus) : ProductionOrderStatus.NeedsReason(from, toStatus);
            string reason = null;
            if (needsReason)
            {
                reason = XtraInputBox.Show("Nhập lý do (bắt buộc):", caption + " " + no, string.Empty);
                if (string.IsNullOrWhiteSpace(reason)) return null;   // cancelled
                reason = reason.Trim();
            }
            else
            {
                string ask = plan && toStatus == ProductionPlanStatus.Firm
                    ? "Chốt kế hoạch " + no + "? Hệ thống chốt BOM / routing hiệu lực và sinh yêu cầu linh kiện."
                    : caption + " cho " + what + " " + no + "?";
                if (XtraMessageBox.Show(owner, ask, "Workflow " + what, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return null;
            }

            IProductionWorkflowService service = MesWorkflowComposition.Create();
            if (plan) service.TransitionPlan(id, from, toStatus, null, reason);
            else service.TransitionOrder(id, from, toStatus, null, reason);

            return what.Substring(0, 1).ToUpperInvariant() + what.Substring(1) + " " + no + " → " +
                (plan ? ProductionPlanStatus.Text(toStatus) : ProductionOrderStatus.Text(toStatus)) + ".";
        }

        /// <summary>Đọc bảng workflow một lần cho mỗi màn hình mở (đổi dòng chọn không phải truy vấn lại SQL).</summary>
        private sealed class TransitionCache
        {
            private readonly string _processCode;
            private IReadOnlyList<WorkflowTransition> _transitions;

            public TransitionCache(string processCode) { _processCode = processCode; }

            public bool Has(int from, int to)
            {
                if (_transitions == null)
                    _transitions = new ComponentPoWorkflowRepository().GetTransitions(_processCode);
                return _transitions.Any(t => t.FromStatus == from && t.ToStatus == to);
            }
        }
    }

    /// <summary>Bộ nút của hai màn hình layout mới (migration 066): Kế hoạch sản xuất và Lệnh sản xuất.</summary>
    public static class ProductionScreenActions
    {
        public const string PlanScreenCode = "PRODUCTION_PLAN";
        public const string OrderScreenCode = "PRODUCTION_ORDER";

        public static bool Handles(string screenCode)
        {
            return string.Equals(screenCode, PlanScreenCode, StringComparison.Ordinal)
                || string.Equals(screenCode, OrderScreenCode, StringComparison.Ordinal);
        }

        public static IEnumerable<UiScreenAction> For(string screenCode)
        {
            var actions = new List<UiScreenAction>();
            if (string.Equals(screenCode, PlanScreenCode, StringComparison.Ordinal))
            {
                actions.Add(ProductionPlanUploadAction.Create());
                actions.Add(ProductionPlanTemplateAction.Create());
                actions.AddRange(ProductionWorkflowActions.CreatePlanSteps());
            }
            else if (string.Equals(screenCode, OrderScreenCode, StringComparison.Ordinal))
            {
                actions.AddRange(ProductionWorkflowActions.CreateOrderSteps());
                actions.AddRange(ProductionOrderLabelActions.Create());
            }
            return actions;
        }
    }
}
