using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Helpers
{
    public static class DinhDanhKeyHelper
    {
        private const string SEP = "|";

        public static string Build(string nhaMay, DateTime? ngayGiao, string gioGiaoFcc, string poNo, short stt)
            => string.Join(SEP,
                (nhaMay ?? "").Trim(),
                ngayGiao?.ToString("yyyy-MM-dd") ?? "",
                (gioGiaoFcc ?? "").Trim(),
                (poNo ?? "").Trim(),
                stt.ToString());

        public static bool TryParse(string key, out string nhaMay, out DateTime? ngayGiao,
            out string gioGiaoFcc, out string poNo, out short stt)
        {
            nhaMay = null; ngayGiao = null; gioGiaoFcc = null; poNo = null; stt = 0;
            if (string.IsNullOrWhiteSpace(key)) return false;
            var parts = key.Split(new[] { SEP }, StringSplitOptions.None);
            if (parts.Length != 5) return false;

            nhaMay = parts[0];
            ngayGiao = DateTime.TryParse(parts[1], out var d) ? d : (DateTime?)null;
            gioGiaoFcc = parts[2];
            poNo = parts[3];
            return short.TryParse(parts[4], out stt);
        }
    }
}
