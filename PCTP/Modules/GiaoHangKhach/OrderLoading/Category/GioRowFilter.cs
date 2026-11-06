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
            if (!ifsData.Columns.Contains("GIOGIAO") && !ifsData.Columns.Contains("NGAYGIAO"))
                return ifsData;

            DataTable result = ifsData.Clone();
            if (checkedGios == null || checkedGios.Count == 0)
                return result;

            var selectedTimes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var selectedHours = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in checkedGios)
            {
                string value = (raw ?? string.Empty).Trim().Trim('\'');
                if (value.Length == 0) continue;

                TimeSpan time;
                if (TimeSpan.TryParse(value, out time))
                {
                    selectedTimes.Add(time.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture));
                    selectedHours.Add(((int)time.TotalHours).ToString("00"));
                }
                else
                {
                    int hour;
                    if (int.TryParse(value, out hour) && hour >= 0 && hour <= 23)
                        selectedHours.Add(hour.ToString("00"));
                }
            }

            foreach (DataRow row in ifsData.Rows)
            {
                string rowTime = string.Empty;
                string rowHour = string.Empty;

                // Ưu tiên thời điểm đầy đủ từ NGAYGIAO để phân biệt các slot
                // cùng giờ nhưng khác phút (ví dụ 11:20 và 11:40).
                if (ifsData.Columns.Contains("NGAYGIAO") && row["NGAYGIAO"] != DBNull.Value)
                {
                    DateTime dateTime;
                    if (DateTime.TryParse(Convert.ToString(row["NGAYGIAO"]),
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeLocal, out dateTime))
                    {
                        rowTime = dateTime.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                        rowHour = dateTime.ToString("HH", System.Globalization.CultureInfo.InvariantCulture);
                    }
                }

                string gioGiao = ifsData.Columns.Contains("GIOGIAO")
                    ? Convert.ToString(row["GIOGIAO"]).Trim()
                    : string.Empty;

                TimeSpan parsedGio;
                if (TimeSpan.TryParse(gioGiao, out parsedGio))
                {
                    if (rowTime.Length == 0)
                        rowTime = parsedGio.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture);
                    if (rowHour.Length == 0)
                        rowHour = ((int)parsedGio.TotalHours).ToString("00");
                }
                else if (rowHour.Length == 0)
                {
                    int hour;
                    if (int.TryParse(gioGiao, out hour) && hour >= 0 && hour <= 23)
                        rowHour = hour.ToString("00");
                }

                bool match = rowTime.Length > 0 && selectedTimes.Count > 0
                    ? selectedTimes.Contains(rowTime)
                    : selectedHours.Contains(rowHour);

                if (match) result.ImportRow(row);
            }

            return result;
        }
    }
}