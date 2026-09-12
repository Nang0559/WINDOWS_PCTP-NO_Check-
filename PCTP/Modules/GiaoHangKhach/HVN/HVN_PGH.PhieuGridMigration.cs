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
        private PhieuHeaderControl _phieuHeaderControl;
        private HangThieuControl _hangThieuControl;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            MigratePhieuHeaderToUserControl();
            MigrateHangThieuToUserControl();
            MigratePhieuOrderGridToUserControl();
            MigrateDocQrGridToUserControl();
        }

        private void MigratePhieuHeaderToUserControl()
        {
            if (_phieuHeaderControl != null || PN_DOCQR_TOP == null || panelPhieu == null)
                return;

            Control parent = panelPhieu.Parent;
            if (parent == null)
                return;

            int childIndex = parent.Controls.GetChildIndex(panelPhieu);

            _phieuHeaderControl = new PhieuHeaderControl();

            parent.Controls.Remove(panelPhieu);
            parent.Controls.Add(_phieuHeaderControl);
            parent.Controls.SetChildIndex(_phieuHeaderControl, childIndex);

            _phieuHeaderControl.Adopt(panelPhieu);
            _phieuHeaderControl.LoaiPhieuChanged += PhieuHeaderControl_LoaiPhieuChanged;
        }

        private void PhieuHeaderControl_LoaiPhieuChanged(object sender, EventArgs e)
        {
            LoaiPhieuChanged.Invoke(this, e);
        }

        private void MigrateHangThieuToUserControl()
        {
            if (_hangThieuControl != null || GCT_HT == null)
                return;

            Control parent = GCT_HT.Parent;
            if (parent == null)
                return;

            int childIndex = parent.Controls.GetChildIndex(GCT_HT);

            _hangThieuControl = new HangThieuControl();

            parent.Controls.Remove(GCT_HT);
            parent.Controls.Add(_hangThieuControl);
            parent.Controls.SetChildIndex(_hangThieuControl, childIndex);

            // Adopt the existing Designer grid instead of rebuilding it.
            // This preserves every column/view setting and keeps legacy code
            // that still references GCT_HT valid during the transition.
            _hangThieuControl.Adopt(GCT_HT);
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
                sidePanel2.Controls.Remove(gridCtrDONHANG);

            sidePanel2.Controls.Add(_phieuGridControl);
            sidePanel2.Controls.SetChildIndex(_phieuGridControl, childIndex);

            if (existingDataSource != null)
                _phieuGridControl.OrderGrid.DataSource = existingDataSource;

            gridCtrDONHANG = _phieuGridControl.OrderGrid;
            GridViewDONHANG = _phieuGridControl.OrderView;
            gridBandDH = _phieuGridControl.OrderBand;
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
                sidePanel2.Controls.Remove(gridCtrDOCQrCODE);

            sidePanel2.Controls.Add(_docQrControl);
            sidePanel2.Controls.SetChildIndex(_docQrControl, childIndex);

            if (existingDataSource != null)
                _docQrControl.QrGrid.DataSource = existingDataSource;

            gridCtrDOCQrCODE = _docQrControl.QrGrid;
            gridVDOCQRCODE = _docQrControl.QrView;
            gridVDOCQRCODE.FocusedRowChanged -= gridVDOCQRCODE_FocusedRowChanged;
            gridVDOCQRCODE.FocusedRowChanged += gridVDOCQRCODE_FocusedRowChanged;
        }
    }
}
