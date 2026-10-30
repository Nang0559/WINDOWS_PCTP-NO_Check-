using System;
using System.IO;
using System.Security;
using System.Windows.Forms;

namespace PCTP.Infrastructure.Configuration
{
    /// <summary>
    /// Moves mutable runtime configuration out of the versioned ClickOnce directory.
    /// Files are copied only on first launch and are never overwritten by application updates.
    /// </summary>
    internal static class RuntimeConfigurationBootstrap
    {
        private const string ApplicationFolderName = "PCTP";
        private const string RuntimeConfigFileName = "PCTP.exe.config";
        private const string DatabaseConfigFileName = "PCTP.Database.config";

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

                // Copy the database file first because PCTP.exe.config references it via configSource.
                CopyIfMissing(sourceDatabaseConfig, runtimeDatabaseConfig);
                CopyIfMissing(sourceAppConfig, runtimeAppConfig);

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

        private static void CopyIfMissing(string source, string destination)
        {
            if (File.Exists(destination)) return;
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                throw new FileNotFoundException("Không tìm thấy file cấu hình mặc định để khởi tạo.", source);
            File.Copy(source, destination, false);
        }
    }
}
