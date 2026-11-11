using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Shared.Models;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Shell.UI
{
    /// <summary>
    /// Quản lý quyền đọc QR theo từng khách hàng:
    ///  - Trạng thái: khách nào đang do máy nào đọc (chiếm quyền / nhả khoá).
    ///  - Đăng ký: IP máy nào được phép đọc khách nào ("*" = mọi khách, kể cả khách thêm sau).
    /// Định danh máy = IP do SQL Server tự đọc (client không khai được); tên máy chỉ để hiển thị.
    /// Danh sách khách lấy từ CustomerTableConfig nên khách mới tự xuất hiện.
    /// </summary>
    public sealed class FormQuanLyMayDocQR : XtraForm
    {
        private readonly CustomerQrLockService _locks;
        private readonly QrReaderMachineService _machines;

        private readonly GridControl _gridLocks = new GridControl();
        private readonly GridControl _gridRegs = new GridControl();
        private GridView _viewLocks;
        private GridView _viewRegs;
        private readonly LabelControl _hostLabel = new LabelControl();

        public FormQuanLyMayDocQR()
        {
            var sql = new SQLPROVIDER();
            _locks = new CustomerQrLockService(sql);
            _machines = new QrReaderMachineService(sql);

            Text = "Quản lý máy đọc QR theo khách hàng";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 680);
            MinimumSize = new Size(860, 520);

            BuildLayout();
            Shown += (s, e) => RefreshAll();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // host
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));      // locks grid
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // locks buttons
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));      // regs grid
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // regs buttons
            Controls.Add(root);

            _hostLabel.Text = "Máy này: " + Environment.MachineName;
            _hostLabel.Appearance.Font = new Font(Font, FontStyle.Bold);
            _hostLabel.Padding = new Padding(0, 0, 0, 6);
            root.Controls.Add(_hostLabel, 0, 0);

            _viewLocks = CreateView(_gridLocks);
            root.Controls.Add(_gridLocks, 0, 1);
            root.Controls.Add(Buttons(
                Button("Làm mới", (s, e) => RefreshAll()),
                Button("Chiếm quyền (máy này)", (s, e) => TakeOver()),
                Button("Nhả khoá (admin)", (s, e) => ForceRelease())), 0, 2);

            _viewRegs = CreateView(_gridRegs);
            root.Controls.Add(_gridRegs, 0, 3);
            root.Controls.Add(Buttons(
                Button("Đăng ký máy này…", (s, e) => AddRegistration(true)),
                Button("Thêm máy theo IP…", (s, e) => AddRegistration(false)),
                Button("Bật / Tắt", (s, e) => ToggleRegistration()),
                Button("Xoá", (s, e) => RemoveRegistration())), 0, 4);
        }

        private static GridView CreateView(GridControl grid)
        {
            grid.Dock = DockStyle.Fill;
            var view = new GridView(grid);
            grid.MainView = view;
            view.OptionsBehavior.Editable = false;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ColumnAutoWidth = true;
            return view;
        }

        private static SimpleButton Button(string text, EventHandler onClick)
        {
            var b = new SimpleButton { Text = text, AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            b.Click += onClick;
            return b;
        }

        private static FlowLayoutPanel Buttons(params Control[] buttons)
        {
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 6, 0, 10) };
            p.Controls.AddRange(buttons);
            return p;
        }

        // ── Data ────────────────────────────────────────────────────

        private void RefreshAll()
        {
            try
            {
                string ip = _machines.GetMyClientIp();
                _hostLabel.Text = "Máy này: " + Environment.MachineName +
                    "   |   IP server thấy: " + (ip ?? "(không xác định)");

                BindLocks();
                BindRegistrations();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show("Không tải được dữ liệu: " + ex.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BindLocks()
        {
            var infos = _locks.ListAll();
            var dt = new DataTable();
            dt.Columns.Add("CustomerNo");
            dt.Columns.Add("ScanMode");
            dt.Columns.Add("Khách hàng");
            dt.Columns.Add("Máy đang đọc QR");
            dt.Columns.Add("IP máy đọc");
            dt.Columns.Add("Người dùng");
            dt.Columns.Add("Giữ từ", typeof(DateTime));
            dt.Columns.Add("Heartbeat cuối", typeof(DateTime));
            dt.Columns.Add("Trạng thái");
            dt.Columns.Add("IsMine", typeof(bool));

            foreach (CustomerConfig cfg in CustomerTableConfig.All)
            {
                if (cfg.Delivery == null) continue;
                foreach (string mode in new[] { "MP", "SP" })
                {
                QrLockInfo i = infos.FirstOrDefault(x =>
                    string.Equals(x.CustomerNo, cfg.CustomerNo, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.ScanMode, mode, StringComparison.OrdinalIgnoreCase));
                bool hasOwner = i != null && i.HasOwner;

                string state = "Trống";
                if (hasOwner)
                    state = i.IsStale ? "Không phản hồi (hết hạn)"
                          : i.IsOwnedByMe ? "MÁY NÀY đang đọc"
                          : "Đang đọc";

                DataRow r = dt.NewRow();
                r["CustomerNo"] = cfg.CustomerNo;
                r["ScanMode"] = mode;
                r["Khách hàng"] = cfg.DisplayName;
                r["Máy đang đọc QR"] = hasOwner ? i.OwnerMachine : string.Empty;
                r["IP máy đọc"] = hasOwner ? i.OwnerClientIp : string.Empty;
                r["Người dùng"] = hasOwner ? i.OwnerUser : string.Empty;
                r["Giữ từ"] = hasOwner && i.LockedAt.HasValue ? (object)i.LockedAt.Value : DBNull.Value;
                r["Heartbeat cuối"] = hasOwner && i.HeartbeatAt.HasValue ? (object)i.HeartbeatAt.Value : DBNull.Value;
                r["Trạng thái"] = state;
                r["IsMine"] = hasOwner && i.IsOwnedByMe;
                dt.Rows.Add(r);
                }
            }

            _gridLocks.DataSource = dt;
            Caption(_viewLocks, "ScanMode", "Chế độ");
            var isMine = _viewLocks.Columns["IsMine"];
            if (isMine != null) isMine.Visible = false;
            _viewLocks.BestFitColumns();
        }

        private void BindRegistrations()
        {
            DataTable dt = _machines.ListRegistrations();
            _gridRegs.DataSource = dt;
            Caption(_viewRegs, "CustomerNo", "Khách hàng (* = tất cả)");
            Caption(_viewRegs, "ScanMode", "Chế độ");
            Caption(_viewRegs, "ClientIp", "IP máy");
            Caption(_viewRegs, "MachineName", "Tên máy (hiển thị)");
            Caption(_viewRegs, "IsActive", "Hiệu lực");
            Caption(_viewRegs, "Note", "Ghi chú");
            Caption(_viewRegs, "CreatedAt", "Tạo lúc");
            _viewRegs.BestFitColumns();
        }

        private static void Caption(GridView view, string field, string caption)
        {
            var col = view.Columns[field];
            if (col != null) col.Caption = caption;
        }

        // ── Actions: trạng thái khoá ────────────────────────────────

        private DataRow FocusedLock() => _viewLocks.GetFocusedDataRow();

        private void TakeOver()
        {
            DataRow row = FocusedLock();
            if (row == null) return;
            string customerNo = Convert.ToString(row["CustomerNo"]);
            string scanMode = Convert.ToString(row["ScanMode"]);
            string name = Convert.ToString(row["Khách hàng"]);
            string owner = Convert.ToString(row["Máy đang đọc QR"]);
            string ownerIp = Convert.ToString(row["IP máy đọc"]);

            if (Convert.ToBoolean(row["IsMine"]))
            {
                XtraMessageBox.Show("Máy này đang giữ quyền đọc QR của khách " + name + ".", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string who = ownerIp.Length == 0 ? "chưa có máy nào"
                       : "máy [" + (owner.Length > 0 ? owner + " - " : string.Empty) + ownerIp + "]";
            if (XtraMessageBox.Show(
                    "Khách " + name + " hiện do " + who + " đọc QR.\r\nChiếm quyền đọc QR của khách này cho máy này?",
                    "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            QrLockInfo info = _locks.TryAcquire(customerNo, scanMode, true);
            if (info.Error != null)
                XtraMessageBox.Show("Lỗi: " + info.Error, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else if (!info.Registered)
                XtraMessageBox.Show("IP máy này chưa được đăng ký đọc QR cho khách " + name +
                    ".\r\nHãy đăng ký máy này ở bảng bên dưới trước.", "Không thể chiếm quyền",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else if (info.IsOwnedByMe)
                XtraMessageBox.Show("Đã chiếm quyền.\r\nNếu màn hình giao hàng của khách này đang mở ở chế độ xem, " +
                    "hãy đóng và mở lại để đọc QR.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                XtraMessageBox.Show("Không chiếm được quyền: " + info.OwnerDisplay, "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

            RefreshAll();
        }

        private void ForceRelease()
        {
            DataRow row = FocusedLock();
            if (row == null) return;
            string customerNo = Convert.ToString(row["CustomerNo"]);
            string scanMode = Convert.ToString(row["ScanMode"]);
            string name = Convert.ToString(row["Khách hàng"]);
            string owner = Convert.ToString(row["Máy đang đọc QR"]);
            string ownerIp = Convert.ToString(row["IP máy đọc"]);
            if (ownerIp.Length == 0)
            {
                XtraMessageBox.Show("Khách " + name + " hiện không có máy nào giữ quyền.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (XtraMessageBox.Show(
                    "Nhả khoá đọc QR của khách " + name + " (máy [" + (owner.Length > 0 ? owner + " - " : string.Empty) + ownerIp + "] đang giữ)?\r\n" +
                    "Máy đó sẽ bị chặn đọc QR ở lần quét kế tiếp. Chỉ làm khi máy đó treo hoặc không còn dùng.",
                    "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            Try(() => _machines.ForceRelease(customerNo, scanMode));
            RefreshAll();
        }

        // ── Actions: đăng ký máy ────────────────────────────────────

        private DataRow FocusedReg() => _viewRegs.GetFocusedDataRow();

        private void AddRegistration(bool thisMachine)
        {
            string ip = string.Empty;
            if (thisMachine)
            {
                try { ip = _machines.GetMyClientIp(); }
                catch (Exception ex)
                {
                    XtraMessageBox.Show("Không lấy được IP từ server: " + ex.Message, "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(ip))
                {
                    XtraMessageBox.Show("SQL Server không xác định được IP của kết nối này nên không thể đăng ký máy này.",
                        "Không thể đăng ký", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.Equals(ip, QrReaderMachineService.LocalMachineIp, StringComparison.OrdinalIgnoreCase)
                    && XtraMessageBox.Show(
                        "App đang chạy cùng máy với SQL Server nên server chỉ thấy \"" + ip + "\".\r\n" +
                        "Mọi máy chạy trực tiếp trên server sẽ dùng chung định danh này. Vẫn đăng ký?",
                        "Cảnh báo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            using (var dlg = new RegistrationDialog(ip, thisMachine ? Environment.MachineName : string.Empty, thisMachine))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Try(() => _machines.Upsert(dlg.CustomerScope, dlg.ScanMode, dlg.ClientIp, dlg.MachineName, true, dlg.Note));
            }
            RefreshAll();
        }

        private void ToggleRegistration()
        {
            DataRow row = FocusedReg();
            if (row == null) return;
            bool active = Convert.ToBoolean(row["IsActive"]);
            string scope = Convert.ToString(row["CustomerNo"]);
            string mode = Convert.ToString(row["ScanMode"]);
            string ip = Convert.ToString(row["ClientIp"]);

            if (active && XtraMessageBox.Show(
                    "Tắt đăng ký của máy IP [" + ip + "] (" + scope + ")?\r\nNếu máy đang giữ khoá, máy sẽ mất quyền đọc QR ngay.",
                    "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            Try(() => _machines.Upsert(scope, mode, ip, null, !active, null));
            RefreshAll();
        }

        private void RemoveRegistration()
        {
            DataRow row = FocusedReg();
            if (row == null) return;
            string scope = Convert.ToString(row["CustomerNo"]);
            string mode = Convert.ToString(row["ScanMode"]);
            string ip = Convert.ToString(row["ClientIp"]);

            if (XtraMessageBox.Show(
                    "Xoá đăng ký của máy IP [" + ip + "] (" + scope + ")?\r\nNếu máy đang giữ khoá, máy sẽ mất quyền đọc QR ngay.",
                    "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            Try(() => _machines.Remove(scope, mode, ip));
            RefreshAll();
        }

        private static void Try(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                XtraMessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Dialog thêm đăng ký ─────────────────────────────────────

        private sealed class RegistrationDialog : XtraForm
        {
            private readonly ComboBoxEdit _customer = new ComboBoxEdit();
            private readonly ComboBoxEdit _mode = new ComboBoxEdit();
            private readonly TextEdit _ip = new TextEdit();
            private readonly TextEdit _machine = new TextEdit();
            private readonly TextEdit _note = new TextEdit();

            public string CustomerScope
            {
                get
                {
                    string t = (_customer.Text ?? string.Empty).Trim();
                    int sp = t.IndexOf(' ');
                    return sp > 0 ? t.Substring(0, sp) : t;
                }
            }

            public string ScanMode => (_mode.Text ?? "MP").Trim().ToUpperInvariant();
            public string ClientIp => (_ip.Text ?? string.Empty).Trim();
            public string MachineName => (_machine.Text ?? string.Empty).Trim();
            public string Note => (_note.Text ?? string.Empty).Trim();

            public RegistrationDialog(string defaultIp, string defaultMachine, bool ipLocked)
            {
                Text = "Đăng ký máy đọc QR (định danh theo IP)";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ClientSize = new Size(480, 270);

                _customer.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
                _customer.Properties.Items.Add("* (tất cả khách hàng, kể cả khách thêm sau)");
                foreach (CustomerConfig cfg in CustomerTableConfig.All)
                    if (cfg.Delivery != null)
                        _customer.Properties.Items.Add(cfg.CustomerNo + " - " + cfg.DisplayName);
                _customer.SelectedIndex = 0;
                _mode.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
                _mode.Properties.Items.AddRange(new object[] { "MP", "SP" });
                _mode.SelectedIndex = 0;

                _ip.Text = defaultIp ?? string.Empty;
                _ip.Properties.ReadOnly = ipLocked; // "Đăng ký máy này": IP lấy từ server, không cho sửa
                _machine.Text = defaultMachine ?? string.Empty;

                Add("Khách hàng", _customer, 14);
                Add("Chế độ", _mode, 54);
                Add("IP máy", _ip, 94);
                Add("Tên máy", _machine, 134);
                Add("Ghi chú", _note, 174);

                var ok = new SimpleButton { Text = "Lưu", DialogResult = DialogResult.OK, Location = new Point(290, 224), Width = 80 };
                var cancel = new SimpleButton { Text = "Huỷ", DialogResult = DialogResult.Cancel, Location = new Point(380, 224), Width = 80 };
                Controls.Add(ok);
                Controls.Add(cancel);
                AcceptButton = ok;
                CancelButton = cancel;
            }

            private void Add(string caption, Control editor, int top)
            {
                Controls.Add(new LabelControl { Text = caption, Location = new Point(14, top + 4) });
                editor.Location = new Point(100, top);
                editor.Width = 360;
                Controls.Add(editor);
            }
        }
    }
}
