using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Security;

namespace PCTP.Modules.IfsSync
{
    /// <summary>
    /// Đối chiếu mã chuyền 5 ký tự (chuẩn tham chiếu của nhà máy) với mã 3 ký tự dùng thật trong LOT, và sinh cặp mã mới.
    /// Quyền MES.SITE_DEPARTMENT_LINE (VIEW xem, EDIT sinh mã, theo Site).
    /// </summary>
    public sealed class FormLineCodeCrossRef : XtraForm
    {
        private readonly string _site;
        private readonly ISiteDepartmentLineRepository _repository;
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view;
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 26 };
        private SimpleButton _generate;
        private SimpleButton _add;
        private SimpleButton _edit;
        private SimpleButton _delete;

        private static bool CanEdit
        {
            get { return UserSession.Current != null && UserSession.Current.HasPermission(SiteDepartmentLineRepository.FunctionCode, "EDIT"); }
        }

        public FormLineCodeCrossRef(string siteCode) : this(siteCode, new SiteDepartmentLineRepository()) { }

        public FormLineCodeCrossRef(string siteCode, ISiteDepartmentLineRepository repository)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            _site = siteCode.Trim().ToUpperInvariant();
            _repository = repository ?? throw new ArgumentNullException("repository");
            _view = new GridView(_grid);
            Text = "Mã chuyền 5 ký tự ↔ 3 ký tự (LOT) - Site " + _site;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1150, 680);
            BuildUi();
            Load += (s, e) => Reload();
        }

        private void BuildUi()
        {
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsView.ColumnAutoWidth = false;

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6, 6, 6, 0) };
            var reload = new SimpleButton { Text = "TẢI LẠI", Width = 90, Height = 30 };
            reload.Click += (s, e) => Reload();
            _generate = new SimpleButton { Text = "Sinh mã…", Width = 110, Height = 30, Enabled = CanEdit };
            if (!CanEdit) _generate.ToolTip = "Bạn chỉ có quyền xem; cần " + SiteDepartmentLineRepository.FunctionCode + " / EDIT.";
            _generate.Click += (s, e) => GenerateNew();
            _add = new SimpleButton { Text = "Thêm tay…", Width = 100, Height = 30, Enabled = CanEdit };
            _edit = new SimpleButton { Text = "Sửa…", Width = 80, Height = 30, Enabled = CanEdit };
            _delete = new SimpleButton { Text = "Xóa", Width = 80, Height = 30, Enabled = CanEdit };
            _add.ToolTip = "Thêm dòng khi IFS có chuyền mới mà chưa có mã 5 / 3 ký tự.";
            _add.Click += (s, e) => EditRow(null);
            _edit.Click += (s, e) => EditRow(_view.GetFocusedDataRow());
            _delete.Click += (s, e) => DeleteRow(_view.GetFocusedDataRow());
            bar.Controls.Add(reload);
            bar.Controls.Add(_generate);
            bar.Controls.Add(_add);
            bar.Controls.Add(_edit);
            bar.Controls.Add(_delete);

            Controls.Add(_grid);
            Controls.Add(bar);
            Controls.Add(_status);
        }

        private void SetStatus(string text, bool error)
        {
            _status.Text = text;
            _status.Appearance.ForeColor = error ? Color.Firebrick : Color.FromArgb(70, 85, 100);
        }

        private DataTable _table;

        private void Reload()
        {
            try
            {
                _table = _repository.GetLineCodeCrossRef(_site);
                _grid.DataSource = _table;
                Caption("LineCode5", "Mã 5 ký tự"); Caption("LineCode3", "Mã 3 ký tự (LOT)"); Caption("FamilyCode", "Họ máy");
                Caption("SeqNo", "Số TT"); Caption("Description", "Mô tả"); Caption("IfsProductionLineCode", "Chuyền IFS");
                Caption("IfsProductionLineDescription", "Mô tả chuyền IFS"); Caption("LotLineName", "Tên chuyền LOT");
                Caption("LotLineStatus", "Chuyền LOT trong PCTP"); Caption("IsLegacySeed", "Mã có sẵn");
                Caption("CreatedAtUtc", "Tạo lúc (UTC)"); Caption("UpdatedAtUtc", "Sửa lúc (UTC)"); Caption("UpdatedBy", "Người sửa"); Caption("CreatedBy", "Người tạo");
                if (_view.Columns["SiteCode"] != null) _view.Columns["SiteCode"].Visible = false;
                _view.BestFitColumns();
                SetStatus(_table.Rows.Count + " cặp mã. Chuyền LOT trong PCTP = NOT_DEFINED khi mã 3 ký tự chưa có trong dbo.FVN_ProductionLine.", false);
            }
            catch (Exception ex) { SetStatus("Không tải được: " + ex.GetBaseException().Message, true); }
        }

        private void Caption(string field, string caption)
        {
            var col = _view.Columns[field];
            if (col != null) col.Caption = caption;
        }

        private List<string> Families()
        {
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_table != null)
                foreach (DataRow r in _table.Rows)
                    if (!r.IsNull("FamilyCode")) set.Add(Convert.ToString(r["FamilyCode"]).Trim());
            return new List<string>(set);
        }

        private void GenerateNew()
        {
            if (!CanEdit) return;
            try
            {
                DataTable ifsLines = _repository.GetIfsProductionLines(_site);
                using (var dlg = new GenerateDialog(_site, _repository, ifsLines, Families()))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                }
                Reload();
                SetStatus("Đã sinh và lưu cặp mã 5 / 3 ký tự.", false);
            }
            catch (Exception ex)
            {
                string message = ex.GetBaseException().Message;
                SetStatus("Sinh mã thất bại: " + message, true);
                XtraMessageBox.Show(this, message, "Sinh mã chuyền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string Cell(DataRow r, string col)
        {
            return r == null || !r.Table.Columns.Contains(col) || r.IsNull(col) ? string.Empty : Convert.ToString(r[col]).Trim();
        }

        private void EditRow(DataRow row)
        {
            if (!CanEdit) return;
            try
            {
                DataTable ifsLines = _repository.GetIfsProductionLines(_site);
                using (var dlg = new EditDialog(_site, _repository, ifsLines, row))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                }
                Reload();
                SetStatus("Đã lưu cặp mã 5 / 3 ký tự.", false);
            }
            catch (Exception ex)
            {
                string message = ex.GetBaseException().Message;
                SetStatus("Lưu thất bại: " + message, true);
                XtraMessageBox.Show(this, message, "Mã chuyền 5 / 3 ký tự", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DeleteRow(DataRow row)
        {
            if (!CanEdit) return;
            if (row == null) { SetStatus("Chưa chọn dòng nào.", true); return; }
            string c5 = Cell(row, "LineCode5");
            try
            {
                if (XtraMessageBox.Show(this, "Xóa dòng đối chiếu " + c5 + " → " + Cell(row, "LineCode3") + "?\r\nChỉ xóa được khi mã 3 ký tự chưa được định nghĩa trong PCTP.",
                        "Mã chuyền 5 / 3 ký tự", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _repository.DeleteLineCodeCrossRef(_site, c5);
                Reload();
                SetStatus("Đã xóa dòng " + c5 + ".", false);
            }
            catch (Exception ex)
            {
                string message = ex.GetBaseException().Message;
                SetStatus("Xóa thất bại: " + message, true);
                XtraMessageBox.Show(this, message, "Mã chuyền 5 / 3 ký tự", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Thêm hoặc sửa tay một dòng đối chiếu (row = null: thêm mới).</summary>
        private sealed class EditDialog : XtraForm
        {
            private readonly string _site;
            private readonly ISiteDepartmentLineRepository _repository;
            private readonly string _original5;
            private readonly List<string> _ifsCodes = new List<string>();
            private readonly TextEdit _code5 = new TextEdit();
            private readonly TextEdit _code3 = new TextEdit();
            private readonly TextEdit _family = new TextEdit();
            private readonly TextEdit _seq = new TextEdit();
            private readonly TextEdit _description = new TextEdit();
            private readonly ComboBoxEdit _ifs = new ComboBoxEdit();

            public EditDialog(string site, ISiteDepartmentLineRepository repository, DataTable ifsLines, DataRow row)
            {
                _site = site;
                _repository = repository;
                _original5 = row == null ? null : FormLineCodeCrossRef.Cell(row, "LineCode5");
                base.Text = (row == null ? "Thêm" : "Sửa") + " cặp mã chuyền - Site " + site;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;
                ClientSize = new Size(520, 300);

                _code5.Properties.CharacterCasing = CharacterCasing.Upper; _code5.Properties.MaxLength = 5;
                _code3.Properties.CharacterCasing = CharacterCasing.Upper; _code3.Properties.MaxLength = 3;
                _family.Properties.CharacterCasing = CharacterCasing.Upper; _family.Properties.MaxLength = 3;
                _description.Properties.MaxLength = 200;
                _ifs.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
                _ifs.Properties.Items.Add("(không gắn chuyền IFS)");
                _ifsCodes.Add(null);
                string currentIfs = FormLineCodeCrossRef.Cell(row, "IfsProductionLineCode");
                int selected = 0;
                if (ifsLines != null)
                    foreach (DataRow l in ifsLines.Rows)
                    {
                        string code = Convert.ToString(l["ProductionLineCode"]).Trim();
                        string desc = l.IsNull("Description") ? string.Empty : Convert.ToString(l["Description"]).Trim();
                        _ifsCodes.Add(code);
                        _ifs.Properties.Items.Add(desc.Length > 0 ? code + " — " + desc : code);
                        if (string.Equals(code, currentIfs, StringComparison.OrdinalIgnoreCase)) selected = _ifsCodes.Count - 1;
                    }
                _ifs.SelectedIndex = selected;

                _code5.Text = _original5 ?? string.Empty;
                _code3.Text = FormLineCodeCrossRef.Cell(row, "LineCode3");
                _family.Text = FormLineCodeCrossRef.Cell(row, "FamilyCode");
                _seq.Text = FormLineCodeCrossRef.Cell(row, "SeqNo");
                _description.Text = FormLineCodeCrossRef.Cell(row, "Description");

                AddRow(0, "Mã 5 ký tự (đúng 5):", _code5);
                AddRow(1, "Mã 3 ký tự LOT (đúng 3):", _code3);
                AddRow(2, "Họ máy (tùy chọn):", _family);
                AddRow(3, "Số TT (tùy chọn):", _seq);
                AddRow(4, "Mô tả:", _description);
                AddRow(5, "Chuyền IFS (tùy chọn):", _ifs);

                var ok = new SimpleButton { Text = "Lưu", Width = 90, Location = new Point(310, 256) };
                var cancel = new SimpleButton { Text = "Hủy", Width = 90, Location = new Point(410, 256), DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => Save();
                Controls.Add(ok); Controls.Add(cancel);
                CancelButton = cancel;
            }

            private void AddRow(int index, string caption, BaseEdit edit)
            {
                int y = 18 + index * 36;
                Controls.Add(new LabelControl { Text = caption, Location = new Point(12, y + 3), AutoSizeMode = LabelAutoSizeMode.None, Width = 155 });
                edit.Location = new Point(170, y); edit.Width = 330;
                Controls.Add(edit);
            }

            private void Save()
            {
                try
                {
                    int? seq = null;
                    string seqText = _seq.Text.Trim();
                    if (seqText.Length > 0)
                    {
                        int parsed;
                        if (!int.TryParse(seqText, out parsed)) throw new InvalidOperationException("Số TT phải là số nguyên.");
                        seq = parsed;
                    }
                    string ifs = _ifs.SelectedIndex > 0 && _ifs.SelectedIndex < _ifsCodes.Count ? _ifsCodes[_ifs.SelectedIndex] : null;
                    _repository.SaveLineCodeCrossRef(_site, _code5.Text, _code3.Text, _description.Text, ifs, _family.Text, seq, _original5);
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show(this, ex.GetBaseException().Message, base.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private sealed class GenerateDialog : XtraForm
        {
            private readonly string _site;
            private readonly ISiteDepartmentLineRepository _repository;
            private readonly List<string> _ifsCodes = new List<string>();
            private readonly ComboBoxEdit _mode = new ComboBoxEdit();
            private readonly ComboBoxEdit _family = new ComboBoxEdit();
            private readonly TextEdit _body = new TextEdit();
            private readonly TextEdit _description = new TextEdit();
            private readonly ComboBoxEdit _ifs = new ComboBoxEdit();
            private readonly TextEdit _override3 = new TextEdit();
            private readonly LabelControl _code5 = new LabelControl();
            private readonly LabelControl _code3 = new LabelControl();

            public GenerateDialog(string site, ISiteDepartmentLineRepository repository, DataTable ifsLines, List<string> families)
            {
                _site = site;
                _repository = repository;
                Text = "Sinh mã chuyền - Site " + site;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;
                ClientSize = new Size(520, 392);

                _mode.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
                _mode.Properties.Items.Add("Dãy máy đánh số (SK01, SK02…)");
                _mode.Properties.Items.Add("Máy đơn lẻ (4 ký tự)");
                _mode.SelectedIndex = 0;
                _mode.SelectedIndexChanged += (s, e) => ApplyMode();

                foreach (string f in families) _family.Properties.Items.Add(f);
                _family.Properties.CharacterCasing = CharacterCasing.Upper;
                _body.Properties.CharacterCasing = CharacterCasing.Upper;
                _override3.Properties.CharacterCasing = CharacterCasing.Upper;
                _family.Properties.MaxLength = 3;
                _body.Properties.MaxLength = 4;
                _override3.Properties.MaxLength = 3;

                _ifs.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
                _ifs.Properties.Items.Add("(không gắn chuyền IFS)");
                _ifsCodes.Add(null);
                if (ifsLines != null)
                    foreach (DataRow l in ifsLines.Rows)
                    {
                        string code = Convert.ToString(l["ProductionLineCode"]).Trim();
                        string desc = l.IsNull("Description") ? string.Empty : Convert.ToString(l["Description"]).Trim();
                        _ifsCodes.Add(code);
                        _ifs.Properties.Items.Add(desc.Length > 0 ? code + " — " + desc : code);
                    }
                _ifs.SelectedIndex = 0;

                var suggest = new SimpleButton { Text = "Gợi ý từ mô tả", Width = 110, Location = new Point(400, 18 + 3 * 32) };
                suggest.Click += (s, e) => _body.Text = SiteDepartmentLineRepository.SuggestBody(_description.Text);

                AddRow(0, "Kiểu mã:", _mode);
                AddRow(1, "Họ máy (2-3 ký tự):", _family);
                AddRow(2, "Thân mã (đúng 4 ký tự):", _body);
                AddRow(3, "Mô tả:", _description);
                _description.Width = 220;
                Controls.Add(suggest);
                AddRow(4, "Chuyền IFS (tùy chọn):", _ifs);
                AddRow(5, "Mã 3 ký tự (để trống = tự sinh):", _override3);

                _code5.Location = new Point(170, 18 + 6 * 32 + 6); _code5.Width = 330;
                _code3.Location = new Point(170, 18 + 6 * 32 + 30); _code3.Width = 330;
                Controls.Add(new LabelControl { Text = "Mã 5 ký tự:", Location = new Point(12, 18 + 6 * 32 + 6), Width = 155 });
                Controls.Add(new LabelControl { Text = "Mã 3 ký tự (LOT):", Location = new Point(12, 18 + 6 * 32 + 30), Width = 155 });
                Controls.Add(_code5); Controls.Add(_code3);

                var preview = new SimpleButton { Text = "Xem trước", Width = 90, Location = new Point(200, 350) };
                var ok = new SimpleButton { Text = "Lưu", Width = 90, Location = new Point(300, 350) };
                var cancel = new SimpleButton { Text = "Hủy", Width = 90, Location = new Point(400, 350), DialogResult = DialogResult.Cancel };
                preview.Click += (s, e) => Run(true);
                ok.Click += (s, e) => Run(false);
                Controls.Add(preview); Controls.Add(ok); Controls.Add(cancel);
                CancelButton = cancel;
                ApplyMode();
            }

            private bool Numbered { get { return _mode.SelectedIndex == 0; } }

            private void ApplyMode()
            {
                _family.Enabled = Numbered;
                _body.Enabled = !Numbered;
                _code5.Text = string.Empty; _code3.Text = string.Empty;
            }

            private void AddRow(int index, string caption, BaseEdit edit)
            {
                int y = 18 + index * 32;
                Controls.Add(new LabelControl { Text = caption, Location = new Point(12, y + 3), AutoSizeMode = LabelAutoSizeMode.None, Width = 155 });
                edit.Location = new Point(170, y); edit.Width = 310;
                Controls.Add(edit);
            }

            private void Run(bool preview)
            {
                try
                {
                    string ifs = _ifs.SelectedIndex > 0 && _ifs.SelectedIndex < _ifsCodes.Count ? _ifsCodes[_ifs.SelectedIndex] : null;
                    DataTable result = _repository.GenerateLineCode(_site,
                        Numbered ? _family.Text : null, Numbered ? null : _body.Text,
                        _description.Text, ifs, _override3.Text, preview);
                    if (result.Rows.Count == 0) throw new InvalidOperationException("Không nhận được mã từ SQL.");
                    _code5.Text = Convert.ToString(result.Rows[0]["LineCode5"]);
                    _code3.Text = Convert.ToString(result.Rows[0]["LineCode3"]);
                    if (!preview) DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show(this, ex.GetBaseException().Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
