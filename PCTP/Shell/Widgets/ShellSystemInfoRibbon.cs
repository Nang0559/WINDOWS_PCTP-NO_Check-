using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using System;
using System.Data.SqlClient;
using System.Drawing;

namespace PCTP.Shell.Widgets
{
    /// <summary>
    /// Hiển thị trên Ribbon của Main_APP: máy đang chạy app, máy được phép bắn QR
    /// và cơ sở dữ liệu app đang thao tác. Không bao giờ hiển thị mật khẩu.
    /// </summary>
    internal sealed class ShellSystemInfoRibbon
    {
        private static readonly Color OkColor = Color.FromArgb(0, 110, 0);
        private static readonly Color WarnColor = Color.FromArgb(180, 90, 0);
        private static readonly Color LiveColor = Color.FromArgb(180, 0, 0);

        private readonly BarStaticItem _hostItem;
        private readonly BarStaticItem _qrItem;
        private readonly BarStaticItem _serverItem;
        private readonly BarStaticItem _databaseItem;

        internal ShellSystemInfoRibbon(RibbonControl ribbon, RibbonPage page)
        {
            if (ribbon == null) throw new ArgumentNullException("ribbon");
            if (page == null) throw new ArgumentNullException("page");

            RibbonPageGroup group = new RibbonPageGroup("Hệ thống");
            group.Name = "ribbonPageGroupSystemInfo";

            _hostItem = CreateItem(ribbon, group, "Máy này: ...");
            _qrItem = CreateItem(ribbon, group, "Máy bắn QR: ...");
            _serverItem = CreateItem(ribbon, group, "SQL Server: ...");
            _databaseItem = CreateItem(ribbon, group, "Database: ...");

            page.Groups.Add(group);
        }

        /// <summary>
        /// Cập nhật thông tin. hostName / qrMachine lấy từ Main_APP.hostname / Main_APP.TM_BANQR.
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

            bool qcFixture = !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(ClassSQL.QcSqlOverride.ConnectionEnvVar));

            if (error != null && server.Length == 0)
            {
                _serverItem.Caption = "SQL Server: (không đọc được cấu hình)";
                _databaseItem.Caption = "Database: (lỗi)";
                SetColor(_serverItem, LiveColor);
                SetColor(_databaseItem, LiveColor);
                _databaseItem.Hint = error;
                return;
            }

            _serverItem.Caption = "SQL Server: " + Display(server);
            SetColor(_serverItem, Color.Empty);

            string env = qcFixture ? "QC FIXTURE" : "LIVE";
            string state = error == null ? string.Empty : "  —  KHÔNG KẾT NỐI ĐƯỢC";
            _databaseItem.Caption = "Database: " + Display(database) + "  [" + env + "]" + state;
            SetColor(_databaseItem, error != null ? LiveColor : (qcFixture ? OkColor : LiveColor));

            _serverItem.Hint = "Theo cấu hình PCTP.Database.config";
            _databaseItem.Hint = error != null
                ? error
                : "SQL Server đang trả về (@@SERVERNAME | DB_NAME()): " + actual;
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
