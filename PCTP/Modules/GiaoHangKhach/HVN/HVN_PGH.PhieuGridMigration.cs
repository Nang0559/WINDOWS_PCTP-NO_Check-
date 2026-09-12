using System;
using System.Windows.Forms;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.BandedGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.QRCODE_HVN.PGH.Controls;

namespace PCTP.QRCODE_HVN.PGH
{
    partial class HVN_PGH
    {
        private PhieuGridControl _phieuGridControl;
        private DocQrControl _docQrControl;
        private PhieuBottomStateControl _phieuBottomStateControl;
        private PhieuHeaderControl _phieuHeaderControl;

        // Compatibility bridge: HVN_PGH business code keeps the legacy member
        // names while the actual visual owner is PhieuBottomStateControl.
        private GridControl gridCLECH { get { return _phieuBottomStateControl.LechGrid; } }
        private GridControl gridCTTGL { get { return _phieuBottomStateControl.GhepLotGrid; } }
        private GridControl gridCtrSUASL { get { return _phieuBottomStateControl.SuaSlGrid; } }
        private GridView gridVSUASL { get { return _phieuBottomStateControl.SuaSlView; } }
        private BandedGridView bandedGridViewLECH { get { return _phieuBottomStateControl.LechView; } }
        private GridBand gridBandLECH { get { return _phieuBottomStateControl.LechBand; } }
        private BandedGridColumn colLechMaHang { get { return _phieuBottomStateControl.LechMaHang; } }
        private BandedGridColumn colLechTenHang { get { return _phieuBottomStateControl.LechTenHang; } }
        private BandedGridColumn colLechSoLuong { get { return _phieuBottomStateControl.LechSoLuong; } }
        private BandedGridColumn colLechNguon { get { return _phieuBottomStateControl.LechNguon; } }
        private BandedGridView GridVTTGL { get { return _phieuBottomStateControl.GhepLotView; } }
        private GridBand gridBand1 { get { return _phieuBottomStateControl.GhepLotBand; } }
        private BandedGridColumn gridColumn4 { get { return _phieuBottomStateControl.GhepLotMaHang; } }
        private BandedGridColumn gridColumn5 { get { return _phieuBottomStateControl.GhepLotGio; } }
        private BandedGridColumn gridColumn6 { get { return _phieuBottomStateControl.GhepLotLot; } }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            MigratePhieuHeaderToUserControl();
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
