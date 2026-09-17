using System;
using System.Data;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Shared.Models;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading.Category
{
    /// <summary>
    /// Lọc MP/SP theo DockCode cấu hình.
    /// IFS hiện tại trả DOCK_CODE dưới alias TRUYEN; một số luồng/table
    /// có thể trả DOCKCODE hoặc DOCK_CODE trực tiếp, dữ liệu legacy dùng CUA.
    /// Ưu tiên cột phản ánh DOCK_CODE thật để điều kiện SP của 100001
    /// thực hiện đúng: DOCK_CODE = cfg.Delivery.DockCodeSP (HVN).
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
            // IFSRepository.GetCustomerOrderJoin currently aliases col.DOCK_CODE as TRUYEN.
            if (data.Columns.Contains("DOCKCODE")) return "DOCKCODE";
            if (data.Columns.Contains("DOCK_CODE")) return "DOCK_CODE";
            if (data.Columns.Contains("TRUYEN")) return "TRUYEN";
            // Legacy/table-order fallback. CUA is SUB_DOCK_CODE, not the IFS DOCK_CODE,
            // but is retained only for old datasets that do not expose DOCK_CODE.
            if (data.Columns.Contains("CUA")) return "CUA";
            return null;
        }
    }
}