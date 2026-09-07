using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Modules.NhapKho.Services;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace PCTP.VIEWSTOCK.UCControls
{
    // PCTP/VIEWSTOCK/UCControls/SlotDetailPanel.cs
    public class SlotDetailPanel : PanelControl
    {
        private readonly LabelControl _lblTitle;
        private readonly GridControl _gridStockTp;
        private readonly GridView _gvStockTp;
        private readonly GridControl _gridSlotLot;
        private readonly GridView _gvSlotLot;
        private readonly MemoEdit _memoCanhBao; // ← đổi từ LabelControl sang MemoEdit để wrap + scroll ổn định

        public SlotDetailPanel()
        {
            Dock = DockStyle.Right;
            Width = 340;
            Appearance.BorderColor = Color.Gainsboro;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); // ← tăng cao + cho scroll thay vì cắt
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));

            _lblTitle = new LabelControl
            {
                Dock = DockStyle.Fill,
                Appearance = { Font = new Font("Tahoma", 10, FontStyle.Bold) }
            };

            // ── MemoEdit read-only: tự wrap nhiều dòng + có scrollbar khi cảnh báo dài ──
            _memoCanhBao = new MemoEdit
            {
                Dock = DockStyle.Fill,
                Properties =
            {
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true
            }
            };

            // Cấu hình Appearance và Font thông qua Properties.Appearance chuẩn của DevExpress
            _memoCanhBao.Properties.Appearance.ForeColor = Color.Red;
            _memoCanhBao.Properties.Appearance.Font = new Font("Tahoma", 8.5F, FontStyle.Bold);
            _memoCanhBao.Properties.Appearance.BackColor = Color.FromArgb(255, 245, 245);
            _memoCanhBao.Properties.Appearance.Options.UseForeColor = true;
            _memoCanhBao.Properties.Appearance.Options.UseFont = true;
            _memoCanhBao.Properties.Appearance.Options.UseBackColor = true;
            _memoCanhBao.Properties.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;

            _gridStockTp = new GridControl { Dock = DockStyle.Fill };
            _gvStockTp = new GridView(_gridStockTp);
            _gridStockTp.MainView = _gvStockTp;
            _gvStockTp.OptionsView.ShowGroupPanel = false;
            _gvStockTp.OptionsBehavior.Editable = false;
            _gvStockTp.OptionsView.ColumnAutoWidth = true;

            _gridSlotLot = new GridControl { Dock = DockStyle.Fill };
            _gvSlotLot = new GridView(_gridSlotLot);
            _gridSlotLot.MainView = _gvSlotLot;
            _gvSlotLot.OptionsView.ShowGroupPanel = false;
            _gvSlotLot.OptionsBehavior.Editable = false;
            _gvSlotLot.OptionsView.ColumnAutoWidth = true;

            layout.Controls.Add(_lblTitle, 0, 0);
            layout.Controls.Add(_memoCanhBao, 0, 1);
            layout.Controls.Add(new GroupControl { Text = "STOCKTP (nguồn tồn kho chính)", Dock = DockStyle.Fill, Controls = { _gridStockTp } }, 0, 2);
            layout.Controls.Add(new GroupControl { Text = "Đang nằm trong Slot này", Dock = DockStyle.Fill, Controls = { _gridSlotLot } }, 0, 3);
            Controls.Add(layout);
        }

        /// <summary>Nạp thông tin đối chiếu cho 1 Slot đang chọn.</summary>
        public void ShowSlot(Slot slot, IStockTpLookupService stockTpLookup)
        {
            if (slot == null || !slot.IsOccupied)
            {
                _lblTitle.Text = "Chưa chọn Slot có hàng";
                _memoCanhBao.Text = "";
                BindGrid(_gridStockTp, _gvStockTp, null, "SLCONLAI", "SLXUAT");
                BindGrid(_gridSlotLot, _gvSlotLot, null, "Quantity");
                return;
            }

            _lblTitle.Text = $"{slot.whname} / {slot.RackName} / Slot {slot.SlotNumber}";

            // ── Bảng SlotLot đang có trong Slot này ──
            var dtSlot = new DataTable();
            dtSlot.Columns.Add("LotNo", typeof(string));
            dtSlot.Columns.Add("ItemCode", typeof(string));
            dtSlot.Columns.Add("Quantity", typeof(int));
            foreach (var lot in slot.Lots)
                dtSlot.Rows.Add(lot.LotNo ?? "", lot.QRInfo?.ItemCode ?? "", lot.Quantity);

            BindGrid(_gridSlotLot, _gvSlotLot, dtSlot, "Quantity");

            // ── Đối chiếu từng LOT với STOCKTP ──
            var dtStockTp = new DataTable();
            dtStockTp.Columns.Add("LOT", typeof(string));
            dtStockTp.Columns.Add("SLCONLAI", typeof(int));
            dtStockTp.Columns.Add("SLXUAT", typeof(int));
            dtStockTp.Columns.Add("Status", typeof(string));

            var canhBao = new List<string>();
            foreach (var lot in slot.Lots.GroupBy(l => l.LotNo))
            {
                var item = stockTpLookup.GetByLot(lot.Key);
                if (item == null)
                {
                    canhBao.Add($"⚠ LOT [{lot.Key}] không có trong STOCKTP!");
                    continue;
                }
                dtStockTp.Rows.Add(item.Lot, item.SlConLai ?? 0, item.SlXuat ?? 0,
                    item.Satus == 1 ? "Đã đủ" : "Đang SX");

                int tongTrongSlot = lot.Sum(x => x.Quantity);
                if (tongTrongSlot != (item.SlConLai ?? 0))
                    canhBao.Add($"⚠ LOT [{lot.Key}]: Slot có {tongTrongSlot} nhưng STOCKTP.SLCONLAI = {item.SlConLai}");
            }

            BindGrid(_gridStockTp, _gvStockTp, dtStockTp, "SLCONLAI", "SLXUAT");

            _memoCanhBao.Text = canhBao.Count > 0 ? string.Join(Environment.NewLine, canhBao) : "✅ Khớp dữ liệu";
            _memoCanhBao.Properties.Appearance.ForeColor = canhBao.Count > 0 ? Color.Red : Color.SeaGreen;
            _memoCanhBao.Properties.Appearance.Options.UseForeColor = true;
        }

        /// <summary>
        /// Ép GridView tái tạo cột mỗi lần bind — tránh lỗi cột "trơ" (hiển thị header
        /// nhưng rỗng dữ liệu) do DevExpress giữ schema cũ khi DataSource đổi liên tục.
        /// </summary>
        private void BindGrid(GridControl grid, GridView view, DataTable dt, params string[] numericColsFormatN0)
        {
            grid.DataSource = null;
            view.Columns.Clear();
            grid.DataSource = dt;

            if (dt == null) return;

            view.PopulateColumns();

            foreach (var colName in numericColsFormatN0)
            {
                var col = view.Columns[colName];
                if (col == null) continue;
                col.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
                col.DisplayFormat.FormatString = "n0";
            }

            view.BestFitColumns();
        }
    }
}
