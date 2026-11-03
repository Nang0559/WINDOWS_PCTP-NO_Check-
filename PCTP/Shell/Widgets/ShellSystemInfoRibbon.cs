using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using PCTP.Infrastructure.Configuration;
using System;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;

namespace PCTP.Shell.Widgets
{
    /// <summary>
    /// Hiển thị trên Ribbon của Main_APP (sát mép phải): máy đang chạy app, máy được
    /// phép bắn QR và cơ sở dữ liệu app đang thao tác. Không bao giờ hiển thị mật khẩu.
    /// </summary>
    internal sealed class ShellSystemInfoRibbon
    {
        private static readonly Color OkColor = Color.FromArgb(0, 110, 0);
        private static readonly Color WarnColor = Color.FromArgb(180, 90, 0);
        private static readonly Color LiveColor = Color.FromArgb(180, 0, 0);

        private readonly BarStaticItem _hostItem;
        private readonly BarStaticItem _qrItem;
        private readonly BarStaticItem _databaseItem;

        internal ShellSystemInfoRibbon(RibbonControl ribbon, RibbonPage page)
        {
            if (ribbon == null) throw new ArgumentNullException("ribbon");
            if (page == null) throw new ArgumentNullException("page");

            RibbonPageGroup group = new RibbonPageGroup("Hệ thống");
            group.Name = "ribbonPageGroupSystemInfo";
            group.Alignment = RibbonPageGroupAlignment.Far; // sát mép phải Ribbon

            _hostItem = CreateItem(ribbon, group, "Máy này: ...");
            _qrItem = CreateItem(ribbon, group, "Máy bắn QR: ...");
            _databaseItem = CreateItem(ribbon, group, "Database: ...");

            page.Groups.Add(group);
        }

        /// <summary>
        /// hostName / qrMachine lấy từ Main_APP.hostname / Main_APP.TM_BANQR.
        /// </summary>
        internal void Update(string hostName, string qrMachine)
        {
            hostName = (hostName ?? string.Empty).Trim();
            qrMachine = (qrMachine ?? string.Empty).Trim();

            _hostItem.Caption = "Máy này: " + Display(hostName);

            if (qrMachine.Length == 0)
            {
                _qrItem.Caption = "Máy bắn QR: (chưa cấu hình)";
                SetColor(_qrItem, WarnColor);
            }
            else if (string.Equals(hostName, qrMachine, StringComparison.OrdinalIgnoreCase))
            {
                _qrItem.Caption = "Máy bắn QR: " + qrMachine + "  —  MÁY NÀY ĐƯỢC BẮN QR";
                SetColor(_qrItem, OkColor);
            }
            else
            {
                _qrItem.Caption = "Máy bắn QR: " + qrMachine + "  —  máy này chỉ XEM";
                SetColor(_qrItem, WarnColor);
            }

            UpdateDatabase();
        }

        private void UpdateDatabase()
        {
            string server = string.Empty;
            string database = string.Empty;
            string actual = string.Empty;
            string error = null;

            try
            {
                SqlConnectionStringBuilder builder =
                    new SqlConnectionStringBuilder(ClassSQL.SQLPROVIDER.GetConfiguredConnection());
                server = builder.DataSource;
                database = builder.InitialCatalog;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            // Hỏi thẳng SQL Server đang kết nối để biết DB THỰC SỰ app đang thao tác.
            if (error == null)
            {
                try
                {
                    ClassSQL.SQLPROVIDER sql = new ClassSQL.SQLPROVIDER();
                    actual = sql.ExecuteReader(sql.B7R2_FCCdb,
                        "SELECT CAST(@@SERVERNAME AS NVARCHAR(128)) + N' | ' + CAST(DB_NAME() AS NVARCHAR(128))");
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
            }

            string configInfo = DescribeConfigFile();

            if (server.Length == 0)
            {
                _databaseItem.Caption = "Database: (không đọc được cấu hình)";
                SetColor(_databaseItem, LiveColor);
                _databaseItem.Hint = (error ?? string.Empty) + Environment.NewLine + configInfo + ConfigDiagnostics();
                return;
            }

            bool qcFixture = !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(ClassSQL.QcSqlOverride.ConnectionEnvVar));

            Color color;
            string tag = Classify(server, qcFixture, out color);
            string state = error == null ? string.Empty : "  —  KHÔNG KẾT NỐI ĐƯỢC";

            _databaseItem.Caption = "DB: " + Display(server) + " / " + Display(database) + "  [" + tag + "]" + state +
                (RuntimeConfigurationBootstrap.Diagnostics.Length > 0 ? "  [!] xem tooltip" : string.Empty);
            SetColor(_databaseItem, error != null ? LiveColor : color);

            _databaseItem.Hint =
                "File cấu hình đang dùng: " + configInfo + Environment.NewLine +
                (error != null
                    ? "Lỗi kết nối: " + error
                    : "SQL Server trả về (@@SERVERNAME | DB_NAME()): " + actual) +
                ConfigDiagnostics();
        }

        /// <summary>
        /// App copy PCTP.Database.config sang thư mục runtime (ProgramData\PCTP) ở lần chạy đầu và
        /// KHÔNG ghi đè nữa, nên file đang có hiệu lực có thể khác file trong project/bin.
        /// </summary>
        private static string ConfigDiagnostics()
        {
            string notes = RuntimeConfigurationBootstrap.Diagnostics;
            return notes.Length == 0 ? string.Empty : Environment.NewLine + Environment.NewLine + notes;
        }

        private static string DescribeConfigFile()
        {
            try
            {
                string appConfig = AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE") as string;
                if (string.IsNullOrWhiteSpace(appConfig))
                    appConfig = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

                string directory = Path.GetDirectoryName(appConfig);
                string databaseConfig = Path.Combine(directory ?? string.Empty, "PCTP.Database.config");
                string stamp = File.Exists(databaseConfig)
                    ? File.GetLastWriteTime(databaseConfig).ToString("yyyy-MM-dd HH:mm:ss")
                    : "không tồn tại";
                return databaseConfig + " (sửa lần cuối: " + stamp + ")";
            }
            catch (Exception ex)
            {
                return "(không xác định được: " + ex.Message + ")";
            }
        }

        private static string Classify(string server, bool qcFixture, out Color color)
        {
            if (qcFixture)
            {
                color = OkColor;
                return "QC FIXTURE";
            }

            string s = (server ?? string.Empty).ToLowerInvariant();
            if (s.Contains("192.168.200.14") || s.Contains("bravo"))
            {
                color = LiveColor;
                return "DB14 · BRAVO";
            }
            if (s.Contains("192.168.200.57"))
            {
                color = WarnColor;
                return "DB57 · TEST";
            }

            color = LiveColor;
            return "LIVE";
        }

        private static BarStaticItem CreateItem(RibbonControl ribbon, RibbonPageGroup group, string caption)
        {
            BarStaticItem item = new BarStaticItem();
            item.Caption = caption;
            item.ItemAppearance.Normal.Font = new Font("Tahoma", 8.25f, FontStyle.Bold);
            ribbon.Items.Add(item);
            group.ItemLinks.Add(item);
            return item;
        }

        private static void SetColor(BarStaticItem item, Color color)
        {
            item.ItemAppearance.Normal.ForeColor = color;
        }

        private static string Display(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(trống)" : value.Trim();
        }
    }
}
