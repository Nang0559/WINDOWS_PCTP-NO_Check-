using System;
using System.Data;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Shared.Models;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading.Category
{
    /// <summary>
    /// Chuyển nguyên logic từ PhieuService.FilterIfsDataByDockCode — KHÔNG đổi
    /// hành vi, chỉ tách ra dùng chung. LƯU Ý: TableOrderRepo.cs (SQL) áp dụng
    /// CÙNG quy tắc này (RTRIM(o.CUA) = / <> DockCodeSP) nhưng lọc ở tầng SQL vì
    /// lý do hiệu năng — sửa quy tắc MP/SP phải sửa CẢ 2 nơi.
    /// </summary>
    public class DockCodeRowCategoryFilter : IRowCategoryFilter
    {
        public DataTable Filter(DataTable data, OrderCategory wanted, CustomerConfig cfg)
        {
            if (data == null) return new DataTable();
            if (!cfg.Delivery.CoLoaiSP) return data;
            if (!data.Columns.Contains("CUA")) return data;

            string dockCodeSP = (cfg.Delivery.DockCodeSP ?? "").Trim();

            DataTable result = data.Clone();
            foreach (DataRow row in data.Rows)
            {
                string cua = (row["CUA"]?.ToString() ?? "").Trim();
                bool isRowSP = string.Equals(cua, dockCodeSP, StringComparison.OrdinalIgnoreCase);

                if ((wanted == OrderCategory.SP) == isRowSP)
                    result.ImportRow(row);
            }
            return result;
        }
    }
}