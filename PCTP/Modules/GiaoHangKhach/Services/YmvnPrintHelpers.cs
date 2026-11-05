using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PCTP.Applications.Services
{
    /// <summary>
    /// Định dạng số PO in trên phiếu YMVN: "#" + PO_ITEM.
    /// Nếu PO_ITEM là số thì đệm số 0 bên trái cho đủ 5 ký tự (#00123).
    /// Giá trị không phải số (hoặc dài hơn 5 ký tự) được giữ nguyên sau dấu "#".
    /// </summary>
    public static class YmvnPoFormat
    {
        public const int Width = 5;

        public static string Format(string poItem)
        {
            string s = (poItem ?? string.Empty).Trim();
            if (s.Length == 0) return string.Empty;
            if (s[0] == '#') s = s.Substring(1).Trim();
            if (s.Length == 0) return string.Empty;

            bool allDigits = true;
            foreach (char c in s)
                if (c < '0' || c > '9') { allDigits = false; break; }

            if (allDigits && s.Length < Width) s = s.PadLeft(Width, '0');
            return "#" + s;
        }
    }

    /// <summary>
    /// So khớp giờ giao của dòng phiếu với các giờ đang tích trong danh sách giờ xuất.
    /// Chỉ so theo GIỜ (HH) vì TMP.GIOGIAO lưu "15" còn danh sách giờ là "15:00:00"/"15:00"/"15H".
    /// Danh sách giờ rỗng = không lọc (in toàn bộ).
    /// </summary>
    public sealed class YmvnGioMatcher
    {
        private readonly HashSet<string> _hours;

        private YmvnGioMatcher(HashSet<string> hours) { _hours = hours; }

        public static YmvnGioMatcher Create(IEnumerable<string> checkedGios)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (checkedGios != null)
                foreach (string g in checkedGios)
                {
                    string h = NormalizeHour(g);
                    if (h.Length > 0) set.Add(h);
                }
            return new YmvnGioMatcher(set);
        }

        public bool HasFilter { get { return _hours.Count > 0; } }

        public bool Matches(DateTime? ngayGiao, string gioGiao)
        {
            if (_hours.Count == 0) return true;
            string h = NormalizeHour(gioGiao);
            if (h.Length == 0 && ngayGiao.HasValue)
                h = ngayGiao.Value.Hour.ToString("00", CultureInfo.InvariantCulture);
            return h.Length > 0 && _hours.Contains(h);
        }

        public static string NormalizeHour(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string s = value.Replace("H", "").Replace("h", "").Trim();
            int colon = s.IndexOf(':');
            if (colon >= 0) s = s.Substring(0, colon);
            int hour;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out hour)
                ? hour.ToString("00", CultureInfo.InvariantCulture) : string.Empty;
        }
    }
}
