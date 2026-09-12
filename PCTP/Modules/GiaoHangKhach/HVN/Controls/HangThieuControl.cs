using System.Drawing;
using DevExpress.Utils;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.BandedGrid;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the "Hàng thiếu" grid.
    /// Keeps the legacy grid configuration without moving business logic here.
    /// </summary>
    public sealed class HangThieuControl : DevExpress.XtraEditors.XtraUserControl
    {
        private readonly GridControl _grid;
        private readonly BandedGridView _view;
        private readonly GridBand _band;
        private readonly BandedGridColumn _maHang;
        private readonly BandedGridColumn _gioThieu;
        private readonly BandedGridColumn _soLuongGiao;
        private readonly BandedGridColumn _soLuongThieu;

        public HangThieuControl()
        {
            _grid = new GridControl();
            _view = new BandedGridView();
            _band = new GridBand();
            _maHang = new BandedGridColumn();
            _gioThieu = new BandedGridColumn();
            _soLuongGiao = new BandedGridColumn();
            _soLuongThieu = new BandedGridColumn();

            SuspendLayout();

            Dock = System.Windows.Forms.DockStyle.Fill;
            Name = "hangThieuControl";

            _grid.Dock = System.Windows.Forms.DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { _view });

            _view.ActiveFilterEnabled = false;
            _view.GridControl = _grid;
            _view.GroupCount = 1;
            _view.Name = "bandedGridViewHangThieu";
            _view.OptionsSelection.MultiSelect = true;
            _view.SortInfo.AddRange(new DevExpress.XtraGrid.Columns.GridColumnSortInfo[]
            {
                new DevExpress.XtraGrid.Columns.GridColumnSortInfo(
                    _maHang,
                    DevExpress.Data.ColumnSortOrder.Ascending)
            });

            _view.Bands.AddRange(new GridBand[] { _band });
            _view.Columns.AddRange(new BandedGridColumn[]
            {
                _maHang,
                _gioThieu,
                _soLuongGiao,
                _soLuongThieu
            });

            _band.AppearanceHeader.BackColor = Color.Red;
            _band.AppearanceHeader.BorderColor = Color.Yellow;
            _band.AppearanceHeader.Font = new Font("Times New Roman", 10.8F, FontStyle.Bold);
            _band.AppearanceHeader.Options.UseBackColor = true;
            _band.AppearanceHeader.Options.UseBorderColor = true;
            _band.AppearanceHeader.Options.UseFont = true;
            _band.AppearanceHeader.Options.UseTextOptions = true;
            _band.AppearanceHeader.TextOptions.HAlignment = HorzAlignment.Center;
            _band.AppearanceHeader.TextOptions.VAlignment = VertAlignment.Center;
            _band.Caption = "THÔNG TIN HÀNG THIẾU";
            _band.Columns.Add(_maHang);
            _band.Columns.Add(_gioThieu);
            _band.Columns.Add(_soLuongGiao);
            _band.Columns.Add(_soLuongThieu);
            _band.MinWidth = 26;
            _band.Name = "gridBandHangThieu";
            _band.VisibleIndex = 0;
            _band.Width = 1066;

            ConfigureColumn(_maHang, "Mã Hàng", "MH", 236, 64);
            ConfigureColumn(_gioThieu, "Giờ Thiếu", "GIOGIAO", 274, 64);
            ConfigureColumn(_soLuongGiao, "Số Lượng Giao", "SLGIAO", 428, 64);
            ConfigureColumn(_soLuongThieu, "SỐ LƯỢNG THIẾU", "SLTHIEU", 128, 34);

            Controls.Add(_grid);
            ResumeLayout(false);
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

        public GridControl Grid { get { return _grid; } }
        public BandedGridView View { get { return _view; } }
        public GridBand Band { get { return _band; } }
    }
}
