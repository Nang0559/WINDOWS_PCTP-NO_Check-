using System;
using System.Globalization;

namespace PCTP.Shared.Helpers
{
    /// <summary>
    /// Định dạng / phân tích ngày dùng nội bộ giữa UI → Presenter → Service → SQL.
    ///
    /// Vấn đề gốc: UI tạo chuỗi "MM/dd/yyyy" rồi tầng dưới gọi DateTime.TryParse(chuỗi)
    /// theo culture của Windows. Máy đặt dd/MM/yyyy hiểu "11/04/2026" là 11 tháng 4,
    /// máy đặt MM/dd/yyyy hiểu là 4 tháng 11 → hai máy ra hai ngày khác nhau.
    ///
    /// Quy tắc: chuỗi ngày do chính app sinh ra luôn được TẠO và PHÂN TÍCH bằng
    /// InvariantCulture với định dạng cố định, không phụ thuộc Regional Settings.
    /// </summary>
    public static class DateText
    {
        public const string MdyFormat = "MM/dd/yyyy";
        public const string IsoFormat = "yyyy-MM-dd";
        public const string IsoDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        // Thứ tự quan trọng: yyyyMMdd phải thử trước ddMMyyyy.
        private static readonly string[] AppFormats =
        {
            "MM/dd/yyyy",
            "MM/dd/yyyy H:mm:ss",
            "MM/dd/yyyy HH:mm:ss",
            "yyyy-MM-dd",
            "yyyy-MM-dd H:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
            "yyyyMMdd",
            "ddMMyyyy"
        };

        /// <summary>MM/dd/yyyy — định dạng truyền cho CONVERT(..., 101) của YMVN.</summary>
        public static string ToMdy(DateTime value)
        {
            return value.ToString(MdyFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>yyyy-MM-dd — định dạng ngày chuẩn khi đưa vào SQL.</summary>
        public static string ToIso(DateTime value)
        {
            return value.ToString(IsoFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>yyyy-MM-dd HH:mm:ss.</summary>
        public static string ToIsoDateTime(DateTime value)
        {
            return value.ToString(IsoDateTimeFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Phân tích chuỗi ngày do app sinh ra (MM/dd/yyyy, yyyy-MM-dd, yyyyMMdd, ddMMyyyy,
        /// kèm giờ nếu có). Chỉ khi không khớp định dạng nào của app mới rơi về culture hiện
        /// tại (giữ hành vi cũ cho chuỗi do chính máy đó định dạng).
        /// </summary>
        public static bool TryParseApp(string text, out DateTime result)
        {
            result = default(DateTime);
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string trimmed = text.Trim();

            if (DateTime.TryParseExact(trimmed, AppFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out result))
                return true;

            return DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out result);
        }

        /// <summary>
        /// Giá trị đọc từ DataRow: DateTime dùng thẳng (không đi qua chuỗi), chuỗi thì phân
        /// tích như TryParseApp.
        /// </summary>
        public static bool TryParseDb(object value, out DateTime result)
        {
            result = default(DateTime);
            if (value == null || value == DBNull.Value)
                return false;

            if (value is DateTime)
            {
                result = (DateTime)value;
                return true;
            }

            return TryParseApp(Convert.ToString(value, CultureInfo.InvariantCulture), out result);
        }
    }
}
