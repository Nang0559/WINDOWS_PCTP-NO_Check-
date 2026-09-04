using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK.UCControls
{
    /// <summary>
    /// Form chỉ XEM danh sách hàng đang tồn trong kho ảo A0 (BulkImportConfig).
    /// KHÔNG có thao tác xuất kho thủ công — việc trừ kho A0 được thực hiện tự động
    /// khi HVN_PGH cập nhật kho (xem BulkStockAdjustService.TruKhoAoTheoLot).
    /// </summary>
    public partial class FormBulkSlotView : DevExpress.XtraEditors.XtraForm
    {
        private readonly Slot _slot;
        private readonly ISlotService _slotService;   // ← thay cho StockService
        private GridControl _grid;
        private GridView _gridView;
        private LabelControl _lblSummary;

        public FormBulkSlotView(Slot slot, ISlotService slotService)
        {
            _slot = slot ?? throw new ArgumentNullException(nameof(slot));
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
            InitializeComponent();
            BuildUI();
            LoadData();
        }

        private void BuildUI()
        {
            this.Text = $"Kho tạm A0 — {_slot.whname}/{_slot.RackName}";
            this.Size = new Size(900, 550);
            this.StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // summary
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // grid
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // buttons

            // ── Row 0: Summary ───────────────────────────────────
            _lblSummary = new LabelControl
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 6, 0, 0),
                Appearance =
                {
                    Font = new Font("Tahoma", 9, FontStyle.Bold),
                    ForeColor = Color.DarkSlateGray
                }
            };
            mainLayout.Controls.Add(_lblSummary, 0, 0);

            // ── Row 1: Grid ───────────────────────────────────────
            _grid = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _gridView.OptionsView.ShowGroupPanel = true;
            _gridView.OptionsBehavior.Editable = false; // ← read-only, không cho sửa
            _gridView.OptionsSelection.EnableAppearanceFocusedCell = false;

            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "ItemCode",
                Caption = "Mã hàng",
                Width = 150,
                VisibleIndex = 0,
                OptionsColumn = { AllowGroup = DevExpress.Utils.DefaultBoolean.True }
            });
            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "LotNo",
                Caption = "LotNo",
                Width = 220,
                VisibleIndex = 1
            });
            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "Quantity",
                Caption = "Số lượng",
                Width = 100,
                VisibleIndex = 2
            });
            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "TemCode",
                Caption = "TemCode",
                Width = 150,
                VisibleIndex = 3
            });
            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "NgaySX",
                Caption = "Ngày SX",
                Width = 100,
                VisibleIndex = 4
            });
            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "ImportDate",
                Caption = "Ngày nhập",
                Width = 130,
                VisibleIndex = 5
            });
            _gridView.Columns["ImportDate"].DisplayFormat.FormatType =
                DevExpress.Utils.FormatType.DateTime;
            _gridView.Columns["ImportDate"].DisplayFormat.FormatString =
                "dd/MM/yyyy HH:mm";

            // Group mặc định theo mã hàng — dễ nhìn tổng SL từng mã đang tồn trong A0
            _gridView.Columns["ItemCode"].GroupIndex = 0;
            _gridView.GroupSummary.Add(new DevExpress.XtraGrid.GridGroupSummaryItem(
                DevExpress.Data.SummaryItemType.Sum, "Quantity", _gridView.Columns["Quantity"],
                "Tổng: {0}"));

            mainLayout.Controls.Add(_grid, 0, 1);

            // ── Row 2: Buttons ────────────────────────────────────
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(5)
            };

            var btnRefresh = new SimpleButton { Text = "🔄 Làm mới", Width = 100, Height = 32 };
            btnRefresh.Click += (s, e) => LoadData();

            var btnClose = new SimpleButton { Text = "Đóng", Width = 90, Height = 32 };
            btnClose.Click += (s, e) => this.Close();

            btnPanel.Controls.Add(btnClose);
            btnPanel.Controls.Add(btnRefresh);
            mainLayout.Controls.Add(btnPanel, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private void LoadData()
        {
            var lots = _slotService.GetLots(_slot.SlotId);   // ← thay _stockService.GetSlotLots(...)
            var dt = new DataTable();
            dt.Columns.Add("ItemCode", typeof(string));
            dt.Columns.Add("LotNo", typeof(string));
            dt.Columns.Add("Quantity", typeof(int));
            dt.Columns.Add("TemCode", typeof(string));
            dt.Columns.Add("NgaySX", typeof(string));
            dt.Columns.Add("ImportDate", typeof(DateTime));
            foreach (var lot in lots)
            {
                dt.Rows.Add(
                    lot.QRInfo?.ItemCode ?? "",
                    lot.LotNo ?? "",
                    lot.Quantity,
                    lot.TemCode ?? "",
                    lot.QRInfo?.NgaySX ?? "",
                    lot.QRInfo?.ImportDate ?? (object)DBNull.Value);
            }
            _grid.DataSource = dt;
            _gridView.BestFitColumns();
            int soMaHang = lots.Select(l => l.QRInfo?.ItemCode).Distinct().Count();
            int tongSL = lots.Sum(l => l.Quantity);
            _lblSummary.Text =
                $"Kho tạm: {_slot.whname} / {_slot.RackName}   |   " +
                $"Số mã hàng: {soMaHang}   |   Tổng số lượng: {tongSL}   |   " +
                $"Số LOT: {lots.Count}";
        }
    }
}