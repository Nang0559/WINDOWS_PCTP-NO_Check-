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
    /// Nguồn Site DUY NHẤT của phiên đăng nhập, dùng cho dữ liệu RIÊNG của từng nhà máy (kho, slot, chuyền, ledger...).
    /// Không còn hỏi Site khi mở phần mềm: dữ liệu dùng chung giữa các nhà máy không cần Site. Site chỉ được xác định khi
    /// một chức năng thực sự cần dữ liệu riêng của nhà máy, theo thứ tự: (1) tài khoản chỉ có đúng một Site thì tự dùng;
    /// (2) Site người dùng đã chọn ở chức năng trước đó hoặc ở ô chọn Site trong form (ghi vào đây nên mọi nơi thống nhất);
    /// (3) chưa có thì hỏi MỘT lần qua <see cref="Resolver"/> (shell đăng ký hộp thoại chọn Site) rồi nhớ cho cả phiên.
    /// </summary>
    public static class ActiveSiteStore
    {
        private static readonly object Gate = new object();
        private static string _current;

        /// <summary>Phát khi Site làm việc đổi (form đang mở có thể nạp lại dữ liệu riêng của nhà máy).</summary>
        public static event EventHandler Changed;

        /// <summary>
        /// Shell đăng ký: nhận mã chức năng đang cần Site, trả về Site đã chọn (null nếu người dùng hủy hoặc không có Site).
        /// Chỉ được gọi khi chưa có Site làm việc và chức năng cần dữ liệu riêng nhà máy.
        /// </summary>
        public static Func<string, string> Resolver { get; set; }

        public static string Current
        {
            get { lock (Gate) return _current; }
        }

        public static void Set(string siteCode)
        {
            string value = string.IsNullOrWhiteSpace(siteCode) ? null : siteCode.Trim();
            bool changed;
            lock (Gate)
            {
                changed = !string.Equals(_current, value, StringComparison.OrdinalIgnoreCase);
                _current = value;
            }
            if (changed)
            {
                EventHandler handler = Changed;
                if (handler != null) handler(null, EventArgs.Empty);
            }
        }

        public static void Clear() { Set(null); }

        /// <summary>Site làm việc hiện tại; chưa có thì hỏi một lần qua <see cref="Resolver"/>. Null khi không xác định được.</summary>
        public static string Resolve(string functionCode)
        {
            string current = Current;
            if (!string.IsNullOrWhiteSpace(current)) return current;
            Func<string, string> resolver = Resolver;
            if (resolver == null) return null;
            string chosen = resolver(functionCode);
            if (!string.IsNullOrWhiteSpace(chosen)) Set(chosen);
            return Current;
        }
    }

    /// <summary>
    /// Site KHÔNG gắn theo máy: nhiều nhà máy dùng chung một máy nên appSettings["PctpSiteCode"] không còn được dùng.
    /// Site do quyền của tài khoản (phạm vi dữ liệu SITE) quyết định. Thứ tự xác định SiteCode:
    /// (2) Site làm việc của phiên (ActiveSiteStore.Resolve: tự dùng khi chỉ có một Site, hoặc hỏi một lần khi cần dữ liệu riêng
    /// nhà máy). Không xác định được thì fail-closed: không đoán Site.
    /// </summary>
    public sealed class AppSettingsSiteContext : ISiteContext
    {
        /// <summary>Khóa cũ (gắn Site theo máy). Không còn hiệu lực; giữ tên để tài liệu/cấu hình cũ không gãy.</summary>
        public const string SettingKey = "PctpSiteCode";
        public static readonly AppSettingsSiteContext Default = new AppSettingsSiteContext();

        public string SiteCode
        {
            get
            {
                string active = ActiveSiteStore.Resolve("SITE_CONTEXT");
                if (!string.IsNullOrWhiteSpace(active)) return active;

                throw new InvalidOperationException(
                    "Chưa xác định được Site làm việc của FVN_PCTP. Đăng nhập bằng tài khoản đã được gán Site " +
                    "(phân quyền phạm vi SITE) rồi chọn Site ở thanh trạng thái.");
            }
        }
    }
}
