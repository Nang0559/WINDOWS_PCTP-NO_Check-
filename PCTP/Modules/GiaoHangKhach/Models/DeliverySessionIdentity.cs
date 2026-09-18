using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Models
{
    /// <summary>
    /// Identity bất biến của một phiên đọc QR.
    ///
    /// Business identity:
    ///   ADDNM + NGAYGIAO + concrete GIOGIAO + loại phiếu.
    ///
    /// GIOGIAO trong TMP là một giờ cụ thể (ví dụ "15"), trong khi
    /// GioXuat.Ma của Radio có thể là một nhóm giờ (ví dụ "'15','16'").
    /// Vì vậy khi validate UI, concrete hour phải thuộc hour-set của Radio.
    ///
    /// NHAMAY KHÔNG thuộc identity: tên hiển thị có thể khác format giữa
    /// TMP/config/UI; ADDNM mới là khóa nhà máy ổn định.
    /// </summary>
    internal sealed class DeliverySessionIdentity
    {
        public int AddNM { get; private set; }
        public DateTime NgayGiao { get; private set; }
        public string GioGiao { get; private set; }
        public bool IsSP { get; private set; }

        public DeliverySessionIdentity(int addNM, DateTime ngayGiao, string gioGiao, string nhaMay, bool isSP)
        {
            AddNM = addNM;
            NgayGiao = ngayGiao.Date;
            GioGiao = NormalizeHours(gioGiao);
            IsSP = isSP;
        }

        public bool Matches(DateTime ngayGiao, int addNM, string gioXuatMa, string nhaMay, bool isSP)
        {
            if (AddNM != addNM || NgayGiao != ngayGiao.Date || IsSP != isSP)
                return false;

            if (IsSP)
                return true;

            var sessionHours = ParseHours(GioGiao);
            var uiHours = ParseHours(gioXuatMa);

            // Sessions without a radio hour (SP/YMVN/table-order flows) are
            // intentionally date + plant + category scoped only.
            if (sessionHours.Count == 0)
                return true;

            if (uiHours.Count == 0)
                return false;

            // Normal case after restoring TMP: concrete TMP hour "15"
            // must be contained by the selected Radio group "'15','16'".
            if (sessionHours.Count == 1)
                return uiHours.Contains(sessionHours.First());

            // When the session was created from a currently selected Radio
            // group, keep that group stable. This also supports existing
            // sessions created before TMP has been persisted with one
            // concrete hour.
            return sessionHours.SetEquals(uiHours);
        }

        public static bool ContainsHour(string gioXuatMa, string concreteHour)
        {
            var target = ParseHours(concreteHour);
            var group = ParseHours(gioXuatMa);
            if (target.Count != 1 || group.Count == 0)
                return false;

            return group.Contains(target.First());
        }

        private static HashSet<string> ParseHours(string value)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value))
                return result;

            foreach (string token in value
                .Replace("H", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Split(new[] { ',', '+' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string normalized = NormalizeHour(token);
                if (!string.IsNullOrEmpty(normalized))
                    result.Add(normalized);
            }

            return result;
        }

        private static string NormalizeHours(string value)
        {
            return string.Join(",", ParseHours(value).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }

        private static string NormalizeHour(string value)
        {
            string s = (value ?? string.Empty).Trim().Trim('\'');
            int h;
            return int.TryParse(s, out h) ? h.ToString("00") : s;
        }
    }
}