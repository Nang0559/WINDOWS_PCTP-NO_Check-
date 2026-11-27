using System.Globalization;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>
    /// Yêu cầu mua hàng linh kiện (PO) cho kho PART: upload Excel → DRAFT → VALIDATED → RELEASED
    /// → (nhận QR) PARTIALLY_RECEIVED / RECEIVED → CLOSED, hoặc CANCELLED.
    /// Trạng thái dùng bảng workflow chuẩn dbo.FVN_sys_WorkflowTransitions (ProcessCode MES_COMPONENT_PO);
    /// SQL kiểm tra lại transition và ghi nhật ký trong cùng transaction (migration 043).
    /// </summary>
    public static class ComponentPoStatus
    {
        public const string ProcessCode = "MES_COMPONENT_PO";
        public const string Function = "WAREHOUSE.COMPONENT_PO";

        public const int Draft = 0;
        public const int Validated = 10;
        public const int Released = 20;
        public const int PartiallyReceived = 30;
        public const int Received = 40;
        public const int Closed = 50;
        public const int Cancelled = 900;

        /// <summary>30 / 40 do thủ tục nhận hàng QR ghi nhận, không đổi thủ công.</summary>
        public static bool IsReceiptDriven(int statusCode)
        {
            return statusCode == PartiallyReceived || statusCode == Received;
        }

        public static string Text(int statusCode)
        {
            switch (statusCode)
            {
                case Draft: return "0. Nháp (mới upload)";
                case Validated: return "1. Đã kiểm tra";
                case Released: return "2. Đã phát hành (chờ nhận)";
                case PartiallyReceived: return "3. Đang nhận";
                case Received: return "4. Đã nhận đủ";
                case Closed: return "5. Đã đóng";
                case Cancelled: return "Đã hủy";
                default: return statusCode.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Đọc tên trạng thái lưu trong MES.FVN_ComponentPurchaseOrder.Status (DRAFT, VALIDATED, ...) thành mã workflow.
        /// Cùng bảng ánh xạ với MES.fn_FVN_ComponentPoStatusCode; SQL vẫn kiểm lại trạng thái thật khi chuyển.
        /// </summary>
        public static bool TryFromName(string name, out int statusCode)
        {
            switch ((name ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "DRAFT": statusCode = Draft; return true;
                case "VALIDATED": statusCode = Validated; return true;
                case "RELEASED": statusCode = Released; return true;
                case "PARTIALLY_RECEIVED": statusCode = PartiallyReceived; return true;
                case "RECEIVED": statusCode = Received; return true;
                case "CLOSED": statusCode = Closed; return true;
                case "CANCELLED": statusCode = Cancelled; return true;
                default: statusCode = -1; return false;
            }
        }

        /// <summary>Hủy PO và đóng sớm PO đang nhận dở bắt buộc nhập lý do (service và SQL cũng kiểm).</summary>
        public static bool NeedsReason(int fromStatus, int toStatus)
        {
            return toStatus == Cancelled || (toStatus == Closed && fromStatus == PartiallyReceived);
        }

        /// <summary>Quyền (action) cần có để đưa PO tới trạng thái đích.</summary>
        public static string RequiredAction(int toStatus)
        {
            switch (toStatus)
            {
                case Validated: return "CONFIRM";
                case Released: return "APPROVE";
                case Closed: return "APPROVE";
                case Cancelled: return "CANCEL";
                default: return null;
            }
        }
    }
}
