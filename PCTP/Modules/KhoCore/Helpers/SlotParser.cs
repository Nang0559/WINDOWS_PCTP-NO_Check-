using System;
using System.Text.RegularExpressions;

namespace PCTP.Modules.KhoVatLy.Application.Helpers
{
    /// <summary>
    /// Parse chuỗi hiển thị Slot: "WH : {wh} - Rack : {rack} - Slot : {n} - Capacity : {cap}".
    /// KHÔNG tách theo '-' vì tên Rack/Kho có thể chứa '-' (vd "22201-KYHN-A400").
    /// Neo theo các nhãn "WH :", "Rack :", "Slot :", "Capacity :".
    /// </summary>
    public static class SlotParser
    {
        private static readonly Regex _rx = new Regex(
            @"^\s*WH\s*:\s*(?<wh>.+?)\s+-\s+Rack\s*:\s*(?<rack>.+?)\s+-\s+Slot\s*:\s*(?<slot>\d+)(?:\s+-\s+Capacity\s*:\s*(?<cap>\d+))?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool TryParse(
            string text,
            out string warehouse,
            out string rack,
            out int slot,
            out int capacity)
        {
            warehouse = null;
            rack = null;
            slot = 0;
            capacity = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var m = _rx.Match(text);
            if (!m.Success)
                return false;

            warehouse = m.Groups["wh"].Value.Trim();
            rack = m.Groups["rack"].Value.Trim();

            if (!int.TryParse(m.Groups["slot"].Value, out slot))
                return false;

            if (m.Groups["cap"].Success)
                int.TryParse(m.Groups["cap"].Value, out capacity);

            return !string.IsNullOrWhiteSpace(warehouse)
                && !string.IsNullOrWhiteSpace(rack)
                && slot > 0;
        }
    }
}
