using System;

// Keep IEquatable/object overrides compatible with the application's C# 7.3 target.
#pragma warning disable CS8767, CS8765, CS8604
using System.Globalization;
using System.Text.RegularExpressions;

namespace PCTP.Modules.GiaoHangKhach.Models
{
    /// <summary>
    /// Định danh DUY NHẤT của một dòng đơn hàng trong bảng đơn riêng
    /// (Purchase_Order_YMVN / Purchase_Order_HTN / ...):
    ///
    ///     PO (Oder_no) + MaHang (Part_no) + NgayGiao (ngày) + Giờ:Phút giao (HH:mm)
    ///
    /// PO + ngày + mã hàng KHÔNG đủ định danh: cùng ngày, cùng PO, cùng mã hàng
    /// vẫn có thể có nhiều đợt giao ở các giờ khác nhau. Mọi nơi đánh dấu / ghép
    /// dữ liệu theo "dòng đơn hàng" phải dùng khoá này, không tự ghép chuỗi.
    ///
    /// Độ chính xác thời gian là PHÚT. TableOrderRepo giữ từng record nguồn, không gộp
    /// số lượng; phần giây không tham gia định danh và được bỏ qua khi so khớp.
    ///
    /// Lớp này thuần (không phụ thuộc WinForms/SQL) để test được trên .NET 8.
    /// Chỉ dùng cú pháp C# 7.3 vì PCTP build bằng .NET Framework 4.7.2.
    /// </summary>
    public sealed class OrderLineKey : IEquatable<OrderLineKey>
    {
        public string PoNo { get; }
        public string MaHang { get; }

        /// <summary>Chỉ phần ngày (00:00:00).</summary>
        public DateTime NgayGiao { get; }

        /// <summary>Giờ:phút trong ngày (giây = 0).</summary>
        public TimeSpan GioGiao { get; }

        private OrderLineKey(string poNo, string maHang, DateTime ngayGiao, TimeSpan gioGiao)
        {
            PoNo = poNo;
            MaHang = maHang;
            NgayGiao = ngayGiao;
            GioGiao = gioGiao;
        }

        public static OrderLineKey Create(string poNo, string maHang, DateTime ngayGiao, TimeSpan gioGiao)
        {
            string po = (poNo ?? string.Empty).Trim();
            string part = (maHang ?? string.Empty).Trim();

            if (po.Length == 0)
                throw new ArgumentException("PO không được để trống trong định danh dòng đơn hàng.", nameof(poNo));
            if (part.Length == 0)
                throw new ArgumentException("Mã hàng không được để trống trong định danh dòng đơn hàng.", nameof(maHang));
            if (gioGiao < TimeSpan.Zero || gioGiao >= TimeSpan.FromDays(1))
                throw new ArgumentOutOfRangeException(nameof(gioGiao), "Giờ giao phải nằm trong 00:00-23:59.");

            return new OrderLineKey(
                po,
                part,
                ngayGiao.Date,
                new TimeSpan(gioGiao.Hours, gioGiao.Minutes, 0));
        }

        /// <summary>Tạo từ một giá trị DateTime đã có đủ ngày + giờ (vd cột NgayGiao của bảng đơn).</summary>
        public static OrderLineKey FromDateTime(string poNo, string maHang, DateTime ngayGioGiao)
        {
            return Create(poNo, maHang, ngayGioGiao.Date, ngayGioGiao.TimeOfDay);
        }

        /// <summary>
        /// Tạo từ dòng TMP đã load từ bảng đơn riêng.
        /// NGAYGIAO trong TMP là "yyyy-MM-dd HH:mm:ss" (có giờ). Nếu phần giờ của NGAYGIAO là
        /// 00:00 mà cột GIOGIAO có một giờ cụ thể ("14:00") thì dùng GIOGIAO. GIOGIAO dạng
        /// nhóm ("'15','16'") không hợp lệ làm định danh nên bị bỏ qua.
        /// </summary>
        /// <exception cref="FormatException">NGAYGIAO không đọc được.</exception>
        public static OrderLineKey FromTmpValues(string poNo, string maHang, object ngayGiao, string gioGiao)
        {
            DateTime ngay;
            if (ngayGiao is DateTime)
            {
                ngay = (DateTime)ngayGiao;
            }
            else
            {
                string text = Convert.ToString(ngayGiao, CultureInfo.InvariantCulture) ?? string.Empty;
                if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out ngay))
                    throw new FormatException("Ngày giao không hợp lệ: '" + text + "'.");
            }

            TimeSpan gio = ngay.TimeOfDay;
            if (gio == TimeSpan.Zero)
            {
                TimeSpan parsed;
                bool hasMinute;
                if (TryParseGio(gioGiao, out parsed, out hasMinute))
                    gio = parsed;
            }

            return Create(poNo, maHang, ngay.Date, gio);
        }

        /// <summary>
        /// Đọc giờ từ các dạng đang tồn tại trong hệ thống: "8", "08", "8H", "'15'",
        /// "08:30", "08:30:00". Dạng nhóm giờ ("'15','16'", "15+16") và giá trị rác trả về false
        /// — KHÔNG ép về "00" như GioHelper.NormalizeGio vì sẽ khớp nhầm đơn lúc 00:00.
        /// <paramref name="hasMinute"/> = false khi chỉ có giờ (dữ liệu cũ).
        /// </summary>
        public static bool TryParseGio(string raw, out TimeSpan gio, out bool hasMinute)
        {
            gio = TimeSpan.Zero;
            hasMinute = false;

            if (string.IsNullOrWhiteSpace(raw)) return false;

            string s = raw.Trim().Trim('\'').Trim();
            if (s.Length == 0 || s.IndexOf(',') >= 0 || s.IndexOf('+') >= 0) return false;

            s = s.Replace("H", string.Empty).Replace("h", string.Empty).Trim();
            string[] parts = s.Split(':');
            if (parts.Length == 0 || parts.Length > 3) return false;

            int hh;
            if (!int.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out hh)
                || hh < 0 || hh > 23)
                return false;

            int mm = 0;
            if (parts.Length >= 2)
            {
                if (!int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out mm)
                    || mm < 0 || mm > 59)
                    return false;
                hasMinute = true;
            }

            if (parts.Length == 3)
            {
                int ss;
                if (!int.TryParse(parts[2].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ss)
                    || ss < 0 || ss > 59)
                {
                    hasMinute = false;
                    return false;
                }
            }

            gio = new TimeSpan(hh, mm, 0);
            return true;
        }

        public bool Equals(OrderLineKey other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;

            // SQL Server mặc định so sánh không phân biệt hoa/thường cho PO và mã hàng.
            return string.Equals(PoNo, other.PoNo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(MaHang, other.MaHang, StringComparison.OrdinalIgnoreCase)
                && NgayGiao == other.NgayGiao
                && GioGiao == other.GioGiao;
        }

        public override bool Equals(object obj) { return Equals(obj as OrderLineKey); }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = StringComparer.OrdinalIgnoreCase.GetHashCode(PoNo);
                h = (h * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(MaHang);
                h = (h * 397) ^ NgayGiao.GetHashCode();
                h = (h * 397) ^ GioGiao.GetHashCode();
                return h;
            }
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "PO={0}|Part={1}|{2:yyyy-MM-dd} {3:hh\\:mm}",
                PoNo, MaHang, NgayGiao, GioGiao);
        }
    }

    /// <summary>
    /// SQL dùng chung cho bảng đơn riêng. Tách ra để cùng một câu SQL được
    /// PhieuKhoRepository (production) và PCTP.QC.SqlIntegration (chạy thật trên SQL Server test) dùng.
    /// </summary>
    public static class OrderTableSql
    {
        private static readonly Regex SafeName =
            new Regex("^#?[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Đánh dấu ĐÚNG MỘT dòng đơn (PO + mã hàng + ngày + giờ:phút) là đã giao.
        /// Tham số: @po NVarChar(100), @pno NVarChar(100), @ng Date, @hh Int, @mi Int.
        /// Không bao giờ chỉ lọc theo ngày: các đợt giao khác giờ của cùng PO/mã hàng không bị đụng tới.
        /// </summary>
        public static string MarkDelivered(string orderTable)
        {
            if (orderTable == null || !SafeName.IsMatch(orderTable))
                throw new ArgumentException("Tên bảng đơn hàng không hợp lệ.", nameof(orderTable));

            return "UPDATE [" + orderTable + "] SET IsDelivered = 1, DeliveredDate = GETDATE() " +
                   "WHERE Oder_no = @po AND Part_no = @pno " +
                   "AND CAST(NgayGiao AS DATE) = @ng " +
                   "AND DATEPART(HH, NgayGiao) = @hh " +
                   "AND DATEPART(MI, NgayGiao) = @mi " +
                   "AND IsDelivered = 0";
        }
    }
}
