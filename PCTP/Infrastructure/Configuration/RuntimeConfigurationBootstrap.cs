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
        // Set to 1 to keep the runtime PCTP.Database.config even when the shipped one is newer/different.
        private const string KeepRuntimeEnvVar = "PCTP_KEEP_RUNTIME_DATABASE_CONFIG";
        // A shipped file that still holds these template placeholders must never replace real credentials.
        private const string TemplateMarker = "YOUR_SQL_";

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
                FollowShippedDatabaseConfig(sourceDatabaseConfig, runtimeDatabaseConfig, notes);
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
        /// Keeps the runtime PCTP.Database.config in step with the shipped one (the file edited in the project
        /// and copied next to PCTP.exe by the build). When the two differ, the runtime file is backed up
        /// (.bak-timestamp) and replaced, so editing PCTP\PCTP.Database.config and rebuilding is enough.
        /// Skipped when the shipped file is still the template (YOUR_SQL_* placeholders), so a published
        /// template never wipes real credentials, and when PCTP_KEEP_RUNTIME_DATABASE_CONFIG=1.
        /// </summary>
        private static void FollowShippedDatabaseConfig(string source, string runtime, List<string> notes)
        {
            try
            {
                if (string.Equals(Environment.GetEnvironmentVariable(KeepRuntimeEnvVar), "1", StringComparison.Ordinal))
                    return;
                if (string.IsNullOrWhiteSpace(source) || !File.Exists(source) || !File.Exists(runtime))
                    return;

                string shipped = File.ReadAllText(source);
                if (shipped.IndexOf(TemplateMarker, StringComparison.OrdinalIgnoreCase) >= 0)
                    return;
                if (string.Equals(NormalizeForCompare(shipped), NormalizeForCompare(File.ReadAllText(runtime)), StringComparison.Ordinal))
                    return;

                string backup = Backup(runtime);
                File.Copy(source, runtime, true);
                notes.Add("PCTP.Database.config runtime đã được cập nhật theo file đóng gói (" + source + "). Bản cũ lưu tại " + backup +
                    ". Đặt " + KeepRuntimeEnvVar + "=1 nếu muốn giữ file runtime.");
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is SecurityException)
            {
                notes.Add("Không đồng bộ được PCTP.Database.config runtime theo file đóng gói: " + ex.Message);
            }
        }

        private static string NormalizeForCompare(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Trim();
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
