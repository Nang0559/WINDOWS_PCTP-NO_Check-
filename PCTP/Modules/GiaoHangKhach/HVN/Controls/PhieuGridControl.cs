using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.BandedGrid;
using DevExpress.XtraGrid.Views.Base;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the normal Phiếu/Order grid area of HVN_PGH.
    ///
    /// Phase 9B:
    /// - Owns the Order GridControl and BandedGridView.
    /// - Keeps the existing field names/column layout from HVN_PGH.Designer.cs.
    /// - Does not own business logic or DataSource decisions.
    /// - The parent form can continue to bind through OrderGrid/OrderView.
    /// </summary>
    public class PhieuGridControl : XtraUserControl
    {
        private readonly GridControl gridCtrDONHANG;
        private readonly BandedGridView GridViewDONHANG;
        private readonly GridBand gridBandDH;

        private readonly BandedGridColumn STT;
        private readonly BandedGridColumn GIO;
        private readonly BandedGridColumn cua;
        private readonly BandedGridColumn truyen;
        private readonly BandedGridColumn mahang;
        private readonly BandedGridColumn tenhang;
        private readonly BandedGridColumn bandedGridColumn9;
        private readonly BandedGridColumn LOT;
        private readonly BandedGridColumn dovi;
        private readonly BandedGridColumn soluong;
        private readonly BandedGridColumn xe;
        private readonly BandedGridColumn Hop;
        private readonly BandedGridColumn STATUSDOC;
        private readonly BandedGridColumn STATUSCNK;
        private readonly BandedGridColumn bandedGridColumn8;
        private readonly BandedGridColumn bandedGridColumn6;
        private readonly BandedGridColumn bandedGridColumn7;
        private readonly BandedGridColumn bandedGridColumn4;

        public PhieuGridControl()
        {
            Dock = DockStyle.Fill;

            gridCtrDONHANG = new GridControl();
            GridViewDONHANG = new BandedGridView(gridCtrDONHANG);
            gridBandDH = new GridBand();

            STT = new BandedGridColumn();
            GIO = new BandedGridColumn();
            cua = new BandedGridColumn();
            truyen = new BandedGridColumn();
            mahang = new BandedGridColumn();
            tenhang = new BandedGridColumn();
            bandedGridColumn9 = new BandedGridColumn();
            LOT = new BandedGridColumn();
            dovi = new BandedGridColumn();
            soluong = new BandedGridColumn();
            xe = new BandedGridColumn();
            Hop = new BandedGridColumn();
            STATUSDOC = new BandedGridColumn();
            STATUSCNK = new BandedGridColumn();
            bandedGridColumn8 = new BandedGridColumn();
            bandedGridColumn6 = new BandedGridColumn();
            bandedGridColumn7 = new BandedGridColumn();
            bandedGridColumn4 = new BandedGridColumn();

            ConfigureGrid();

            gridCtrDONHANG.Dock = DockStyle.Fill;
            Controls.Add(gridCtrDONHANG);
        }

        public GridControl OrderGrid
        {
            get { return gridCtrDONHANG; }
        }

        public BandedGridView OrderView
        {
            get { return GridViewDONHANG; }
        }

        public GridBand OrderBand
        {
            get { return gridBandDH; }
        }

        /// <summary>
        /// Applies customer-specific presentation settings to the order grid.
        /// This keeps DevExpress column manipulation inside the grid boundary;
        /// customer/business decisions remain owned by the caller.
        /// </summary>
        public void SetupForCustomer(bool usePrivateOrderTable)
        {
            SetColumnVisible("GEAR", usePrivateOrderTable);
            SetColumnVisible("PO_NO", usePrivateOrderTable);

            if (!usePrivateOrderTable)
                return;

            SetColumnCaption("GEAR", "Gear Sử Dụng");
            SetColumnCaption("CUA", "Cửa");
            SetColumnCaption("TRUYEN", "Truyền");
            SetColumnCaption("GIOGIAO", "Giờ");
            SetColumnCaption("PO_NO", "Số PO");
        }

        private void SetColumnVisible(string fieldName, bool visible)
        {
            var column = GridViewDONHANG.Columns.ColumnByFieldName(fieldName);
            if (column != null)
                column.Visible = visible;
        }

        private void SetColumnCaption(string fieldName, string caption)
        {
            var column = GridViewDONHANG.Columns.ColumnByFieldName(fieldName);
            if (column != null)
                column.Caption = caption;
        }

        private void ConfigureGrid()
        {
            gridCtrDONHANG.EmbeddedNavigator.Margin = new Padding(3, 2, 3, 2);
            gridCtrDONHANG.MainView = GridViewDONHANG;
            gridCtrDONHANG.Margin = new Padding(3, 2, 3, 2);
            gridCtrDONHANG.Name = "gridCtrDONHANG";
            gridCtrDONHANG.TabIndex = 4;
            gridCtrDONHANG.ViewCollection.AddRange(new BaseView[] { GridViewDONHANG });

            GridViewDONHANG.Appearance.ColumnFilterButton.Font =
                new Font("Tahoma", 10.2F, FontStyle.Bold);
            GridViewDONHANG.Appearance.ColumnFilterButton.Options.UseFont = true;
            GridViewDONHANG.Bands.AddRange(new[] { gridBandDH });
            GridViewDONHANG.Columns.AddRange(new[]
            {
                STT, GIO, cua, truyen, mahang, tenhang, LOT, dovi, soluong,
                xe, Hop, STATUSDOC, STATUSCNK, bandedGridColumn6,
                bandedGridColumn7, bandedGridColumn8, bandedGridColumn4,
                bandedGridColumn9
            });
            GridViewDONHANG.GridControl = gridCtrDONHANG;
            GridViewDONHANG.Name = "GridViewDONHANG";
            GridViewDONHANG.OptionsFilter.AllowMultiSelectInCheckedFilterPopup = false;
            GridViewDONHANG.OptionsFilter.ShowAllTableValuesInCheckedFilterPopup = false;
            GridViewDONHANG.OptionsSelection.CheckBoxSelectorColumnWidth = 24;
            GridViewDONHANG.OptionsSelection.CheckBoxSelectorField = "CHON";
            GridViewDONHANG.OptionsSelection.MultiSelect = true;
            GridViewDONHANG.OptionsSelection.ShowCheckBoxSelectorInColumnHeader =
                DevExpress.Utils.DefaultBoolean.False;

            gridBandDH.AppearanceHeader.BackColor = Color.FromArgb(128, 255, 255);
            gridBandDH.AppearanceHeader.BorderColor = Color.Red;
            gridBandDH.AppearanceHeader.Font =
                new Font("Times New Roman", 12F, FontStyle.Bold);
            gridBandDH.AppearanceHeader.Options.UseBackColor = true;
            gridBandDH.AppearanceHeader.Options.UseBorderColor = true;
            gridBandDH.AppearanceHeader.Options.UseFont = true;
            gridBandDH.AppearanceHeader.Options.UseTextOptions = true;
            gridBandDH.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridBandDH.AppearanceHeader.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            gridBandDH.Caption = "gridBand3";
            gridBandDH.Columns.Add(STT);
            gridBandDH.Columns.Add(GIO);
            gridBandDH.Columns.Add(cua);
            gridBandDH.Columns.Add(truyen);
            gridBandDH.Columns.Add(mahang);
            gridBandDH.Columns.Add(tenhang);
            gridBandDH.Columns.Add(bandedGridColumn9);
            gridBandDH.Columns.Add(LOT);
            gridBandDH.Columns.Add(dovi);
            gridBandDH.Columns.Add(soluong);
            gridBandDH.Columns.Add(xe);
            gridBandDH.Columns.Add(Hop);
            gridBandDH.Columns.Add(STATUSDOC);
            gridBandDH.Columns.Add(STATUSCNK);
            gridBandDH.Columns.Add(bandedGridColumn8);
            gridBandDH.Columns.Add(bandedGridColumn6);
            gridBandDH.Columns.Add(bandedGridColumn7);
            gridBandDH.Columns.Add(bandedGridColumn4);
            gridBandDH.MinWidth = 12;
            gridBandDH.Name = "gridBandDH";
            gridBandDH.RowCount = 2;
            gridBandDH.VisibleIndex = 0;
            gridBandDH.Width = 1549;

            ConfigureColumn(STT, "GSTT", "STT", 34, 12);
            STT.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;

            ConfigureColumn(GIO, "Giờ", "GIOGIAO", 90, 24);
            GIO.DisplayFormat.FormatString = "HH";
            GIO.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;

            ConfigureColumn(cua, "Cửa", "CUA", 90, 24);
            ConfigureColumn(truyen, "Truyền", "TRUYEN", 90, 24);
            ConfigureColumn(mahang, "Mã hàng", "MAHANG", 90, 24);
            ConfigureColumn(tenhang, "Tên Hàng", "TENHANG", 90, 24);
            ConfigureColumn(bandedGridColumn9, "Gear Sử Dụng", "GEAR", 94, 25);
            ConfigureColumn(LOT, "Số Lô", "LOT", 90, 24);
            ConfigureColumn(dovi, "Đơn Vị", "DV", 90, 24);

            ConfigureColumn(soluong, "Số Lượng", "SOLUONG", 90, 24);
            soluong.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;

            ConfigureColumn(xe, "Xe", null, 79, 24);

            ConfigureColumn(Hop, "Hộp", "HOP", 89, 24);
            Hop.DisplayFormat.FormatString = "n";
            Hop.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;

            ConfigureColumn(STATUSDOC, "Kết Quả Đọc QRcode", "STATUSDOC", 64, 24);
            STATUSDOC.AppearanceCell.Options.UseTextOptions = true;
            STATUSDOC.AppearanceCell.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            STATUSDOC.AppearanceCell.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;

            ConfigureColumn(STATUSCNK, "Trạng Thái Cập Nhập Kho", "STATUS", 117, 12);
            STATUSCNK.AppearanceCell.Options.UseTextOptions = true;
            STATUSCNK.AppearanceCell.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            STATUSCNK.AppearanceCell.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;

            ConfigureColumn(bandedGridColumn8, "TT Phiếu", "TTPHIEU", 94, 25);
            ConfigureColumn(bandedGridColumn6, "PO No", "PO_NO", 94, 25);
            ConfigureColumn(bandedGridColumn7, "PO_ITEM", "PO_ITEM", 94, 25);
            ConfigureColumn(bandedGridColumn4, "Ghi Chú", "Note", 70, 29);
        }

        private static void ConfigureColumn(
            BandedGridColumn column,
            string caption,
            string fieldName,
            int width,
            int minWidth)
        {
            column.Caption = caption;
            column.FieldName = fieldName;
            column.MinWidth = minWidth;
            column.Visible = true;
            column.Width = width;
        }
    }
}
