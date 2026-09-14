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

                // Exact operational forms first. These are more reliable than
                // broad module-name matching because several legacy forms live
                // under PCTP.QRCODE_HVN namespaces.
                if (IsAny(name, type, "FormBaoCaoTraceability"))
                    return "BaoCao.TraCuu";
                if (IsAny(name, type, "FormBaoCaoStockHistory"))
                    return "BaoCao.TraCuu";
                if (IsAny(name, type, "FormBaoCaoQualityHistory"))
                    return "BaoCao.TraCuu";

                if (IsAny(name, type, "GIAOHANGYMN"))
                    return "GiaoHang.YMVN";
                if (IsAny(name, type, "YAMAHAQRCDE_SP"))
                    return "GiaoHang.YMVN";

                if (IsAny(name, type, "HVN_PGH"))
                    return "GiaoHang.HVN";
                if (IsAny(name, type, "Edit_DH"))
                    return "GiaoHang.HVN";

                if (IsAny(name, type, "BaoCao"))
                    return "BaoCao.TraCuu";
                if (IsAny(name, type, "NhapKho"))
                    return "NhapKho.QR";
                if (IsAny(name, type, "GiaoHangHVN") || IsAny(name, type, "HVN"))
                    return "GiaoHang.HVN";
                if (IsAny(name, type, "GiaoHangYMVN") || IsAny(name, type, "YMVN"))
                    return "GiaoHang.YMVN";
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
