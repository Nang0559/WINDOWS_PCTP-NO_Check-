using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraBars.Navigation;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
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
        private bool _eventsWired;
        private bool _checkGxEventWired;
        private bool _suspendDateChanged;
        private bool _suspendGioXuatChanged;
        private CustomerConfig _cfg;

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

        public GioXuat CurrentGioXuat { get; private set; }

        public event EventHandler LoaiPhieuChanged = delegate { };
        public event EventHandler DateChanged = delegate { };
        public event EventHandler GioXuatChanged = delegate { };
        public event EventHandler GioXuatCheckedChanged = delegate { };
        public event EventHandler CheckGX_ItemCheck = delegate { };
        public event EventHandler TabChanged = delegate { };

        public void SetDate(DateTime date)
        {
            var control = FindControl<DateEdit>("dateNX");
            if (control == null)
                return;

            _suspendDateChanged = true;
            try { control.DateTime = date; }
            finally { _suspendDateChanged = false; }
        }

        public void SuspendGioXuatChanged()
        {
            _suspendGioXuatChanged = true;
        }

        public void ResumeGioXuatChanged()
        {
            _suspendGioXuatChanged = false;
        }

        public void Adopt(Control content)
        {
            if (content == null || ReferenceEquals(_content, content))
                return;

            if (_content != null)
                UnwireHeaderEvents();

            if (_content != null)
                Controls.Remove(_content);

            _content = content;
            Controls.Add(_content);
            _content.Dock = DockStyle.Fill;
            _content.Margin = new Padding(0);

            WireHeaderEvents();
        }

        /// <summary>
        /// New boundary API. The header owns discovery of its adopted controls.
        /// </summary>
        public void ConfigureCustomer(CustomerConfig cfg)
        {
            if (cfg == null || cfg.Delivery == null || _content == null)
                return;

            _cfg = cfg;

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

        /// <summary>
        /// Compatibility overload for HVN_PGH during the incremental migration.
        /// The legacy parameters are intentionally ignored because this control
        /// now resolves the adopted header controls itself.
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
            ConfigureCustomer(cfg);
        }

        public void BindGioXuatCheckList(List<string> danhSachGio)
        {
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList == null)
                return;

            if (checkList.InvokeRequired)
            {
                checkList.Invoke(new Action(() => BindGioXuatCheckList(danhSachGio)));
                return;
            }

            UnwireCheckGxEvent();
            checkList.Items.Clear();

            if (danhSachGio != null)
            {
                foreach (var gio in danhSachGio)
                    checkList.Items.Add(gio, true);
            }

            WireCheckGxEvent();
        }

        public List<string> GetCheckedGioXuat()
        {
            var result = new List<string>();
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList == null)
                return result;

            foreach (object item in checkList.CheckedItems)
                result.Add(item.ToString());

            return result;
        }

        public void SetCheckedGiosYMVN(List<string> checkedGios)
        {
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList == null)
                return;

            var selected = new HashSet<string>(
                checkedGios ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            UnwireCheckGxEvent();
            try
            {
                for (int i = 0; i < checkList.Items.Count; i++)
                {
                    object item = checkList.Items[i];
                    bool isChecked = selected.Contains(item == null ? string.Empty : item.ToString());
                    checkList.SetItemChecked(i, isChecked);
                }
            }
            finally
            {
                WireCheckGxEvent();
            }
        }

        public void LockCheckListYMVN()
        {
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList == null)
                return;

            UnwireCheckGxEvent();
            checkList.Enabled = false;
        }

        public void UnlockCheckListYMVN()
        {
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList == null)
                return;

            checkList.Enabled = true;
            WireCheckGxEvent();
        }

        private void WireHeaderEvents()
        {
            if (_eventsWired || _content == null)
                return;

            var date = FindControl<DateEdit>("dateNX");
            var tabPane = FindControl<TabPane>("tabPaneHVN");
            var radioVp = FindControl<RadioGroup>("radioGroup2");
            var radioHn = FindControl<RadioGroup>("RDO_GXHN");

            if (date != null)
                date.EditValueChanged += HeaderDateChanged;
            if (tabPane != null)
                tabPane.Click += HeaderTabChanged;
            if (radioVp != null)
                radioVp.SelectedIndexChanged += HeaderGioXuatChanged;
            if (radioHn != null)
                radioHn.SelectedIndexChanged += HeaderGioXuatChanged;

            WireCheckGxEvent();
            _eventsWired = true;
        }

        private void UnwireHeaderEvents()
        {
            if (!_eventsWired || _content == null)
                return;

            var date = FindControl<DateEdit>("dateNX");
            var tabPane = FindControl<TabPane>("tabPaneHVN");
            var radioVp = FindControl<RadioGroup>("radioGroup2");
            var radioHn = FindControl<RadioGroup>("RDO_GXHN");

            if (date != null)
                date.EditValueChanged -= HeaderDateChanged;
            if (tabPane != null)
                tabPane.Click -= HeaderTabChanged;
            if (radioVp != null)
                radioVp.SelectedIndexChanged -= HeaderGioXuatChanged;
            if (radioHn != null)
                radioHn.SelectedIndexChanged -= HeaderGioXuatChanged;

            UnwireCheckGxEvent();
            _eventsWired = false;
        }

        private void WireCheckGxEvent()
        {
            if (_checkGxEventWired)
                return;

            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList == null)
                return;

            checkList.ItemCheck += HeaderCheckGxItemCheck;
            _checkGxEventWired = true;
        }

        private void UnwireCheckGxEvent()
        {
            if (!_checkGxEventWired)
                return;

            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList != null)
                checkList.ItemCheck -= HeaderCheckGxItemCheck;

            _checkGxEventWired = false;
        }

        private void HeaderCheckGxItemCheck(object sender, ItemCheckEventArgs e)
        {
            var checkList = sender as CheckedListBoxControl;
            if (checkList != null && checkList.IsHandleCreated)
            {
                try
                {
                    checkList.BeginInvoke(new Action(() =>
                        GioXuatCheckedChanged.Invoke(this, EventArgs.Empty)));
                }
                catch (InvalidOperationException)
                {
                    GioXuatCheckedChanged.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                GioXuatCheckedChanged.Invoke(this, EventArgs.Empty);
            }

            CheckGX_ItemCheck.Invoke(this, EventArgs.Empty);
        }

        private void HeaderDateChanged(object sender, EventArgs e)
        {
            if (_suspendDateChanged)
                return;
            DateChanged.Invoke(this, EventArgs.Empty);
        }

        private void HeaderGioXuatChanged(object sender, EventArgs e)
        {
            if (_suspendGioXuatChanged)
                return;

            if (!TryUpdateCurrentGioXuat())
                return;

            GioXuatChanged.Invoke(this, EventArgs.Empty);
        }

        private void HeaderTabChanged(object sender, EventArgs e)
        {
            TabChanged.Invoke(this, EventArgs.Empty);
        }

        private bool TryUpdateCurrentGioXuat()
        {
            if (_cfg == null || _cfg.Delivery == null)
                return false;

            if (!_cfg.Delivery.CoNhieuNhaMay)
                return TryReadGioXuat(FindControl<RadioGroup>("radioGroup2"));

            var tabPane = FindControl<TabPane>("tabPaneHVN");
            var tabHn = FindControl<NavigationPage>("tabHN");
            return tabPane != null && tabHn != null && tabPane.SelectedPage == tabHn
                ? TryReadGioXuat(FindControl<RadioGroup>("RDO_GXHN"))
                : TryReadGioXuat(FindControl<RadioGroup>("radioGroup2"));
        }

        private bool TryReadGioXuat(RadioGroup radio)
        {
            if (radio == null)
                return false;

            int idx = radio.SelectedIndex;
            if (idx < 0 || idx >= radio.Properties.Items.Count)
                return false;

            var item = radio.Properties.Items[idx] as RadioGroupItem;
            if (item == null)
                return false;

            string ma = item.AccessibleName ?? "'06'";
            string moTa = item.Description ?? "(6H)";
            CurrentGioXuat = new GioXuat(ma, moTa);
            return true;
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