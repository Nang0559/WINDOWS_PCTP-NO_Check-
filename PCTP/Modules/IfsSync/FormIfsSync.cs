using DevExpress.XtraEditors;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.IfsSync
{
    /// <summary>
    /// Administrative UI for one-way IFS master data synchronization.
    /// This form never writes to IFS; it invokes the existing SyncAll service.
    /// </summary>
    public sealed class FormIfsSync : XtraForm
    {
        private readonly LabelControl _connectionInfo = new LabelControl();
        private readonly LabelControl _status = new LabelControl();
        private readonly DataGridView _batches = new DataGridView();
        private readonly TextBox _details = new TextBox();
        private readonly SimpleButton _checkButton = new SimpleButton();
        private readonly SimpleButton _syncButton = new SimpleButton();
        private readonly SimpleButton _refreshButton = new SimpleButton();
        private bool _isRunning;

        private string OracleConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["IFS_ORACLE"]?.ConnectionString; }
        }

        private string TargetConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString; }
        }

        public FormIfsSync()
        {
            Text = "Đồng bộ dữ liệu IFS → FVN_PCTP";
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1050, 680);
            AutoScaleMode = AutoScaleMode.Font;
            BuildLayout();
            Shown += (s, e) => RefreshHistory();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
            Controls.Add(root);

            var heading = new GroupControl
            {
                Text = "Nguồn và đích đồng bộ",
                Dock = DockStyle.Fill
            };
            _connectionInfo.Dock = DockStyle.Fill;
            _connectionInfo.Padding = new Padding(8, 4, 8, 4);
            _connectionInfo.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            _connectionInfo.Text =
                "Nguồn: Oracle IFS (chỉ đọc)     →     Đích: SQL Server / FVN_PCTP\r\n" +
                "Dữ liệu: Part Catalog, Inventory Part, BOM Header, Alternative và Component.";
            heading.Controls.Add(_connectionInfo);
            root.Controls.Add(heading, 0, 0);

            var commandPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 8, 0, 8)
            };
            commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 440));
            _status.Dock = DockStyle.Fill;
            _status.AutoSizeMode = LabelAutoSizeMode.None;
            _status.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _status.Text = "Sẵn sàng. Chưa thực hiện đồng bộ trong phiên này.";
            commandPanel.Controls.Add(_status, 0, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            _syncButton.Text = "Đồng bộ toàn bộ";
            _syncButton.Width = 145;
            _syncButton.Height = 34;
            _syncButton.Click += async (s, e) => await RunSyncAsync();

            _checkButton.Text = "Kiểm tra kết nối";
            _checkButton.Width = 125;
            _checkButton.Height = 34;
            _checkButton.Click += async (s, e) => await CheckConnectionsAsync();

            _refreshButton.Text = "Làm mới lịch sử";
            _refreshButton.Width = 125;
            _refreshButton.Height = 34;
            _refreshButton.Click += (s, e) => RefreshHistory();

            buttons.Controls.Add(_syncButton);
            buttons.Controls.Add(_checkButton);
            buttons.Controls.Add(_refreshButton);
            commandPanel.Controls.Add(buttons, 1, 0);
            root.Controls.Add(commandPanel, 0, 1);

            var history = new GroupControl
            {
                Text = "Lịch sử các lần đồng bộ (100 batch gần nhất)",
                Dock = DockStyle.Fill
            };
            ConfigureBatchGrid();
            history.Controls.Add(_batches);
            root.Controls.Add(history, 0, 2);

            var errorGroup = new GroupControl
            {
                Text = "Chi tiết lỗi của batch đang chọn",
                Dock = DockStyle.Fill
            };
            _details.Dock = DockStyle.Fill;
            _details.Multiline = true;
            _details.ReadOnly = true;
            _details.ScrollBars = ScrollBars.Both;
            _details.WordWrap = false;
            _details.Font = new Font("Consolas", 9F);
            _details.Text = "Chọn một batch để xem lỗi ghi nhận (nếu có).";
            errorGroup.Controls.Add(_details);
            root.Controls.Add(errorGroup, 0, 3);
        }

        private void ConfigureBatchGrid()
        {
            _batches.Dock = DockStyle.Fill;
            _batches.ReadOnly = true;
            _batches.AllowUserToAddRows = false;
            _batches.AllowUserToDeleteRows = false;
            _batches.AllowUserToResizeRows = false;
            _batches.MultiSelect = false;
            _batches.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _batches.AutoGenerateColumns = true;
            _batches.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            _batches.RowHeadersVisible = false;
            _batches.BackgroundColor = SystemColors.Window;
            _batches.SelectionChanged += (s, e) => LoadSelectedBatchErrors();
        }

        private async Task CheckConnectionsAsync()
        {
            if (_isRunning) return;
            SetBusy(true, "Đang kiểm tra kết nối Oracle IFS và SQL Server FVN_PCTP...");
            try
            {
                string message = await Task.Run(() =>
                {
                    if (string.IsNullOrWhiteSpace(OracleConnectionString) ||
                        OracleConnectionString.IndexOf("IFS_ORACLE", StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new InvalidOperationException("Chưa cấu hình IFS_ORACLE.");

                    if (string.IsNullOrWhiteSpace(TargetConnectionString))
                        throw new InvalidOperationException("Thiếu connection string FVN_PCTP trong PCTP.Database.config.");

                    SqlConnectionStringBuilder targetBuilder;
                    try
                    {
                        targetBuilder = new SqlConnectionStringBuilder(TargetConnectionString);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Connection string FVN_PCTP không hợp lệ: " + ex.Message, ex);
                    }

                    if (!string.Equals(targetBuilder.InitialCatalog, "FVN_PCTP", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            "Từ chối kết nối: Initial Catalog của FVN_PCTP phải là FVN_PCTP, hiện tại là '" +
                            targetBuilder.InitialCatalog + "'.");

                    using (var oracle = new OracleConnection(OracleConnectionString))
                    using (var command = new OracleCommand("SELECT 1 FROM DUAL", oracle))
                    {
                        oracle.Open();
                        command.ExecuteScalar();
                    }

                    using (var sql = new SqlConnection(TargetConnectionString))
                    {
                        sql.Open();
                        string database;
                        using (var command = new SqlCommand("SELECT DB_NAME();", sql))
                        {
                            database = Convert.ToString(command.ExecuteScalar());
                        }

                        if (!string.Equals(database, "FVN_PCTP", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Từ chối: kết nối SQL hiện tại không trỏ đến FVN_PCTP.");

                        // Do not use OBJECT_ID here: SQL Server may return NULL when
                        // the login cannot see object metadata, even when the tables exist.
                        // Probe the real objects so missing-table and permission errors
                        // are reported separately by SQL Server.
                        try
                        {
                            using (var command = new SqlCommand(@"
SELECT TOP (0) SyncBatchId FROM [IFS].[SyncBatch];
SELECT TOP (0) SyncStageRowId FROM [IFS].[SyncStageRow];", sql))
                            {
                                command.ExecuteNonQuery();
                            }
                        }
                        catch (SqlException ex)
                        {
                            if (ex.Number == 208)
                                throw new InvalidOperationException(
                                    "Không tìm thấy bảng IFS.SyncBatch hoặc IFS.SyncStageRow trong FVN_PCTP. " +
                                    "Hãy kiểm tra đúng database/schema và trạng thái migration 012. " +
                                    "SQL: " + ex.Message, ex);
                            if (ex.Number == 229)
                                throw new InvalidOperationException(
                                    "Các bảng đồng bộ có thể đã tồn tại nhưng tài khoản SQL của ứng dụng thiếu quyền SELECT. " +
                                    "Cấp quyền phù hợp trên schema IFS. SQL: " + ex.Message, ex);
                            throw;
                        }
                    }

                    return "Kết nối Oracle IFS OK; SQL Server FVN_PCTP OK; migration 012 đã có.";
                });

                _status.Text = message;
                _details.Text = message;
                XtraMessageBox.Show(this, message, "Kiểm tra kết nối",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _status.Text = "Kiểm tra kết nối thất bại.";
                _details.Text = ex.ToString();
                XtraMessageBox.Show(this, ex.Message, "Kiểm tra kết nối",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                SetBusy(false, _status.Text);
            }
        }

        private async Task RunSyncAsync()
        {
            if (_isRunning) return;

            var confirm = XtraMessageBox.Show(this,
                "Chương trình sẽ đọc 5 IFS View và đồng bộ Full Snapshot vào FVN_PCTP.\r\n\r\n" +
                "Dữ liệu không bị xóa chỉ vì không xuất hiện trong lần đọc này.\r\n" +
                "Quá trình có thể mất nhiều thời gian. Bạn muốn tiếp tục?",
                "Xác nhận đồng bộ IFS",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            SetBusy(true, "Đang đồng bộ. Không đóng ứng dụng cho đến khi hoàn tất...");
            _details.Text = "Bắt đầu đồng bộ Full Snapshot. Đang đọc IFS và ghi staging theo từng nhóm.";
            try
            {
                IfsSyncResult result = await Task.Run(() => new IfsSyncService().SyncAll());
                _status.Text = "Đồng bộ thành công. Batch #" + result.SyncBatchId;
                _details.Text =
                    "Trạng thái: " + result.Status + Environment.NewLine +
                    "Batch ID: " + result.SyncBatchId + Environment.NewLine +
                    "PART_CATALOG: " + result.PartCatalogRows + Environment.NewLine +
                    "INVENTORY_PART: " + result.InventoryPartRows + Environment.NewLine +
                    "BOM_HEADER: " + result.BomHeaderRows + Environment.NewLine +
                    "BOM_ALTERNATIVE: " + result.BomAlternativeRows + Environment.NewLine +
                    "BOM_COMPONENT: " + result.BomComponentRows;
                RefreshHistory();
                XtraMessageBox.Show(this, _details.Text, "Đồng bộ hoàn tất",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _status.Text = "Đồng bộ thất bại. Xem chi tiết lỗi bên dưới.";
                _details.Text = ex.ToString();
                RefreshHistory();
                XtraMessageBox.Show(this, ex.Message, "Đồng bộ IFS thất bại",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, _status.Text);
            }
        }

        private void RefreshHistory()
        {
            if (_isRunning) return;
            try
            {
                using (var connection = new SqlConnection(TargetConnectionString))
                using (var adapter = new SqlDataAdapter(@"
SELECT TOP (100)
       b.SyncBatchId,
       b.SyncMode,
       b.Status,
       b.StartedAtUtc,
       b.CompletedAtUtc,
       b.InitiatedBy,
       b.PartCatalogRows,
       b.InventoryPartRows,
       b.BomHeaderRows,
       b.BomAlternativeRows,
       b.BomComponentRows,
       b.InsertCount,
       b.UpdateCount,
       b.ErrorSummary,
       (SELECT COUNT(*) FROM IFS.SyncError e WHERE e.SyncBatchId=b.SyncBatchId) AS ErrorCount
FROM IFS.SyncBatch b
ORDER BY b.SyncBatchId DESC;", connection))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    _batches.DataSource = table;
                    if (_batches.Columns.Contains("StartedAtUtc"))
                        _batches.Columns["StartedAtUtc"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss";
                    if (_batches.Columns.Contains("CompletedAtUtc"))
                        _batches.Columns["CompletedAtUtc"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss";
                    _status.Text = "Đã tải " + table.Rows.Count + " batch gần nhất.";
                    if (table.Rows.Count == 0)
                        _details.Text = "Chưa có lịch sử đồng bộ.";
                }
            }
            catch (Exception ex)
            {
                _status.Text = "Không đọc được lịch sử. Kiểm tra kết nối và migration 012.";
                _details.Text = ex.ToString();
            }
        }

        private void LoadSelectedBatchErrors()
        {
            if (_isRunning) return;
            var selected = _batches.CurrentRow?.DataBoundItem as DataRowView;
            if (selected == null || !selected.Row.Table.Columns.Contains("SyncBatchId"))
                return;

            long batchId = Convert.ToInt64(selected["SyncBatchId"]);
            try
            {
                using (var connection = new SqlConnection(TargetConnectionString))
                using (var command = new SqlCommand(@"
SELECT TOP (20) EntityName, ErrorCode, ErrorMessage, LoggedAtUtc, ErrorDetails
FROM IFS.SyncError
WHERE SyncBatchId=@SyncBatchId
ORDER BY SyncErrorId DESC;", connection))
                {
                    command.Parameters.Add("@SyncBatchId", SqlDbType.BigInt).Value = batchId;
                    connection.Open();
                    using (var reader = command.ExecuteReader())
                    {
                        var output = new System.Text.StringBuilder();
                        while (reader.Read())
                        {
                            output.AppendLine("[" + Convert.ToString(reader["LoggedAtUtc"]) + "] " +
                                Convert.ToString(reader["EntityName"]) + " / " +
                                Convert.ToString(reader["ErrorCode"]));
                            output.AppendLine(Convert.ToString(reader["ErrorMessage"]));
                            if (!reader.IsDBNull(reader.GetOrdinal("ErrorDetails")))
                            {
                                string details = Convert.ToString(reader["ErrorDetails"]);
                                output.AppendLine(details.Length > 4000 ? details.Substring(0, 4000) : details);
                            }
                            output.AppendLine(new string('-', 70));
                        }
                        _details.Text = output.Length == 0
                            ? "Batch #" + batchId + ": không có lỗi được ghi nhận."
                            : output.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                _details.Text = "Không đọc được chi tiết lỗi: " + ex.Message;
            }
        }

        private void SetBusy(bool busy, string message)
        {
            _isRunning = busy;
            _syncButton.Enabled = !busy;
            _checkButton.Enabled = !busy;
            _refreshButton.Enabled = !busy;
            UseWaitCursor = busy;
            if (!string.IsNullOrWhiteSpace(message))
                _status.Text = message;
        }
    }
}
