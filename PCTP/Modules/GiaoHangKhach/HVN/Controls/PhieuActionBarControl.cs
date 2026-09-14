using DevExpress.XtraBars.Docking2010;
using DevExpress.XtraEditors;
using System;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.HVN.Controls
{
    public enum PhieuActionBarAction
    {
        None,
        DocQRCode,
        KiemTraGhepLot,
        InPhieu,
        InGhepLot,
        InTachLot,
        CapNhapKho,
        KiemTraMaNG,
        XemHangThieuCaNgay,
        XoaDongQR,
        XoaToanBoQR,
        SuaSoLuongTem,
        LayLaiLot,
        UploadGiaoDB,
        GhiChuStop,
        XoaGhiChuStop,
        HoanThanh,
        HoanThanhYMVN,
        UploadMilkrunSP,
        ToggleLoaiPhieu
    }

    public sealed class PhieuActionBarEventArgs : EventArgs
    {
        public PhieuActionBarAction Action { get; private set; }
        public WindowsUIButton Button { get; private set; }

        public PhieuActionBarEventArgs(PhieuActionBarAction action, WindowsUIButton button)
        {
            Action = action;
            Button = button;
        }
    }

    /// <summary>
    /// Owns the action-bar button configuration and action interpretation.
    /// The legacy WindowsUIButtonPanel is adopted so the WinForms Designer
    /// structure remains unchanged during the refactor.
    /// </summary>
    public sealed class PhieuActionBarControl : XtraUserControl
    {
        private const string ActionTagPrefix = "ACTION:";
        private WindowsUIButtonPanel _panel;
        public PhieuActionBarControl()
        {
            Dock = DockStyle.Fill;
        }
        public WindowsUIButtonPanel Panel { get { return _panel; } }

        public event EventHandler<PhieuActionBarEventArgs> ActionClicked = delegate { };

        public void Adopt(WindowsUIButtonPanel panel)
        {
            if (panel == null || ReferenceEquals(_panel, panel))
                return;

            if (_panel != null)
                _panel.ButtonClick -= Panel_ButtonClick;

            _panel = panel;
            Controls.Add(_panel);
            _panel.Dock = System.Windows.Forms.DockStyle.Fill;
            _panel.ButtonClick += Panel_ButtonClick;
        }

        public void Clear()
        {
            if (_panel == null)
                return;

            _panel.AllowGlyphSkinning = false;
            _panel.Buttons.Clear();
        }

        public void ConfigureNormal(string ghepLotCaption, bool showCapNhapKho, bool showKiemTraMaNG,
            bool showGhepLot, bool showDocQRCode, bool showLayLaiLot, bool showStop,
            bool showHangThieuCaNgay, DevExpress.Utils.ImageCollection imageCollection)
        {
            if (_panel == null) return;
            Clear();
            Add("In Phiếu", "Print;Size16x16;Colored", PhieuActionBarAction.InPhieu);
            Add("In Ghép Lot", "Print;Size16x16;Colored", PhieuActionBarAction.InGhepLot);
            Add("In Tách Lot", "Print;Size16x16;Colored", PhieuActionBarAction.InTachLot);
            if (showHangThieuCaNgay)
                Add("Xem Hàng Thiếu Cả Ngày", "Find;Size16x16;Colored", PhieuActionBarAction.XemHangThieuCaNgay);
            _panel.Buttons.Insert(0, new WindowsUISeparator());
            if (showDocQRCode)
                _panel.Buttons.Insert(0, CreateButton("DOC QRCODE", "IndentIncrease;Size16x16;Colored", PhieuActionBarAction.DocQRCode));
            if (showLayLaiLot)
                _panel.Buttons.Insert(0, CreateButton("Lấy Lại Lot", "IndentIncrease;Size16x16;Colored", PhieuActionBarAction.LayLaiLot));
            if (showGhepLot)
            {
                var button = CreateButton(ghepLotCaption, null, PhieuActionBarAction.KiemTraGhepLot);
                button.Tag = ActionTagPrefix + "GhepLotToggle";
                if (imageCollection != null && imageCollection.Images.Count > 1)
                    button.Image = imageCollection.Images[1];
                _panel.Buttons.Add(button);
            }
            if (showCapNhapKho)
                Add("Cập Nhập Kho", "Save;Size16x16;Colored", PhieuActionBarAction.CapNhapKho);
            if (showKiemTraMaNG)
                Add("Kiểm tra mã NG", "SpellCheckAsYouType;Size16x16;Colored", PhieuActionBarAction.KiemTraMaNG);
            if (showStop)
            {
                _panel.Buttons.Add(new WindowsUISeparator());
                Add("Ghi Chú STOP", "Warning;Size16x16;Colored", PhieuActionBarAction.GhiChuStop);
                Add("Xóa Ghi Chú STOP", "Clear;Size16x16;Colored", PhieuActionBarAction.XoaGhiChuStop);
            }
        }

        public void ConfigurePhieuView(string ghepLotCaption, DevExpress.Utils.ImageCollection imageCollection)
        {
            if (_panel == null) return;
            Clear();
            var ghepLot = CreateButton(ghepLotCaption, null, PhieuActionBarAction.KiemTraGhepLot);
            ghepLot.Tag = ActionTagPrefix + "GhepLotToggle";
            if (imageCollection != null && imageCollection.Images.Count > 1)
                ghepLot.Image = imageCollection.Images[1];
            _panel.Buttons.Add(CreateButton("DOC QRCODE", "IndentIncrease;Size16x16;Colored", PhieuActionBarAction.DocQRCode));
            _panel.Buttons.Insert(1, new WindowsUISeparator());
            _panel.Buttons.Add(ghepLot);
            _panel.Buttons.Add(CreateButton("In Phiếu", "Print;Size16x16;Colored", PhieuActionBarAction.InPhieu));
        }

        public void ConfigureDocQr()
        {
            if (_panel == null) return;
            Clear();
            Add("Xóa Dòng Được Chọn", "Delete;Size16x16;Colored", PhieuActionBarAction.XoaDongQR);
            Add("Xóa Toàn Bộ Dữ Liệu", "clear;Size16x16;Colored", PhieuActionBarAction.XoaToanBoQR);
            Add("Hoàn Thành", "apply;Size16x16;Colored", PhieuActionBarAction.HoanThanh);
            _panel.Buttons.Insert(2, new WindowsUISeparator());
        }

        public void ConfigureGiaoDb()
        {
            if (_panel == null) return;
            Clear();
            Add("Upload Đơn Hàng", "Import;Size16x16;Colored", PhieuActionBarAction.UploadGiaoDB);
            Add("DOC QRCODE", "IndentIncrease;Size16x16;Colored", PhieuActionBarAction.DocQRCode);
            Add("In Phiếu", "Print;Size16x16;Colored", PhieuActionBarAction.InPhieu);
        }

        public void ConfigureYmvN(bool isLoaiSP)
        {
            if (_panel == null) return;
            Clear();
            Add("In Phiếu", "Print;Size16x16;Colored", PhieuActionBarAction.InPhieu);
            Add(isLoaiSP ? "Đang xem: SP" : "Đang xem: MP", "Refresh;Size16x16;Colored", PhieuActionBarAction.ToggleLoaiPhieu);
        }

        public void UpdateLoaiPhieuCaption(bool isLoaiSP)
        {
            if (_panel == null) return;
            foreach (var button in _panel.Buttons)
            {
                var windowsButton = button as WindowsUIButton;
                if (windowsButton == null) continue;
                if (windowsButton.Tag as string == ActionTagPrefix + "ToggleLoaiPhieu")
                {
                    windowsButton.Caption = isLoaiSP ? "Đang xem: SP" : "Đang xem: MP";
                    break;
                }
            }
        }

        private WindowsUIButton CreateButton(string caption, string imageUri, PhieuActionBarAction action)
        {
            var button = new WindowsUIButton
            {
                Caption = caption,
                Style = ButtonStyle.PushButton,
                Tag = ActionTagPrefix + action.ToString()
            };
            if (!string.IsNullOrWhiteSpace(imageUri)) button.ImageUri = imageUri;
            return button;
        }

        private void Add(string caption, string imageUri, PhieuActionBarAction action)
        {
            _panel.Buttons.Add(CreateButton(caption, imageUri, action));
        }

        private void Panel_ButtonClick(object sender, ButtonEventArgs e)
        {
            var button = e.Button as WindowsUIButton;
            if (button == null) return;
            var action = ResolveAction(button);
            if (action == PhieuActionBarAction.None) return;
            ActionClicked.Invoke(this, new PhieuActionBarEventArgs(action, button));
        }

        private PhieuActionBarAction ResolveAction(WindowsUIButton button)
        {
            var tag = button.Tag as string;
            if (string.IsNullOrWhiteSpace(tag) || !tag.StartsWith(ActionTagPrefix, StringComparison.Ordinal))
                return PhieuActionBarAction.None;
            var value = tag.Substring(ActionTagPrefix.Length);
            if (value == "GhepLotToggle") return PhieuActionBarAction.KiemTraGhepLot;
            PhieuActionBarAction action;
            return Enum.TryParse(value, out action) ? action : PhieuActionBarAction.None;
        }
    }
}
