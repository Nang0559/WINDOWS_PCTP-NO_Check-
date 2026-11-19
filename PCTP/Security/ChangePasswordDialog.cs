using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Security
{
    internal sealed class ChangePasswordDialog : Form
    {
        private readonly TextBox _current = new TextBox();
        private readonly TextBox _next = new TextBox();
        private readonly TextBox _confirm = new TextBox();
        public string NewPassword { get; private set; }
        public string CurrentPassword => _current.Text;

        public ChangePasswordDialog(bool firstPassword)
        {
            Text = firstPassword ? "Thiết lập mật khẩu mới" : "Đổi mật khẩu";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ClientSize = new Size(440, firstPassword ? 190 : 230);
            Build(firstPassword);
        }

        private void Build(bool firstPassword)
        {
            int y = 20;
            if (!firstPassword) AddField("Mật khẩu hiện tại", _current, ref y);
            AddField("Mật khẩu mới (tối thiểu 12 ký tự)", _next, ref y);
            AddField("Nhập lại mật khẩu mới", _confirm, ref y);
            var save = new Button { Text = "CẬP NHẬT", Width = 120, Height = 32, Left = 285, Top = y + 8 };
            save.Click += (s, e) =>
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
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            Controls.Add(save);
            AcceptButton = save;
            CancelButton = new Button { DialogResult = DialogResult.Cancel };
        }

        private void AddField(string caption, TextBox box, ref int y)
        {
            Controls.Add(new Label { Text = caption, Left = 18, Top = y + 4, Width = 190 });
            box.Left = 210; box.Top = y; box.Width = 210; box.UseSystemPasswordChar = true;
            Controls.Add(box); y += 48;
        }
    }
}