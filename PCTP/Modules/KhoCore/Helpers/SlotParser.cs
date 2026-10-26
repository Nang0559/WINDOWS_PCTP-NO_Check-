using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Application.Helpers
{
    public static class SlotParser
    {
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

            string[] parts =
                text.Split('-');

            if (parts.Length < 3)
                return false;

            warehouse = parts[0]
                .Replace("WH :", "")
                .Trim();

            rack = parts[1]
                .Replace("Rack :", "")
                .Trim();

            if (!int.TryParse(
                parts[2]
                    .Replace("Slot :", "")
                    .Trim(),
                out slot))
            {
                return false;
            }

            if (parts.Length >= 4)
            {
                int.TryParse(
                    parts[3]
                        .Replace("Capacity :", "")
                        .Trim(),
                    out capacity);
            }

            return
                !string.IsNullOrWhiteSpace(warehouse) &&
                !string.IsNullOrWhiteSpace(rack) &&
                slot > 0;
        }
    }
}
