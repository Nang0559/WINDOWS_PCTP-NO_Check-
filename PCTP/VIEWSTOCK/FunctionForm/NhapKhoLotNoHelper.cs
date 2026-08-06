using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.FunctionForm
{
    // ── Fuction/LotNoHelper.cs — thêm BuildFindList ──────────────────────────
    public static class NhapKhoLotNoHelper
    {
        // Giữ nguyên logic cũ từ NHAP_TP — build danh sách FIND để tìm grid
        public static List<string> BuildFindList(string lotNoSL, string idSP)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(lotNoSL) || string.IsNullOrEmpty(idSP))
                return result;

            if (!int.TryParse(idSP, out int intId)) return result;

            int prefixLen = 6 + idSP.Length;
            if (lotNoSL.Length <= prefixLen) return result;

            string ca = lotNoSL.Substring(prefixLen - 1, 1);
            string lot = lotNoSL.Substring(0, 6) + intId + ca;

            // Dạng 1 — LOT cơ bản
            result.Add(lot);

            // Dạng 2 — BP từ cuối
            if (lotNoSL.Length >= 8)
            {
                string bp = lotNoSL.Substring(lotNoSL.Length - 8, 4);
                string gear = lotNoSL.Length > lot.Length + 1
                    ? lotNoSL.Substring(lot.Length + 1, 1) : "";
                result.Add(lotNoSL.Substring(0, 6) + intId + ca + bp + gear);
            }

            // Dạng 3 — BP từ vị trí khác
            if (lotNoSL.Length >= lot.Length + 6)
            {
                string bp2 = lotNoSL.Substring(lot.Length + 2, 4);
                string gear2 = lotNoSL.Substring(lot.Length + 1, 1);
                result.Add(lotNoSL.Substring(0, 6) + intId + ca + bp2 + gear2);
            }

            // Dạng LOTCH — 20 ký tự đầu (= vNhapTP.FIND)
            if (lotNoSL.Length >= 20)
                result.Add(lotNoSL.Substring(0, 20));

            return result;
        }
    }
}
