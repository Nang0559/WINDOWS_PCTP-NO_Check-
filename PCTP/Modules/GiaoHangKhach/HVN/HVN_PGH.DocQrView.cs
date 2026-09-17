using System;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.HVN
{
    public partial class HVN_PGH
    {
        /// <summary>
        /// Enables/disables the QR scanner input according to the current DocQR state.
        /// Kept in a separate partial class so the form's generated UI code remains untouched.
        /// </summary>
        public void SetDocQrScanInputEnabled(bool enabled)
        {
            if (_docQrInputControl == null)
                return;

            if (_docQrInputControl.InvokeRequired)
            {
                _docQrInputControl.BeginInvoke(new Action<bool>(SetDocQrScanInputEnabled), enabled);
                return;
            }

            _docQrInputControl.Enabled = enabled;
            if (enabled)
                _docQrInputControl.FocusInput();
        }

        /// <summary>
        /// Hides the quantity-edit panel when entering the DocQR scanning mode.
        /// </summary>
        public void HideDocQrQuantityEditPanel()
        {
            if (PN_DOCQR_SUASL1 == null)
                return;

            if (PN_DOCQR_SUASL1.InvokeRequired)
            {
                PN_DOCQR_SUASL1.BeginInvoke(new Action(HideDocQrQuantityEditPanel));
                return;
            }

            PN_DOCQR_SUASL1.Visible = false;
        }

        /// <summary>
        /// Cập nhật caption của GridBand3 và hướng dẫn đọc QR theo đúng loại phiếu.
        /// MP: FCC -> khách hàng theo giờ.
        /// SP: chỉ đọc FCC, không đọc tem khách hàng, theo ngày + nhà máy + dock.
        /// </summary>
        public void SetDocQrDisplayContext(bool isSP, string nhaMay, string gioMoTa, string configuredLabel)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool, string, string, string>(SetDocQrDisplayContext), isSP, nhaMay, gioMoTa, configuredLabel);
                return;
            }

            string plant = string.IsNullOrWhiteSpace(nhaMay) ? "Nhà máy" : nhaMay.Trim();
            string gio = string.IsNullOrWhiteSpace(gioMoTa) ? "Tất cả ca" : gioMoTa.Trim();

            // gridBandDH chính là GridBand3 trong Designer hiện tại.
            if (gridBandDH != null)
            {
                gridBandDH.Caption = isSP
                    ? string.Format("{0} - SP (Tất cả ca)", plant)
                    : string.Format("{0} - {1}", plant, gio);
            }

            if (lblDocQrcode != null)
            {
                if (isSP)
                {
                    lblDocQrcode.Text = "(Đọc QRCODE: FCC (SP) — KHÔNG ĐỌC TEM KHÁCH HÀNG)";
                }
                else
                {
                    string label = string.IsNullOrWhiteSpace(configuredLabel)
                        ? "Đọc QRCode theo thứ tự: FCC → KHÁCH HÀNG"
                        : configuredLabel.Trim();

                    // Chuẩn hóa label MP để luôn thể hiện rõ loại phiếu.
                    if (label.IndexOf("MP", StringComparison.OrdinalIgnoreCase) < 0)
                        label += " (MP)";

                    lblDocQrcode.Text = "(" + label + ")";
                }
            }
        }
    }
}
