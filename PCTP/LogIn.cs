using System;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using PCTP.Security;

namespace PCTP
{
    /// <summary>
    /// Legacy FIFO configuration gate. Authentication is centralized at application startup;
    /// this form no longer maintains a separate username/password or fallback credential.
    /// </summary>
    public partial class LogIn : XtraForm
    {
        public LogIn()
        {
            InitializeComponent();
            Text = "Xác thực quyền cấu hình FIFO";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            textBox2.UseSystemPasswordChar = true;
            simpleButton1.Click += (s, e) => DangNhap();
            textBox1.KeyDown += Enter_KeyDown;
            textBox2.KeyDown += Enter_KeyDown;
            Shown += (s, e) => textBox1.Focus();
        }

        private void Enter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.Handled = true; e.SuppressKeyPress = true; DangNhap();
        }

        private void DangNhap()
        {
            if (FifoConfigAuth.KiemTra(null, null))
            {
                DialogResult = DialogResult.OK; Close(); return;
            }
            XtraMessageBox.Show(this, "Tài khoản hiện tại không có quyền quản trị cấu hình FIFO.",
                "Không đủ quyền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.Cancel; Close();
        }

        public static bool XacThuc(IWin32Window owner)
        {
            if (FifoConfigAuth.KiemTra(null, null)) return true;
            XtraMessageBox.Show(owner, "Bạn không có quyền quản trị cấu hình FIFO.",
                "Không đủ quyền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    /// <summary>Compatibility adapter for legacy callers; delegates to central RBAC.</summary>
    public static class FifoConfigAuth
    {
        public static bool KiemTra(string user, string password)
        {
            var session = UserSession.Current;
            return session != null && session.HasPermission("SYSTEM.FIFO_CONFIG", "ADMIN");
        }
    }
}