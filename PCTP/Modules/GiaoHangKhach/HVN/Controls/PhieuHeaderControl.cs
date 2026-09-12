using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraBars.Navigation;
using DevExpress.XtraEditors;
using PCTP.Shared.Models;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// Visual and customer-specific UI boundary for the delivery-form header.
    /// The existing panel is adopted intact so the migration does not rebuild
    /// the legacy layout or change its child controls.
    /// </summary>
    public sealed class PhieuHeaderControl : XtraUserControl
    {
        private Control _content;
        private Button _btnToggleLoaiPhieu;
        private bool _isLoaiSP;

        public PhieuHeaderControl()
        {
            Dock = DockStyle.Fill;
            Name = "phieuHeaderControl";
        }

        public Control ContentControl
        {
            get { return _content; }
        }

        public bool IsLoaiSP
        {
            get { return _isLoaiSP; }
        }

        public event EventHandler LoaiPhieuChanged = delegate { };

        public void Adopt(Control content)
        {
            if (content == null || ReferenceEquals(_content, content))
                return;

            if (_content != null)
                Controls.Remove(_content);

            _content = content;
            Controls.Add(_content);
            _content.Dock = DockStyle.Fill;
            _content.Margin = new Padding(0);
        }

        /// <summary>
        /// Applies customer-dependent presentation rules to the controls
        /// already adopted from the legacy header panel.
        /// </summary>
        public void ConfigureCustomer(CustomerConfig cfg)
        {
            if (cfg == null || cfg.Delivery == null || _content == null)
                return;

            var tabPaneControl = FindControl<TabPane>("tabPaneHVN");
            var tabVpPage = FindControl<NavigationPage>("tabVP");
            var tabHnPage = FindControl<NavigationPage>("tabHN");
            var radioVp = FindControl<RadioGroup>("radioGroup2");
            var radioHn = FindControl<RadioGroup>("RDO_GXHN");
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            var btnUploadMilkrun = FindControl<SimpleButton>("btnUploadMilkrun");

            if (tabPaneControl == null || tabVpPage == null || tabHnPage == null ||
                radioVp == null || radioHn == null || checkList == null ||
                btnUploadMilkrun == null)
                return;

            if (cfg.Delivery.CoNhieuNhaMay)
            {
                tabVpPage.PageVisible = true;
                tabHnPage.PageVisible = true;
                tabPaneControl.Visible = true;
                radioVp.Visible = true;
                radioHn.Visible = true;
                checkList.Visible = false;
                btnUploadMilkrun.Visible = false;
                HideLoaiPhieuToggle();
            }
            else if (cfg.Delivery.CoGear)
            {
                tabPaneControl.Visible = false;
                tabVpPage.PageVisible = false;
                tabHnPage.PageVisible = false;
                radioVp.Visible = false;
                radioHn.Visible = false;
                checkList.Visible = true;
                checkList.BringToFront();
                btnUploadMilkrun.Visible = true;
                ShowLoaiPhieuToggle(btnUploadMilkrun);
            }
            else if (cfg.Delivery.LoadTheoNgay)
            {
                tabPaneControl.Visible = false;
                tabVpPage.PageVisible = false;
                tabHnPage.PageVisible = false;
                radioVp.Visible = false;
                radioHn.Visible = false;
                checkList.Visible = false;
                btnUploadMilkrun.Text = "Upload PO HTN";
                btnUploadMilkrun.Visible = true;
                HideLoaiPhieuToggle();
            }
            else
            {
                tabVpPage.PageVisible = true;
                tabHnPage.PageVisible = false;
                tabPaneControl.TabAlignment = Alignment.Far;
                tabPaneControl.Visible = true;
                radioVp.Visible = true;
                radioHn.Visible = false;
                checkList.Visible = false;
                btnUploadMilkrun.Visible = false;
                HideLoaiPhieuToggle();
            }
        }

        private T FindControl<T>(string name) where T : Control
        {
            if (_content == null)
                return null;

            return FindControlRecursive<T>(_content, name);
        }

        private static T FindControlRecursive<T>(Control parent, string name) where T : Control
        {
            if (parent == null)
                return null;

            foreach (Control child in parent.Controls)
            {
                if (child.Name == name)
                    return child as T;

                T nested = FindControlRecursive<T>(child, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private void ShowLoaiPhieuToggle(SimpleButton btnUploadMilkrun)
        {
            if (_btnToggleLoaiPhieu == null)
            {
                _btnToggleLoaiPhieu = new Button
                {
                    Text = "Xem: MP",
                    Width = 100,
                    Height = btnUploadMilkrun.Height,
                    Location = new Point(btnUploadMilkrun.Right + 8, btnUploadMilkrun.Top),
                    BackColor = Color.SteelBlue,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Arial", 9, FontStyle.Bold)
                };
                _btnToggleLoaiPhieu.Click += BtnToggleLoaiPhieu_Click;
                btnUploadMilkrun.Parent.Controls.Add(_btnToggleLoaiPhieu);
            }

            _btnToggleLoaiPhieu.Visible = true;
        }

        private void HideLoaiPhieuToggle()
        {
            if (_btnToggleLoaiPhieu != null)
                _btnToggleLoaiPhieu.Visible = false;
        }

        private void BtnToggleLoaiPhieu_Click(object sender, EventArgs e)
        {
            _isLoaiSP = !_isLoaiSP;
            _btnToggleLoaiPhieu.Text = _isLoaiSP ? "Xem: SP" : "Xem: MP";
            _btnToggleLoaiPhieu.BackColor = _isLoaiSP
                ? Color.OrangeRed
                : Color.SteelBlue;
            LoaiPhieuChanged.Invoke(this, EventArgs.Empty);
        }
    }
}