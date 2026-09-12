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
        /// Applies only the customer-dependent presentation rules of the
        /// existing header. Business decisions remain in the presenter/config.
        /// </summary>
        public void ConfigureCustomer(
            CustomerConfig cfg,
            Control tabPane,
            Control tabVP,
            Control tabHN,
            Control radioGroup2,
            Control rdoGxHn,
            Control checkGx,
            Button btnUploadMilkrun)
        {
            if (cfg == null || cfg.Delivery == null)
                return;

            var tabPaneControl = tabPane as DevExpress.XtraBars.Navigation.TabPane;
            var tabVpPage = tabVP as DevExpress.XtraBars.Navigation.NavigationPage;
            var tabHnPage = tabHN as DevExpress.XtraBars.Navigation.NavigationPage;
            var radioVp = radioGroup2 as RadioGroup;
            var radioHn = rdoGxHn as RadioGroup;
            var checkList = checkGx as DevExpress.XtraEditors.CheckedListBoxControl;

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

        private void ShowLoaiPhieuToggle(Button btnUploadMilkrun)
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