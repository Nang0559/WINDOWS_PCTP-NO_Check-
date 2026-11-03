using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace PCTP.Infrastructure.Configuration
{
    /// <summary>
    /// Moves mutable runtime configuration out of the versioned ClickOnce directory.
    /// Files are copied only on first launch and are never overwritten by application updates.
    /// Because of that, the files in %ProgramData%\PCTP can drift from the ones shipped with the app
    /// (bin\Debug, ClickOnce). Initialize() therefore also checks the runtime files and records what it
    /// found in <see cref="Diagnostics"/> (shown in the ribbon tooltip).
    /// </summary>
    internal static class RuntimeConfigurationBootstrap
    {
        private const string ApplicationFolderName = "PCTP";
        private const string RuntimeConfigFileName = "PCTP.exe.config";
        private const string DatabaseConfigFileName = "PCTP.Database.config";
        // Set to 1 to back up and re-copy both runtime files from the shipped ones on the next start.
        private const string ResetEnvVar = "PCTP_RESET_RUNTIME_CONFIG";

        private static string _diagnostics = string.Empty;

        /// <summary>What Initialize() found or repaired in the runtime config files (empty when everything matches).</summary>
        internal static string Diagnostics
        {
            get { return _diagnostics; }
        }

        public static void Initialize()
        {
            string sourceDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string sourceAppConfig = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            if (string.IsNullOrWhiteSpace(sourceAppConfig) || !File.Exists(sourceAppConfig))
                sourceAppConfig = Path.Combine(sourceDirectory, RuntimeConfigFileName);

            string sourceDatabaseConfig = Path.Combine(sourceDirectory, DatabaseConfigFileName);
            string runtimeDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                ApplicationFolderName);
            string runtimeAppConfig = Path.Combine(runtimeDirectory, RuntimeConfigFileName);
            string runtimeDatabaseConfig = Path.Combine(runtimeDirectory, DatabaseConfigFileName);

            try
            {
                Directory.CreateDirectory(runtimeDirectory);

                var notes = new List<string>();
                if (string.Equals(Environment.GetEnvironmentVariable(ResetEnvVar), "1", StringComparison.Ordinal))
                {
                    notes.Add("Biến môi trường " + ResetEnvVar + "=1: đã sao lưu rồi copy lại cấu hình mặc định.");
                    BackupAndDelete(runtimeDatabaseConfig);
                    BackupAndDelete(runtimeAppConfig);
                }

                // Copy the database file first because PCTP.exe.config references it via configSource.
                CopyIfMissing(sourceDatabaseConfig, runtimeDatabaseConfig);
                CopyIfMissing(sourceAppConfig, runtimeAppConfig);
                RepairRuntimeFiles(sourceAppConfig, sourceDatabaseConfig, runtimeAppConfig, runtimeDatabaseConfig, notes);
                _diagnostics = string.Join(Environment.NewLine, notes);

                if (!File.Exists(runtimeDatabaseConfig))
                    throw new FileNotFoundException("Không tìm thấy cấu hình SQL runtime.", runtimeDatabaseConfig);
                if (!File.Exists(runtimeAppConfig))
                    throw new FileNotFoundException("Không tìm thấy cấu hình ứng dụng runtime.", runtimeAppConfig);

                // Set this before any ConfigurationManager / EF / XPO access.
                AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", runtimeAppConfig);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is SecurityException ||
                ex is ArgumentException)
            {
                MessageBox.Show(
                    "Không thể khởi tạo cấu hình PCTP dùng chung.\r\n" +
                    "Thư mục cấu hình: " + runtimeDirectory + "\r\n\r\n" + ex.Message,
                    "PCTP - Runtime configuration",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                throw;
            }
        }

        /// <summary>
        /// The runtime files are copied once and never refreshed, so check the two ways they silently go wrong:
        /// (1) an old PCTP.exe.config with inline connection strings makes PCTP.Database.config irrelevant;
        /// (2) names missing from the runtime PCTP.Database.config make Settings.Default fall back to the
        ///     (different) defaults compiled into Settings.Designer.cs.
        /// Existing values are never overwritten; differences are only reported.
        /// </summary>
        private static void RepairRuntimeFiles(string sourceAppConfig, string sourceDatabaseConfig,
            string runtimeAppConfig, string runtimeDatabaseConfig, List<string> notes)
        {
            try
            {
                if (!DelegatesConnectionStrings(runtimeAppConfig) && File.Exists(sourceAppConfig))
                {
                    string backup = Backup(runtimeAppConfig);
                    File.Copy(sourceAppConfig, runtimeAppConfig, true);
                    notes.Add("PCTP.exe.config runtime là bản cũ (connectionStrings nằm trực tiếp trong file, bỏ qua PCTP.Database.config). " +
                        "Đã thay bằng bản hiện tại; bản cũ lưu tại " + backup);
                }

                if (!File.Exists(sourceDatabaseConfig)) return;
                XDocument shipped = XDocument.Load(sourceDatabaseConfig);
                XDocument runtime = XDocument.Load(runtimeDatabaseConfig);
                XElement shippedRoot = shipped.Root;
                XElement runtimeRoot = runtime.Root;
                if (shippedRoot == null || runtimeRoot == null) return;

                var runtimeByName = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
                foreach (XElement e in runtimeRoot.Elements("add"))
                {
                    string name = (string)e.Attribute("name");
                    if (!string.IsNullOrEmpty(name)) runtimeByName[name] = e;
                }

                var added = new List<string>();
                var differs = new List<string>();
                foreach (XElement e in shippedRoot.Elements("add").ToList())
                {
                    string name = (string)e.Attribute("name");
                    if (string.IsNullOrEmpty(name)) continue;
                    XElement existing;
                    if (!runtimeByName.TryGetValue(name, out existing))
                    {
                        runtimeRoot.Add(new XElement(e));
                        added.Add(name);
                    }
                    else
                    {
                        string runtimeTarget = Describe((string)existing.Attribute("connectionString"));
                        string shippedTarget = Describe((string)e.Attribute("connectionString"));
                        if (!string.Equals(runtimeTarget, shippedTarget, StringComparison.OrdinalIgnoreCase))
                            differs.Add("'" + name + "': đang dùng " + runtimeTarget + "  (bản đóng gói khai báo " + shippedTarget + ")");
                    }
                }

                if (added.Count > 0)
                {
                    string backup = Backup(runtimeDatabaseConfig);
                    runtime.Save(runtimeDatabaseConfig);
                    notes.Add("PCTP.Database.config runtime thiếu " + added.Count + " tên kết nối (nếu thiếu, Settings.Designer.cs sẽ ngầm dùng giá trị mặc định khác). " +
                        "Đã bổ sung: " + string.Join(", ", added) + ". Bản cũ lưu tại " + backup);
                }

                if (differs.Count > 0)
                    notes.Add("File runtime KHÁC file đóng gói (không tự ghi đè, hãy sửa file runtime hoặc đặt " + ResetEnvVar + "=1):" +
                        Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", differs));

                var groups = runtimeRoot.Elements("add")
                    .Where(e => e.Attribute("connectionString") != null)
                    .GroupBy(e => Describe((string)e.Attribute("connectionString")))
                    .ToList();
                if (groups.Count > 1)
                    notes.Add("CẢNH BÁO: các chuỗi kết nối trong file runtime trỏ tới " + groups.Count + " server/database khác nhau:" +
                        Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", groups.Select(g =>
                            g.Key + "  <-  " + g.Count() + " tên (" + string.Join(", ", g.Take(4).Select(e => (string)e.Attribute("name"))) +
                            (g.Count() > 4 ? ", ..." : string.Empty) + ")")));
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is SecurityException ||
                ex is XmlException)
            {
                notes.Add("Không kiểm tra/sửa được file cấu hình runtime: " + ex.Message);
            }
        }

        private static bool DelegatesConnectionStrings(string appConfigPath)
        {
            XDocument doc = XDocument.Load(appConfigPath);
            XElement connectionStrings = doc.Root == null ? null : doc.Root.Element("connectionStrings");
            if (connectionStrings == null) return false;
            string source = (string)connectionStrings.Attribute("configSource");
            return string.Equals(source, DatabaseConfigFileName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>"server / database" of a connection string (SQL, EF or XPO style); never returns credentials.</summary>
        private static string Describe(string connectionString)
        {
            return ExtractValue(connectionString, "data source|server|address|addr|network address") + " / " +
                   ExtractValue(connectionString, "initial catalog|database");
        }

        private static string ExtractValue(string connectionString, string keys)
        {
            if (string.IsNullOrEmpty(connectionString)) return "?";
            Match m = Regex.Match(connectionString,
                "(?:^|[;\"&\\s])(?:" + keys + ")\\s*=\\s*([^;\"&]+)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : "?";
        }

        private static string Backup(string path)
        {
            string backup = path + ".bak-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            File.Copy(path, backup, true);
            return backup;
        }

        private static void BackupAndDelete(string path)
        {
            if (!File.Exists(path)) return;
            Backup(path);
            File.Delete(path);
        }

        private static void CopyIfMissing(string source, string destination)
        {
            if (File.Exists(destination)) return;
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                throw new FileNotFoundException("Không tìm thấy file cấu hình mặc định để khởi tạo.", source);
            File.Copy(source, destination, false);
        }
    }
}
