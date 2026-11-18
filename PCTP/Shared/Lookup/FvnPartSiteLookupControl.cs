using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Shared.Lookup
{
    /// <summary>
    /// Reusable lookup for an FVN site and its inventory parts.
    /// Part choices are always scoped to the currently selected SiteCode.
    /// </summary>
    public sealed class FvnPartSiteLookupControl : UserControl
    {
        private readonly GridLookUpEdit _siteEdit = new GridLookUpEdit();
        private readonly GridLookUpEdit _partEdit = new GridLookUpEdit();
        private bool _binding;

        public event EventHandler SiteChanged;
        public event EventHandler PartChanged;

        public FvnPartSiteLookupControl()
        {
            MinimumSize = new Size(420, 32);
            Height = 32;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));

            var siteLabel = new Label { Text = "Site:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
            var partLabel = new Label { Text = "Part No:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
            ConfigureLookup(_siteEdit, "Chọn Site...");
            ConfigureLookup(_partEdit, "Chọn Site trước, sau đó tìm Part...");
            _partEdit.Enabled = false;

            layout.Controls.Add(siteLabel, 0, 0);
            layout.Controls.Add(_siteEdit, 1, 0);
            layout.Controls.Add(partLabel, 2, 0);
            layout.Controls.Add(_partEdit, 3, 0);
            Controls.Add(layout);

            _siteEdit.EditValueChanged += (s, e) =>
            {
                if (_binding) return;
                _partEdit.EditValue = null;
                _partEdit.Properties.DataSource = null;
                string site = SelectedSiteCode;
                _partEdit.Enabled = !string.IsNullOrWhiteSpace(site);
                if (!string.IsNullOrWhiteSpace(site) && !string.IsNullOrWhiteSpace(_connectionString))
                    LoadParts(_connectionString, site);
                SiteChanged?.Invoke(this, EventArgs.Empty);
            };
            _partEdit.EditValueChanged += (s, e) =>
            {
                if (!_binding) PartChanged?.Invoke(this, EventArgs.Empty);
            };
        }

        private string _connectionString;

        public string SelectedSiteCode
        {
            get { return (Convert.ToString(_siteEdit.EditValue) ?? string.Empty).Trim(); }
        }

        public string SelectedPartNo
        {
            get { return (Convert.ToString(_partEdit.EditValue) ?? string.Empty).Trim(); }
        }

        public GridLookUpEdit SiteEditor { get { return _siteEdit; } }
        public GridLookUpEdit PartEditor { get { return _partEdit; } }

        public void LoadSites(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");

            _connectionString = connectionString;
            const string sql = @"
SELECT SiteCode, SiteName
FROM dbo.FVN_PCTPSite
WHERE IsActive = 1
ORDER BY SiteCode;";
            var table = new DataTable();
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            using (var adapter = new SqlDataAdapter(command))
                adapter.Fill(table);

            _binding = true;
            try
            {
                BindLookup(_siteEdit, table, "SiteCode", "SiteName", "Site");
                _siteEdit.EditValue = null;
                _partEdit.EditValue = null;
                _partEdit.Properties.DataSource = null;
                _partEdit.Enabled = false;
            }
            finally { _binding = false; }
        }

        private void LoadParts(string connectionString, string siteCode)
        {
            const string sql = @"
SELECT SiteCode, PartNo, Description, UnitMeas, PartStatus
FROM dbo.FVN_InventoryPart
WHERE SiteCode = @SiteCode
ORDER BY PartNo;";
            var table = new DataTable();
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            using (var adapter = new SqlDataAdapter(command))
            {
                command.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 20).Value = siteCode;
                adapter.Fill(table);
            }

            _binding = true;
            try
            {
                BindLookup(_partEdit, table, "PartNo", "Description", "Part");
                _partEdit.EditValue = null;
            }
            finally { _binding = false; }
        }

        private static void ConfigureLookup(GridLookUpEdit editor, string prompt)
        {
            editor.Dock = DockStyle.Fill;
            editor.Properties.NullText = prompt;
            editor.Properties.TextEditStyle = TextEditStyles.Standard;
            editor.Properties.PopupFilterMode = PopupFilterMode.Contains;
            editor.Properties.ImmediatePopup = true;
            editor.Properties.SearchMode = DevExpress.XtraEditors.Repository.GridLookUpSearchMode.AutoSearch;
            editor.Properties.AllowNullInput = DevExpress.Utils.DefaultBoolean.True;
            editor.Properties.View.OptionsView.ShowGroupPanel = false;
            editor.Properties.View.OptionsView.ShowAutoFilterRow = true;
            editor.Properties.View.OptionsBehavior.Editable = false;
        }

        private static void BindLookup(GridLookUpEdit editor, DataTable source,
            string valueField, string descriptionField, string kind)
        {
            editor.Properties.DataSource = source;
            editor.Properties.ValueMember = valueField;
            editor.Properties.DisplayMember = valueField;
            editor.Properties.View.Columns.Clear();
            editor.Properties.View.Columns.AddVisible(valueField, kind == "Site" ? "Site" : "Part No");

            if (source.Columns.Contains(descriptionField))
                editor.Properties.View.Columns.AddVisible(descriptionField,
                    kind == "Site" ? "Tên Site" : "Mô tả");

            if (kind == "Part")
            {
                if (source.Columns.Contains("UnitMeas"))
                    editor.Properties.View.Columns.AddVisible("UnitMeas", "ĐVT");
                if (source.Columns.Contains("PartStatus"))
                    editor.Properties.View.Columns.AddVisible("PartStatus", "Trạng thái");
            }
        }
    }
}
