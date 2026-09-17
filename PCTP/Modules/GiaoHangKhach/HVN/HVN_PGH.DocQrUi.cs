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

        /// <summary>
        /// Disables/enables the physical QR input while the presenter controls scan processing.
        /// </summary>
        public void SetDocQrScanInputEnabled(bool enabled)
        {
            if (_docQrInputControl == null)
                return;

            _docQrInputControl.Enabled = enabled;

            if (enabled)
                _docQrInputControl.FocusInput();
        }

        /// <summary>
        /// Quantity correction is not part of the QR workflow.
        /// Keep the legacy quantity-edit panel hidden.
        /// Also reapplies the QR display context after SwitchToDocQRView().
        /// </summary>
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
                SetDocQrDisplayContext(IsLoaiSP, nhaMay, gioMoTa, label)));
        }

        /// <summary>
        /// Locks the exact delivery context used to create the QR session.
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

            // The delivery date is part of both MP and SP QR session identity.
            if (dateNX != null)
                dateNX.Enabled = false;

            // The MP/SP mode itself is part of the QR session identity.
            SetLoaiPhieuToggleEnabled(false);

            // Plant is part of both MP and SP identity. Keep the selected plant,
            // but make the other plant unavailable while QR data is being read.
            if (tabPaneHVN != null && tabPaneHVN.SelectedPage != null)
            {
                if (!_docQrLockedTabVisibilityCaptured)
                {
                    _docQrLockedTabVpVisible = tabVP != null && tabVP.PageVisible;
                    _docQrLockedTabHnVisible = tabHN != null && tabHN.PageVisible;
                    _docQrLockedTabVisibilityCaptured = true;
                }

                if (tabVP != null && tabHN != null)
                {
                    bool vpSelected = tabPaneHVN.SelectedPage == tabVP;
                    tabVP.PageVisible = vpSelected;
                    tabHN.PageVisible = !vpSelected;
                }
            }

            // Hour is a key only for MP. SP has no selectable hour.
            LockDocQrHourGroups(isSP ? string.Empty : gioFCC);
        }

        /// <summary>
        /// Releases the QR session context lock after QR data is cleared/completed.
        /// </summary>
        public void UnlockDocQrDeliveryContext()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UnlockDocQrDeliveryContext));
                return;
            }

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
            var gioSet = new HashSet<string>(
                (gioFCC ?? string.Empty).Split(','),
                StringComparer.OrdinalIgnoreCase);

            foreach (string gio in new List<string>(gioSet))
            {
                // Normalize the same representation used by RadioGroupItem.AccessibleName.
                gioSet.Remove(gio);
                gioSet.Add(gio.Trim().Trim('\''));
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

                var itemSet = new HashSet<string>(
                    (item.AccessibleName ?? string.Empty).Split(','),
                    StringComparer.OrdinalIgnoreCase);

                var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string value in itemSet)
                    normalized.Add((value ?? string.Empty).Trim().Trim('\''));

                bool selected = !string.IsNullOrWhiteSpace(string.Join(",", gioSet))
                    && normalized.SetEquals(gioSet);

                // SP: gioSet is empty, therefore all hour choices are disabled.
                // MP: only the session's hour remains enabled.
                item.Enabled = selected;
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

        /// <summary>
        /// Hooks the real header state-change events once the form is shown.
        /// GridBand3 must be correct immediately when the Phiếu screen opens and
        /// must change again when the user changes hour, plant or MP/SP.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            GioXuatChanged -= GridCaptionContextChanged;
            GioXuatChanged += GridCaptionContextChanged;

            TabChanged -= GridCaptionContextChanged;
            TabChanged += GridCaptionContextChanged;

            LoaiPhieuChanged -= GridCaptionContextChanged;
            LoaiPhieuChanged += GridCaptionContextChanged;

            BeginInvoke(new Action(UpdateGridCaptionFromCurrentState));
        }

        private void GridCaptionContextChanged(object sender, EventArgs e)
        {
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

        /// <summary>
        /// Updates the actual order-grid band and the QR instruction according to
        /// the current plant, selected hour and order category.
        ///
        /// MP: plant + selected hour; FCC -> customer label.
        /// SP: plant + all-day; FCC only, customer label is not required.
        /// </summary>
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
            {
                _phieuGridControl.SetCaption(caption);
            }
            else if (gridBandDH != null)
            {
                gridBandDH.Caption = caption;
            }

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
