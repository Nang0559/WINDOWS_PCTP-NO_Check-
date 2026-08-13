using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.FuctionMain
{
    public static class SqlHelper
    {
        // <summary>Escape dấu nháy đơn — dùng trong dynamic SQL string</summary>
        public static string Esc(string s) => (s ?? "").Replace("'", "''");

        /// <summary>Escape tên bảng/cột — bọc trong ngoặc vuông</summary>
        public static string EscName(string name) =>
            "[" + (name ?? "").Replace("]", "]]") + "]";
    }
}
