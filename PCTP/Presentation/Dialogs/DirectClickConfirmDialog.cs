using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Presentation.Dialogs
{
    /// <summary>
    /// Confirmation dialog dành cho workflow có máy quét barcode/QR dùng keyboard wedge.
    /// Không dùng AcceptButton/CancelButton và không cho phép phím Enter kích hoạt nút.
    /// Chỉ thao tác chuột/touch trực tiếp trên nút mới được xem là xác nhận.
    /// </summary>
    internal sealed class DirectClickConfirmDialog : Form
    {
        private readonly Button _btnAgree;
        private readonly Button _btnNo;
        private bool _agreePointerDown;
        private bool _noPointerDown;

        private DirectClickConfirmDialog(string message)
        {
            Text = "Xác nhận chênh lệch số lượng";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            KeyPreview = true;
            Width = 560;
            Height = 280;

            // Quan trọng: tuyệt đối không cấu hình AcceptButton/CancelButton.
            AcceptButton = null;
            CancelButton = null;

            var lbl = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Text = message,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 12, 18, 8)
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10),
                WrapContents = false
            };

            _btnAgree = new Button
            {
                Text = "Đồng ý",
                Width = 120,
                Height = 36,
                TabStop = false
            };
            _btnNo = new Button
            {
                Text = "Không",
                Width = 120,
                Height = 36,
                TabStop = false
            };

            // Không dùng Click vì Button.Click có thể phát sinh từ keyboard.
            _btnAgree.MouseDown += BtnAgree_MouseDown;
            _btnAgree.MouseUp += BtnAgree_MouseUp;
            _btnNo.MouseDown += BtnNo_MouseDown;
            _btnNo.MouseUp += BtnNo_MouseUp;

            buttons.Controls.Add(_btnAgree);
            buttons.Controls.Add(_btnNo);
            Controls.Add(lbl);
            Controls.Add(buttons);

            Shown += delegate
            {
                // Không đặt focus vào Đồng ý. Scanner gửi Enter vào dialog vẫn bị chặn.
                ActiveControl = null;
            };

            KeyDown += DirectClickConfirmDialog_KeyDown;
            FormClosing += DirectClickConfirmDialog_FormClosing;
        }

        public static bool Show(string message)
        {
            using (var dialog = new DirectClickConfirmDialog(message))
            {
                dialog.ShowDialog();
                return dialog.DialogResult == DialogResult.Yes;
            }
        }

        private void DirectClickConfirmDialog_KeyDown(object sender, KeyEventArgs e)
        {
            // Keyboard wedge/scanner không được phép điều khiển dialog.
            // Đặc biệt Enter/Space/Tab tuyệt đối không được kích hoạt nút.
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void BtnAgree_MouseDown(object sender, MouseEventArgs e)
        {
            _agreePointerDown = e.Button == MouseButtons.Left;
        }

        private void BtnAgree_MouseUp(object sender, MouseEventArgs e)
        {
            bool directClick = _agreePointerDown && e.Button == MouseButtons.Left;
            _agreePointerDown = false;

            if (!directClick)
                return;

            DialogResult = DialogResult.Yes;
            Close();
        }

        private void BtnNo_MouseDown(object sender, MouseEventArgs e)
        {
            _noPointerDown = e.Button == MouseButtons.Left;
        }

        private void BtnNo_MouseUp(object sender, MouseEventArgs e)
        {
            bool directClick = _noPointerDown && e.Button == MouseButtons.Left;
            _noPointerDown = false;

            if (!directClick)
                return;

            DialogResult = DialogResult.No;
            Close();
        }

        private void DirectClickConfirmDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            // X/Alt+F4 được xem là Không, không bao giờ là Đồng ý.
            if (DialogResult == DialogResult.None)
                DialogResult = DialogResult.No;
        }
    }
}
