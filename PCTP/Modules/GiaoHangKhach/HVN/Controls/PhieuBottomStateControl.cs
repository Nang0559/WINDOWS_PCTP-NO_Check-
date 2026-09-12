using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.BandedGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    public sealed class PhieuBottomStateControl : XtraUserControl
    {
        private readonly GridControl gridCLECH;
        private readonly BandedGridView bandedGridViewLECH;
        private readonly BandedGridColumn colLechMaHang, colLechTenHang, colLechSoLuong, colLechNguon;
        private readonly GridBand gridBandLECH;
        private readonly GridControl gridCTTGL;
        private readonly BandedGridView GridVTTGL;
        private readonly GridBand gridBand1;
        private readonly BandedGridColumn gridColumn4, gridColumn5, gridColumn6;
        private readonly GridControl gridCtrSUASL;
        private readonly GridView gridVSUASL;

        public GridControl LechGrid { get { return gridCLECH; } }
        public GridControl GhepLotGrid { get { return gridCTTGL; } }
        public GridControl SuaSlGrid { get { return gridCtrSUASL; } }
        public GridView SuaSlView { get { return gridVSUASL; } }
        public BandedGridView LechView { get { return bandedGridViewLECH; } }
        public GridBand LechBand { get { return gridBandLECH; } }
        public BandedGridColumn LechMaHang { get { return colLechMaHang; } }
        public BandedGridColumn LechTenHang { get { return colLechTenHang; } }
        public BandedGridColumn LechSoLuong { get { return colLechSoLuong; } }
        public BandedGridColumn LechNguon { get { return colLechNguon; } }
        public BandedGridView GhepLotView { get { return GridVTTGL; } }
        public GridBand GhepLotBand { get { return gridBand1; } }
        public BandedGridColumn GhepLotMaHang { get { return gridColumn4; } }
        public BandedGridColumn GhepLotGio { get { return gridColumn5; } }
        public BandedGridColumn GhepLotLot { get { return gridColumn6; } }

        public PhieuBottomStateControl()
        {
            gridCLECH = new GridControl();
            bandedGridViewLECH = new BandedGridView();
            colLechMaHang = new BandedGridColumn();
            colLechTenHang = new BandedGridColumn();
            colLechSoLuong = new BandedGridColumn();
            colLechNguon = new BandedGridColumn();
            gridBandLECH = new GridBand();
            gridCTTGL = new GridControl();
            GridVTTGL = new BandedGridView();
            gridBand1 = new GridBand();
            gridColumn4 = new BandedGridColumn();
            gridColumn5 = new BandedGridColumn();
            gridColumn6 = new BandedGridColumn();
            gridCtrSUASL = new GridControl();
            gridVSUASL = new GridView();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            ((System.ComponentModel.ISupportInitialize)gridCTTGL).BeginInit();
            ((System.ComponentModel.ISupportInitialize)GridVTTGL).BeginInit();
            ((System.ComponentModel.ISupportInitialize)gridCtrSUASL).BeginInit();
            ((System.ComponentModel.ISupportInitialize)gridVSUASL).BeginInit();
            ((System.ComponentModel.ISupportInitialize)gridCLECH).BeginInit();
            ((System.ComponentModel.ISupportInitialize)bandedGridViewLECH).BeginInit();
            // gridCTTGL
            // 
            gridCTTGL.Dock = System.Windows.Forms.DockStyle.Fill;
            gridCTTGL.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            gridCTTGL.Location = new System.Drawing.Point(0, 0);
            gridCTTGL.MainView = GridVTTGL;
            gridCTTGL.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            gridCTTGL.Name = "gridCTTGL";
            gridCTTGL.Size = new System.Drawing.Size(390, 133);
            gridCTTGL.TabIndex = 8;
            gridCTTGL.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { GridVTTGL });
            GridVTTGL.ActiveFilterEnabled = false;
            GridVTTGL.Bands.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.GridBand[] { gridBand1 });
            GridVTTGL.Columns.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn[] { gridColumn4, gridColumn5, gridColumn6 });
            GridVTTGL.GridControl = gridCTTGL;
            GridVTTGL.GroupCount = 1;
            GridVTTGL.Name = "GridVTTGL";
            GridVTTGL.OptionsSelection.MultiSelect = true;
            GridVTTGL.OptionsSelection.MultiSelectMode = DevExpress.XtraGrid.Views.Grid.GridMultiSelectMode.CheckBoxRowSelect;
            GridVTTGL.OptionsSelection.ShowCheckBoxSelectorInGroupRow = DevExpress.Utils.DefaultBoolean.True;
            GridVTTGL.SortInfo.AddRange(new DevExpress.XtraGrid.Columns.GridColumnSortInfo[] {
                new DevExpress.XtraGrid.Columns.GridColumnSortInfo(gridColumn5, DevExpress.Data.ColumnSortOrder.Ascending)});
            gridBand1.AppearanceHeader.BackColor = System.Drawing.Color.FromArgb(128, 255, 255);
            gridBand1.AppearanceHeader.BorderColor = System.Drawing.Color.Yellow;
            gridBand1.AppearanceHeader.Font = new System.Drawing.Font("Times New Roman", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            gridBand1.AppearanceHeader.Options.UseBackColor = true;
            gridBand1.AppearanceHeader.Options.UseBorderColor = true;
            gridBand1.AppearanceHeader.Options.UseFont = true;
            gridBand1.AppearanceHeader.Options.UseTextOptions = true;
            gridBand1.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridBand1.AppearanceHeader.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            gridBand1.Caption = "THÔNG TIN LOT CẦN GHÉP";
            gridBand1.Columns.Add(gridColumn4);
            gridBand1.Columns.Add(gridColumn5);
            gridBand1.Columns.Add(gridColumn6);
            gridBand1.MinWidth = 16;
            gridBand1.Name = "gridBand1";
            gridBand1.VisibleIndex = 0;
            gridBand1.Width = 435;
            gridColumn4.Caption = "Mã Hàng";
            gridColumn4.FieldName = "MH";
            gridColumn4.MinWidth = 40;
            gridColumn4.Name = "gridColumn4";
            gridColumn4.Visible = true;
            gridColumn4.Width = 190;
            gridColumn5.Caption = "Giờ Giao";
            gridColumn5.DisplayFormat.FormatString = "HH";
            gridColumn5.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            gridColumn5.FieldName = "GG";
            gridColumn5.MinWidth = 40;
            gridColumn5.Name = "gridColumn5";
            gridColumn5.Visible = true;
            gridColumn5.Width = 96;
            gridColumn6.Caption = "LOT GHEP";
            gridColumn6.FieldName = "LG";
            gridColumn6.MinWidth = 40;
            gridColumn6.Name = "gridColumn6";
            gridColumn6.Visible = true;
            gridColumn6.Width = 149;
            // gridCtrSUASL
            // 
            gridCtrSUASL.Dock = System.Windows.Forms.DockStyle.Fill;
            gridCtrSUASL.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            gridCtrSUASL.Location = new System.Drawing.Point(0, 0);
            gridCtrSUASL.MainView = gridVSUASL;
            gridCtrSUASL.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            gridCtrSUASL.Name = "gridCtrSUASL";
            gridCtrSUASL.Size = new System.Drawing.Size(390, 133);
            gridCtrSUASL.TabIndex = 16;
            gridCtrSUASL.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { gridVSUASL });
            gridVSUASL.Appearance.GroupPanel.GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Vertical;
            gridVSUASL.GridControl = gridCtrSUASL;
            gridVSUASL.Name = "gridVSUASL";
            gridVSUASL.OptionsView.ShowGroupPanel = false;
            // gridCLECH
            // 
            gridCLECH.Dock = System.Windows.Forms.DockStyle.Fill;
            gridCLECH.Location = new System.Drawing.Point(2, -1);
            gridCLECH.MainView = bandedGridViewLECH;
            gridCLECH.Name = "gridCLECH";
            gridCLECH.Size = new System.Drawing.Size(389, 134);
            gridCLECH.TabIndex = 3;
            gridCLECH.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { bandedGridViewLECH });
            gridCLECH.Visible = false;
            bandedGridViewLECH.Bands.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.GridBand[] { gridBandLECH });
            bandedGridViewLECH.Columns.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn[] { colLechMaHang, colLechTenHang, colLechSoLuong, colLechNguon });
            bandedGridViewLECH.GridControl = gridCLECH;
            bandedGridViewLECH.Name = "bandedGridViewLECH";
            colLechMaHang.Caption = "Mã Hàng";
            colLechMaHang.FieldName = "MAHANG";
            colLechMaHang.MinWidth = 25;
            colLechMaHang.Name = "colLechMaHang";
            colLechMaHang.Visible = true;
            colLechMaHang.Width = 100;
            colLechTenHang.Caption = "Tên Hàng";
            colLechTenHang.FieldName = "TENHANG";
            colLechTenHang.MinWidth = 25;
            colLechTenHang.Name = "colLechTenHang";
            colLechTenHang.Visible = true;
            colLechTenHang.Width = 120;
            colLechSoLuong.Caption = "Số Lượng";
            colLechSoLuong.FieldName = "SOLUONG";
            colLechSoLuong.MinWidth = 25;
            colLechSoLuong.Name = "colLechSoLuong";
            colLechSoLuong.Visible = true;
            colLechSoLuong.Width = 70;
            colLechNguon.Caption = "Nguồn Lệch";
            colLechNguon.FieldName = "NGUON_LECH";
            colLechNguon.MinWidth = 25;
            colLechNguon.Name = "colLechNguon";
            colLechNguon.Visible = true;
            colLechNguon.Width = 130;
            gridBandLECH.Caption = "LỆCH GIỮA IFS VÀ THỰC TẾ";
            gridBandLECH.Columns.Add(colLechNguon);
            gridBandLECH.Columns.Add(colLechMaHang);
            gridBandLECH.Columns.Add(colLechTenHang);
            gridBandLECH.Columns.Add(colLechSoLuong);
            gridBandLECH.Name = "gridBandLECH";
            gridBandLECH.VisibleIndex = 0;
            gridBandLECH.Width = 420;
            Controls.Add(gridCLECH);
            Controls.Add(gridCTTGL);
            Controls.Add(gridCtrSUASL);
            ((System.ComponentModel.ISupportInitialize)gridCTTGL).EndInit();
            ((System.ComponentModel.ISupportInitialize)GridVTTGL).EndInit();
            ((System.ComponentModel.ISupportInitialize)gridCtrSUASL).EndInit();
            ((System.ComponentModel.ISupportInitialize)gridVSUASL).EndInit();
            ((System.ComponentModel.ISupportInitialize)gridCLECH).EndInit();
            ((System.ComponentModel.ISupportInitialize)bandedGridViewLECH).EndInit();
        }

        public void ShowLech()
        {
            gridCLECH.Visible = true;
            gridCTTGL.Visible = false;
            gridCtrSUASL.Visible = false;
            gridCLECH.BringToFront();
        }

        public void ShowGhepLot()
        {
            gridCLECH.Visible = false;
            gridCTTGL.Visible = true;
            gridCtrSUASL.Visible = false;
            gridCTTGL.BringToFront();
        }

        public void ShowSuaSoLuong()
        {
            gridCLECH.Visible = false;
            gridCTTGL.Visible = false;
            gridCtrSUASL.Visible = true;
            gridCtrSUASL.BringToFront();
        }
    }
}
