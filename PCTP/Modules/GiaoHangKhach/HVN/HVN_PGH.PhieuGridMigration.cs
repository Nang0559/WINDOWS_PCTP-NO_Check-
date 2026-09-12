using System;
using PCTP.QRCODE_HVN.PGH.Controls;

namespace PCTP.QRCODE_HVN.PGH
{
    partial class HVN_PGH
    {
        private PhieuGridControl _phieuGridControl;

        /// <summary>
        /// Phase 9B: the Phiếu/Order grid is now created and owned by
        /// PhieuGridControl. The legacy Designer grid is only used as a
        /// compatibility source during the migration and is removed from
        /// the visual tree before the existing Load handlers continue.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            MigratePhieuOrderGridToUserControl();
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

            // Compatibility bridge: all existing HVN_PGH code keeps using
            // gridCtrDONHANG/GridViewDONHANG/gridBandDH, but those references
            // now point to the controls owned by PhieuGridControl.
            gridCtrDONHANG = _phieuGridControl.OrderGrid;
            GridViewDONHANG = _phieuGridControl.OrderView;
            gridBandDH = _phieuGridControl.OrderBand;

            // Keep the existing form-level row styling handler unchanged.
            _phieuGridControl.OrderView.RowCellStyle += GridViewDONHANG_RowCellStyle;
        }
    }
}
