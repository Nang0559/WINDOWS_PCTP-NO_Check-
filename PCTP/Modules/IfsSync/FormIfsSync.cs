using DevExpress.XtraEditors;
using Oracle.ManagedDataAccess.Client;
using PCTP.Shared.Helpers;
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
        private readonly Label _status = new Label();
        private readonly DataGridView _entities = new DataGridView();
        private readonly DataGridView _batches = new DataGridView();
        private readonly TextBox _details = new TextBox();
        private readonly SimpleButton _checkButton = new SimpleButton();
        private readonly SimpleButton _syncButton = new SimpleButton();
        private readonly SimpleButton _selectiveSyncButton = new SimpleButton();
        private readonly CheckBox[] _entityChecks = new CheckBox[7];
        private readonly SimpleButton _refreshButton = new SimpleButton();
        private readonly IWaitFormService _waitForm;
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
            _waitForm = new WaitFormService(this);
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
                RowCount = 6
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
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
                "Dữ liệu: Part Catalog, Inventory Part, BOM, Warehouse và Supplier.";
            heading.Controls.Add(_connectionInfo);
            root.Controls.Add(heading, 0, 0);

            var selectionGroup = new GroupControl
            {
                Text = "Chọn nhóm dữ liệu cần đồng bộ (có thể chọn nhiều nhóm)",
                Dock = DockStyle.Fill
            };
            var selectionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 4, 8, 2),
                WrapContents = true,
                AutoScroll = true
            };
            string[] entityNames = { "PART_CATALOG", "INVENTORY_PART", "BOM_HEADER", "BOM_ALTERNATIVE", "BOM_COMPONENT", "WAREHOUSE", "SUPPLIER" };
            for (int i = 0; i < entityNames.Length; i++)
            {
                _entityChecks[i] = new CheckBox { Text = entityNames[i], AutoSize = true, Margin = new Padding(8, 6, 18, 4) };
                selectionPanel.Controls.Add(_entityChecks[i]);
            }
            selectionGroup.Controls.Add(selectionPanel);
            root.Controls.Add(selectionGroup, 0, 1);

            var commandPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 2, 0, 2)
            };
            commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 590));
            _status.Dock = DockStyle.Fill;
            _status.AutoSize = false;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            _status.Padding = new Padding(8, 0, 8, 0);
            _status.AutoEllipsis = false;
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
            _syncButton.Click += async (s, e) => await RunSyncAsync(false);

            _selectiveSyncButton.Text = "Đồng bộ mục đã chọn";
            _selectiveSyncButton.Width = 155;
            _selectiveSyncButton.Height = 34;
            _selectiveSyncButton.Click += async (s, e) => await RunSyncAsync(true);

            _checkButton.Text = "Kiểm tra kết nối";
            _checkButton.Width = 125;
            _checkButton.Height = 34;
            _checkButton.Click += async (s, e) => await CheckConnectionsAsync();

            _refreshButton.Text = "Làm mới lịch sử";
            _refreshButton.Width = 125;
            _refreshButton.Height = 34;
            _refreshButton.Click += (s, e) => RefreshHistory();

            buttons.Controls.Add(_syncButton);
            buttons.Controls.Add(_selectiveSyncButton);
            buttons.Controls.Add(_checkButton);
            buttons.Controls.Add(_refreshButton);
            commandPanel.Controls.Add(buttons, 1, 0);
            root.Controls.Add(commandPanel, 0, 2);

            var entityStatus = new GroupControl
            {
                Text = "Trạng thái đồng bộ theo entity (đối chiếu nguồn gần nhất và dữ liệu đích)",
                Dock = DockStyle.Fill
            };
            ConfigureEntityGrid();
            entityStatus.Controls.Add(_entities);
            root.Controls.Add(entityStatus, 0, 3);

            var history = new GroupControl
            {
                Text = "Lịch sử các lần đồng bộ (100 batch gần nhất)",
                Dock = DockStyle.Fill
            };
            ConfigureBatchGrid();
            history.Controls.Add(_batches);
            root.Controls.Add(history, 0, 4);

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
            root.Controls.Add(errorGroup, 0, 5);
        }

        private void ConfigureEntityGrid()
        {
            _entities.Dock = DockStyle.Fill;
            _entities.ReadOnly = true;
            _entities.AllowUserToAddRows = false;
            _entities.AllowUserToDeleteRows = false;
            _entities.AllowUserToResizeRows = false;
            _entities.MultiSelect = false;
            _entities.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _entities.AutoGenerateColumns = true;
            _entities.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            _entities.RowHeadersVisible = false;
            _entities.BackgroundColor = SystemColors.Window;
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
                string message = await _waitForm.RunAsync(() => Task.Run(() =>
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
SELECT TOP (0) SyncBatchId, WarehouseRows, SelectedEntities FROM [IFS].[SyncBatch];
SELECT TOP (0) SyncStageRowId FROM [IFS].[SyncStageRow];
SELECT TOP (0) SiteCode, WarehouseCode, Description FROM [MES].[FVN_IfsWarehouse];
SELECT TOP (0) SupplierCode, SupplierName FROM [IFS].[FVN_Supplier];", sql))
                            {
                                command.ExecuteNonQuery();
                            }
                        }
                        catch (SqlException ex)
                        {
                            if (ex.Number == 208)
                                throw new InvalidOperationException(
                                    "Không tìm thấy một hoặc nhiều bảng đồng bộ (IFS.SyncBatch, IFS.SyncStageRow, IFS.FVN_Supplier) trong FVN_PCTP. " +
                                    "Hãy kiểm tra đúng database/schema và trạng thái migration 012. " +
                                    "SQL: " + ex.Message, ex);
                            if (ex.Number == 229)
                                throw new InvalidOperationException(
                                    "Các bảng đồng bộ có thể đã tồn tại nhưng tài khoản SQL của ứng dụng thiếu quyền SELECT. " +
                                    "Cấp quyền phù hợp trên schema IFS. SQL: " + ex.Message, ex);
                            throw;
                        }
                    }

                    return "Kết nối Oracle IFS OK; SQL Server FVN_PCTP OK; migration 012, 027, 028 và 037 đã sẵn sàng.";
                }), "Đang kiểm tra kết nối Oracle IFS và SQL Server FVN_PCTP...");

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

        private async Task RunSyncAsync(bool selectedOnly)
        {
            if (_isRunning) return;

            var selectedEntities = new System.Collections.Generic.List<string>();
            if (selectedOnly)
            {
                foreach (CheckBox check in _entityChecks)
                    if (check != null && check.Checked) selectedEntities.Add(check.Text);
                if (selectedEntities.Count == 0)
                {
                    XtraMessageBox.Show(this, "Hãy chọn ít nhất một nhóm dữ liệu.", "Đồng bộ IFS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (selectedEntities.Contains("BOM_COMPONENT") && !selectedEntities.Contains("BOM_ALTERNATIVE"))
                    selectedEntities.Add("BOM_ALTERNATIVE");
                if ((selectedEntities.Contains("BOM_COMPONENT") || selectedEntities.Contains("BOM_ALTERNATIVE")) && !selectedEntities.Contains("BOM_HEADER"))
                    selectedEntities.Add("BOM_HEADER");
            }
            string modeDescription = selectedOnly
                ? "các nhóm đã chọn: " + string.Join(", ", selectedEntities)
                : "toàn bộ 7 nhóm dữ liệu";
            var confirm = XtraMessageBox.Show(this,
                "Chương trình sẽ đọc " + modeDescription + " từ Oracle IFS và Apply vào FVN_PCTP.\r\n\r\n" +
                "Chỉ các entity trong batch này được cập nhật; entity không chọn sẽ được giữ nguyên.\r\n" +
                "Quá trình có thể mất nhiều thời gian. Bạn muốn tiếp tục?",
                "Xác nhận đồng bộ IFS",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            SetBusy(true, "Đang đồng bộ. Không đóng ứng dụng cho đến khi hoàn tất...");
            _details.Text = selectedOnly
                ? "Bắt đầu đồng bộ chọn lọc: " + string.Join(", ", selectedEntities) + Environment.NewLine + "Đang đọc IFS và ghi staging."
                : "Bắt đầu đồng bộ Full Snapshot. Đang đọc IFS và ghi staging theo từng nhóm.";
            try
            {
                // Keep the form visible: report progress inline instead of covering
                // the connection summary with a modal WaitForm.
                var uiContext = System.Threading.SynchronizationContext.Current;
                Action<string> reportPhase = phase =>
                {
                    if (uiContext == null)
                        _status.Text = phase;
                    else
                        uiContext.Post(_ => { if (!IsDisposed) _status.Text = phase; }, null);
                };
                IfsSyncService service = new IfsSyncService();
                IfsSyncResult result = await Task.Run(() => selectedOnly
                    ? service.SyncSelected(selectedEntities, reportPhase)
                    : service.SyncAll(reportPhase));
                _status.Text = "Đồng bộ thành công. Batch #" + result.SyncBatchId;
                _details.Text =
                    "Trạng thái: " + result.Status + Environment.NewLine +
                    "Batch ID: " + result.SyncBatchId + Environment.NewLine +
                    "PART_CATALOG: " + result.PartCatalogRows + Environment.NewLine +
                    "INVENTORY_PART: " + result.InventoryPartRows + Environment.NewLine +
                    "BOM_HEADER: " + result.BomHeaderRows + Environment.NewLine +
                    "BOM_ALTERNATIVE: " + result.BomAlternativeRows + Environment.NewLine +
                    "BOM_COMPONENT: " + result.BomComponentRows + Environment.NewLine +
                    "WAREHOUSE: " + result.WarehouseRows + Environment.NewLine +
                    "SUPPLIER: " + result.SupplierRows;
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
                RefreshHistory();
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
       b.SelectedEntities,
       b.Status,
       b.StartedAtUtc,
       b.CompletedAtUtc,
       b.InitiatedBy,
       b.PartCatalogRows,
       b.InventoryPartRows,
       b.BomHeaderRows,
       b.BomAlternativeRows,
       b.BomComponentRows,
       b.WarehouseRows,
       b.SupplierRows,
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
                    RefreshEntityStatus();
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
                _status.Text = "Không đọc được lịch sử. Kiểm tra kết nối và migration 027.";
                _details.Text = ex.ToString();
            }
        }

        private void RefreshEntityStatus()
        {
            try
            {
                const string sql = @"
;WITH Latest AS
(
    SELECT TOP (1) SyncBatchId, SyncMode, SelectedEntities, Status, StartedAtUtc, CompletedAtUtc, ErrorSummary,
           PartCatalogRows, InventoryPartRows, BomHeaderRows, BomAlternativeRows,
           BomComponentRows, WarehouseRows, SupplierRows
    FROM IFS.SyncBatch
    ORDER BY SyncBatchId DESC
)
SELECT v.EntityName, v.SourceObject, v.TargetObject, v.SourceRows, v.TargetRows,
       CASE
         WHEN l.SyncBatchId IS NULL THEN N'CHƯA ĐỒNG BỘ'
         WHEN entityError.SyncErrorId IS NOT NULL THEN N'LỖI ENTITY'
         WHEN l.SyncMode = 'SELECTIVE' AND CHARINDEX(v.EntityName, COALESCE(l.SelectedEntities,N'')) = 0 THEN N'KHÔNG CHỌN TRONG BATCH GẦN NHẤT'
         WHEN l.Status = 'FAILED' THEN N'LỖI BATCH'
         WHEN l.Status <> 'APPLIED' THEN N'ĐANG XỬ LÝ / CHƯA APPLY'
         WHEN v.TargetRows = 0 THEN N'ĐÃ ĐỌC - ĐÍCH CHƯA CÓ DỮ LIỆU'
         ELSE N'ĐÃ ĐỒNG BỘ'
       END AS SyncStatus,
       l.SyncBatchId AS LatestBatchId, l.Status AS BatchStatus,
       COALESCE(l.CompletedAtUtc, l.StartedAtUtc) AS LatestAtUtc,
       COALESCE(entityError.ErrorMessage, l.ErrorSummary) AS ErrorSummary
FROM Latest l
CROSS APPLY
(
    SELECT N'PART_CATALOG' EntityName, N'IFSAPP.PART_CATALOG' SourceObject,
           N'IFS.FVN_PartCatalog' TargetObject, l.PartCatalogRows SourceRows,
           (SELECT COUNT_BIG(*) FROM IFS.FVN_PartCatalog) TargetRows
    UNION ALL
    SELECT N'INVENTORY_PART', N'IFSAPP.INVENTORY_PART', N'IFS.FVN_InventoryPart',
           l.InventoryPartRows, (SELECT COUNT_BIG(*) FROM IFS.FVN_InventoryPart)
    UNION ALL
    SELECT N'BOM_HEADER', N'IFSAPP.MANUF_STRUCTURE_HEAD', N'IFS.FVN_BomHeader',
           l.BomHeaderRows, (SELECT COUNT_BIG(*) FROM IFS.FVN_BomHeader)
    UNION ALL
    SELECT N'BOM_ALTERNATIVE', N'IFSAPP.MANUF_STRUCT_ALTERNATE', N'IFS.FVN_BomAlternative',
           l.BomAlternativeRows, (SELECT COUNT_BIG(*) FROM IFS.FVN_BomAlternative)
    UNION ALL
    SELECT N'BOM_COMPONENT', N'IFSAPP.MANUF_STRUCTURE', N'IFS.FVN_BomComponent',
           l.BomComponentRows, (SELECT COUNT_BIG(*) FROM IFS.FVN_BomComponent)
    UNION ALL
    SELECT N'WAREHOUSE', N'IFSAPP.WAREHOUSE', N'MES.FVN_IfsWarehouse',
           l.WarehouseRows, (SELECT COUNT_BIG(*) FROM MES.FVN_IfsWarehouse)
    UNION ALL
    SELECT N'SUPPLIER', N'IFSAPP.SUPPLIER_INFO_GENERAL', N'IFS.FVN_Supplier',
           l.SupplierRows, (SELECT COUNT_BIG(*) FROM IFS.FVN_Supplier)
) v
OUTER APPLY
(
    SELECT TOP (1) e.SyncErrorId, e.ErrorMessage
    FROM IFS.SyncError AS e
    WHERE e.SyncBatchId = l.SyncBatchId
      AND e.EntityName = v.EntityName
    ORDER BY e.SyncErrorId DESC
) AS entityError
UNION ALL
SELECT v.EntityName, v.SourceObject, v.TargetObject, 0 AS SourceRows, v.TargetRows,
       N'CHƯA CÓ BATCH ĐỒNG BỘ' AS SyncStatus, NULL, NULL, NULL, NULL
FROM
(
    SELECT N'PART_CATALOG' EntityName, N'IFSAPP.PART_CATALOG' SourceObject, N'IFS.FVN_PartCatalog' TargetObject,
           (SELECT COUNT_BIG(*) FROM IFS.FVN_PartCatalog) TargetRows
    UNION ALL SELECT N'INVENTORY_PART', N'IFSAPP.INVENTORY_PART', N'IFS.FVN_InventoryPart',
           (SELECT COUNT_BIG(*) FROM IFS.FVN_InventoryPart)
    UNION ALL SELECT N'BOM_HEADER', N'IFSAPP.MANUF_STRUCTURE_HEAD', N'IFS.FVN_BomHeader',
           (SELECT COUNT_BIG(*) FROM IFS.FVN_BomHeader)
    UNION ALL SELECT N'BOM_ALTERNATIVE', N'IFSAPP.MANUF_STRUCT_ALTERNATE', N'IFS.FVN_BomAlternative',
           (SELECT COUNT_BIG(*) FROM IFS.FVN_BomAlternative)
    UNION ALL SELECT N'BOM_COMPONENT', N'IFSAPP.MANUF_STRUCTURE', N'IFS.FVN_BomComponent',
           (SELECT COUNT_BIG(*) FROM IFS.FVN_BomComponent)
    UNION ALL SELECT N'WAREHOUSE', N'IFSAPP.WAREHOUSE', N'MES.FVN_IfsWarehouse',
           (SELECT COUNT_BIG(*) FROM MES.FVN_IfsWarehouse)
    UNION ALL SELECT N'SUPPLIER', N'IFSAPP.SUPPLIER_INFO_GENERAL', N'IFS.FVN_Supplier',
           (SELECT COUNT_BIG(*) FROM IFS.FVN_Supplier)
) v
WHERE NOT EXISTS (SELECT 1 FROM IFS.SyncBatch);";
                using (var connection = new SqlConnection(TargetConnectionString))
                using (var adapter = new SqlDataAdapter(sql, connection))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    _entities.DataSource = table;
                    if (_entities.Columns.Contains("LatestAtUtc"))
                        _entities.Columns["LatestAtUtc"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss";
                }
            }
            catch (Exception ex)
            {
                _entities.DataSource = null;
                _details.Text = "Không tải được trạng thái entity. Kiểm tra migration 027–028 và quyền SELECT. " + ex.Message;
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
            _selectiveSyncButton.Enabled = !busy;
            foreach (CheckBox check in _entityChecks)
                if (check != null) check.Enabled = !busy;
            _checkButton.Enabled = !busy;
            _refreshButton.Enabled = !busy;
            UseWaitCursor = busy;
            if (!string.IsNullOrWhiteSpace(message))
                _status.Text = message;
        }
    }
}
