using System;
using System.Configuration;

namespace PCTP.Shared.Common
{
    /// <summary>
    /// SiteCode của máy/nhà máy đang chạy PCTP. FVN_PCTP là DB đa site: mọi truy vấn kho
    /// và mọi lệnh ghi MES ledger phải mang SiteCode này (không được hard-code trong repository).
    /// </summary>
    public interface ISiteContext
    {
        string SiteCode { get; }
    }

    /// <summary>
    /// Site đang làm việc của phiên đăng nhập (đặt sau khi đăng nhập, xem Main_APP.EnsureActiveSite).
    /// Dùng khi App.config không cố định PctpSiteCode: một tài khoản có thể thuộc một hoặc nhiều Site (dbo.FVN_PCTPSite),
    /// Site được suy ra từ phạm vi dữ liệu của tài khoản, không còn bắt buộc cấu hình tay trên từng máy.
    /// </summary>
    public static class ActiveSiteStore
    {
        private static readonly object Gate = new object();
        private static string _current;

        public static string Current
        {
            get { lock (Gate) return _current; }
        }

        public static void Set(string siteCode)
        {
            string value = string.IsNullOrWhiteSpace(siteCode) ? null : siteCode.Trim();
            lock (Gate) _current = value;
        }

        public static void Clear() { Set(null); }
    }

    /// <summary>
    /// Thứ tự xác định SiteCode: (1) appSettings["PctpSiteCode"] nếu có (máy cố định một nhà máy, ghi đè);
    /// (2) Site đang chọn của phiên đăng nhập (ActiveSiteStore). Không có cả hai thì fail-closed: không đoán Site.
    /// </summary>
    public sealed class AppSettingsSiteContext : ISiteContext
    {
        public const string SettingKey = "PctpSiteCode";
        public static readonly AppSettingsSiteContext Default = new AppSettingsSiteContext();

        public string SiteCode
        {
            get
            {
                string value = ConfigurationManager.AppSettings[SettingKey];
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();

                string active = ActiveSiteStore.Current;
                if (!string.IsNullOrWhiteSpace(active)) return active;

                throw new InvalidOperationException(
                    "Chưa xác định được Site làm việc của FVN_PCTP. Đăng nhập bằng tài khoản đã được gán Site " +
                    "(phân quyền phạm vi SITE) hoặc khai báo appSettings['" + SettingKey + "'] trong App.config.");
            }
        }
    }
}
