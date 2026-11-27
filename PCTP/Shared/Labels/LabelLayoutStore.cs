using System;
using System.Configuration;
using System.IO;
using System.Text.RegularExpressions;
using DevExpress.XtraReports.UI;

namespace PCTP.Shared.Labels
{
    /// <summary>
    /// Lưu layout tem đã chỉnh trong Report Designer dưới dạng file {Mã}.repx.
    /// Thư mục: appSettings "LabelLayoutFolder" (nên là thư mục mạng dùng chung để mọi máy in cùng layout);
    /// để trống = %ProgramData%\PCTP\LabelLayouts. Mỗi lần lưu / khôi phục, bản cũ được chép vào thư mục con "history".
    /// </summary>
    public static class LabelLayoutStore
    {
        private static readonly Regex CodePattern = new Regex("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

        public static string Folder
        {
            get
            {
                string configured = ConfigurationManager.AppSettings["LabelLayoutFolder"];
                if (!string.IsNullOrWhiteSpace(configured)) return Environment.ExpandEnvironmentVariables(configured.Trim());
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PCTP", "LabelLayouts");
            }
        }

        /// <summary>Đường dẫn file layout tùy chỉnh nếu có, ngược lại null.</summary>
        public static string FindCustom(string code)
        {
            string path = PathOf(code);
            return File.Exists(path) ? path : null;
        }

        public static DateTime? LastModified(string code)
        {
            string path = FindCustom(code);
            return path == null ? (DateTime?)null : File.GetLastWriteTime(path);
        }

        public static void Save(string code, XtraReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            string target = PathOf(code);
            Directory.CreateDirectory(Folder);
            string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                report.SaveLayoutToXml(temp);
                Archive(code);
                File.Copy(temp, target, true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        /// <summary>Bỏ layout tùy chỉnh để quay về bố cục gốc trong code (bản cũ vẫn nằm ở history).</summary>
        public static void Reset(string code)
        {
            string path = FindCustom(code);
            if (path == null) return;
            Archive(code);
            File.Delete(path);
        }

        private static void Archive(string code)
        {
            string path = FindCustom(code);
            if (path == null) return;
            string history = Path.Combine(Folder, "history");
            Directory.CreateDirectory(history);
            File.Copy(path, Path.Combine(history, code + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".repx"), true);
        }

        private static string PathOf(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || !CodePattern.IsMatch(code))
                throw new ArgumentException("Mã tem chỉ gồm chữ, số và gạch dưới: '" + code + "'.", nameof(code));
            return Path.Combine(Folder, code + ".repx");
        }
    }
}
