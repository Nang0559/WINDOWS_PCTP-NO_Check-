using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.BandedGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.QRCODE_HVN.PGH
{
    partial class HVN_PGH
    {
        // Transitional UI bridge.
        // Legacy HVN_PGH code keeps these names while the actual visual
        // controls are owned by the specialized UserControls.
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
    }
}
