using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraLayout;

namespace PCTP.Framework.Documents.UI
{
    /// <summary>
    /// Metadata-driven document viewer: header fields (DevExpress LayoutControl), detail grid and
    /// registered commands. Hiding or disabling a button is a usability aid only; every command goes
    /// through DocumentCommandDispatcher (RBAC + Site scope) and the SQL procedure's own guards.
    /// </summary>
    public sealed class DocumentEditorForm : XtraForm
    {
        private readonly DocumentRuntimeServices _runtime;
        private readonly string _moduleCode;
        private readonly string _siteCode;
        private readonly object _key;
        private readonly IDocumentCommandInputProvider _inputProvider;
        private readonly LayoutControl _headerLayout = new LayoutControl();
        private readonly DocumentDetailControl _detail = new DocumentDetailControl();
        private readonly FlowLayoutPanel _commandPanel = new FlowLayoutPanel();
        private readonly LabelControl _status = new LabelControl();
        private ResolvedDocument _document;
        private DataTable _headerTable;

        public DocumentEditorForm(DocumentRuntimeServices runtime, string moduleCode, string siteCode,
            object key, IDocumentCommandInputProvider inputProvider = null)
        {
            if (runtime == null) throw new ArgumentNullException("runtime");
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            if (key == null) throw new ArgumentNullException("key");
            _runtime = runtime;
            _moduleCode = moduleCode;
            _siteCode = siteCode.Trim();
            _key = key;
            _inputProvider = inputProvider;
            Text = "Chứng từ " + moduleCode;
            StartPosition = FormStartPosition.CenterParent;
            Size = new System.Drawing.Size(1180, 720);
            MinimumSize = new System.Drawing.Size(900, 560);
            BuildUi();
            Load += (s, e) => Reload();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            var split = new SplitContainerControl { Dock = DockStyle.Fill, Horizontal = false };
            _headerLayout.Dock = DockStyle.Fill;
            split.Panel1.Controls.Add(_headerLayout);
            split.Panel2.Controls.Add(_detail);

            _commandPanel.Dock = DockStyle.Fill;
            _commandPanel.WrapContents = false;
            _commandPanel.FlowDirection = FlowDirection.RightToLeft;
            _status.Dock = DockStyle.Fill;

            root.Controls.Add(split, 0, 0);
            root.Controls.Add(_commandPanel, 0, 1);
            root.Controls.Add(_status, 0, 2);
            Controls.Add(root);
        }

        private void Reload()
        {
            try
            {
                _document = _runtime.Resolve(_moduleCode);
                Text = _document.DisplayName + " - " + _key;
                _headerTable = _runtime.Data.GetHeader(_moduleCode, _siteCode, _key);
                var detailTable = _runtime.Data.GetDetail(_moduleCode, _siteCode, _key);
                BuildHeader();
                _detail.Bind(_document, detailTable);
                BuildCommands();
                _status.Text = "Đã tải " + detailTable.Rows.Count.ToString("N0") + " dòng chi tiết. Lúc " + DateTime.Now.ToString("HH:mm:ss");
                _status.Appearance.ForeColor = System.Drawing.Color.FromArgb(70, 85, 100);
            }
            catch (Exception ex)
            {
                _status.Text = "Không tải được chứng từ: " + ex.GetBaseException().Message;
                _status.Appearance.ForeColor = System.Drawing.Color.Firebrick;
            }
        }

        private void BuildHeader()
        {
            DataRow row = _headerTable != null && _headerTable.Rows.Count > 0 ? _headerTable.Rows[0] : null;
            _headerLayout.Clear();
            _headerLayout.BeginUpdate();
            try
            {
                if (_document.HeaderVisible)
                {
                    foreach (var field in _document.HeaderFields.Where(f => f.IsVisible))
                    {
                        BaseEdit editor = CreateEditor(field);
                        editor.EditValue = row == null || row[field.FieldCode] == DBNull.Value ? null : row[field.FieldCode];
                        _headerLayout.AddItem(field.Caption, editor);
                    }
                }
            }
            finally
            {
                _headerLayout.EndUpdate();
            }
        }

        // v1 has no header save path, so every header editor is read-only regardless of metadata.
        private static BaseEdit CreateEditor(ResolvedField field)
        {
            BaseEdit editor;
            switch (field.Editor)
            {
                case DocumentFieldType.Check: editor = new CheckEdit(); break;
                case DocumentFieldType.Memo: editor = new MemoEdit(); break;
                case DocumentFieldType.Date:
                case DocumentFieldType.DateTime: editor = new DateEdit(); break;
                default: editor = new TextEdit(); break;
            }
            editor.Properties.ReadOnly = true;
            if (!string.IsNullOrEmpty(field.DisplayFormat))
            {
                editor.Properties.DisplayFormat.FormatType = DocumentGridConfigurator.ToFormatType(field.Editor);
                editor.Properties.DisplayFormat.FormatString = field.DisplayFormat;
            }
            return editor;
        }

        private void BuildCommands()
        {
            _commandPanel.Controls.Clear();
            foreach (var command in _document.Commands.Where(c => c.IsVisible))
            {
                bool allowed = _runtime.Authorizer.IsCommandAllowed(_document.Definition, command.Definition, _siteCode);
                bool hasInput = _inputProvider != null;
                var button = new SimpleButton { Text = command.Caption, Width = 170, Height = 32, Tag = command };
                button.Enabled = allowed && hasInput;
                button.ToolTip = !allowed
                    ? "Bạn chỉ được xem; không có quyền thực hiện thao tác này."
                    : !hasInput
                        ? "Thao tác này cần màn hình nhập liệu riêng; hiện dùng màn hình nghiệp vụ hiện có."
                        : "Quyền được kiểm tra lại ở máy chủ khi thực hiện.";
                button.Click += Command_Click;
                _commandPanel.Controls.Add(button);
            }
        }

        private void Command_Click(object sender, EventArgs e)
        {
            var command = (ResolvedCommand)((SimpleButton)sender).Tag;
            try
            {
                DataRow header = _headerTable != null && _headerTable.Rows.Count > 0 ? _headerTable.Rows[0] : null;
                IDictionary<string, object> input;
                if (!_inputProvider.TryGetInput(_document, command, header, _detail.FocusedRow, this, out input)) return;
                _runtime.Dispatcher.Execute(_moduleCode, command.CommandCode, _siteCode, input);
                XtraMessageBox.Show(this, "Đã thực hiện: " + command.Caption, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Reload();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.GetBaseException().Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
