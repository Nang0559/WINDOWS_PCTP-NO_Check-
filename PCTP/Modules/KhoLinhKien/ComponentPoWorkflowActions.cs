using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using PCTP.Framework.UiLayout;
using PCTP.Security;
using PCTP.Shared.UiMd;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>
    /// Nút workflow cho màn hình layout "Phiếu đặt hàng linh kiện": Kiểm tra (DRAFT→VALIDATED), Phát hành
    /// (VALIDATED→RELEASED, sinh LOT/QR), Đóng PO và Hủy PO, tác động lên PO đang chọn.
    /// Chuyển trạng thái nào hợp lệ lấy từ bảng workflow chuẩn (ProcessCode MES_COMPONENT_PO); service kiểm lại quyền
    /// theo Site và transition, SQL kiểm lại trạng thái thật trong transaction. Bật/tắt nút chỉ để dễ dùng, không phải cổng chặn.
    /// </summary>
    public static class ComponentPoWorkflowActions
    {
        private const string KeyColumn = "PurchaseOrderId";

        /// <summary>Các nút Kiểm tra / Phát hành / Đóng PO / Hủy PO, theo thứ tự trái → phải.</summary>
        public static IList<UiScreenAction> CreateSteps()
        {
            var transitions = new TransitionCache();
            return new List<UiScreenAction>
            {
                Step("Kiểm tra", 90, ComponentPoStatus.Validated, transitions),
                Step("Phát hành", 100, ComponentPoStatus.Released, transitions),
                Step("Đóng PO", 90, ComponentPoStatus.Closed, transitions),
                Step("Hủy PO", 90, ComponentPoStatus.Cancelled, transitions)
            };
        }

        private static UiScreenAction Step(string caption, int width, int toStatus, TransitionCache transitions)
        {
            return new UiScreenAction(caption, width, KeyColumn,
                () =>
                {
                    var session = UserSession.Current;
                    return session != null && session.HasPermission(ComponentPoStatus.Function, ComponentPoStatus.RequiredAction(toStatus));
                },
                row => IsEnabled(row, toStatus, transitions),
                (owner, site, row) => Run(owner, row, caption, toStatus));
        }

        private static bool IsEnabled(UiScreenRowContext row, int toStatus, TransitionCache transitions)
        {
            if (row == null || !row.HasRow) return false;
            if (ComponentPoStatus.IsReceiptDriven(toStatus)) return false;
            int from;
            if (!ComponentPoStatus.TryFromName(row.GetText("Status"), out from)) return false;
            return transitions.Has(from, toStatus);
        }

        private static string Run(IWin32Window owner, UiScreenRowContext row, string caption, int toStatus)
        {
            if (row == null || !row.HasRow) throw new InvalidOperationException("Chưa chọn phiếu đặt hàng nào.");
            object idValue = row.GetValue(KeyColumn);
            if (idValue == null) throw new InvalidOperationException("Không đọc được Id của phiếu đặt hàng.");
            long id = Convert.ToInt64(idValue);
            string poNo = row.GetText("PurchaseOrderNo") ?? id.ToString();
            int from;
            if (!ComponentPoStatus.TryFromName(row.GetText("Status"), out from))
                throw new InvalidOperationException("Không đọc được trạng thái của PO " + poNo + ".");

            string reason = null;
            if (ComponentPoStatus.NeedsReason(from, toStatus))
            {
                string label = toStatus == ComponentPoStatus.Closed ? "Đóng sớm PO" : caption;
                reason = XtraInputBox.Show("Nhập lý do (bắt buộc):", label + " " + poNo, string.Empty);
                if (string.IsNullOrWhiteSpace(reason)) return null;   // cancelled
                reason = reason.Trim();
            }
            else
            {
                string ask = toStatus == ComponentPoStatus.Released
                    ? "Phát hành PO " + poNo + " và sinh LOT/QR cho từng dòng hàng?"
                    : caption + " cho PO " + poNo + "?";
                if (XtraMessageBox.Show(owner, ask, "Workflow PO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return null;
            }

            var service = new ComponentPurchaseOrderService(
                new ComponentPurchaseOrderRepository(),
                new WorkflowTransitionService(new ComponentPoWorkflowRepository()));
            DataTable result = service.Transition(id, from, toStatus, reason);

            string text = "PO " + poNo + " → " + ComponentPoStatus.Text(toStatus) + ".";
            if (toStatus == ComponentPoStatus.Released && result.Rows.Count > 0 && result.Columns.Contains("LotsIssued"))
                text += " Đã có " + result.Rows[0]["LotsIssued"] + " LOT/tem QR.";
            return text;
        }

        /// <summary>Đọc bảng workflow một lần cho mỗi màn hình mở (đổi dòng chọn không phải truy vấn lại SQL).</summary>
        private sealed class TransitionCache
        {
            private IReadOnlyList<WorkflowTransition> _transitions;

            public bool Has(int from, int to)
            {
                if (_transitions == null)
                    _transitions = new ComponentPoWorkflowRepository().GetTransitions(ComponentPoStatus.ProcessCode);
                return _transitions.Any(t => t.FromStatus == from && t.ToStatus == to);
            }
        }
    }
}
