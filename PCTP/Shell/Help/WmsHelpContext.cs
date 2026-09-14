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

                if (Contains(name, "BaoCao") || Contains(type, "BaoCao"))
                    return "BaoCao.TraCuu";
                if (Contains(name, "NhapKho") || Contains(type, "NhapKho"))
                    return "NhapKho.QR";
                if (Contains(name, "GiaoHangHVN") || Contains(type, "HVN"))
                    return "GiaoHang.HVN";
                if (Contains(name, "GiaoHangYMVN") || Contains(type, "YMVN"))
                    return "GiaoHang.YMVN";
                if (Contains(name, "HangLoi") || Contains(type, "HangLoi"))
                    return "XuLyHangLoi";

                current = current.Parent;
            }

            return "Dashboard";
        }

        private static bool Contains(string value, string token)
        {
            return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
