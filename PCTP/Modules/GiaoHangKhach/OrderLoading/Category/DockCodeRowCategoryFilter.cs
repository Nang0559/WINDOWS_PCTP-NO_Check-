using System;
using System.Data;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Shared.Models;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading.Category
{
    /// <summary>
    /// Lọc MP/SP theo DockCode cấu hình.
    /// IFS có thể trả cột DOCKCODE; một số luồng/table cũ dùng CUA.
    /// Ưu tiên DOCKCODE để phản ánh đúng điều kiện nghiệp vụ dockcode = DockCodeSP,
    /// fallback CUA để giữ tương thích dữ liệu hiện hữu của TableOrder/IFS legacy.
    /// </summary>
    public class DockCodeRowCategoryFilter : IRowCategoryFilter
    {
        public DataTable Filter(DataTable data, OrderCategory wanted, CustomerConfig cfg)
        {
            if (data == null) return new DataTable();
            if (cfg == null || cfg.Delivery == null || !cfg.Delivery.CoLoaiSP) return data;

            string dockColumn = ResolveDockColumn(data);
            if (dockColumn == null) return data;

            string dockCodeSP = (cfg.Delivery.DockCodeSP ?? "").Trim();

            DataTable result = data.Clone();
            foreach (DataRow row in data.Rows)
            {
                string dockCode = (row[dockColumn]?.ToString() ?? "").Trim();
                bool isRowSP = string.Equals(dockCode, dockCodeSP, StringComparison.OrdinalIgnoreCase);

                if ((wanted == OrderCategory.SP) == isRowSP)
                    result.ImportRow(row);
            }
            return result;
        }

        private static string ResolveDockColumn(DataTable data)
        {
            if (data.Columns.Contains("DOCKCODE")) return "DOCKCODE";
            if (data.Columns.Contains("DOCK_CODE")) return "DOCK_CODE";
            if (data.Columns.Contains("CUA")) return "CUA";
            return null;
        }
    }
}