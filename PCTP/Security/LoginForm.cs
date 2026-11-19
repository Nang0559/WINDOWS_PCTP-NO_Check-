using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Security
{
    public sealed class LoginForm : Form
    {
        private readonly AuthenticationService _authentication;
        private readonly TextBox _userName = new TextBox();
        private readonly TextBox _displayName = new TextBox();
        private readonly TextBox _password = new TextBox();
        private readonly TextBox _confirm = new TextBox();
        private readonly Label _heading = new Label();
        private readonly Label _hint = new Label();
        private readonly Button _submit = new Button();
        private bool _bootstrap;

        public UserSession AuthenticatedSession { get; private set; }

        public LoginForm(AuthenticationService authentication)
        {
            _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
            Text = "PCTP / MES - Đăng nhập";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ClientSize = new Size(480, 310);
            Build();
            Shown += InitializeMode;
        }

        private void Build()
        {
            _heading.SetBounds(24, 20, 430, 34);
            _heading.Font = new Font(Font, FontStyle.Bold);
            _heading.Font = new Font(_heading.Font.FontFamily, 14, FontStyle.Bold);
            Controls.Add(_heading);

            AddField("Tên đăng nhập", _userName, 76);
            AddField("Họ tên hiển thị", _displayName, 120);
            AddField("Mật khẩu", _password, 164);
            AddField("Xác nhận mật khẩu", _confirm, 208);
            _password.UseSystemPasswordChar = true;
            _confirm.UseSystemPasswordChar = true;

            _hint.SetBounds(24, 250, 430, 22);
            _hint.ForeColor = Color.DimGray;
            Controls.Add(_hint);
            _submit.Text = "ĐĂNG NHẬP";
            _submit.SetBounds(330, 274, 124, 30);
            _submit.Click += Submit;
            Controls.Add(_submit);
            AcceptButton = _submit;
            CancelButton = new Button { DialogResult = DialogResult.Cancel };
        }

        private void AddField(string caption, TextBox box, int y)
        {
            var label = new Label { Text = caption, Left = 24, Top = y + 4, Width = 175 };
            box.SetBounds(205, y, 249, 26);
            Controls.Add(label); Controls.Add(box);
        }

        private void InitializeMode(object sender, EventArgs e)
        {
            try
            {
                _bootstrap = !_authentication.HasUsers();
                _heading.Text = _bootstrap ? "Khởi tạo quản trị viên đầu tiên" : "Đăng nhập hệ thống";
                _displayName.Visible = _bootstrap;
                _confirm.Visible = _bootstrap;
                Controls[1].Visible = true; // username label
                Controls[2].Visible = true; // username textbox
                Controls[3].Visible = _bootstrap;
                Controls[4].Visible = _bootstrap;
                Controls[5].Visible = true;
                Controls[6].Visible = _bootstrap;
                _hint.Text = _bootstrap
                    ? "Khởi tạo lần đầu: tài khoản này nhận vai trò SYSTEM_ADMIN."
                    : "Tài khoản được quản lý tập trung trong FVN_PCTP.";
                _submit.Text = _bootstrap ? "TẠO QUẢN TRỊ" : "ĐĂNG NHẬP";
                _userName.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Không thể kết nối hệ thống xác thực.\r\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Abort;
                Close();
            }
        }

        private void Submit(object sender, EventArgs e)
        {
            try
            {
                if (_bootstrap)
                {
                    if (!string.Equals(_password.Text, _confirm.Text, StringComparison.Ordinal))
                        throw new InvalidOperationException("Mật khẩu xác nhận không khớp.");
                    AuthenticatedSession = _authentication.InitializeFirstAdmin(
                        _userName.Text, _displayName.Text, _password.Text);
                }
                else
                {
                    AuthenticatedSession = _authentication.Authenticate(_userName.Text, _password.Text);
                    if (AuthenticatedSession.MustChangePassword)
                    {
                        using (var dialog = new ChangePasswordDialog(true))
                        {
                            if (dialog.ShowDialog(this) != DialogResult.OK)
                            {
                                AuthenticatedSession = null;
                                return;
                            }
                            _authentication.ChangePassword(AuthenticatedSession, null, dialog.NewPassword);
                            AuthenticatedSession = _authentication.Authenticate(_userName.Text, dialog.NewPassword);
                        }
                    }
                    else
                    {
                        using (var dialog = new ChangePasswordDialog(false))
                        {
                            // Password change is optional; it is shown only through a separate command.
                        }
                    }
                }

                UserSession.SignIn(AuthenticatedSession);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "PCTP - Xác thực",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _password.Clear(); _confirm.Clear(); _password.Focus();
            }
        }
    }
}