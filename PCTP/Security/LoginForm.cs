using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.Security
{
    /// <summary>Đăng nhập / khởi tạo quản trị viên bằng bộ điều khiển DevExpress.</summary>
    public sealed class LoginForm : XtraForm
    {
        private readonly AuthenticationService _authentication;
        private readonly TextEdit _userName = new TextEdit();
        private readonly TextEdit _displayName = new TextEdit();
        private readonly TextEdit _password = new TextEdit();
        private readonly TextEdit _confirm = new TextEdit();
        private readonly LabelControl _heading = new LabelControl();
        private readonly LabelControl _subHeading = new LabelControl();
        private readonly LabelControl _hint = new LabelControl();
        private readonly LabelControl _displayNameLabel = new LabelControl { Text = "Họ tên hiển thị" };
        private readonly LabelControl _confirmLabel = new LabelControl { Text = "Xác nhận mật khẩu" };
        private readonly SimpleButton _submit = new SimpleButton();
        private bool _bootstrap;

        public UserSession AuthenticatedSession { get; private set; }

        public LoginForm(AuthenticationService authentication)
        {
            _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
            Text = "PCTP / MES - Đăng nhập";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ClientSize = new Size(520, 430);
            Appearance.Font = new Font("Segoe UI", 9F);
            Build();
            Shown += InitializeMode;
        }

        private void Build()
        {
            var card = new PanelControl
            {
                Dock = DockStyle.Fill,
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                Padding = new Padding(28, 24, 28, 20)
            };
            Controls.Add(card);

            _heading.Text = "PCTP  |  MES";
            _heading.Appearance.Font = new Font("Segoe UI", 19F, FontStyle.Bold);
            _heading.Appearance.ForeColor = Color.FromArgb(35, 75, 120);
            _heading.SetBounds(28, 24, 440, 38);
            card.Controls.Add(_heading);

            _subHeading.Text = "HỆ THỐNG QUẢN LÝ SẢN XUẤT";
            _subHeading.Appearance.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _subHeading.Appearance.ForeColor = Color.Gray;
            _subHeading.SetBounds(30, 64, 440, 22);
            card.Controls.Add(_subHeading);

            AddField(card, "Tên đăng nhập", _userName, 112);
            AddField(card, "Họ tên hiển thị", _displayName, 174);
            AddField(card, "Mật khẩu", _password, 236);
            AddField(card, "Xác nhận mật khẩu", _confirm, 298);

            _password.Properties.UseSystemPasswordChar = true;
            _confirm.Properties.UseSystemPasswordChar = true;

            _hint.Appearance.ForeColor = Color.DimGray;
            _hint.Appearance.Options.UseForeColor = true;
            _hint.AutoSizeMode = LabelAutoSizeMode.None;
            _hint.SetBounds(30, 356, 455, 30);
            card.Controls.Add(_hint);

            _submit.Text = "ĐĂNG NHẬP";
            _submit.Appearance.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _submit.Appearance.BackColor = Color.FromArgb(35, 105, 165);
            _submit.Appearance.ForeColor = Color.White;
            _submit.Appearance.Options.UseBackColor = true;
            _submit.Appearance.Options.UseForeColor = true;
            _submit.SetBounds(330, 390, 155, 34);
            _submit.Click += Submit;
            card.Controls.Add(_submit);
            AcceptButton = _submit;
        }

        private static void AddField(Control parent, string caption, TextEdit editor, int top)
        {
            var label = new LabelControl { Text = caption };
            label.Appearance.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label.SetBounds(30, top, 455, 20);
            editor.SetBounds(30, top + 23, 455, 30);
            editor.Properties.NullValuePrompt = caption;
            editor.Properties.NullValuePromptShowForEmptyValue = true;
            parent.Controls.Add(label);
            parent.Controls.Add(editor);
        }

        private void InitializeMode(object sender, EventArgs e)
        {
            try
            {
                _bootstrap = !_authentication.HasUsers();
                _heading.Text = _bootstrap ? "Khởi tạo quản trị viên" : "Chào mừng trở lại";
                _subHeading.Text = _bootstrap ? "THIẾT LẬP TÀI KHOẢN QUẢN TRỊ ĐẦU TIÊN" : "ĐĂNG NHẬP HỆ THỐNG PCTP / MES";
                _displayName.Visible = _bootstrap;
                _displayNameLabel.Visible = _bootstrap;
                _confirm.Visible = _bootstrap;
                _confirmLabel.Visible = _bootstrap;
                // Ẩn cả caption tương ứng, không để lại khoảng trống gây rối mắt.
                foreach (Control control in _displayName.Parent.Controls)
                    if (control is LabelControl && ((LabelControl)control).Text == "Họ tên hiển thị") control.Visible = _bootstrap;
                foreach (Control control in _confirm.Parent.Controls)
                    if (control is LabelControl && ((LabelControl)control).Text == "Xác nhận mật khẩu") control.Visible = _bootstrap;
                _hint.Text = _bootstrap
                    ? "Tài khoản khởi tạo đầu tiên sẽ được gán vai trò SYSTEM_ADMIN."
                    : "Sử dụng tài khoản được quản lý trong cơ sở dữ liệu FVN_PCTP.";
                _submit.Text = _bootstrap ? "TẠO QUẢN TRỊ VIÊN" : "ĐĂNG NHẬP";
                _userName.Focus();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Không thể kết nối hệ thống xác thực.\r\n" + ex.Message,
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
                }

                UserSession.SignIn(AuthenticatedSession);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, "PCTP - Xác thực",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _password.Text = string.Empty;
                _confirm.Text = string.Empty;
                _password.Focus();
            }
        }
    }
}