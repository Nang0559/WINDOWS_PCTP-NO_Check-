using DevExpress.XtraBars.Navigation;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using PCTP.Domain.Entities;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.HVN.Controls
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
        private HashSet<string> _deliveredYmvnHours = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _ymvnChecklistLocked;
        private bool _qrDeliveryContextLocked;

        public PhieuHeaderControl()
        {
            Dock = DockStyle.Fill;
            Name = "phieuHeaderControl";
        }

        public Control ContentControl { get { return _content; } }
        public bool IsLoaiSP { get { return _isLoaiSP; } }
        public bool IsQrDeliveryContextLocked { get { return _qrDeliveryContextLocked; } }

        public void SetQrDeliveryContextLocked(bool locked)
        {
            _qrDeliveryContextLocked = locked;

            // The QR session lock is a business-state lock, not only a visual
            // lock. Apply it directly to every header selector so a later
            // DevExpress layout refresh cannot silently re-enable one control.
            var date = FindControl<DateEdit>("dateNX");
            if (date != null)
                date.Enabled = !locked;

            var tabPane = FindControl<TabPane>("tabPaneHVN");
            if (tabPane != null)
                tabPane.Enabled = !locked;

            var radioVp = FindControl<RadioGroup>("radioGroup2");
            if (radioVp != null)
                radioVp.Enabled = !locked;

            var radioHn = FindControl<RadioGroup>("RDO_GXHN");
            if (radioHn != null)
                radioHn.Enabled = !locked;

            // MP/SP is a view mode, not part of the immutable delivery
            // session identity. It must remain switchable while Date/Plant/Hour
            // are locked.
            if (_btnToggleLoaiPhieu != null)
                _btnToggleLoaiPhieu.Enabled = true;

            if (locked)
            {
                LockAllRadioItems();
            }
            else
            {
                UnlockAllRadioItems();
            }
        }

        private void LockAllRadioItems()
        {
            var radioVp = FindControl<RadioGroup>("radioGroup2");
            var radioHn = FindControl<RadioGroup>("RDO_GXHN");

            if (radioVp != null)
                foreach (RadioGroupItem item in radioVp.Properties.Items)
                    item.Enabled = false;

            if (radioHn != null)
                foreach (RadioGroupItem item in radioHn.Properties.Items)
                    item.Enabled = false;
        }

        private void UnlockAllRadioItems()
        {
            var radioVp = FindControl<RadioGroup>("radioGroup2");
            var radioHn = FindControl<RadioGroup>("RDO_GXHN");

            if (radioVp != null)
                foreach (RadioGroupItem item in radioVp.Properties.Items)
                    item.Enabled = true;

            if (radioHn != null)
                foreach (RadioGroupItem item in radioHn.Properties.Items)
                    item.Enabled = true;
        }
        public GioXuat CurrentGioXuat { get; private set; }
        public event EventHandler LoaiPhieuChanged = delegate { };
        public event EventHandler DateChanged = delegate { };
        public event EventHandler GioXuatChanged = delegate { };
        public event EventHandler GioXuatCheckedChanged = delegate { };
        public event EventHandler CheckGX_ItemCheck = delegate { };
        public event EventHandler TabChanged = delegate { };

        public DateTime SelectedDate
        {
            get { var control = FindControl<DateEdit>("dateNX"); return control != null ? control.DateTime : DateTime.MinValue; }
        }

        public int SelectedTabAddNM
        {
            get
            {
                if (_cfg == null || _cfg.Delivery == null) return 1;
                if (!_cfg.Delivery.CoNhieuNhaMay) return _cfg.Delivery.AddNmMacDinh;
                var tabPane = FindControl<TabPane>("tabPaneHVN");
                var tabHn = FindControl<TabNavigationPage>("tabHN");
                return tabPane != null && tabHn != null && tabPane.SelectedPage == tabHn ? 2 : 1;
            }
        }

        public void SetTab(int addNM)
        {
            // Programmatic restoration is still allowed while the QR session
            // is locked. The actual TabPane is disabled by
            // SetQrDeliveryContextLocked(), so the user cannot change it.
            // Blocking SetTab() here also blocked RestoreQrHeaderFromSnapshot()
            // because that restore intentionally runs after the lock is applied.
            var tabPane = FindControl<TabPane>("tabPaneHVN");
            var tabVp = FindControl<TabNavigationPage>("tabVP");
            var tabHn = FindControl<TabNavigationPage>("tabHN");
            if (tabPane == null || tabVp == null || tabHn == null) return;

            // Never hide the other plant page here. SetTab is also used
            // during QR-session restore and DevExpress can automatically move
            // SelectedPage when the current page is hidden. That was allowing
            // the UI to fall back to VP even though ADDNM=2.
            tabVp.PageVisible = true;
            tabHn.PageVisible = true;
            tabPane.SelectedPage = addNM == 2 ? tabHn : tabVp;

            // SetTab() được gọi khi khôi phục session từ TMP/DOCQRCODE.
            // SelectedPage đã thay đổi nhưng event Click của TabPane không
            // nhất thiết chạy theo cùng thứ tự. Phải đồng bộ giờ ngay tại đây
            // để CurrentGioXuat không còn giữ giờ của tab trước.
            TryUpdateCurrentGioXuat();
        }

        /// <summary>
        /// Đồng bộ giờ hiện tại theo đúng nhà máy đang được chọn.
        /// Không lấy CurrentGioXuat của tab trước làm fallback.
        /// </summary>
        public void SyncCurrentGioXuatWithSelectedTab()
        {
            TryUpdateCurrentGioXuat();
        }

        public void BindGioXuatVP(IReadOnlyList<GioXuat> danhSach)
        {
            var radioGroup2 = FindControl<RadioGroup>("radioGroup2");
            if (radioGroup2 == null) return;
            radioGroup2.Properties.Items.Clear();
            for (int i = 0; i < danhSach.Count; i++) { var gio = danhSach[i]; radioGroup2.Properties.Items.Add(new RadioGroupItem(i, gio.MoTa, true, null, gio.Ma)); }
            if (radioGroup2.Properties.Items.Count > 0)
                radioGroup2.EditValue = 0;
        }

        public void BindGioXuatHN(IReadOnlyList<GioXuat> danhSach)
        {
            var radio = FindControl<RadioGroup>("RDO_GXHN");
            if (radio == null) return;
            radio.Properties.Items.Clear();
            for (int i = 0; i < danhSach.Count; i++) { var gio = danhSach[i]; radio.Properties.Items.Add(new RadioGroupItem(i, gio.MoTa, true, null, gio.Ma)); }
            if (radio.Properties.Items.Count > 0)
                radio.EditValue = 0;
        }

        public void LockRadioExcept(string gioFCC)
        {
            var gioSet = new HashSet<string>((gioFCC ?? "").Split(',').Select(g => g.Trim().Trim('\'')), StringComparer.OrdinalIgnoreCase);
            var radioGroup2 = FindControl<RadioGroup>("radioGroup2");
            var rdoGxHn = FindControl<RadioGroup>("RDO_GXHN");
            if (radioGroup2 == null || rdoGxHn == null) return;
            LockRadioGroup(radioGroup2.Properties.Items, gioSet, i => radioGroup2.SelectedIndex = i);
            LockRadioGroup(rdoGxHn.Properties.Items, gioSet, i => rdoGxHn.SelectedIndex = i);
        }

        public void UnlockAllRadio()
        {
            var radioGroup2 = FindControl<RadioGroup>("radioGroup2");
            var rdoGxHn = FindControl<RadioGroup>("RDO_GXHN");
            var tabVP = FindControl<TabNavigationPage>("tabVP");
            var tabHN = FindControl<TabNavigationPage>("tabHN");
            if (radioGroup2 == null || rdoGxHn == null || tabVP == null || tabHN == null) return;
            foreach (RadioGroupItem item in radioGroup2.Properties.Items) item.Enabled = true;
            foreach (RadioGroupItem item in rdoGxHn.Properties.Items) item.Enabled = true;
            tabVP.PageVisible = true;
            tabHN.PageVisible = true;
        }

        private void LockRadioGroup(RadioGroupItemCollection items, HashSet<string> gioSet, Action<int> setIndex)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = (RadioGroupItem)items[i];
                var itemSet = new HashSet<string>((item.AccessibleName ?? "").Split(',').Select(g => g.Trim().Trim('\'')), StringComparer.OrdinalIgnoreCase);
                if (itemSet.SetEquals(gioSet))
                    setIndex(i);

                // Lock the whole selector. The matching item is only selected
                // for visual restore; it must not remain clickable.
                item.Enabled = false;
            }
        }

        /// <summary>
        /// Select the RadioGroup item that contains the concrete TMP delivery hour.
        /// TMP.GIOGIAO is one hour (for example 15), while RadioGroupItem.AccessibleName
        /// can represent a group (for example "'15','16'").
        /// </summary>
        /// <summary>
        /// Compatibility entry point for legacy callers that restore the hour
        /// from DB/TMP. The actual UI selection is resolved from the concrete
        /// delivery hour, so grouped Radio items such as '15','16' are handled
        /// correctly.
        /// </summary>
        public bool UpdateGioXuatFromDB(string gioFCC)
        {
            return SelectGioXuatByConcreteHour(gioFCC);
        }

        public bool SelectGioXuatByConcreteHour(string gioGiao)
        {
            if (string.IsNullOrWhiteSpace(gioGiao))
                return false;

            RadioGroup selectedRadio = ResolveSelectedPlantRadioGroup();
            if (selectedRadio == null)
                return false;

            string target = NormalizeHour(gioGiao);
            if (string.IsNullOrWhiteSpace(target))
                return false;

            _suspendGioXuatChanged = true;
            try
            {
                for (int i = 0; i < selectedRadio.Properties.Items.Count; i++)
                {
                    var item = selectedRadio.Properties.Items[i] as RadioGroupItem;
                    if (item == null)
                        continue;

                    if (!RadioItemContainsHour(item, target))
                        continue;

                    selectedRadio.SelectedIndex = i;
                    CurrentGioXuat = new GioXuat(
                        item.AccessibleName ?? string.Empty,
                        item.Description ?? target + "H");
                    return true;
                }
            }
            finally
            {
                _suspendGioXuatChanged = false;
            }

            return false;
        }

        private static bool RadioItemContainsHour(RadioGroupItem item, string concreteHour)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.AccessibleName))
                return false;

            foreach (string token in item.AccessibleName.Split(new[] { ',', '+', 'H', 'h' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string normalized = NormalizeHour(token);
                if (string.Equals(normalized, concreteHour, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string NormalizeHour(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string s = value.Trim().Trim('\'');
            int hour;
            return int.TryParse(s, out hour) ? hour.ToString("00") : string.Empty;
        }

        private RadioGroup ResolveSelectedPlantRadioGroup()
        {
            if (_cfg == null || _cfg.Delivery == null)
                return FindControl<RadioGroup>("radioGroup2");

            if (!_cfg.Delivery.CoNhieuNhaMay)
                return FindControl<RadioGroup>("radioGroup2");

            var tabPane = FindControl<TabPane>("tabPaneHVN");
            var tabHn = FindControl<TabNavigationPage>("tabHN");

            if (tabPane != null && tabHn != null && tabPane.SelectedPage == tabHn)
                return FindControl<RadioGroup>("RDO_GXHN");

            return FindControl<RadioGroup>("radioGroup2");
        }

        private bool TrySelectRadioFromDb(RadioGroupItemCollection items, HashSet<string> gioSet, string gioFCC, Action<int> setIndex)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = (RadioGroupItem)items[i];
                if (string.IsNullOrEmpty(item.AccessibleName)) continue;
                var itemSet = new HashSet<string>(item.AccessibleName.Split(',').Select(g => g.Trim().Trim('\'')), StringComparer.OrdinalIgnoreCase);
                if (!itemSet.SetEquals(gioSet)) continue;
                _suspendGioXuatChanged = true;
                try { setIndex(i); CurrentGioXuat = new GioXuat(gioFCC, item.Description ?? gioFCC); }
                finally { _suspendGioXuatChanged = false; }
                GioXuatChanged.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        public void LockDatePicker() { var control = FindControl<DateEdit>("dateNX"); if (control != null) control.Enabled = false; }
        public void UnlockDatePicker() { var control = FindControl<DateEdit>("dateNX"); if (control != null) control.Enabled = true; }
        public void SetDate(DateTime date) { var control = FindControl<DateEdit>("dateNX"); if (control == null) return; _suspendDateChanged = true; try { control.DateTime = date; } finally { _suspendDateChanged = false; } }
        public void SuspendGioXuatChanged() { _suspendGioXuatChanged = true; }
        public void ResumeGioXuatChanged() { _suspendGioXuatChanged = false; }

        public void Adopt(Control content)
        {
            if (content == null || ReferenceEquals(_content, content)) return;
            if (_content != null) UnwireHeaderEvents();
            if (_content != null) Controls.Remove(_content);
            _content = content;
            Controls.Add(_content);
            _content.Dock = DockStyle.Fill;
            _content.Margin = new Padding(0);
            WireHeaderEvents();
        }

        public void ConfigureCustomer(CustomerConfig cfg)
        {
            if (cfg == null || cfg.Delivery == null || _content == null) return;
            _cfg = cfg;
            var tabPaneControl = FindControl<TabPane>("tabPaneHVN");
            var tabVpPage = FindControl<TabNavigationPage>("tabVP");
            var tabHnPage = FindControl<TabNavigationPage>("tabHN");
            var radioVp = FindControl<RadioGroup>("radioGroup2");
            var radioHn = FindControl<RadioGroup>("RDO_GXHN");
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            var btnUploadMilkrun = FindControl<SimpleButton>("btnUploadMilkrun");
            if (tabPaneControl == null || tabVpPage == null || tabHnPage == null || radioVp == null || radioHn == null || checkList == null || btnUploadMilkrun == null) return;
            if (cfg.Delivery.CoNhieuNhaMay)
            {
                tabVpPage.PageVisible = true; tabHnPage.PageVisible = true; tabPaneControl.Visible = true; radioVp.Visible = true; radioHn.Visible = true; checkList.Visible = false; btnUploadMilkrun.Visible = false;
                if (cfg.Delivery.CoLoaiSP) ShowLoaiPhieuToggle(btnUploadMilkrun); else HideLoaiPhieuToggle();
            }
            else if (cfg.Delivery.CoGear)
            {
                tabPaneControl.Visible = false; tabVpPage.PageVisible = false; tabHnPage.PageVisible = false; radioVp.Visible = false; radioHn.Visible = false; checkList.Visible = true; checkList.BringToFront(); btnUploadMilkrun.Visible = true; ShowLoaiPhieuToggle(btnUploadMilkrun);
            }
            else if (cfg.Delivery.LoadTheoNgay)
            {
                tabPaneControl.Visible = false; tabVpPage.PageVisible = false; tabHnPage.PageVisible = false; radioVp.Visible = false; radioHn.Visible = false; checkList.Visible = false; btnUploadMilkrun.Text = "Upload PO HTN"; btnUploadMilkrun.Visible = true; HideLoaiPhieuToggle();
            }
            else
            {
                tabVpPage.PageVisible = true; tabHnPage.PageVisible = false; tabPaneControl.TabAlignment = Alignment.Far; tabPaneControl.Visible = true; radioVp.Visible = true; radioHn.Visible = false; checkList.Visible = false; btnUploadMilkrun.Visible = false; HideLoaiPhieuToggle();
            }
        }

        public void ConfigureCustomer(CustomerConfig cfg, Control tabPane, Control tabVP, Control tabHN, Control radioGroup2, Control rdoGxHn, Control checkGx, Button btnUploadMilkrun) { ConfigureCustomer(cfg); }

        public void BindGioXuatCheckList(List<string> danhSachGio)
        {
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList == null) return;
            if (checkList.InvokeRequired) { checkList.Invoke(new Action(() => BindGioXuatCheckList(danhSachGio))); return; }
            UnwireCheckGxEvent();
            checkList.Items.Clear();
            if (danhSachGio != null) foreach (var gio in danhSachGio) checkList.Items.Add(gio, true);
            _deliveredYmvnHours.Clear();
            WireCheckGxEvent();
        }

        public List<string> GetCheckedGioXuat()
        {
            var result = new List<string>();
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList == null) return result;
            foreach (object item in checkList.CheckedItems) result.Add(item.ToString());
            return result;
        }

        public void SetCheckedGiosYMVN(List<string> checkedGios)
        {
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList == null) return;
            _deliveredYmvnHours = new HashSet<string>(checkedGios ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            UnwireCheckGxEvent();
            try
            {
                for (int i = 0; i < checkList.Items.Count; i++)
                {
                    string gio = checkList.Items[i] == null ? string.Empty : checkList.Items[i].ToString();
                    bool delivered = _deliveredYmvnHours.Contains(gio);
                    checkList.SetItemChecked(i, delivered);
                    checkList.Items[i].Enabled = !delivered && !_ymvnChecklistLocked;
                }
            }
            finally { WireCheckGxEvent(); }
        }

        public void MarkYmvnHoursDelivered(List<string> deliveredHours)
        {
            SetCheckedGiosYMVN(deliveredHours);
        }

        public void LockCheckListYMVN()
        {
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList == null) return;
            _ymvnChecklistLocked = true;
            UnwireCheckGxEvent();
            checkList.Enabled = false;
        }

        public void UnlockCheckListYMVN()
        {
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList == null) return;
            _ymvnChecklistLocked = false;
            checkList.Enabled = true;
            UnwireCheckGxEvent();
            try
            {
                for (int i = 0; i < checkList.Items.Count; i++)
                {
                    string gio = checkList.Items[i] == null ? string.Empty : checkList.Items[i].ToString();
                    bool delivered = _deliveredYmvnHours.Contains(gio);
                    checkList.SetItemChecked(i, delivered);
                    checkList.Items[i].Enabled = !delivered;
                }
            }
            finally { WireCheckGxEvent(); }
        }

        private void WireHeaderEvents()
        {
            if (_eventsWired || _content == null) return;
            var date = FindControl<DateEdit>("dateNX");
            var tabPane = FindControl<TabPane>("tabPaneHVN");
            var radioVp = FindControl<RadioGroup>("radioGroup2");
            var radioHn = FindControl<RadioGroup>("RDO_GXHN");
            if (date != null) date.EditValueChanged += HeaderDateChanged;
            if (tabPane != null) tabPane.Click += HeaderTabChanged;
            if (radioVp != null) radioVp.SelectedIndexChanged += HeaderGioXuatChanged;
            if (radioHn != null) radioHn.SelectedIndexChanged += HeaderGioXuatChanged;
            WireCheckGxEvent();
            _eventsWired = true;
        }

        private void UnwireHeaderEvents()
        {
            if (!_eventsWired || _content == null) return;
            var date = FindControl<DateEdit>("dateNX");
            var tabPane = FindControl<TabPane>("tabPaneHVN");
            var radioVp = FindControl<RadioGroup>("radioGroup2");
            var radioHn = FindControl<RadioGroup>("RDO_GXHN");
            if (date != null) date.EditValueChanged -= HeaderDateChanged;
            if (tabPane != null) tabPane.Click -= HeaderTabChanged;
            if (radioVp != null) radioVp.SelectedIndexChanged -= HeaderGioXuatChanged;
            if (radioHn != null) radioHn.SelectedIndexChanged -= HeaderGioXuatChanged;
            UnwireCheckGxEvent();
            _eventsWired = false;
        }

        private void WireCheckGxEvent()
        {
            if (_checkGxEventWired) return;
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList == null) return;
            checkList.ItemCheck += HeaderCheckGxItemCheck;
            _checkGxEventWired = true;
        }

        private void UnwireCheckGxEvent()
        {
            if (!_checkGxEventWired) return;
            var checkList = FindControl<CheckedListBoxControl>("CheckGX");
            if (checkList != null) checkList.ItemCheck -= HeaderCheckGxItemCheck;
            _checkGxEventWired = false;
        }

        private void HeaderCheckGxItemCheck(object sender, DevExpress.XtraEditors.Controls.ItemCheckEventArgs e)
        {
            if (_ymvnChecklistLocked) return;
            var checkList = sender as CheckedListBoxControl;
            if (checkList != null && checkList.IsHandleCreated)
            {
                try { checkList.BeginInvoke(new Action(() => GioXuatCheckedChanged.Invoke(this, EventArgs.Empty))); }
                catch (InvalidOperationException) { GioXuatCheckedChanged.Invoke(this, EventArgs.Empty); }
            }
            else GioXuatCheckedChanged.Invoke(this, EventArgs.Empty);
            CheckGX_ItemCheck.Invoke(this, EventArgs.Empty);
        }

        private void HeaderDateChanged(object sender, EventArgs e)
        {
            if (_suspendDateChanged || _qrDeliveryContextLocked) return;
            DateChanged.Invoke(this, EventArgs.Empty);
        }

        private void HeaderGioXuatChanged(object sender, EventArgs e)
        {
            if (_suspendGioXuatChanged || _qrDeliveryContextLocked) return;
            if (!TryUpdateCurrentGioXuat()) return;
            GioXuatChanged.Invoke(this, EventArgs.Empty);
        }

        private void HeaderTabChanged(object sender, EventArgs e)
        {
            if (_qrDeliveryContextLocked) return;
            // TabPane.Click có thể chạy trước khi SelectedPage được cập nhật.
            // Đọc giờ ngay trong Click sẽ dễ lấy lại giờ của tab cũ (VP -> HN).
            // Đẩy xử lý sang message queue để SelectedPage đã là tab mới.
            if (!IsHandleCreated || IsDisposed)
                return;

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed)
                    return;

                if (!TryUpdateCurrentGioXuat())
                    return;

                TabChanged.Invoke(this, EventArgs.Empty);
            }));
        }

        private bool TryUpdateCurrentGioXuat()
        {
            if (_cfg == null || _cfg.Delivery == null) return false;
            if (!_cfg.Delivery.CoNhieuNhaMay) return TryReadGioXuat(FindControl<RadioGroup>("radioGroup2"));
            var tabPane = FindControl<TabPane>("tabPaneHVN");
            var tabHn = FindControl<TabNavigationPage>("tabHN");
            return tabPane != null && tabHn != null && tabPane.SelectedPage == tabHn ? TryReadGioXuat(FindControl<RadioGroup>("RDO_GXHN")) : TryReadGioXuat(FindControl<RadioGroup>("radioGroup2"));
        }

        private bool TryReadGioXuat(RadioGroup radio)
        {
            if (radio == null) return false;
            int idx = radio.SelectedIndex;
            if (idx < 0 || idx >= radio.Properties.Items.Count) return false;
            var item = radio.Properties.Items[idx] as RadioGroupItem;
            if (item == null) return false;
            string ma = item.AccessibleName ?? "'06'";
            string moTa = item.Description ?? "(6H)";
            CurrentGioXuat = new GioXuat(ma, moTa);
            return true;
        }

        private T FindControl<T>(string name) where T : Control { if (_content == null) return null; return FindControlRecursive<T>(_content, name); }
        private static T FindControlRecursive<T>(Control parent, string name) where T : Control
        {
            if (parent == null) return null;
            foreach (Control child in parent.Controls) { if (child.Name == name) return child as T; T nested = FindControlRecursive<T>(child, name); if (nested != null) return nested; }
            return null;
        }

        private void ShowLoaiPhieuToggle(SimpleButton btnUploadMilkrun)
        {
            if (_btnToggleLoaiPhieu == null)
            {
                _btnToggleLoaiPhieu = new Button { Text = "XEM MP", Width = 100, Height = btnUploadMilkrun.Height, Location = new Point(btnUploadMilkrun.Right + 8, btnUploadMilkrun.Top), BackColor = Color.SteelBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
                _btnToggleLoaiPhieu.Click += BtnToggleLoaiPhieu_Click;
                btnUploadMilkrun.Parent.Controls.Add(_btnToggleLoaiPhieu);
            }
            _btnToggleLoaiPhieu.Visible = true;
            _btnToggleLoaiPhieu.Enabled = !_qrDeliveryContextLocked;
        }

        private void HideLoaiPhieuToggle() { if (_btnToggleLoaiPhieu != null) _btnToggleLoaiPhieu.Visible = false; }

        private void ApplyLoaiPhieuHourVisibility()
        {
            var radioVp = FindControl<RadioGroup>("radioGroup2");
            var radioHn = FindControl<RadioGroup>("RDO_GXHN");

            // 100001 SP is date-only. The MP hour controls remain in the
            // same header/tab area, but are hidden while SP is selected.
            bool showHour = !_isLoaiSP;

            if (radioVp != null)
                radioVp.Visible = showHour;
            if (radioHn != null)
                radioHn.Visible = showHour;
        }

        public void ToggleLoaiPhieu()
        {
            // MP/SP is a separate view/category context. It is intentionally
            // switchable while a QR session is active. The QR lock protects
            // the shared Date + Plant and the MP hour, not this toggle.
            _isLoaiSP = !_isLoaiSP;

            // SP does not own an hour. Clear the in-memory hour immediately;
            // otherwise the previous MP hour (14H/15H) remains in the context.
            if (_isLoaiSP)
                CurrentGioXuat = new GioXuat(string.Empty, string.Empty);

            ApplyLoaiPhieuHourVisibility();

            if (_btnToggleLoaiPhieu != null)
            {
                _btnToggleLoaiPhieu.Text = _isLoaiSP ? "XEM MP" : "XEM SP";
                _btnToggleLoaiPhieu.BackColor =
                    _isLoaiSP ? Color.OrangeRed : Color.SteelBlue;
                _btnToggleLoaiPhieu.Enabled = true;
            }

            LoaiPhieuChanged.Invoke(this, EventArgs.Empty);
        }

        private void BtnToggleLoaiPhieu_Click(object sender, EventArgs e) { ToggleLoaiPhieu(); }
    }
}