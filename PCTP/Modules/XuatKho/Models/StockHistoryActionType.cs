using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Models
{
    public static class StockHistoryActionType
    {
        // ActionType là nvarchar(10) — PHẢI giữ mọi giá trị ≤ 10 ký tự
        public const string ChoGiao = "CHO_GIAO";
        public const string Export = "EXPORT";
        public const string Rework = "REWORK";
    }
}
