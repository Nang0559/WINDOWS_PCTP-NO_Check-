using System;
using System.Windows.Forms;
using PCTP.QRCODE_HVN.PGH.Controls;

namespace PCTP.QRCODE_HVN.PGH
{
    partial class HVN_PGH
    {
        private PhieuGridControl _phieuGridControl;
        private DocQrControl _docQrControl;
        private PhieuBottomStateControl _phieuBottomStateControl;

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
            MigrateSidePanel4ToUserControl();
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

        /// <summary>
        /// Replaces the legacy sidePanel4 visual container with a UserControl
        /// boundary while deliberately keeping all three legacy grids under
        /// the SAME immediate parent.
        ///
        /// This is the critical compatibility point: HVN_PGH already switches
        /// between Lệch IFS / Ghép Lot / Sửa Số Lượng by setting Visible and
        /// calling BringToFront() on the grid fields. Re-parenting all three
        /// grids into one UserControl means those calls keep exactly the same
        /// z-order semantics.
        /// </summary>
        private void MigrateSidePanel4ToUserControl()
        {
            if (_phieuBottomStateControl != null || sidePanel3 == null || sidePanel4 == null)
                return;

            int childIndex = sidePanel3.Controls.GetChildIndex(sidePanel4);
            int width = sidePanel4.Width;

            _phieuBottomStateControl = new PhieuBottomStateControl
            {
                Dock = DockStyle.Left,
                Width = width,
                Name = "phieuBottomStateControl",
                Margin = sidePanel4.Margin
            };

            // Adopt the existing Designer-created controls instead of creating
            // new grids. DataSource, GridView instances and event subscriptions
            // therefore remain intact.
            _phieuBottomStateControl.Adopt(
                gridCLECH,
                gridCTTGL,
                gridCtrSUASL,
                gridVSUASL);

            // Replace the old sidePanel4 at the exact same position in sidePanel3.
            sidePanel3.Controls.Remove(sidePanel4);
            sidePanel3.Controls.Add(_phieuBottomStateControl);
            sidePanel3.Controls.SetChildIndex(_phieuBottomStateControl, childIndex);

            // Compatibility bridge: fields remain unchanged because the actual
            // GridControl instances were only re-parented, not recreated.
            gridCLECH = _phieuBottomStateControl.LechGrid;
            gridCTTGL = _phieuBottomStateControl.GhepLotGrid;
            gridCtrSUASL = _phieuBottomStateControl.SuaSlGrid;
            gridVSUASL = _phieuBottomStateControl.SuaSlView;

            // Keep the legacy container out of the visual tree. It remains owned
            // by the Designer until Phase 9F removes the old declarations.
            sidePanel4.Visible = false;
        }
    }
}
