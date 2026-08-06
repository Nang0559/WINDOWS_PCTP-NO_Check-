using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Fuction
{
    public static class PhieuNoHelper
    {
        /// <summary>Sinh số phiếu mới cho 1 lần nhập hoặc 1 lần tách.
        /// Format: {LOT 8 ký tự cuối}-{yyMMddHHmmssfff} — đủ unique, dễ trace ngược về LOT gốc.</summary>
        public static string NewMaPhieu(string lotNo)
        {
            string lotShort = string.IsNullOrEmpty(lotNo)
                ? "NA"
                : lotNo.Length > 8 ? lotNo.Substring(lotNo.Length - 8) : lotNo;

            return $"{lotShort}-{DateTime.Now:yyMMddHHmmssfff}";
        }
    }
}
