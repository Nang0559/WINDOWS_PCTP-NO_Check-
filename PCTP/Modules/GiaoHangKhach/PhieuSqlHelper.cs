using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach
{
    internal static class PhieuSqlHelper
    {
        public static void ValidateTenBan(string tenBan)
        {
            if (string.IsNullOrWhiteSpace(tenBan) ||
                Regex.IsMatch(tenBan, @"[^A-Za-z0-9_]"))
            {
                throw new ArgumentException(
                    $"Tên bảng không hợp lệ: '{tenBan}'");
            }
        }
    }
}
