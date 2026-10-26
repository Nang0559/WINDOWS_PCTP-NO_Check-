using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Helpers
{
    public static class GioHelper
    {
        /// <summary>
        /// Chuẩn hóa giờ về dạng 2 chữ số.
        /// Ví dụ:
        /// "6"     -> "06"
        /// "06"    -> "06"
        /// "6H"    -> "06"
        /// "06H"   -> "06"
        /// "06:30" -> "06"
        /// " 7:00 H " -> "07"
        /// null    -> "00"
        /// </summary>
        public static string NormalizeGio(string gio)
        {
            if (string.IsNullOrWhiteSpace(gio))
                return "00";

            gio = gio
                .Replace("H", "")
                .Replace("h", "")
                .Trim();

            int colonIdx = gio.IndexOf(':');

            if (colonIdx >= 0)
            {
                gio = gio.Substring(0, colonIdx).Trim();
            }

            return int.TryParse(gio, out int gioInt)
                ? gioInt.ToString("00")
                : "00";
        }
    }
}
