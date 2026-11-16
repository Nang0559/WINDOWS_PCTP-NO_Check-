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
    /// Đọc SiteCode từ App.config: &lt;add key="PctpSiteCode" value="..." /&gt;.
    /// Thiếu cấu hình thì fail-closed: không đoán site.
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
                if (string.IsNullOrWhiteSpace(value))
                    throw new InvalidOperationException(
                        "Thiếu appSettings['" + SettingKey + "'] trong App.config; không thể xác định SiteCode của FVN_PCTP.");
                return value.Trim();
            }
        }
    }
}
