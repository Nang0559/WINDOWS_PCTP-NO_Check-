using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.XuLyHangLoi
{
    /// <summary>Một lần giao gốc được chọn + SL khách trả thuộc lần giao đó.</summary>
    public sealed class PhieuGiaoChon
    {
        public PhieuGiaoUngVienInfo Phieu { get; set; }

        /// <summary>SL của LOT trong phiếu giao (nếu tra theo LOT), ngược lại tổng SL dòng giao.</summary>
        public int SoLuongDaGiao { get; set; }

        /// <summary>SL khách trả thuộc lần giao này (có thể khác SL đã giao).</summary>
        public int SoLuongTra { get; set; }
    }

    /// <summary>
    /// Đối chiếu dòng khách trả với thông tin HÀNG ĐÃ GIAO.
    ///
    /// Cùng một mã hàng / LOT có thể được giao nhiều lần, khách lại thường chỉ báo
    /// khoảng thời gian → form liệt kê mọi lần giao trong [Từ ngày, Đến ngày]
    /// (lọc theo khách, mã hàng, LOT nếu có). Người tiếp nhận tick một hoặc nhiều
    /// lần giao và nhập SL khách trả cho từng lần (SL trả được phép khác SL đã giao).
    /// </summary>
    public sealed class FormDoiChieuPhieuGiao : XtraForm
    {
        public sealed class DongDoiChieu
        {
            public bool Chon { get; set; }
            public DateTime? NgayGiao { get; set; }
            public string GioGiao { get; set; }
            public string NhaMay { get; set; }
            public string PoNo { get; set; }
            public string Lot { get; set; }
            public int SoLuongPhieu { get; set; }
            public int? SoLuongLot { get; set; }
            public int SoLuongDaGiao { get; set; }
            public int SoLuongTra { get; set; }
            public string Note { get; set; }

            [Browsable(false)]
            public PhieuGiaoUngVienInfo Phieu { get; set; }
        }

        private readonly IKhachTraHangService _service;
        private readonly string _customerNo;
        private readonly string _maHang;
        private readonly int _soLuongKhachBao;

        private TextEdit _txtMaHang;
        private TextEdit _txtLot;
        private DateEdit _dtTu;
        private DateEdit _dtDen;
        private LabelControl _lblKetQua;
        private LabelControl _lblTong;
        private GridControl _grid;
        private GridView _view;
        private List<DongDoiChieu> _rows = new List<DongDoiChieu>();

        public List<PhieuGiaoChon> KetQua { get; private set; } = new List<PhieuGiaoChon>();

        /// <summary>LOT dùng để đối chiếu (người dùng có thể sửa trong form).</summary>
        public string LotNo { get; private set; }

        public FormDoiChieuPhieuGiao(
            IKhachTraHangService service,
            string customerNo,
            string maHang,
            string tenHang,
            string lotNo,
            DateTime? ngayGiaoKhachBao,
            int soLuongKhachBao)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _customerNo = customerNo;
            _maHang = (maHang ?? string.Empty).Trim();
            _soLuongKhachBao = soLuongKhachBao;

            BuildUi(tenHang, lotNo, ngayGiaoKhachBao);

            Shown += (s, e) => TimKiem();
        }

        private void BuildUi(string tenHang, string lotNo, DateTime? ngayGiaoKhachBao)
        {
            Text = "Đối chiếu phiếu giao gốc";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1180;
            Height = 640;

            var top = new Panel { Dock = DockStyle.Top, Height = 108 };

            top.Controls.Add(new LabelControl { Text = "Mã hàng", Left = 12, Top = 16, Width = 70 });
            _txtMaHang = new TextEdit { Left = 86, Top = 12, Width = 230 };
            _txtMaHang.Text = _maHang;
            _txtMaHang.Properties.ReadOnly = true;
            top.Controls.Add(_txtMaHang);

            top.Controls.Add(new LabelControl
            {
                Text = string.IsNullOrWhiteSpace(tenHang) ? string.Empty : tenHang,
                Left = 324, Top = 16, Width = 460, AutoSizeMode = LabelAutoSizeMode.None
            });

            top.Controls.Add(new LabelControl { Text = "Lot (nếu có)", Left = 12, Top = 52, Width = 70 });
            _txtLot = new TextEdit { Left = 86, Top = 48, Width = 230 };
            _txtLot.Text = (lotNo ?? string.Empty).Trim();
            _txtLot.Properties.NullValuePrompt = "Để trống = mọi LOT của mã";
            _txtLot.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; e.SuppressKeyPress = true; TimKiem(); } };
            top.Controls.Add(_txtLot);

            top.Controls.Add(new LabelControl { Text = "Giao từ ngày", Left = 340, Top = 52, Width = 80 });
            _dtTu = TaoDateEdit(424, 48);
            top.Controls.Add(_dtTu);

            top.Controls.Add(new LabelControl { Text = "đến ngày", Left = 548, Top = 52, Width = 60 });
            _dtDen = TaoDateEdit(612, 48);
            top.Controls.Add(_dtDen);

            // Khách báo ngày cụ thể → mặc định đúng ngày đó; không có → 30 ngày gần nhất
            // (90 ngày nếu đã biết LOT vì LOT thường giao sau khi sản xuất một thời gian).
            if (ngayGiaoKhachBao.HasValue)
            {
                _dtTu.EditValue = ngayGiaoKhachBao.Value.Date;
                _dtDen.EditValue = ngayGiaoKhachBao.Value.Date;
            }
            else
            {
                bool coLot = !string.IsNullOrWhiteSpace(lotNo);
                _dtTu.EditValue = DateTime.Today.AddDays(coLot ? -90 : -30);
                _dtDen.EditValue = DateTime.Today;
            }

            var btnTim = new SimpleButton { Text = "🔎 Tìm lần giao", Left = 740, Top = 46, Width = 130 };
            btnTim.Click += (s, e) => TimKiem();
            top.Controls.Add(btnTim);

            _lblKetQua = new LabelControl { Left = 12, Top = 84, Width = 1100, AutoSizeMode = LabelAutoSizeMode.None };
            top.Controls.Add(_lblKetQua);

            // ---- grid
            _grid = new GridControl { Dock = DockStyle.Fill };
            _view = new GridView(_grid);
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);

            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsBehavior.Editable = true;
            _view.OptionsSelection.MultiSelect = false;

            var chk = new RepositoryItemCheckEdit();
            chk.EditValueChanged += (s, e) => _view.PostEditor();
            _grid.RepositoryItems.Add(chk);

            var colChon = ThemCot(nameof(DongDoiChieu.Chon), "Chọn", 55, true);
            colChon.ColumnEdit = chk;
            ThemCot(nameof(DongDoiChieu.NgayGiao), "Ngày giao", 90, false)
                .DisplayFormat.SetFormat(DevExpress.Utils.FormatType.DateTime, "dd/MM/yyyy");
            ThemCot(nameof(DongDoiChieu.GioGiao), "Giờ giao", 80, false);
            ThemCot(nameof(DongDoiChieu.NhaMay), "Nhà máy", 200, false);
            ThemCot(nameof(DongDoiChieu.PoNo), "PO", 110, false);
            ThemCot(nameof(DongDoiChieu.Lot), "LOT trên phiếu giao", 260, false);
            ThemCot(nameof(DongDoiChieu.SoLuongPhieu), "SL dòng giao", 90, false);
            ThemCot(nameof(DongDoiChieu.SoLuongLot), "SL của LOT", 80, false);
            ThemCot(nameof(DongDoiChieu.SoLuongDaGiao), "SL đã giao", 85, false);
            ThemCot(nameof(DongDoiChieu.SoLuongTra), "SL khách trả", 95, true);
            ThemCot(nameof(DongDoiChieu.Note), "Ghi chú phiếu giao", 160, false);

            _view.CellValueChanged += View_CellValueChanged;
            _view.RowCellStyle += View_RowCellStyle;

            // ---- bottom
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 52 };

            _lblTong = new LabelControl { Left = 12, Top = 8, Width = 760, Height = 36, AutoSizeMode = LabelAutoSizeMode.None };
            bottom.Controls.Add(_lblTong);

            var btnOk = new SimpleButton { Text = "✔ Gán vào dòng khách trả", Left = 800, Top = 10, Width = 190 };
            btnOk.Click += (s, e) => XacNhan();
            var btnHuy = new SimpleButton { Text = "Hủy", Left = 1000, Top = 10, Width = 90, DialogResult = DialogResult.Cancel };
            bottom.Controls.Add(btnOk);
            bottom.Controls.Add(btnHuy);

            Controls.Add(_grid);
            Controls.Add(top);
            Controls.Add(bottom);

            CancelButton = btnHuy;
        }

        private DateEdit TaoDateEdit(int left, int top)
        {
            var d = new DateEdit { Left = left, Top = top, Width = 120 };
            d.Properties.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            d.Properties.DisplayFormat.FormatString = "dd/MM/yyyy";
            d.Properties.EditFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            d.Properties.EditFormat.FormatString = "dd/MM/yyyy";
            d.Properties.Mask.EditMask = "dd/MM/yyyy";
            d.Properties.Mask.UseMaskAsDisplayFormat = true;
            return d;
        }

        private GridColumn ThemCot(string field, string caption, int width, bool editable)
        {
            var col = _view.Columns.AddField(field);
            col.Caption = caption;
            col.Visible = true;
            col.Width = width;
            col.OptionsColumn.AllowEdit = editable;
            return col;
        }

        private void TimKiem()
        {
            _view.CloseEditor();

            DateTime tu = (_dtTu.EditValue as DateTime?) ?? DateTime.Today.AddDays(-30);
            DateTime den = (_dtDen.EditValue as DateTime?) ?? DateTime.Today;

            if (tu.Date > den.Date)
            {
                XtraMessageBox.Show(
                    "Từ ngày phải nhỏ hơn hoặc bằng Đến ngày.",
                    "Khoảng thời gian không hợp lệ",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string lot = _txtLot.Text.Trim();

            try
            {
                var list = _service.TimPhieuGiaoTheoKhoang(_customerNo, _maHang, lot, tu, den);

                _rows = list.Select(p => new DongDoiChieu
                {
                    Phieu = p,
                    NgayGiao = p.NGAYGIAO,
                    GioGiao = string.IsNullOrWhiteSpace(p.GIOGIAOFCC) ? p.GIOGIAO : p.GIOGIAOFCC,
                    NhaMay = p.NHAMAY,
                    PoNo = p.PO_NO,
                    Lot = p.LOT,
                    SoLuongPhieu = p.SOLUONG,
                    SoLuongLot = p.SoLuongLot,
                    SoLuongDaGiao = p.SoLuongLot ?? p.SOLUONG,
                    Note = p.Note
                }).ToList();

                _grid.DataSource = _rows;

                if (_rows.Count == 1)
                {
                    _rows[0].Chon = true;
                    _rows[0].SoLuongTra = MacDinhSoLuongTra(_rows[0]);
                    _grid.RefreshDataSource();
                }

                _lblKetQua.Text = _rows.Count == 0
                    ? "Không có lần giao nào khớp trong khoảng thời gian này. Mở rộng khoảng ngày hoặc bỏ trống LOT."
                    : $"Tìm thấy {_rows.Count} lần giao. Tick các lần giao khách đã trả và nhập SL trả của từng lần.";
                _lblKetQua.Appearance.ForeColor = _rows.Count == 0 ? Color.Firebrick : Color.DimGray;

                CapNhatTong();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Không thể tra cứu phiếu giao.\r\n\r\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // SL mặc định khi tick: phần khách báo còn chưa phân bổ, tối đa bằng SL đã giao của lần đó.
        private int MacDinhSoLuongTra(DongDoiChieu row)
        {
            if (_soLuongKhachBao <= 0)
                return row.SoLuongDaGiao;

            int daPhanBo = _rows.Where(r => r != row && r.Chon).Sum(r => r.SoLuongTra);
            int conLai = _soLuongKhachBao - daPhanBo;

            return conLai > 0 ? Math.Min(row.SoLuongDaGiao, conLai) : 0;
        }

        private void View_CellValueChanged(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {
            var row = _view.GetRow(e.RowHandle) as DongDoiChieu;

            if (row == null)
                return;

            if (e.Column.FieldName == nameof(DongDoiChieu.Chon))
            {
                row.SoLuongTra = row.Chon ? MacDinhSoLuongTra(row) : 0;
                _view.RefreshRow(e.RowHandle);
            }
            else if (e.Column.FieldName == nameof(DongDoiChieu.SoLuongTra) && row.SoLuongTra > 0 && !row.Chon)
            {
                row.Chon = true;
                _view.RefreshRow(e.RowHandle);
            }

            CapNhatTong();
        }

        private void View_RowCellStyle(object sender, RowCellStyleEventArgs e)
        {
            if (e.Column.FieldName != nameof(DongDoiChieu.SoLuongTra))
                return;

            var row = _view.GetRow(e.RowHandle) as DongDoiChieu;

            if (row != null && row.Chon && row.SoLuongTra > row.SoLuongDaGiao)
                e.Appearance.ForeColor = Color.Firebrick;
        }

        private void CapNhatTong()
        {
            int tong = _rows.Where(r => r.Chon).Sum(r => r.SoLuongTra);
            int soLan = _rows.Count(r => r.Chon);

            string text = $"Đã chọn {soLan} lần giao, tổng SL trả: {tong:n0}";

            if (_soLuongKhachBao > 0)
            {
                text += $"   |   Khách báo trả: {_soLuongKhachBao:n0}";

                int lech = tong - _soLuongKhachBao;
                if (soLan > 0 && lech != 0)
                    text += $"   |   Chênh lệch: {lech:+#,0;-#,0}";
            }

            _lblTong.Text = text;
            _lblTong.Appearance.ForeColor =
                _soLuongKhachBao > 0 && soLan > 0 && tong != _soLuongKhachBao
                    ? Color.DarkOrange
                    : Color.DimGray;
        }

        private void XacNhan()
        {
            _view.CloseEditor();
            _view.UpdateCurrentRow();

            var chon = _rows.Where(r => r.Chon).ToList();

            if (chon.Count == 0)
            {
                XtraMessageBox.Show(
                    "Chọn ít nhất một lần giao mà khách đã trả.",
                    "Chưa chọn phiếu giao",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (chon.Any(r => r.SoLuongTra <= 0))
            {
                XtraMessageBox.Show(
                    "Mỗi lần giao đã chọn phải có SL khách trả lớn hơn 0.",
                    "Số lượng không hợp lệ",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // SL trả được phép KHÁC SL đã giao → chỉ cảnh báo, không chặn.
            var vuot = chon.Where(r => r.SoLuongTra > r.SoLuongDaGiao).ToList();

            if (vuot.Count > 0 &&
                XtraMessageBox.Show(
                    $"{vuot.Count} lần giao có SL khách trả LỚN HƠN SL đã giao.\r\n" +
                    "Vẫn tiếp tục gán?",
                    "SL trả lớn hơn SL đã giao",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int tong = chon.Sum(r => r.SoLuongTra);

            if (_soLuongKhachBao > 0 && tong != _soLuongKhachBao &&
                XtraMessageBox.Show(
                    $"Tổng SL trả theo các lần giao ({tong:n0}) khác SL khách báo ({_soLuongKhachBao:n0}).\r\n" +
                    "Dòng khách trả sẽ lấy theo tổng SL đã phân bổ. Tiếp tục?",
                    "Chênh lệch số lượng",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            KetQua = chon.Select(r => new PhieuGiaoChon
            {
                Phieu = r.Phieu,
                SoLuongDaGiao = r.SoLuongDaGiao,
                SoLuongTra = r.SoLuongTra
            }).ToList();

            LotNo = _txtLot.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
