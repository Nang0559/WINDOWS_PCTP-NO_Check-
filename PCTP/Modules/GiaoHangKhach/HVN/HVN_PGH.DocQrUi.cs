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

            string nhaMay;
            if (_cfg != null && _cfg.Delivery != null && _cfg.Delivery.LoadTheoNgay)
                nhaMay = _cfg.DisplayName;
            else if (_cfg != null && _cfg.Delivery != null && _cfg.Delivery.CoNhieuNhaMay
                     && tabPaneHVN != null && tabPaneHVN.SelectedPage != null)
                nhaMay = tabPaneHVN.SelectedPage.Caption;
            else
                nhaMay = _cfg != null && !string.IsNullOrWhiteSpace(_cfg.DisplayName)
                    ? _cfg.DisplayName
                    : "Nhà máy";

            string gioMoTa = CurrentGioXuat != null ? CurrentGioXuat.MoTa : "";
            string label = _cfg != null && _cfg.Delivery != null
                ? _cfg.Delivery.LabelDocQR
                : "";

            BeginInvoke(new Action(() =>
            {
                SetDocQrDisplayContext(IsLoaiSP, nhaMay, gioMoTa, label);

                // Called after BindDocQRCode + SwitchToDocQRView.
                // At this point the QR session context is fixed.
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

            // Delivery date is part of both MP and SP QR session identity.
            if (dateNX != null)
                dateNX.Enabled = false;

            // MP/SP is part of the session identity too.
            SetLoaiPhieuToggleEnabled(false);

            // Plant is part of both MP and SP identity.
            // Keep both pages visible but disable the TabPane itself so the
            // selected plant cannot be changed during the QR session.
            // This also avoids restoring a stale "hidden page" state after
            // the QR session finishes.
            if (tabPaneHVN != null && tabVP != null && tabHN != null)
            {
                tabVP.PageVisible = true;
                tabHN.PageVisible = true;
                tabPaneHVN.Enabled = false;
            }

            // SP has no hour context. MP keeps only the exact hour used by the QR session.
            LockDocQrHourGroups(isSP ? string.Empty : gioFCC);
        }

        public void UnlockDocQrDeliveryContext()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UnlockDocQrDeliveryContext));
                return;
            }

            if (!_docQrDeliveryContextLocked)
                return;

            _docQrDeliveryContextLocked = false;

            if (dateNX != null)
                dateNX.Enabled = true;

            SetLoaiPhieuToggleEnabled(true);
            UnlockDocQrHourGroups();

            if (tabPaneHVN != null)
                tabPaneHVN.Enabled = true;

            // Restore the visibility required by the customer configuration,
            // not blindly "both visible" (100003/LoadTheoNgay has no plant tabs).
            if (_cfg != null && _cfg.Delivery != null)
            {
                if (_cfg.Delivery.CoNhieuNhaMay)
                {
                    if (tabVP != null) tabVP.PageVisible = true;
                    if (tabHN != null) tabHN.PageVisible = true;
                }
                else if (_cfg.Delivery.CoGear || _cfg.Delivery.LoadTheoNgay)
                {
                    if (tabVP != null) tabVP.PageVisible = false;
                    if (tabHN != null) tabHN.PageVisible = false;
                }
                else
                {
                    if (tabVP != null) tabVP.PageVisible = true;
                    if (tabHN != null) tabHN.PageVisible = false;
                }
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

            XoaToanBoQRClicked -= UnlockDocQrContextOnEvent;
            XoaToanBoQRClicked += UnlockDocQrContextOnEvent;

            // HoanThanhClicked is intentionally NOT used to unlock because the
            // presenter may reject the completion while QR data is still pending.
            // The actual QR grid visibility change is the completion boundary.
            if (gridCtrDOCQrCODE != null)
            {
                gridCtrDOCQrCODE.VisibleChanged -= DocQrGridVisibilityChanged;
                gridCtrDOCQrCODE.VisibleChanged += DocQrGridVisibilityChanged;
            }

            BeginInvoke(new Action(UpdateGridCaptionFromCurrentState));
        }

        private void DocQrGridVisibilityChanged(object sender, EventArgs e)
        {
            if (gridCtrDOCQrCODE != null && !gridCtrDOCQrCODE.Visible)
                UnlockDocQrDeliveryContext();
        }

        private void UnlockDocQrContextOnEvent(object sender, EventArgs e)
        {
            UnlockDocQrDeliveryContext();
        }

        private void GridCaptionContextChanged(object sender, EventArgs e)
        {
            // Khi QR session bị khoá, context không được phép thay đổi;
            // caption vẫn phải phản ánh đúng context đã khoá.
            UpdateGridCaptionFromCurrentState();
        }

        private void UpdateGridCaptionFromCurrentState()
        {
            if (IsDisposed || _phieuGridControl == null)
                return;

            // Customer LoadTheoNgay (ví dụ 100003) không có plant/hour selector.
            // Không được lấy Caption của tab ẩn hoặc giờ còn sót từ customer khác.
            string nhaMay;
            if (_cfg != null && _cfg.Delivery != null && _cfg.Delivery.LoadTheoNgay)
            {
                nhaMay = _cfg.DisplayName;
            }
            else if (_cfg != null && _cfg.Delivery != null && _cfg.Delivery.CoNhieuNhaMay
                     && tabPaneHVN != null && tabPaneHVN.SelectedPage != null)
            {
                nhaMay = tabPaneHVN.SelectedPage.Caption;
            }
            else
            {
                nhaMay = _cfg != null && !string.IsNullOrWhiteSpace(_cfg.DisplayName)
                    ? _cfg.DisplayName
                    : "Nhà máy";
            }

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

            string caption;
            if (_cfg != null && _cfg.Delivery != null && _cfg.Delivery.LoadTheoNgay)
            {
                // Day-based customer: DisplayName is the only valid grid identity.
                caption = plant;
            }
            else
            {
                caption = isSP
                    ? string.Format("{0} - SP (Tất cả ca)", plant)
                    : string.Format("{0} - {1}", plant, gio);
            }

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
