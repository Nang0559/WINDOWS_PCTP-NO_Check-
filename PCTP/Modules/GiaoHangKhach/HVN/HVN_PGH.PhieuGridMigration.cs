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
        private PhieuActionBarControl _phieuActionBarControl;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            MigratePhieuHeaderToUserControl();
            MigrateHangThieuToUserControl();
            MigratePhieuOrderGridToUserControl();
            MigrateDocQrGridToUserControl();
            MigratePhieuActionBarToUserControl();
        }

        private int ReplaceControl(Control existing, Control replacement, Control parent)
        {
            if (existing == null || replacement == null || parent == null)
                return -1;

            int childIndex = parent.Controls.GetChildIndex(existing);
            parent.Controls.Remove(existing);
            parent.Controls.Add(replacement);
            parent.Controls.SetChildIndex(replacement, childIndex);
            return childIndex;
        }

        private void MigratePhieuHeaderToUserControl()
        {
            if (_phieuHeaderControl != null || PN_DOCQR_TOP == null || panelPhieu == null)
                return;

            Control parent = panelPhieu.Parent;
            if (parent == null)
                return;

            _phieuHeaderControl = new PhieuHeaderControl();
            ReplaceControl(panelPhieu, _phieuHeaderControl, parent);

            _phieuHeaderControl.Adopt(panelPhieu);
            _phieuHeaderControl.DateChanged += PhieuHeaderControl_DateChanged;
            _phieuHeaderControl.GioXuatChanged += PhieuHeaderControl_GioXuatChanged;
            _phieuHeaderControl.TabChanged += PhieuHeaderControl_TabChanged;
            _phieuHeaderControl.GioXuatCheckedChanged += PhieuHeaderControl_GioXuatCheckedChanged;
            _phieuHeaderControl.CheckGX_ItemCheck += PhieuHeaderControl_CheckGX_ItemCheck;
            _phieuHeaderControl.LoaiPhieuChanged += PhieuHeaderControl_LoaiPhieuChanged;
        }

        private void PhieuHeaderControl_DateChanged(object sender, EventArgs e)
        {
            DateChanged.Invoke(this, EventArgs.Empty);
        }

        private void PhieuHeaderControl_GioXuatChanged(object sender, EventArgs e)
        {
            GioXuatChanged.Invoke(this, EventArgs.Empty);
        }

        private void PhieuHeaderControl_TabChanged(object sender, EventArgs e)
        {
            TabChanged.Invoke(this, EventArgs.Empty);
        }

        private void PhieuHeaderControl_GioXuatCheckedChanged(object sender, EventArgs e)
        {
            GioXuatCheckedChanged.Invoke(this, EventArgs.Empty);
        }

        private void PhieuHeaderControl_CheckGX_ItemCheck(object sender, EventArgs e)
        {
            CheckGX_ItemCheck.Invoke(this, EventArgs.Empty);
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

            _hangThieuControl = new HangThieuControl();
            ReplaceControl(GCT_HT, _hangThieuControl, parent);

            _hangThieuControl.Adopt(GCT_HT);
        }

        private void MigratePhieuOrderGridToUserControl()
        {
            if (_phieuGridControl != null || sidePanel2 == null)
                return;

            object existingDataSource = gridCtrDONHANG != null
                ? gridCtrDONHANG.DataSource
                : null;

            _phieuGridControl = new PhieuGridControl();

            if (gridCtrDONHANG != null)
                ReplaceControl(gridCtrDONHANG, _phieuGridControl, sidePanel2);
            else
                sidePanel2.Controls.Add(_phieuGridControl);

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

            _docQrControl = new DocQrControl();

            if (gridCtrDOCQrCODE != null)
                ReplaceControl(gridCtrDOCQrCODE, _docQrControl, sidePanel2);
            else
                sidePanel2.Controls.Add(_docQrControl);

            if (existingDataSource != null)
                _docQrControl.QrGrid.DataSource = existingDataSource;

            gridCtrDOCQrCODE = _docQrControl.QrGrid;
            gridVDOCQRCODE = _docQrControl.QrView;
            gridVDOCQRCODE.FocusedRowChanged -= gridVDOCQRCODE_FocusedRowChanged;
            gridVDOCQRCODE.FocusedRowChanged += gridVDOCQRCODE_FocusedRowChanged;
        }

        private void MigratePhieuActionBarToUserControl()
        {
            if (_phieuActionBarControl != null || sidePanel3 == null || UIButton == null)
                return;

            Control parent = UIButton.Parent;
            if (parent == null)
                return;

            _phieuActionBarControl = new PhieuActionBarControl();
            ReplaceControl(UIButton, _phieuActionBarControl, parent);
            _phieuActionBarControl.Adopt(UIButton);
        }
    }
}
