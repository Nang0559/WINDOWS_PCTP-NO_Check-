using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using PCTP.Shared.Lookup;

namespace PCTP.Modules.MesBom
{
    /// <summary>
    /// Read-only IFS-style graphical BOM viewer backed by the synchronized IFS snapshot in FVN_PCTP.
    /// </summary>
    public sealed class FormProductStructureGraphic : XtraForm
    {
        private readonly FvnPartSiteLookupControl _partSiteLookup = new FvnPartSiteLookupControl();
        private readonly DateTimePicker _asOf = new DateTimePicker();
        private readonly Button _load = new Button();
        private readonly Button _exportSite = new Button();
        private readonly Button _exportCurrent = new Button();
        private readonly Label _summary = new Label();
        private readonly TreeView _tree = new TreeView();
        private readonly DataGridView _components = new DataGridView();
        private readonly TabControl _tabs = new TabControl();
        private readonly Label _revisionInfo = new Label();
        private readonly string _connectionString;
        private readonly HashSet<string> _path = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private const int MaxDepth = 20;

        private sealed class BomNode
        {
            public string SiteCode;
            public string PartNo;
            public string EngChgLevel;
            public string BomTypeDb;
            public string AlternativeNo;
            public string Description;
            public DateTime EffectiveInDate;
            public DateTime? EffectiveOutDate;
            public int Depth;
        }

        public FormProductStructureGraphic()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            Text = "Product Structure Graphic - MES / FVN_PCTP";
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1100, 720);
            BuildLayout();
            Load += (s, e) =>
            {
                try { _partSiteLookup.LoadSites(_connectionString); }
                catch (Exception ex)
                {
                    MessageBox.Show("Không thể nạp danh sách Site/Part từ FVN_PCTP: " + ex.Message,
                        "BOM Lookup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            Controls.Add(root);

            var header = new GroupBox { Text = "Product / Structure parameters", Dock = DockStyle.Fill, Padding = new Padding(10) };
            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 2 };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            _partSiteLookup.Dock = DockStyle.Fill;
            fields.Controls.Add(_partSiteLookup, 0, 0);
            fields.SetColumnSpan(_partSiteLookup, 4);
            AddField(fields, "As of:", _asOf, 4, 0);
            _asOf.Format = DateTimePickerFormat.Short;
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 2, 0, 0)
            };
            _load.Text = "Load";
            _load.Width = 70;
            _load.Height = 30;
            _load.Click += (s, e) => LoadStructure();
            _exportSite.Text = "Export BOM / Site";
            _exportSite.Width = 112;
            _exportSite.Height = 30;
            _exportSite.Click += (s, e) => ExportBom(false);
            _exportCurrent.Text = "Export BOM đang xem";
            _exportCurrent.Width = 125;
            _exportCurrent.Height = 30;
            _exportCurrent.Click += (s, e) => ExportBom(true);
            actions.Controls.Add(_load);
            actions.Controls.Add(_exportSite);
            actions.Controls.Add(_exportCurrent);
            fields.Controls.Add(actions, 7, 0);
            _summary.Dock = DockStyle.Fill;
            _summary.TextAlign = ContentAlignment.MiddleLeft;
            _summary.ForeColor = Color.DarkSlateGray;
            fields.Controls.Add(_summary, 1, 1);
            fields.SetColumnSpan(_summary, 7);
            header.Controls.Add(fields);
            root.Controls.Add(header, 0, 0);

            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 390, FixedPanel = FixedPanel.Panel1 };
            var treeGroup = new GroupBox { Text = "Graphical Structure", Dock = DockStyle.Fill, Padding = new Padding(6) };
            _tree.Dock = DockStyle.Fill;
            _tree.HideSelection = false;
            _tree.AfterSelect += (s, e) => LoadComponents(e.Node?.Tag as BomNode);
            treeGroup.Controls.Add(_tree);
            split.Panel1.Controls.Add(treeGroup);

            var right = new Panel { Dock = DockStyle.Fill };
            _tabs.Dock = DockStyle.Fill;
            var graphical = new TabPage("Graphical Structure");
            ConfigureGrid();
            graphical.Controls.Add(_components);
            _tabs.TabPages.Add(graphical);

            var revisions = new TabPage("Revisions");
            _revisionInfo.Dock = DockStyle.Fill;
            _revisionInfo.Padding = new Padding(12);
            _revisionInfo.Text = "Revision information is shown for the selected structure header.";
            revisions.Controls.Add(_revisionInfo);
            _tabs.TabPages.Add(revisions);

            AddReadOnlyTab("Engineering Change Orders", "Engineering Change Order data is not included in the current synchronized BOM snapshot.");
            AddReadOnlyTab("By-Product Cost Distribution", "By-product cost distribution is not included in the current synchronized BOM snapshot.");
            AddReadOnlyTab("Attributes", "Source attributes are retained in IFS payload snapshots; a governed attribute mapping is required before displaying them here.");

            right.Controls.Add(_tabs);
            split.Panel2.Controls.Add(right);
            root.Controls.Add(split, 0, 1);

            var footer = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray,
                Text = "Nguồn: FVN_PCTP / IFS snapshot  •  Read-only  •  Không ghi ngược về Oracle IFS" };
            root.Controls.Add(footer, 0, 2);
        }

        private static void AddField(TableLayoutPanel panel, string caption, Control input, int col, int row)
        {
            var label = new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
            input.Dock = DockStyle.Fill;
            panel.Controls.Add(label, col, row);
            panel.Controls.Add(input, col + 1, row);
        }

        private void AddReadOnlyTab(string title, string message)
        {
            var page = new TabPage(title);
            page.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.DimGray, Text = message });
            _tabs.TabPages.Add(page);
        }

        private void ConfigureGrid()
        {
            _components.Dock = DockStyle.Fill;
            _components.ReadOnly = true;
            _components.AllowUserToAddRows = false;
            _components.AllowUserToDeleteRows = false;
            _components.AllowUserToResizeRows = false;
            _components.MultiSelect = false;
            _components.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _components.AutoGenerateColumns = false;
            _components.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            _components.RowHeadersVisible = false;
            _components.BackgroundColor = SystemColors.Window;
            AddColumn("LineItemNo", "Line Item", 85);
            AddColumn("ComponentPartNo", "Component Part", 160);
            AddColumn("Description", "Component Part Description", 240);
            AddColumn("UnitMeas", "UoM", 65);
            AddColumn("QtyPerAssembly", "Qty per Assembly", 110);
            AddColumn("OperationNo", "Operation No", 90);
            AddColumn("ComponentScrap", "Scrap Factor", 90);
            AddColumn("ConsumptionItem", "Consumption Item", 120);
            AddColumn("PhantomConsume", "Phantom Consume", 120);
            AddColumn("EffectiveInDate", "Phase In", 100);
            AddColumn("EffectiveOutDate", "Phase Out", 100);
        }

        private void AddColumn(string name, string title, int width)
        {
            _components.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = title, DataPropertyName = name, Width = width, SortMode = DataGridViewColumnSortMode.Automatic });
        }

        private void LoadStructure()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                MessageBox.Show("Thiếu connection string FVN_PCTP.", "BOM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string part = _partSiteLookup.SelectedPartNo;
            string site = _partSiteLookup.SelectedSiteCode;
            if (part.Length == 0 || site.Length == 0)
            {
                MessageBox.Show("Vui lòng nhập Part No và Site. Site là bắt buộc, không tự mặc định.", "BOM", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _tree.BeginUpdate();
                _tree.Nodes.Clear();
                _components.DataSource = null;
                _path.Clear();
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var header = FindHeader(connection, site, part);
                    if (header == null)
                    {
                        _summary.Text = "Không tìm thấy BOM hiệu lực cho Part/Site đã chọn.";
                        _tree.Nodes.Add("Không có cấu trúc BOM");
                        return;
                    }
                    var root = new TreeNode(part + " - " + header.Description + " [" + header.BomTypeDb + " / Rev " + header.EngChgLevel + "]");
                    root.Tag = header;
                    _tree.Nodes.Add(root);
                    AddChildren(connection, root, header, 0);
                    root.Expand();
                    _tree.SelectedNode = root;
                    _summary.Text = "Site: " + site + "   |   Revision: " + header.EngChgLevel +
                        "   |   Type: " + header.BomTypeDb + "   |   Alternative: " + header.AlternativeNo;
                    _revisionInfo.Text = "Part No: " + part + Environment.NewLine +
                        "Site: " + site + Environment.NewLine +
                        "Engineering Revision: " + header.EngChgLevel + Environment.NewLine +
                        "Structure Type: " + header.BomTypeDb + Environment.NewLine +
                        "Alternative: " + header.AlternativeNo + Environment.NewLine +
                        "Effective In: " + header.EffectiveInDate.ToString("d") + Environment.NewLine +
                        "Effective Out: " + (header.EffectiveOutDate.HasValue ? header.EffectiveOutDate.Value.ToString("d") : "Open");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể tải Product Structure: " + ex.Message, "BOM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { _tree.EndUpdate(); }
        }

        private BomNode FindHeader(SqlConnection connection, string site, string part)
        {
            const string sql = @"
SELECT TOP (1) h.SiteCode,h.PartNo,h.EngChgLevel,h.BomTypeDb,
       h.EffectiveInDate,h.EffectiveOutDate,
       ISNULL(pc.Description,ip.Description) AS Description,
       ISNULL(a.AlternativeNo,'*') AS AlternativeNo
FROM IFS.BomHeader h
LEFT JOIN IFS.PartCatalog pc ON pc.PartNo=h.PartNo
LEFT JOIN IFS.InventoryPart ip ON ip.SiteCode=h.SiteCode AND ip.PartNo=h.PartNo
LEFT JOIN IFS.BomAlternative a ON a.SiteCode=h.SiteCode AND a.PartNo=h.PartNo
 AND a.EngChgLevel=h.EngChgLevel AND a.BomTypeDb=h.BomTypeDb
WHERE h.SiteCode=@SiteCode AND h.PartNo=@PartNo
 AND h.EffectiveInDate<=@AsOf AND (h.EffectiveOutDate IS NULL OR h.EffectiveOutDate>=@AsOf)
ORDER BY CASE WHEN a.AlternativeNo='*' THEN 0 ELSE 1 END,h.EffectiveInDate DESC,h.EngChgLevel DESC,a.AlternativeNo;";
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 20).Value = site;
                cmd.Parameters.Add("@PartNo", SqlDbType.NVarChar, 100).Value = part;
                cmd.Parameters.Add("@AsOf", SqlDbType.DateTime2).Value = _asOf.Value.Date;
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new BomNode
                    {
                        SiteCode = Convert.ToString(reader["SiteCode"]),
                        PartNo = Convert.ToString(reader["PartNo"]),
                        EngChgLevel = Convert.ToString(reader["EngChgLevel"]),
                        BomTypeDb = Convert.ToString(reader["BomTypeDb"]),
                        AlternativeNo = Convert.ToString(reader["AlternativeNo"]),
                        Description = Convert.ToString(reader["Description"]),
                        EffectiveInDate = Convert.ToDateTime(reader["EffectiveInDate"], CultureInfo.InvariantCulture),
                        EffectiveOutDate = reader.IsDBNull(reader.GetOrdinal("EffectiveOutDate"))
                            ? (DateTime?)null
                            : Convert.ToDateTime(reader["EffectiveOutDate"], CultureInfo.InvariantCulture),
                        Depth = 0
                    };
                }
            }
        }

        private void AddChildren(SqlConnection connection, TreeNode parent, BomNode current, int depth)
        {
            if (depth >= MaxDepth) return;
            string pathKey = current.SiteCode + "|" + current.PartNo + "|" + current.EngChgLevel + "|" + current.BomTypeDb + "|" + current.AlternativeNo;
            if (!_path.Add(pathKey)) return;
            foreach (DataRow row in ReadComponents(connection, current).Rows)
            {
                string childPart = Convert.ToString(row["ComponentPartNo"]);
                var childNode = new TreeNode(childPart + " - " + Convert.ToString(row["Description"]));
                var childBom = FindHeader(connection, current.SiteCode, childPart);
                if (childBom != null)
                {
                    childNode.Tag = childBom;
                    parent.Nodes.Add(childNode);
                    AddChildren(connection, childNode, childBom, depth + 1);
                }
                else
                {
                    childNode.Tag = new BomNode { SiteCode = current.SiteCode, PartNo = childPart, Description = Convert.ToString(row["Description"]), Depth = depth + 1 };
                    parent.Nodes.Add(childNode);
                }
            }
            _path.Remove(pathKey);
        }

        private DataTable ReadComponents(SqlConnection connection, BomNode node)
        {
            const string sql = @"
SELECT c.LineItemNo,c.ComponentPartNo,ISNULL(pc.Description,ip.Description) AS Description,
       ISNULL(ip.UnitMeas,pc.UnitCode) AS UnitMeas,c.QtyPerAssembly,c.OperationNo,
       c.ComponentScrap,c.ConsumptionItem,c.PhantomConsume,c.EffectiveInDate,c.EffectiveOutDate
FROM IFS.BomComponent c
LEFT JOIN IFS.PartCatalog pc ON pc.PartNo=c.ComponentPartNo
LEFT JOIN IFS.InventoryPart ip ON ip.SiteCode=c.SiteCode AND ip.PartNo=c.ComponentPartNo
WHERE c.SiteCode=@SiteCode AND c.PartNo=@PartNo AND c.EngChgLevel=@Revision
 AND c.BomTypeDb=@BomType AND c.AlternativeNo=@Alternative
 AND c.EffectiveInDate<=@AsOf AND (c.EffectiveOutDate IS NULL OR c.EffectiveOutDate>=@AsOf)
ORDER BY c.LineSequence,c.LineItemNo;";
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 20).Value = node.SiteCode;
                cmd.Parameters.Add("@PartNo", SqlDbType.NVarChar, 100).Value = node.PartNo;
                cmd.Parameters.Add("@Revision", SqlDbType.NVarChar, 24).Value = node.EngChgLevel;
                cmd.Parameters.Add("@BomType", SqlDbType.NVarChar, 80).Value = node.BomTypeDb;
                cmd.Parameters.Add("@Alternative", SqlDbType.NVarChar, 80).Value = node.AlternativeNo;
                cmd.Parameters.Add("@AsOf", SqlDbType.DateTime2).Value = _asOf.Value.Date;
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }

        /// <summary>
        /// Exports the synchronized BOM snapshot as UTF-8 CSV (Excel compatible).
        /// Site export includes every effective BOM component for the selected site;
        /// current export is limited to the BOM header selected in the structure tree.
        /// </summary>
        private void ExportBom(bool currentOnly)
        {
            string site = _partSiteLookup.SelectedSiteCode;
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                MessageBox.Show("Thiếu connection string FVN_PCTP.", "Export BOM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (site.Length == 0)
            {
                MessageBox.Show("Vui lòng nhập Site trước khi xuất. Không tự mặc định Site.", "Export BOM", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            BomNode selected = currentOnly ? _tree.SelectedNode?.Tag as BomNode : null;
            if (currentOnly && (selected == null || string.IsNullOrWhiteSpace(selected.EngChgLevel)))
            {
                MessageBox.Show("Hãy chọn một BOM header trên cây cấu trúc trước khi xuất BOM đang xem.", "Export BOM", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (selected != null && !string.Equals(selected.SiteCode, site, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Site trên BOM đang chọn không khớp Site nhập ở bộ lọc.", "Export BOM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel CSV (*.csv)|*.csv";
                dialog.DefaultExt = "csv";
                dialog.AddExtension = true;
                dialog.FileName = currentOnly
                    ? "BOM_" + SafeFileName(selected.PartNo) + "_" + SafeFileName(site) + ".csv"
                    : "BOM_ALL_" + SafeFileName(site) + "_" + _asOf.Value.ToString("yyyyMMdd") + ".csv";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    DataTable data;
                    using (var connection = new SqlConnection(_connectionString))
                    using (var command = new SqlCommand(@"
SELECT h.SiteCode, h.PartNo, ISNULL(parent.Description, iparent.Description) AS ParentDescription,
       h.EngChgLevel, h.BomTypeDb, ISNULL(a.AlternativeNo,'*') AS AlternativeNo,
       h.EffectiveInDate AS HeaderEffectiveIn, h.EffectiveOutDate AS HeaderEffectiveOut,
       c.LineItemNo, c.LineSequence, c.ComponentPartNo,
       ISNULL(child.Description, ichild.Description) AS ComponentDescription,
       ISNULL(ichild.UnitMeas,child.UnitCode) AS UnitMeas,
       c.QtyPerAssembly, c.OperationNo, c.ComponentScrap, c.ConsumptionItem,
       c.PhantomConsume, c.EffectiveInDate AS ComponentEffectiveIn,
       c.EffectiveOutDate AS ComponentEffectiveOut
FROM IFS.BomHeader h
LEFT JOIN IFS.BomAlternative a
  ON a.SiteCode=h.SiteCode AND a.PartNo=h.PartNo
 AND a.EngChgLevel=h.EngChgLevel AND a.BomTypeDb=h.BomTypeDb
LEFT JOIN IFS.BomComponent c
  ON c.SiteCode=h.SiteCode AND c.PartNo=h.PartNo
 AND c.EngChgLevel=h.EngChgLevel AND c.BomTypeDb=h.BomTypeDb
 AND c.AlternativeNo=ISNULL(a.AlternativeNo,'*')
 AND c.EffectiveInDate<=@AsOf
 AND (c.EffectiveOutDate IS NULL OR c.EffectiveOutDate>=@AsOf)
LEFT JOIN IFS.PartCatalog parent ON parent.PartNo=h.PartNo
LEFT JOIN IFS.InventoryPart iparent ON iparent.SiteCode=h.SiteCode AND iparent.PartNo=h.PartNo
LEFT JOIN IFS.PartCatalog child ON child.PartNo=c.ComponentPartNo
LEFT JOIN IFS.InventoryPart ichild ON ichild.SiteCode=h.SiteCode AND ichild.PartNo=c.ComponentPartNo
WHERE h.SiteCode=@SiteCode
  AND h.EffectiveInDate<=@AsOf
  AND (h.EffectiveOutDate IS NULL OR h.EffectiveOutDate>=@AsOf)
  AND (@PartNo IS NULL OR (h.PartNo=@PartNo AND h.EngChgLevel=@Revision
       AND h.BomTypeDb=@BomType AND ISNULL(a.AlternativeNo,'*')=@Alternative))
ORDER BY h.PartNo,h.EngChgLevel,h.BomTypeDb,ISNULL(a.AlternativeNo,'*'),c.LineSequence,c.LineItemNo;", connection))
                    {
                        command.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 20).Value = site;
                        command.Parameters.Add("@AsOf", SqlDbType.DateTime2).Value = _asOf.Value.Date;
                        command.Parameters.Add("@PartNo", SqlDbType.NVarChar, 100).Value =
                            currentOnly ? (object)selected.PartNo : DBNull.Value;
                        command.Parameters.Add("@Revision", SqlDbType.NVarChar, 24).Value =
                            currentOnly ? (object)selected.EngChgLevel : DBNull.Value;
                        command.Parameters.Add("@BomType", SqlDbType.NVarChar, 80).Value =
                            currentOnly ? (object)selected.BomTypeDb : DBNull.Value;
                        command.Parameters.Add("@Alternative", SqlDbType.NVarChar, 80).Value =
                            currentOnly ? (object)selected.AlternativeNo : DBNull.Value;
                        using (var adapter = new SqlDataAdapter(command))
                        {
                            data = new DataTable();
                            adapter.Fill(data);
                        }
                    }

                    if (data.Rows.Count == 0)
                    {
                        MessageBox.Show("Không có dữ liệu BOM hiệu lực để xuất theo điều kiện đã chọn.", "Export BOM",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    using (var writer = new StreamWriter(dialog.FileName, false, new UTF8Encoding(true)))
                    {
                        for (int col = 0; col < data.Columns.Count; col++)
                        {
                            if (col > 0) writer.Write(",");
                            writer.Write(Csv(data.Columns[col].ColumnName));
                        }
                        writer.WriteLine();
                        foreach (DataRow row in data.Rows)
                        {
                            for (int col = 0; col < data.Columns.Count; col++)
                            {
                                if (col > 0) writer.Write(",");
                                writer.Write(Csv(Convert.ToString(row[col], CultureInfo.InvariantCulture)));
                            }
                            writer.WriteLine();
                        }
                    }

                    MessageBox.Show("Đã xuất " + data.Rows.Count + " dòng BOM.\r\n" + dialog.FileName +
                        "\r\n\r\nFile CSV UTF-8 có thể mở trực tiếp bằng Microsoft Excel.",
                        "Export BOM", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Xuất BOM thất bại: " + ex.Message, "Export BOM",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string Csv(string value)
        {
            if (value == null) return "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string SafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "NA";
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value;
        }

        private void LoadComponents(BomNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.EngChgLevel))
            {
                _components.DataSource = null;
                return;
            }
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    _components.DataSource = ReadComponents(connection, node);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể đọc component: " + ex.Message, "BOM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
