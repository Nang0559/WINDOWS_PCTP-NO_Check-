using System;
using System.Windows.Forms;
using PCTP.QRCODE_HVN.PGH.Controls;

namespace PCTP.QRCODE_HVN.PGH
{
    partial class HVN_PGH
    {
        private PhieuGridControl _phieuGridControl;
        private DocQrControl _docQrControl;
        /// <summary>
        /// Phase 9E migration entry point.
        /// Existing GridControl instances are re-parented intact so current
        /// bindings, form-level event handlers and legacy state switching remain unchanged.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            MigratePhieuOrderGridToUserControl();
            MigrateDocQrGridToUserControl();
        }

        private void MigratePhieuOrderGridToUserControl()
        {
            if (_phieuGridControl != null || sidePanel2 == null)
                return;

            object existingDataSource = gridCtrDONHANG != null
                ? gridCtrDONHANG.DataSource
                : null;

            int childIndex = gridCtrDONHANG != null
                ? sidePanel2.Controls.GetChildIndex(gridCtrDONHANG)
                : sidePanel2.Controls.Count;

            _phieuGridControl = new PhieuGridControl();

            if (gridCtrDONHANG != null)
            {
                sidePanel2.Controls.Remove(gridCtrDONHANG);
            }

            sidePanel2.Controls.Add(_phieuGridControl);
            sidePanel2.Controls.SetChildIndex(_phieuGridControl, childIndex);

            if (existingDataSource != null)
            {
                _phieuGridControl.OrderGrid.DataSource = existingDataSource;
            }

            // Compatibility bridge: existing HVN_PGH code keeps using the
            // original field names while the visual owner is PhieuGridControl.
            gridCtrDONHANG = _phieuGridControl.OrderGrid;
            GridViewDONHANG = _phieuGridControl.OrderView;
            gridBandDH = _phieuGridControl.OrderBand;

            // Keep the existing form-level row styling handler unchanged.
            _phieuGridControl.OrderView.RowCellStyle += GridViewDONHANG_RowCellStyle;
        }

        private void MigrateDocQrGridToUserControl()
        {
            if (_docQrControl != null || sidePanel2 == null)
                return;

            object existingDataSource = gridCtrDOCQrCODE != null
                ? gridCtrDOCQrCODE.DataSource
                : null;

            int childIndex = gridCtrDOCQrCODE != null
                ? sidePanel2.Controls.GetChildIndex(gridCtrDOCQrCODE)
                : sidePanel2.Controls.Count;

            _docQrControl = new DocQrControl();

            if (gridCtrDOCQrCODE != null)
            {
                sidePanel2.Controls.Remove(gridCtrDOCQrCODE);
            }

            sidePanel2.Controls.Add(_docQrControl);
            sidePanel2.Controls.SetChildIndex(_docQrControl, childIndex);

            if (existingDataSource != null)
            {
                _docQrControl.QrGrid.DataSource = existingDataSource;
            }

            // Compatibility bridge: existing HVN_PGH code keeps using the
            // original QR grid/view field names while DocQrControl owns them.
            gridCtrDOCQrCODE = _docQrControl.QrGrid;
            gridVDOCQRCODE = _docQrControl.QrView;

            // Preserve the existing form-level focused-row event subscription.
            gridVDOCQRCODE.FocusedRowChanged -= gridVDOCQRCODE_FocusedRowChanged;
            gridVDOCQRCODE.FocusedRowChanged += gridVDOCQRCODE_FocusedRowChanged;
        }

    }
}
