using System;

namespace PCTP.Modules.GiaoHangKhach.HVN
{
    public partial class HVN_PGH
    {
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

            // SwitchToDocQRView() currently uses BeginInvoke to restore the view.
            // Reapply this context after that UI transition so the old designer caption
            // and old QR instruction cannot overwrite the current SP/MP context.
            BeginInvoke(new Action(() =>
                SetDocQrDisplayContext(IsLoaiSP, nhaMay, gioMoTa, label)));
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

            // The order grid was migrated into PhieuGridControl. The control owns
            // the real GridBand instance shown on screen, so update it through its
            // boundary instead of relying only on the legacy parent-field reference.
            if (_phieuGridControl != null)
            {
                _phieuGridControl.SetCaption(caption);
                _phieuGridControl.OrderView.LayoutChanged();
            }
            else if (gridBandDH != null)
            {
                // Fallback for the short period before the grid migration is ready.
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
