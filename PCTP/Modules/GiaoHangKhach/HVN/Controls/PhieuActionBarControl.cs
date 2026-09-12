using System;
using DevExpress.XtraBars.Docking2010;
using DevExpress.XtraEditors;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// Owns the action-bar button configuration for the delivery-ticket screen.
    /// The legacy WindowsUIButtonPanel is adopted so the WinForms Designer
    /// structure remains unchanged during the refactor.
    /// </summary>
    public sealed class PhieuActionBarControl : XtraUserControl
    {
        private WindowsUIButtonPanel _panel;

        public WindowsUIButtonPanel Panel
        {
            get { return _panel; }
        }

        public void Adopt(WindowsUIButtonPanel panel)
        {
            if (panel == null || ReferenceEquals(_panel, panel))
                return;

            if (_panel != null)
                Controls.Remove(_panel);

            _panel = panel;
            Controls.Add(_panel);
            _panel.Dock = System.Windows.Forms.DockStyle.Fill;
        }

        public void Clear()
        {
            if (_panel == null)
                return;

            _panel.AllowGlyphSkinning = false;
            _panel.Buttons.Clear();
        }

        public void ConfigureNormal(
            string ghepLotCaption,
            bool showCapNhapKho,
            bool showKiemTraMaNG,
            bool showGhepLot,
            bool showDocQRCode,
            bool showLayLaiLot,
            bool showStop,
            bool showHangThieuCaNgay,
            DevExpress.Utils.ImageCollection imageCollection)
        {
            if (_panel == null)
                return;

            Clear();

            Add("In Phiếu", "Print;Size16x16;Colored");
            Add("In Ghép Lot", "Print;Size16x16;Colored");
            Add("In Tách Lot", "Print;Size16x16;Colored");

            if (showHangThieuCaNgay)
                Add("Xem Hàng Thiếu Cả Ngày", "Find;Size16x16;Colored");

            _panel.Buttons.Insert(0, new WindowsUISeparator());

            if (showDocQRCode)
                _panel.Buttons.Insert(0, new WindowsUIButton
                {
                    Caption = "DOC QRCODE",
                    Style = ButtonStyle.PushButton,
                    ImageUri = "IndentIncrease;Size16x16;Colored"
                });

            if (showLayLaiLot)
                _panel.Buttons.Insert(0, new WindowsUIButton
                {
                    Caption = "Lấy Lại Lot",
                    Style = ButtonStyle.PushButton,
                    ImageUri = "IndentIncrease;Size16x16;Colored"
                });

            if (showGhepLot)
            {
                var button = new WindowsUIButton
                {
                    Caption = ghepLotCaption,
                    Style = ButtonStyle.PushButton,
                    Tag = "BTN_GHEPLOT_TOGGLE"
                };

                if (imageCollection != null && imageCollection.Images.Count > 1)
                    button.Image = imageCollection.Images[1];

                _panel.Buttons.Add(button);
            }

            if (showCapNhapKho)
                Add("Cập Nhập Kho", "Save;Size16x16;Colored");

            if (showKiemTraMaNG)
                Add("Kiểm tra mã NG", "SpellCheckAsYouType;Size16x16;Colored");

            if (showStop)
            {
                _panel.Buttons.Add(new WindowsUISeparator());
                Add("Ghi Chú STOP", "Warning;Size16x16;Colored");
                Add("Xóa Ghi Chú STOP", "Clear;Size16x16;Colored");
            }
        }

        public void ConfigurePhieuView(string ghepLotCaption, DevExpress.Utils.ImageCollection imageCollection)
        {
            if (_panel == null)
                return;

            Clear();

            var ghepLot = new WindowsUIButton
            {
                Caption = ghepLotCaption,
                Style = ButtonStyle.PushButton,
                Tag = "BTN_GHEPLOT_TOGGLE"
            };

            if (imageCollection != null && imageCollection.Images.Count > 1)
                ghepLot.Image = imageCollection.Images[1];

            _panel.Buttons.Add(new WindowsUIButton
            {
                Caption = "DOC QRCODE",
                Style = ButtonStyle.PushButton,
                ImageUri = "IndentIncrease;Size16x16;Colored"
            });
            _panel.Buttons.Insert(1, new WindowsUISeparator());
            _panel.Buttons.Add(ghepLot);
            _panel.Buttons.Add(new WindowsUIButton
            {
                Caption = "In Phiếu",
                Style = ButtonStyle.PushButton,
                ImageUri = "Print;Size16x16;Colored"
            });
        }

        public void ConfigureDocQr()
        {
            if (_panel == null)
                return;

            Clear();
            Add("Xóa Dòng Được Chọn", "Delete;Size16x16;Colored");
            Add("Xóa Toàn Bộ Dữ Liệu", "clear;Size16x16;Colored");
            Add("Hoàn Thành", "apply;Size16x16;Colored");
            _panel.Buttons.Insert(2, new WindowsUISeparator());
        }

        public void ConfigureGiaoDb()
        {
            if (_panel == null)
                return;

            Clear();
            Add("Upload Đơn Hàng", "Import;Size16x16;Colored");
            Add("DOC QRCODE", "IndentIncrease;Size16x16;Colored");
            Add("In Phiếu", "Print;Size16x16;Colored");
        }

        public void ConfigureYmvN(bool isLoaiSP)
        {
            if (_panel == null)
                return;

            Clear();
            Add("In Phiếu", "Print;Size16x16;Colored");
            Add(isLoaiSP ? "Đang xem: SP" : "Đang xem: MP", "Refresh;Size16x16;Colored");
        }

        public void UpdateLoaiPhieuCaption(bool isLoaiSP)
        {
            if (_panel == null)
                return;

            foreach (var button in _panel.Buttons)
            {
                var windowsButton = button as WindowsUIButton;
                if (windowsButton == null)
                    continue;

                if (windowsButton.Caption == "Đang xem: MP" || windowsButton.Caption == "Đang xem: SP")
                {
                    windowsButton.Caption = isLoaiSP ? "Đang xem: SP" : "Đang xem: MP";
                    break;
                }
            }
        }

        private void Add(string caption, string imageUri)
        {
            _panel.Buttons.Add(new WindowsUIButton
            {
                Caption = caption,
                Style = ButtonStyle.PushButton,
                ImageUri = imageUri
            });
        }
    }
}
