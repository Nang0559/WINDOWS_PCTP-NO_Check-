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
    public partial class FormXacNhanGiaoNoiBo : XtraForm
    {
        private readonly TraHangService _traHangService;
        private readonly ITraHangRepository _traHangRepo;
        private GridControl _grid;
        private GridView _gridView;
        private LabelControl _lblSummary;

        public FormXacNhanGiaoNoiBo(TraHangService svc, ITraHangRepository repo)
        {
            _traHangService = svc;
            _traHangRepo = repo;
            BuildUI();
            LoadData();
        }

        private void BuildUI()
        {
            Text = "Xác nhận giao nội bộ (hàng đã rời kho, không qua CNK phiếu HVN)";
            Size = new System.Drawing.Size(1000, 600);
            StartPosition = FormStartPosition.CenterParent;

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));

            var lblWarn = new LabelControl
            {
                Dock = DockStyle.Top,
                Text = "⚠ CHỈ dùng cho hàng KHÔNG đi qua phiếu giao HVN (chuyển kho nội bộ, xuất mục đích khác). " +
                       "Nếu LOT sẽ được CNK ở HVN_PGH, KHÔNG xác nhận ở đây.",
                Appearance = { ForeColor = System.Drawing.Color.DarkOrange, Font = new System.Drawing.Font("Tahoma", 9, System.Drawing.FontStyle.Bold) },
                AutoSizeMode = LabelAutoSizeMode.Vertical
            };

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

            var panelTop = new Panel { Dock = DockStyle.Fill };
            panelTop.Controls.Add(_grid);
            panelTop.Controls.Add(lblWarn);

            main.Controls.Add(panelTop, 0, 0);

            _lblSummary = new LabelControl { Dock = DockStyle.Fill, Appearance = { Font = new System.Drawing.Font("Tahoma", 9, System.Drawing.FontStyle.Bold) } };
            main.Controls.Add(_lblSummary, 0, 1);

            var btnXacNhan = new SimpleButton { Text = "✅ Xác nhận đã giao", Dock = DockStyle.Right, Width = 220 };
            btnXacNhan.Appearance.BackColor = System.Drawing.Color.SeaGreen;
            btnXacNhan.Appearance.ForeColor = System.Drawing.Color.White;
            btnXacNhan.Click += BtnXacNhan_Click;

            var bottomPanel = new Panel { Dock = DockStyle.Fill };
            bottomPanel.Controls.Add(btnXacNhan);
            main.Controls.Add(bottomPanel, 1, 2);

            Controls.Add(main);
        }

        private void LoadData()
        {
            var items = _traHangRepo.GetChoGiaoDangCho();
            _grid.DataSource = items;
            _lblSummary.Text = $"Chờ giao: {items.Count} dòng | Tổng SL: {items.Sum(x => x.SoLuong)}";
        }

        private void BtnXacNhan_Click(object sender, EventArgs e)
        {
            var rows = _gridView.GetSelectedRows()
                .Select(h => _gridView.GetRow(h) as ChoGiaoItem)
                .Where(x => x != null).ToList();

            if (rows.Count == 0)
            {
                XtraMessageBox.Show("Chưa chọn dòng nào.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (XtraMessageBox.Show(
                $"Xác nhận {rows.Count} thùng ({rows.Select(x => x.LotGoc).Distinct().Count()} LOT) đã thực sự rời kho?",
                "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            var result = _traHangService.XacNhanDaGiao(rows.Select(x => x.Id).ToList());

            XtraMessageBox.Show(result.Message, result.IsOK ? "Thành công" : "Lỗi",
                MessageBoxButtons.OK, result.IsOK ? MessageBoxIcon.Information : MessageBoxIcon.Error);

            LoadData();
        }
    }
}