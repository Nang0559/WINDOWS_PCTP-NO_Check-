using System.Globalization;

namespace PCTP.Modules.MesPlanning
{
    /// <summary>
    /// Kế hoạch sản xuất theo ca: upload Excel → IMPORTED(0) → VALIDATED(10) → FIRM(20), hoặc CANCELLED(900).
    /// Mã số khớp bảng workflow chuẩn dbo.FVN_sys_WorkflowTransitions (ProcessCode MES_PRODUCTION_PLAN, migration 036);
    /// SQL kiểm lại transition trong transaction, mã ở đây chỉ để bật / tắt nút và chọn quyền.
    /// </summary>
    public static class ProductionPlanStatus
    {
        public const string ProcessCode = "MES_PRODUCTION_PLAN";
        public const string Function = "MES.PLAN_UPLOAD";

        public const int Imported = 0;
        public const int Validated = 10;
        public const int Firm = 20;
        public const int Cancelled = 900;

        public static string Text(int statusCode)
        {
            switch (statusCode)
            {
                case Imported: return "0. Mới upload";
                case Validated: return "1. Đã kiểm tra";
                case Firm: return "2. Đã chốt kế hoạch";
                case Cancelled: return "Đã hủy";
                default: return statusCode.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Đọc tên trạng thái trong MES.FVN_ProductionPlan.Status (IMPORTED, VALIDATED, FIRM, CANCELLED).</summary>
        public static bool TryFromName(string name, out int statusCode)
        {
            switch ((name ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "IMPORTED": statusCode = Imported; return true;
                case "VALIDATED": statusCode = Validated; return true;
                case "FIRM": statusCode = Firm; return true;
                case "CANCELLED": statusCode = Cancelled; return true;
                default: statusCode = -1; return false;
            }
        }

        public static bool NeedsReason(int fromStatus, int toStatus) { return toStatus == Cancelled; }

        /// <summary>Quyền cần có: khớp ProductionWorkflowService.TransitionPlan (Firm = APPROVE, Cancel = CANCEL, còn lại CONFIRM).</summary>
        public static string RequiredAction(int toStatus)
        {
            switch (toStatus)
            {
                case Firm: return "APPROVE";
                case Cancelled: return "CANCEL";
                case Validated: return "CONFIRM";
                default: return null;
            }
        }
    }

    /// <summary>
    /// Lệnh sản xuất: CREATED(0) → RELEASED(10) → RUNNING(20) ⇄ PAUSED(30) → COMPLETED(40) → CLOSED(50), hoặc CANCELLED(900).
    /// ProcessCode MES_PRODUCTION_ORDER (migration 036).
    /// </summary>
    public static class ProductionOrderStatus
    {
        public const string ProcessCode = "MES_PRODUCTION_ORDER";
        public const string Function = "MES.PRODUCTION_EXECUTION";

        public const int Created = 0;
        public const int Released = 10;
        public const int Running = 20;
        public const int Paused = 30;
        public const int Completed = 40;
        public const int Closed = 50;
        public const int Cancelled = 900;

        public static string Text(int statusCode)
        {
            switch (statusCode)
            {
                case Created: return "0. Mới tạo";
                case Released: return "1. Đã phát hành";
                case Running: return "2. Đang sản xuất";
                case Paused: return "3. Tạm dừng";
                case Completed: return "4. Hoàn thành";
                case Closed: return "5. Đã đóng";
                case Cancelled: return "Đã hủy";
                default: return statusCode.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static bool TryFromName(string name, out int statusCode)
        {
            switch ((name ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "CREATED": statusCode = Created; return true;
                case "RELEASED": statusCode = Released; return true;
                case "RUNNING": statusCode = Running; return true;
                case "PAUSED": statusCode = Paused; return true;
                case "COMPLETED": statusCode = Completed; return true;
                case "CLOSED": statusCode = Closed; return true;
                case "CANCELLED": statusCode = Cancelled; return true;
                default: statusCode = -1; return false;
            }
        }

        public static bool NeedsReason(int fromStatus, int toStatus) { return toStatus == Cancelled; }

        /// <summary>Khớp ProductionWorkflowService.TransitionOrder: Release / Complete / Close = APPROVE, Cancel = CANCEL, còn lại CONFIRM.</summary>
        public static string RequiredAction(int toStatus)
        {
            switch (toStatus)
            {
                case Cancelled: return "CANCEL";
                case Released:
                case Completed:
                case Closed: return "APPROVE";
                case Running:
                case Paused: return "CONFIRM";
                default: return null;
            }
        }

        /// <summary>Tem chỉ tạo / in lần đầu cho lệnh RELEASED, RUNNING, PAUSED, COMPLETED (khớp MES.usp_FVN_ProductionOrder_LabelBuild).</summary>
        public static bool CanBuildLabels(int statusCode)
        {
            return statusCode == Released || statusCode == Running || statusCode == Paused || statusCode == Completed;
        }

        /// <summary>In lại được cả lệnh CLOSED (tem rách / không đọc được mã).</summary>
        public static bool CanReprintLabels(int statusCode) { return CanBuildLabels(statusCode) || statusCode == Closed; }
    }
}
