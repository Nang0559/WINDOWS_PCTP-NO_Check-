using System;
using System.Windows.Forms;

namespace PCTP.Shell.Help
{
    internal static class WmsHelpContext
    {
        internal static string Resolve(Control control)
        {
            Control current = control;
            while (current != null)
            {
                string name = current.Name ?? string.Empty;
                string type = current.GetType().Name ?? string.Empty;

                // BaoCao: read-only query/report screens.
                if (IsAny(name, type, "FormBaoCaoTraceability") ||
                    IsAny(name, type, "FormBaoCaoStockHistory") ||
                    IsAny(name, type, "FormBaoCaoQualityHistory"))
                {
                    return "BaoCao.TraCuu";
                }

                // GiaoHangKhach uses one operational form for all customers:
                // HVN_PGH. Customer-specific behavior/table mapping is resolved
                // inside that form through CustomerConfig -> Delivery
                // (GiaoHangKhachCustomerOptions). Help routing therefore must
                // NOT branch on legacy customer-specific forms or table names.
                if (IsAny(name, type, "HVN_PGH") ||
                    IsAny(name, type, "Edit_DH"))
                {
                    return "GiaoHang.HVN";
                }

                if (IsAny(name, type, "NhapKho"))
                    return "NhapKho.QR";

                if (IsAny(name, type, "BaoCao"))
                    return "BaoCao.TraCuu";

                if (IsAny(name, type, "HangLoi"))
                    return "XuLyHangLoi";

                current = current.Parent;
            }

            return "Dashboard";
        }

        private static bool IsAny(string name, string type, string token)
        {
            return Contains(name, token) || Contains(type, token);
        }

        private static bool Contains(string value, string token)
        {
            return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
