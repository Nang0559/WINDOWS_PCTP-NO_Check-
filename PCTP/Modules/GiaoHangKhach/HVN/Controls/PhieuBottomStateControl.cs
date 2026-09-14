using System;
using System.Data;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.BandedGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Modules.GiaoHangKhach.HVN.Controls
{
    public sealed class PhieuBottomStateControl : XtraUserControl
    {
        private GridControl gridCLECH;
        private BandedGridView bandedGridViewLECH;
        private BandedGridColumn colLechMaHang, colLechTenHang, colLechSoLuong, colLechNguon;
        private GridBand gridBandLECH;
        private GridControl gridCTTGL;
        private BandedGridView GridVTTGL;
        private GridBand gridBand1;
        private BandedGridColumn gridColumn4, gridColumn5, gridColumn6;
        private GridControl gridCtrSUASL;
        private GridView gridVSUASL;

        public event FocusedRowChangedEventHandler SuaSlFocusedRowChanged = delegate { };

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
            InitializeComponent();
            gridVSUASL.FocusedRowChanged += gridVSUASL_FocusedRowChanged;
        }

        private void InitializeComponent()
        {
            this.gridCLECH = new DevExpress.XtraGrid.GridControl();
            this.bandedGridViewLECH = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridView();
            this.gridBandLECH = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
            this.colLechNguon = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.colLechMaHang = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.colLechTenHang = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.colLechSoLuong = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.gridCTTGL = new DevExpress.XtraGrid.GridControl();
            this.GridVTTGL = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridView();
            this.gridBand1 = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
            this.gridColumn4 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.gridColumn5 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.gridColumn6 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.gridCtrSUASL = new DevExpress.XtraGrid.GridControl();
            this.gridVSUASL = new DevExpress.XtraGrid.Views.Grid.GridView();
            ((System.ComponentModel.ISupportInitialize)(this.gridCLECH)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bandedGridViewLECH)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridCTTGL)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GridVTTGL)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridCtrSUASL)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridVSUASL)).BeginInit();
            this.SuspendLayout();
            // 
            // gridCLECH
            // 
            this.gridCLECH.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCLECH.Location = new System.Drawing.Point(0, 0);
            this.gridCLECH.MainView = this.bandedGridViewLECH;
            this.gridCLECH.Name = "gridCLECH";
            this.gridCLECH.Size = new System.Drawing.Size(827, 450);
            this.gridCLECH.TabIndex = 3;
            this.gridCLECH.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.bandedGridViewLECH});
            this.gridCLECH.Visible = false;
            // 
            // bandedGridViewLECH
            // 
            this.bandedGridViewLECH.Bands.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.GridBand[] {
            this.gridBandLECH});
            this.bandedGridViewLECH.Columns.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn[] {
            this.colLechMaHang,
            this.colLechTenHang,
            this.colLechSoLuong,
            this.colLechNguon});
            this.bandedGridViewLECH.GridControl = this.gridCLECH;
            this.bandedGridViewLECH.Name = "bandedGridViewLECH";
            // 
            // gridBandLECH
            // 
            this.gridBandLECH.Caption = "LỆCH GIỮA IFS VÀ THỰC TẾ";
            this.gridBandLECH.Columns.Add(this.colLechNguon);
            this.gridBandLECH.Columns.Add(this.colLechMaHang);
            this.gridBandLECH.Columns.Add(this.colLechTenHang);
            this.gridBandLECH.Columns.Add(this.colLechSoLuong);
            this.gridBandLECH.Name = "gridBandLECH";
            this.gridBandLECH.VisibleIndex = 0;
            this.gridBandLECH.Width = 420;
            // 
            // colLechNguon
            // 
            this.colLechNguon.Caption = "Nguồn Lệch";
            this.colLechNguon.FieldName = "NGUON_LECH";
            this.colLechNguon.MinWidth = 25;
            this.colLechNguon.Name = "colLechNguon";
            this.colLechNguon.Visible = true;
            this.colLechNguon.Width = 130;
            // 
            // colLechMaHang
            // 
            this.colLechMaHang.Caption = "Mã Hàng";
            this.colLechMaHang.FieldName = "MAHANG";
            this.colLechMaHang.MinWidth = 25;
            this.colLechMaHang.Name = "colLechMaHang";
            this.colLechMaHang.Visible = true;
            this.colLechMaHang.Width = 100;
            // 
            // colLechTenHang
            // 
            this.colLechTenHang.Caption = "Tên Hàng";
            this.colLechTenHang.FieldName = "TENHANG";
            this.colLechTenHang.MinWidth = 25;
            this.colLechTenHang.Name = "colLechTenHang";
            this.colLechTenHang.Visible = true;
            this.colLechTenHang.Width = 120;
            // 
            // colLechSoLuong
            // 
            this.colLechSoLuong.Caption = "Số Lượng";
            this.colLechSoLuong.FieldName = "SOLUONG";
            this.colLechSoLuong.MinWidth = 25;
            this.colLechSoLuong.Name = "colLechSoLuong";
            this.colLechSoLuong.Visible = true;
            this.colLechSoLuong.Width = 70;
            // 
            // gridCTTGL
            // 
            this.gridCTTGL.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCTTGL.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.gridCTTGL.Location = new System.Drawing.Point(0, 0);
            this.gridCTTGL.MainView = this.GridVTTGL;
            this.gridCTTGL.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.gridCTTGL.Name = "gridCTTGL";
            this.gridCTTGL.Size = new System.Drawing.Size(827, 450);
            this.gridCTTGL.TabIndex = 8;
            this.gridCTTGL.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.GridVTTGL});
            // 
            // GridVTTGL
            // 
            this.GridVTTGL.ActiveFilterEnabled = false;
            this.GridVTTGL.Bands.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.GridBand[] {
            this.gridBand1});
            this.GridVTTGL.Columns.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn[] {
            this.gridColumn4,
            this.gridColumn5,
            this.gridColumn6});
            this.GridVTTGL.GridControl = this.gridCTTGL;
            this.GridVTTGL.GroupCount = 1;
            this.GridVTTGL.Name = "GridVTTGL";
            this.GridVTTGL.OptionsSelection.MultiSelect = true;
            this.GridVTTGL.OptionsSelection.MultiSelectMode = DevExpress.XtraGrid.Views.Grid.GridMultiSelectMode.CheckBoxRowSelect;
            this.GridVTTGL.OptionsSelection.ShowCheckBoxSelectorInGroupRow = DevExpress.Utils.DefaultBoolean.True;
            this.GridVTTGL.SortInfo.AddRange(new DevExpress.XtraGrid.Columns.GridColumnSortInfo[] {
            new DevExpress.XtraGrid.Columns.GridColumnSortInfo(this.gridColumn5, DevExpress.Data.ColumnSortOrder.Ascending)});
            // 
            // gridBand1
            // 
            this.gridBand1.AppearanceHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.gridBand1.AppearanceHeader.BorderColor = System.Drawing.Color.Yellow;
            this.gridBand1.AppearanceHeader.Font = new System.Drawing.Font("Times New Roman", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gridBand1.AppearanceHeader.Options.UseBackColor = true;
            this.gridBand1.AppearanceHeader.Options.UseBorderColor = true;
            this.gridBand1.AppearanceHeader.Options.UseFont = true;
            this.gridBand1.AppearanceHeader.Options.UseTextOptions = true;
            this.gridBand1.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.gridBand1.AppearanceHeader.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            this.gridBand1.Caption = "THÔNG TIN LOT CẦN GHÉP";
            this.gridBand1.Columns.Add(this.gridColumn4);
            this.gridBand1.Columns.Add(this.gridColumn5);
            this.gridBand1.Columns.Add(this.gridColumn6);
            this.gridBand1.MinWidth = 16;
            this.gridBand1.Name = "gridBand1";
            this.gridBand1.VisibleIndex = 0;
            this.gridBand1.Width = 435;
            // 
            // gridColumn4
            // 
            this.gridColumn4.Caption = "Mã Hàng";
            this.gridColumn4.FieldName = "MH";
            this.gridColumn4.MinWidth = 40;
            this.gridColumn4.Name = "gridColumn4";
            this.gridColumn4.Visible = true;
            this.gridColumn4.Width = 190;
            // 
            // gridColumn5
            // 
            this.gridColumn5.Caption = "Giờ Giao";
            this.gridColumn5.DisplayFormat.FormatString = "HH";
            this.gridColumn5.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            this.gridColumn5.FieldName = "GG";
            this.gridColumn5.MinWidth = 40;
            this.gridColumn5.Name = "gridColumn5";
            this.gridColumn5.Visible = true;
            this.gridColumn5.Width = 96;
            // 
            // gridColumn6
            // 
            this.gridColumn6.Caption = "LOT GHEP";
            this.gridColumn6.FieldName = "LG";
            this.gridColumn6.MinWidth = 40;
            this.gridColumn6.Name = "gridColumn6";
            this.gridColumn6.Visible = true;
            this.gridColumn6.Width = 149;
            // 
            // gridCtrSUASL
            // 
            this.gridCtrSUASL.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCtrSUASL.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.gridCtrSUASL.Location = new System.Drawing.Point(0, 0);
            this.gridCtrSUASL.MainView = this.gridVSUASL;
            this.gridCtrSUASL.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.gridCtrSUASL.Name = "gridCtrSUASL";
            this.gridCtrSUASL.Size = new System.Drawing.Size(827, 450);
            this.gridCtrSUASL.TabIndex = 16;
            this.gridCtrSUASL.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridVSUASL});
            this.gridCtrSUASL.Visible = false;
            // 
            // gridVSUASL
            // 
            this.gridVSUASL.Appearance.GroupPanel.GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Vertical;
            this.gridVSUASL.GridControl = this.gridCtrSUASL;
            this.gridVSUASL.Name = "gridVSUASL";
            this.gridVSUASL.OptionsView.ShowGroupPanel = false;
            // 
            // PhieuBottomStateControl
            // 
            this.Controls.Add(this.gridCLECH);
            this.Controls.Add(this.gridCTTGL);
            this.Controls.Add(this.gridCtrSUASL);
            this.Name = "PhieuBottomStateControl";
            this.Size = new System.Drawing.Size(827, 450);
            ((System.ComponentModel.ISupportInitialize)(this.gridCLECH)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bandedGridViewLECH)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridCTTGL)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GridVTTGL)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridCtrSUASL)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridVSUASL)).EndInit();
            this.ResumeLayout(false);

        }

        private void gridVSUASL_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            SuaSlFocusedRowChanged.Invoke(this, e);
        }

        public void DetachLegacySuaSlFocusedRowChanged(FocusedRowChangedEventHandler handler)
        {
            if (handler != null)
                gridVSUASL.FocusedRowChanged -= handler;
        }

        public void BindLech(DataTable data)
        {
            gridCLECH.DataSource = data;
        }

        public void BindGhepLot(DataTable data)
        {
            gridCTTGL.DataSource = data;
        }

        public void BindSuaSoLuong(DataTable data)
        {
            gridCtrSUASL.DataSource = data;
        }

        public DataRow[] GetSelectedGhepLotRows()
        {
            var selected = GridVTTGL.GetSelectedRows();
            var rows = new System.Collections.Generic.List<DataRow>();
            foreach (int handle in selected)
            {
                DataRow row = GridVTTGL.GetDataRow(handle);
                if (row != null)
                    rows.Add(row);
            }
            return rows.ToArray();
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

        public void HideGhepLot()
        {
            gridCTTGL.Visible = false;
        }

        public void HideSuaSoLuong()
        {
            gridCtrSUASL.Visible = false;
        }

        public void HideLech()
        {
            gridCLECH.Visible = false;
        }

        public void ToggleLechGhepLot()
        {
            if (gridCLECH.Visible)
                ShowGhepLot();
            else
                ShowLech();
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