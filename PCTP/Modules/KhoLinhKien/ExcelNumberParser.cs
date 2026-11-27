using System;
using System.Globalization;
using System.Text;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>
    /// Đọc số từ ô Excel độc lập với Regional Settings của máy.
    /// - Ô số thật (double/decimal/int) lấy nguyên giá trị, không qua chuỗi.
    /// - Ô dạng text ("2.400", "2,400", "1.234,5", "1,234.5", "2 400") đọc theo quy tắc cố định giống nhau trên mọi máy:
    ///   + có cả "." và ",": ký tự đứng cuối là dấu thập phân, ký tự còn lại là dấu nhóm nghìn;
    ///   + một loại ký tự lặp lại nhiều lần: dấu nhóm nghìn (phải đúng dạng 1-3 số rồi từng nhóm 3 số);
    ///   + một ký tự xuất hiện một lần, sau nó đúng 3 số và phần trước là 1-3 số không bắt đầu bằng 0: dấu nhóm nghìn
    ///     (số lượng linh kiện "2.400" = 2400, không phải 2,4); các trường hợp khác là dấu thập phân ("2.5", "0.125").
    /// Chuỗi không đúng dạng số (chữ, nhóm nghìn sai) bị từ chối thay vì đoán.
    /// </summary>
    public static class ExcelNumberParser
    {
        private const double MaxMagnitude = 1e12;

        /// <summary>Đọc ô Excel: ưu tiên giá trị số thật, nếu là chuỗi thì dùng <see cref="TryParseText"/>.</summary>
        public static bool TryParseCell(object value, string text, out decimal number)
        {
            number = 0m;
            if (value is double)
            {
                double d = (double)value;
                if (double.IsNaN(d) || double.IsInfinity(d) || Math.Abs(d) > MaxMagnitude) return false;
                number = Convert.ToDecimal(d);
                return true;
            }
            if (value is decimal) { number = (decimal)value; return true; }
            if (value is int || value is long || value is short || value is float)
            {
                number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            }
            string s = value as string;
            if (s == null && value != null) s = Convert.ToString(value, CultureInfo.InvariantCulture);
            return TryParseText(string.IsNullOrWhiteSpace(s) ? text : s, out number);
        }

        public static bool TryParseText(string text, out decimal number)
        {
            number = 0m;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var sb = new StringBuilder();
            foreach (char ch in text.Trim())
            {
                if (ch == ' ' || ch == '\u00A0' || ch == '\u202F' || ch == '\'') continue;
                sb.Append(ch);
            }
            string s = sb.ToString();
            bool negative = false;
            if (s.Length > 0 && (s[0] == '+' || s[0] == '-')) { negative = s[0] == '-'; s = s.Substring(1); }
            if (s.Length == 0) return false;
            foreach (char ch in s)
                if (!(ch >= '0' && ch <= '9') && ch != '.' && ch != ',') return false;

            int dots = Count(s, '.'), commas = Count(s, ',');
            string normalized;
            if (dots == 0 && commas == 0)
            {
                normalized = s;
            }
            else if (dots > 0 && commas > 0)
            {
                char dec = s.LastIndexOf('.') > s.LastIndexOf(',') ? '.' : ',';
                char grp = dec == '.' ? ',' : '.';
                if (Count(s, dec) != 1) return false;
                int pos = s.IndexOf(dec);
                string intPart = s.Substring(0, pos), frac = s.Substring(pos + 1);
                if (!IsGrouped(intPart, grp) || frac.Length == 0 || !AllDigits(frac)) return false;
                normalized = intPart.Replace(grp.ToString(), string.Empty) + "." + frac;
            }
            else
            {
                char sep = dots > 0 ? '.' : ',';
                int n = dots > 0 ? dots : commas;
                if (n > 1)
                {
                    if (!IsGrouped(s, sep)) return false;
                    normalized = s.Replace(sep.ToString(), string.Empty);
                }
                else
                {
                    int pos = s.IndexOf(sep);
                    string a = s.Substring(0, pos), b = s.Substring(pos + 1);
                    if (b.Length == 0 || !AllDigits(b) || (a.Length > 0 && !AllDigits(a))) return false;
                    bool thousands = b.Length == 3 && a.Length >= 1 && a.Length <= 3 && a[0] != '0';
                    normalized = thousands ? a + b : (a.Length == 0 ? "0" : a) + "." + b;
                }
            }

            decimal result;
            if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result)) return false;
            number = negative ? -result : result;
            return true;
        }

        private static int Count(string s, char c)
        {
            int n = 0;
            foreach (char ch in s) if (ch == c) n++;
            return n;
        }

        private static bool AllDigits(string s)
        {
            if (s.Length == 0) return false;
            foreach (char ch in s) if (ch < '0' || ch > '9') return false;
            return true;
        }

        /// <summary>1-3 số đầu, các nhóm sau đúng 3 số, ngăn cách bằng <paramref name="group"/>.</summary>
        private static bool IsGrouped(string s, char group)
        {
            string[] parts = s.Split(group);
            if (parts.Length == 1) return AllDigits(parts[0]);
            if (!AllDigits(parts[0]) || parts[0].Length > 3) return false;
            for (int i = 1; i < parts.Length; i++)
                if (parts[i].Length != 3 || !AllDigits(parts[i])) return false;
            return true;
        }
    }
}
