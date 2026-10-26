using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading.Category
{
    /// <summary>
    /// Lọc dữ liệu IFS theo danh sách giờ đã chọn (checkedGios) — dùng chung cho
    /// PhieuService.LoadPhieuTuBangRieng_Internal và OrderTableLoadStrategy.SoSanhVoiIFS.
    /// Tách ra để 2 nơi không lệch quy tắc khi cùng cần scope IFS theo giờ.
    /// Chuyển nguyên logic từ PhieuService.FilterIfsDataByGio — KHÔNG đổi hành vi.
    /// </summary>
    public static class GioRowFilter
    {
        public static DataTable Filter(DataTable ifsData, IList<string> checkedGios)
        {
            if (ifsData == null) return new DataTable();
            if (checkedGios == null || checkedGios.Count == 0) return ifsData;
            if (!ifsData.Columns.Contains("GIOGIAO")) return ifsData;

            var hourSet = checkedGios
                .Select(g => g.Split(':')[0].PadLeft(2, '0'))
                .Distinct()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            DataTable result = ifsData.Clone();
            foreach (DataRow row in ifsData.Rows)
            {
                string gioGiao = (row["GIOGIAO"]?.ToString() ?? "").Trim();
                if (hourSet.Contains(gioGiao))
                    result.ImportRow(row);
            }
            return result;
        }
    }
}