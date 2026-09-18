using System;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Models
{
    /// <summary>
    /// Identity bất biến của một phiên đọc QR.
    /// DOCQRCODE + TMPPHIEUGIAOHANG phải luôn được xử lý trong cùng
    /// ngày / nhà máy / giờ giao / loại phiếu.
    /// </summary>
    internal sealed class DeliverySessionIdentity
    {
        public int AddNM { get; private set; }
        public DateTime NgayGiao { get; private set; }
        public string GioGiao { get; private set; }
        public string NhaMay { get; private set; }
        public bool IsSP { get; private set; }

        public DeliverySessionIdentity(int addNM, DateTime ngayGiao, string gioGiao, string nhaMay, bool isSP)
        {
            AddNM = addNM;
            NgayGiao = ngayGiao.Date;
            GioGiao = NormalizeHours(gioGiao);
            NhaMay = (nhaMay ?? string.Empty).Trim();
            IsSP = isSP;
        }

        public bool Matches(DateTime ngayGiao, int addNM, string gioGiao, string nhaMay, bool isSP)
        {
            return AddNM == addNM
                && NgayGiao == ngayGiao.Date
                && IsSP == isSP
                && string.Equals(GioGiao, NormalizeHours(gioGiao), StringComparison.OrdinalIgnoreCase)
                && string.Equals(NhaMay, (nhaMay ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeHours(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return string.Join(",",
                value.Replace("H", string.Empty)
                     .Split(new[] { ',', '+' }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(NormalizeHour));
        }

        private static string NormalizeHour(string value)
        {
            string s = (value ?? string.Empty).Trim().Trim('\'');
            int h;
            return int.TryParse(s, out h) ? h.ToString("00") : s;
        }
    }
}