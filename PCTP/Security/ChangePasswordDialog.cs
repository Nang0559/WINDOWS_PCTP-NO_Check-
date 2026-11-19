using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.Security
{
    /// <summary>Hộp thoại đổi mật khẩu theo chính sách bảo mật hiện hành.</summary>
    internal sealed class ChangePasswordDialog : XtraForm
    {
        private readonly TextEdit _current = new TextEdit();
        private readonly TextEdit _next = new TextEdit();
        private readonly TextEdit _confirm = new TextEdit();

        public string NewPassword { get; private set; }
        public string CurrentPassword => _current.Text;

        public ChangePasswordDialog(bool firstPassword)
        {
            Text = firstPassword ? "Thiết lập mật khẩu mới" : "Đổi mật khẩu";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ClientSize = new Size(500, firstPassword ? 270 : 325);
            Appearance.Font = new Font("Segoe UI", 9F);
            Build(firstPassword);
        }

        private void Build(bool firstPassword)
        {
            var panel = new PanelControl
            {
                Dock = DockStyle.Fill,
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                Padding = new Padding(22)
            };
            Controls.Add(panel);

            var title = new LabelControl
            {
                Text = firstPassword ? "BẢO MẬT TÀI KHOẢN" : "CẬP NHẬT MẬT KHẨU",
                Appearance = { Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = Color.FromArgb(35, 75, 120) }
            };
            title.SetBounds(22, 18, 440, 28);
            panel.Controls.Add(title);

            int y = 62;
            if (!firstPassword) AddField(panel, "Mật khẩu hiện tại", _current, ref y);
            AddField(panel, "Mật khẩu mới (tối thiểu 12 ký tự)", _next, ref y);
            AddField(panel, "Nhập lại mật khẩu mới", _confirm, ref y);
            _current.Properties.UseSystemPasswordChar = true;
            _next.Properties.UseSystemPasswordChar = true;
            _confirm.Properties.UseSystemPasswordChar = true;

            var cancel = new SimpleButton { Text = "HỦY" };
            cancel.SetBounds(270, y + 8, 88, 34);
            cancel.DialogResult = DialogResult.Cancel;
            panel.Controls.Add(cancel);

            var save = new SimpleButton { Text = "CẬP NHẬT" };
            save.Appearance.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            save.SetBounds(366, y + 8, 110, 34);
            save.Click += Save;
            panel.Controls.Add(save);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private static void AddField(Control parent, string caption, TextEdit editor, ref int y)
        {
            var label = new LabelControl { Text = caption };
            label.Appearance.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label.SetBounds(22, y, 450, 20);
            editor.SetBounds(22, y + 22, 450, 30);
            parent.Controls.Add(label);
            parent.Controls.Add(editor);
            y += 62;
        }

        private void Save(object sender, EventArgs e)
        {
            try
            {
                PasswordHasher.ValidatePolicy(_next.Text);
                if (!string.Equals(_next.Text, _confirm.Text, StringComparison.Ordinal))
                    throw new InvalidOperationException("Mật khẩu xác nhận không khớp.");
                NewPassword = _next.Text;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}