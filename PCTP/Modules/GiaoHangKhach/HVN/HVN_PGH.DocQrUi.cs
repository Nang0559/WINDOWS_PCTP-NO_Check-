using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DevExpress.XtraBars.Navigation;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;

namespace PCTP.Modules.GiaoHangKhach.HVN
{
    public partial class HVN_PGH
    {
        private bool _docQrDeliveryContextLocked;
        private bool _docQrLockedTabVisibilityCaptured;
        private bool _docQrLockedTabVpVisible;
        private bool _docQrLockedTabHnVisible;

        public void SetDocQrScanInputEnabled(bool enabled)
        {
            if (_docQrInputControl == null)
                return;

            _docQrInputControl.Enabled = enabled;

            if (enabled)
                _docQrInputControl.FocusInput();
        }

        public void HideDocQrQuantityEditPanel()
        {
            try
            {
                if (PN_DOCQR_SUASL1 != null)
                    PN_DOCQR_SUASL1.Visible = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[HideDocQrQuantityEditPanel] " + ex.Message);
            }

            if (_phieuBottomStateControl != null)
                _phieuBottomStateControl.HideSuaSoLuong();

            string nhaMay = tabPaneHVN != null && tabPaneHVN.SelectedPage != null
                ? tabPaneHVN.SelectedPage.Caption
                : "Nhà máy";
            string gioMoTa = CurrentGioXuat != null ? CurrentGioXuat.MoTa : "";
            string label = _cfg != null && _cfg.Delivery != null
                ? _cfg.Delivery.LabelDocQR
                : "";

            BeginInvoke(new Action(() =>
            {
                SetDocQrDisplayContext(IsLoaiSP, nhaMay, gioMoTa, label);

                // This method is called after BindDocQRCode + SwitchToDocQRView,
                // therefore the QR session has actually been initialized.
                // Lock exactly the context used by that session.
                LockDocQrDeliveryContext(
                    IsLoaiSP,
                    IsLoaiSP || CurrentGioXuat == null ? string.Empty : CurrentGioXuat.Ma);
            }));
        }

        /// <summary>
        /// MP = plant + delivery date + delivery hour.
        /// SP = plant + delivery date; hour is not a business key.
        /// </summary>
        public void LockDocQrDeliveryContext(bool isSP, string gioFCC)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool, string>(LockDocQrDeliveryContext), isSP, gioFCC);
                return;
            }

            _docQrDeliveryContextLocked = true;

            if (dateNX != null)
                dateNX.Enabled = false;

            // MP/SP is also part of the session identity. Do not allow switching
            // from MP to SP (or vice versa) while QR rows exist.
            SetLoaiPhieuToggleEnabled(false);

            // Plant is part of both MP and SP identity. Keep only the selected
            // plant visible so the user cannot switch the QR session to another plant.
            if (tabPaneHVN != null && tabPaneHVN.SelectedPage != null && tabVP != null && tabHN != null)
            {
                if (!_docQrLockedTabVisibilityCaptured)
                {
                    _docQrLockedTabVpVisible = tabVP.PageVisible;
                    _docQrLockedTabHnVisible = tabHN.PageVisible;
                    _docQrLockedTabVisibilityCaptured = true;
                }

                bool vpSelected = tabPaneHVN.SelectedPage == tabVP;
                tabVP.PageVisible = vpSelected;
                tabHN.PageVisible = !vpSelected;
            }

            // SP has no hour context. Disable all hour choices.
            // MP keeps only the exact hour used to create the QR session.
            LockDocQrHourGroups(isSP ? string.Empty : gioFCC);
        }

        public void UnlockDocQrDeliveryContext()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UnlockDocQrDeliveryContext));
                return;
            }

            if (!_docQrDeliveryContextLocked && !_docQrLockedTabVisibilityCaptured)
                return;

            _docQrDeliveryContextLocked = false;

            if (dateNX != null)
                dateNX.Enabled = true;

            SetLoaiPhieuToggleEnabled(true);
            UnlockDocQrHourGroups();

            if (_docQrLockedTabVisibilityCaptured)
            {
                if (tabVP != null) tabVP.PageVisible = _docQrLockedTabVpVisible;
                if (tabHN != null) tabHN.PageVisible = _docQrLockedTabHnVisible;
                _docQrLockedTabVisibilityCaptured = false;
            }
        }

        private void LockDocQrHourGroups(string gioFCC)
        {
            var gioSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string gio in (gioFCC ?? string.Empty).Split(','))
            {
                string normalized = (gio ?? string.Empty).Trim().Trim('\'');
                if (!string.IsNullOrWhiteSpace(normalized))
                    gioSet.Add(normalized);
            }

            LockDocQrRadioGroup(radioGroup2, gioSet);
            LockDocQrRadioGroup(RDO_GXHN, gioSet);
        }

        private static void LockDocQrRadioGroup(RadioGroup radio, HashSet<string> gioSet)
        {
            if (radio == null)
                return;

            for (int i = 0; i < radio.Properties.Items.Count; i++)
            {
                var item = radio.Properties.Items[i] as RadioGroupItem;
                if (item == null)
                    continue;

                var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string value in (item.AccessibleName ?? string.Empty).Split(','))
                {
                    string v = (value ?? string.Empty).Trim().Trim('\'');
                    if (!string.IsNullOrWhiteSpace(v))
                        normalized.Add(v);
                }

                // SP: empty gioSet => all hour choices disabled.
                // MP: only the exact session hour remains enabled.
                item.Enabled = gioSet.Count > 0 && normalized.SetEquals(gioSet);
            }
        }

        private void UnlockDocQrHourGroups()
        {
            UnlockDocQrRadioGroup(radioGroup2);
            UnlockDocQrRadioGroup(RDO_GXHN);
        }

        private static void UnlockDocQrRadioGroup(RadioGroup radio)
        {
            if (radio == null)
                return;

            for (int i = 0; i < radio.Properties.Items.Count; i++)
            {
                var item = radio.Properties.Items[i] as RadioGroupItem;
                if (item != null)
                    item.Enabled = true;
            }
        }

        private void SetLoaiPhieuToggleEnabled(bool enabled)
        {
            if (_phieuHeaderControl == null || _phieuHeaderControl.ContentControl == null)
                return;

            SetLoaiPhieuToggleEnabledRecursive(_phieuHeaderControl.ContentControl, enabled);
        }

        private static void SetLoaiPhieuToggleEnabledRecursive(Control parent, bool enabled)
        {
            if (parent == null)
                return;

            foreach (Control child in parent.Controls)
            {
                var button = child as Button;
                if (button != null &&
                    (string.Equals(button.Text, "XEM MP", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(button.Text, "XEM SP", StringComparison.OrdinalIgnoreCase)))
                {
                    button.Enabled = enabled;
                }

                SetLoaiPhieuToggleEnabledRecursive(child, enabled);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            GioXuatChanged -= GridCaptionContextChanged;
            GioXuatChanged += GridCaptionContextChanged;

            TabChanged -= GridCaptionContextChanged;
            TabChanged += GridCaptionContextChanged;

            LoaiPhieuChanged -= GridCaptionContextChanged;
            LoaiPhieuChanged += GridCaptionContextChanged;

            // Unlock when the QR session is explicitly cleared or completed.
            XoaToanBoQRClicked -= UnlockDocQrContextOnEvent;
            XoaToanBoQRClicked += UnlockDocQrContextOnEvent;
            HoanThanhClicked -= UnlockDocQrContextOnEvent;
            HoanThanhClicked += UnlockDocQrContextOnEvent;

            BeginInvoke(new Action(UpdateGridCaptionFromCurrentState));
        }

        private void UnlockDocQrContextOnEvent(object sender, EventArgs e)
        {
            UnlockDocQrDeliveryContext();
        }

        private void GridCaptionContextChanged(object sender, EventArgs e)
        {
            // Once QR rows exist, the session context is immutable. Do not
            // allow a header event to rewrite the QR session context.
            if (_docQrDeliveryContextLocked)
                return;

            UpdateGridCaptionFromCurrentState();
        }

        private void UpdateGridCaptionFromCurrentState()
        {
            if (IsDisposed || _phieuGridControl == null)
                return;

            string nhaMay = tabPaneHVN != null && tabPaneHVN.SelectedPage != null
                ? tabPaneHVN.SelectedPage.Caption
                : "Nhà máy";

            string gioMoTa = CurrentGioXuat != null
                ? CurrentGioXuat.MoTa
                : "Tất cả ca";

            string configuredLabel = _cfg != null && _cfg.Delivery != null
                ? _cfg.Delivery.LabelDocQR
                : "";

            SetDocQrDisplayContext(
                IsLoaiSP,
                nhaMay,
                gioMoTa,
                configuredLabel);
        }

        public void SetDocQrDisplayContext(bool isSP, string nhaMay, string gioMoTa, string configuredLabel)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool, string, string, string>(SetDocQrDisplayContext),
                    isSP, nhaMay, gioMoTa, configuredLabel);
                return;
            }

            string plant = string.IsNullOrWhiteSpace(nhaMay)
                ? "Nhà máy"
                : nhaMay.Trim();
            string gio = string.IsNullOrWhiteSpace(gioMoTa)
                ? "Tất cả ca"
                : gioMoTa.Trim();

            string caption = isSP
                ? string.Format("{0} - SP (Tất cả ca)", plant)
                : string.Format("{0} - {1}", plant, gio);

            if (_phieuGridControl != null)
                _phieuGridControl.SetCaption(caption);
            else if (gridBandDH != null)
                gridBandDH.Caption = caption;

            if (lblDocQrcode != null)
            {
                if (isSP)
                {
                    lblDocQrcode.Text =
                        "(Đọc QRCODE: FCC (SP) — KHÔNG ĐỌC TEM KHÁCH HÀNG)";
                }
                else
                {
                    string qrLabel = string.IsNullOrWhiteSpace(configuredLabel)
                        ? "Đọc QRCode theo thứ tự: FCC → KHÁCH HÀNG"
                        : configuredLabel.Trim();

                    if (qrLabel.IndexOf("MP", StringComparison.OrdinalIgnoreCase) < 0)
                        qrLabel += " (MP)";

                    lblDocQrcode.Text = "(" + qrLabel + ")";
                }
            }
        }
    }
}
