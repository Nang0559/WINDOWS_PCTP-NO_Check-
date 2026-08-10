using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Models;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK.ViewForm
{
    public partial class FormChoGiaoHuy : XtraForm
    {
        private readonly TraHangService _traHangService;
        private readonly ITraHangRepository _traHangRepo;
        private GridControl _grid;
        private GridView _gridView;
        private TextEdit _txtLyDo;
        private LabelControl _lblSummary;

        public FormChoGiaoHuy(TraHangService svc, ITraHangRepository repo)
        {
            _traHangService = svc;
            _traHangRepo = repo;
            BuildUI();
            LoadData();
        }

        private void BuildUI()
        {
            Text = "Danh sách chờ giao — Huỷ (trả về sản xuất rework)";
            Size = new System.Drawing.Size(1000, 620);
            StartPosition = FormStartPosition.CenterParent;

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));

            _grid = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _gridView.OptionsBehavior.Editable = false;
            _gridView.OptionsSelection.MultiSelect = true;
            _gridView.OptionsSelection.MultiSelectMode = GridMultiSelectMode.CheckBoxRowSelect;

            _gridView.Columns.Add(new GridColumn { FieldName = "LotThung", Caption = "Lot Thùng", Width = 180, VisibleIndex = 0 });
            _gridView.Columns.Add(new GridColumn { FieldName = "LotGoc", Caption = "Lot Gốc", Width = 180, VisibleIndex = 1 });
            _gridView.Columns.Add(new GridColumn { FieldName = "MaHang", Caption = "Mã hàng", Width = 150, VisibleIndex = 2 });
            _gridView.Columns.Add(new GridColumn { FieldName = "SoLuong", Caption = "Số lượng", Width = 90, VisibleIndex = 3 });
            _gridView.Columns.Add(new GridColumn { FieldName = "PhieuGiaoId", Caption = "Phiếu giao", Width = 130, VisibleIndex = 4 });
            _gridView.Columns.Add(new GridColumn { FieldName = "TrangThai", Caption = "Trạng thái", Width = 100, VisibleIndex = 5 });

            main.Controls.Add(_grid, 0, 0);

            _lblSummary = new LabelControl { Dock = DockStyle.Fill, Appearance = { Font = new System.Drawing.Font("Tahoma", 9, System.Drawing.FontStyle.Bold) } };
            main.Controls.Add(_lblSummary, 0, 1);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));

            bottom.Controls.Add(new LabelControl { Text = "Lý do:", Dock = DockStyle.Fill, Appearance = { TextOptions = { VAlignment = DevExpress.Utils.VertAlignment.Center } } }, 0, 0);
            _txtLyDo = new TextEdit { Dock = DockStyle.Fill };
            bottom.Controls.Add(_txtLyDo, 1, 0);

            var btnHuy = new SimpleButton { Text = "Huỷ — Trả về sản xuất", Dock = DockStyle.Fill };
            btnHuy.Appearance.BackColor = System.Drawing.Color.IndianRed;
            btnHuy.Appearance.ForeColor = System.Drawing.Color.White;
            btnHuy.Click += BtnHuy_Click;
            bottom.Controls.Add(btnHuy, 2, 0);

            main.Controls.Add(bottom, 0, 2);
            Controls.Add(main);
        }

        private void LoadData()
        {
            var items = _traHangRepo.GetChoGiaoDangCho();
            _grid.DataSource = items;
            _lblSummary.Text = $"Tổng dòng chờ giao: {items.Count} | Tổng SL: {items.Sum(x => x.SoLuong)}";
        }

        private void BtnHuy_Click(object sender, EventArgs e)
        {
            var rows = _gridView.GetSelectedRows()
                .Select(h => _gridView.GetRow(h) as ChoGiaoItem)
                .Where(x => x != null)
                .ToList();

            if (rows.Count == 0)
            {
                XtraMessageBox.Show("Vui lòng chọn ít nhất 1 dòng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string lyDo = _txtLyDo.Text.Trim();
            if (string.IsNullOrEmpty(lyDo))
            {
                XtraMessageBox.Show("Vui lòng nhập lý do trả hàng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var tongSl = rows.Sum(x => x.SoLuong);
            var soLot = rows.Select(x => x.LotGoc).Distinct().Count();
            if (XtraMessageBox.Show(
                $"Huỷ {rows.Count} thùng ({soLot} LOT, tổng SL {tongSl}) và trả về sản xuất rework?",
                "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            var result = _traHangService.HuyChoGiaoVeSanXuat(rows.Select(x => x.Id).ToList(), lyDo);

            if (!result.IsOK)
                XtraMessageBox.Show(result.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
                XtraMessageBox.Show(result.Message, "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

            LoadData();
        }
    }
}