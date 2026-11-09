using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP
{
    /// <summary>
    /// Form đăng nhập đơn giản dùng để bảo vệ các màn hình quản trị
    /// (hiện dùng cho "Cấu hình bắt buộc FIFO theo mã hàng").
    /// Dùng:  if (!LogIn.XacThuc(this)) return;
    /// </summary>
    public partial class LogIn : DevExpress.XtraEditors.XtraForm
    {
        private const int SoLanNhapToiDa = 3;
        private int _soLanSai;

        public LogIn()
        {
            InitializeComponent();

            this.Text = "Đăng nhập quản trị";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;

            // textBox2 đã đặt PasswordChar trong Designer; đảm bảo luôn che ký tự.
            textBox2.UseSystemPasswordChar = true;

            simpleButton1.Click += (s, e) => DangNhap();
            textBox1.KeyDown += Enter_KeyDown;
            textBox2.KeyDown += Enter_KeyDown;
            this.Shown += (s, e) => (string.IsNullOrEmpty(textBox1.Text) ? textBox1 : textBox2).Focus();
        }

        private void Enter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            DangNhap();
        }

        private void DangNhap()
        {
            if (FifoConfigAuth.KiemTra(textBox1.Text, textBox2.Text))
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
                return;
            }

            _soLanSai++;
            if (_soLanSai >= SoLanNhapToiDa)
            {
                XtraMessageBox.Show("Nhập sai quá " + SoLanNhapToiDa + " lần.",
                    "Đăng nhập thất bại", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return;
            }

            XtraMessageBox.Show(
                "Tên đăng nhập hoặc mật khẩu không đúng (còn " + (SoLanNhapToiDa - _soLanSai) + " lần thử).",
                "Đăng nhập thất bại", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            textBox2.Clear();
            textBox2.Focus();
        }

        /// <summary>Hiện hộp thoại đăng nhập; true nếu đăng nhập đúng.</summary>
        public static bool XacThuc(IWin32Window owner)
        {
            using (var frm = new LogIn())
                return frm.ShowDialog(owner) == DialogResult.OK;
        }
    }

    /// <summary>
    /// Kiểm tra tài khoản quản trị cho form cấu hình FIFO.
    /// Cấu hình trong App.config (appSettings):
    ///   FifoConfig.User           = tên đăng nhập
    ///   FifoConfig.PasswordSha256 = SHA-256 (hex) của chuỗi  "PCTP|{user thường}|{mật khẩu}"
    /// Nếu chưa cấu hình sẽ dùng tài khoản mặc định (xem hằng số bên dưới) — nên đổi sau khi cài.
    /// Mật khẩu không bao giờ được lưu dạng chữ thường trong App.config.
    /// </summary>
    public static class FifoConfigAuth
    {
        private const string KeyUser = "FifoConfig.User";
        private const string KeyHash = "FifoConfig.PasswordSha256";

        private const string DefaultUser = "admin";
        private const string DefaultPassword = "pctp@2026";

        public static bool KiemTra(string user, string password)
        {
            user = (user ?? "").Trim();
            password = password ?? "";
            if (user.Length == 0 || password.Length == 0) return false;

            string cfgUser = ConfigurationManager.AppSettings[KeyUser];
            string cfgHash = ConfigurationManager.AppSettings[KeyHash];

            string expectedUser;
            string expectedHash;
            if (!string.IsNullOrWhiteSpace(cfgUser) && !string.IsNullOrWhiteSpace(cfgHash))
            {
                expectedUser = cfgUser.Trim();
                expectedHash = cfgHash.Trim();
            }
            else
            {
                expectedUser = DefaultUser;
                expectedHash = Hash(DefaultUser, DefaultPassword);
            }

            bool userOk = string.Equals(user, expectedUser, StringComparison.OrdinalIgnoreCase);
            bool passOk = ConstantTimeEquals(Hash(user, password), expectedHash);
            return userOk && passOk;
        }

        /// <summary>Tạo chuỗi hash để dán vào App.config.</summary>
        public static string Hash(string user, string password)
        {
            string input = "PCTP|" + (user ?? "").Trim().ToLowerInvariant() + "|" + (password ?? "");
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static bool ConstantTimeEquals(string a, string b)
        {
            a = (a ?? "").ToLowerInvariant();
            b = (b ?? "").ToLowerInvariant();
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
