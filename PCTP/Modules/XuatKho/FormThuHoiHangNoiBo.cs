using System;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.XuLyHangLoi.Application;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Common;
using PCTP.Modules.GiaoHangKhach;

namespace PCTP.Modules.XuatKho
{
    /// <summary>Thu hồi tồn từ vị trí nội bộ không thuộc Slot về Slot vật lý và STOCKTP.</summary>
    public sealed class FormThuHoiHangNoiBo : XtraForm
    {
        private readonly XuLyHangLoiModuleFactory.Module _module;
        private readonly InternalTransferRepository _repository;
        private readonly TextEdit _txtLot = new TextEdit();
        private readonly SpinEdit _spinQuantity = new SpinEdit();
        private readonly LookUpEdit _lookSlot = new LookUpEdit();
        private readonly MemoEdit _memoReason = new MemoEdit();
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view;
        private readonly SimpleButton _btnSearch = new SimpleButton();
        private readonly SimpleButton _btnReturn = new SimpleButton();
        private readonly LabelControl _lblSummary = new LabelControl();
        private Guid _requestKey = Guid.NewGuid();
        private bool _returnCompleted;
        private DataTable _candidates;

        public FormThuHoiHangNoiBo()
        {
            Text = "Thu hồi hàng từ vị trí nội bộ về kho";
            Width = 1220;
            Height = 760;
            MinimumSize = new System.Drawing.Size(1000, 650);
            StartPosition = FormStartPosition.CenterParent;

            var provider = new PCTP.ClassSQL.SQLPROVIDER();
            var sql = new PhieuSqlExecutor(provider);
            var uow = new UnitOfWork(provider);
            _module = XuLyHangLoiModuleFactory.Build(sql, uow);
            _repository = new InternalTransferRepository(sql, uow);
            _view = new GridView(_grid);
            BuildUi();
            LoadCandidates(string.Empty);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            Controls.Add(root);

            var criteria = new GroupControl { Text = "Thông tin thu hồi", Dock = DockStyle.Fill };
            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, Padding = new Padding(8) };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            AddField(fields, 0, 0, "Lọc theo LOT", _txtLot);
            AddField(fields, 2, 0, "Slot nhận", _lookSlot);
            AddField(fields, 0, 1, "Số lượng", _spinQuantity);
            AddField(fields, 2, 1, "Lý do thu hồi", _memoReason);
            _txtLot.Properties.NullValuePrompt = "Tìm theo phiếu / vị trí nội bộ / mã hàng / LOT";
            _lookSlot.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            _lookSlot.Properties.SearchMode = DevExpress.XtraEditors.Controls.SearchMode.AutoFilter;
            _lookSlot.Properties.PopupFilterMode = DevExpress.XtraEditors.PopupFilterMode.Contains;
            _lookSlot.Properties.ImmediatePopup = true;
            _lookSlot.Properties.PopupWidth = 620;
            _lookSlot.Properties.ShowHeader = true;
            _lookSlot.Properties.ShowFooter = false;
            _lookSlot.EditValueChanged += (s, e) => UpdateReturnButtonState();
            _lookSlot.Properties.NullText = "Chọn Slot nhận hàng";
            _lookSlot.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            _spinQuantity.Properties.MinValue = 1;
            _spinQuantity.Properties.MaxValue = int.MaxValue;
            _spinQuantity.Properties.IsFloatValue = false;
            _memoReason.Properties.NullValuePrompt = "Bắt buộc nhập lý do thu hồi";
            criteria.Controls.Add(fields);
            root.Controls.Add(criteria, 0, 0);

            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsSelection.EnableAppearanceFocusedCell = false;
            _view.FocusRectStyle = DrawFocusRectStyle.RowFullFocus;
            _view.FocusedRowChanged += (s, e) => CandidateChanged();
            _view.DoubleClick += (s, e) => CandidateChanged();
            root.Controls.Add(_grid, 0, 1);

            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(2) };
            _btnSearch.Text = "Tìm nhanh lần chuyển";
            _btnSearch.Width = 165;
            _btnSearch.Click += (s, e) => LoadCandidates((_txtLot.Text ?? string.Empty).Trim());
            _txtLot.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; LoadCandidates((_txtLot.Text ?? string.Empty).Trim()); } };
            _btnReturn.Text = "Xác nhận thu hồi về kho";
            _btnReturn.Width = 190;
            _btnReturn.Enabled = false;
            _btnReturn.Click += (s, e) => ReturnToWarehouse();
            _lblSummary.AutoSizeMode = LabelAutoSizeMode.None;
            _lblSummary.Width = 470;
            _lblSummary.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            footer.Controls.Add(_btnSearch);
            footer.Controls.Add(_btnReturn);
            footer.Controls.Add(_lblSummary);
            root.Controls.Add(footer, 0, 2);
        }

        private static void AddField(TableLayoutPanel layout, int col, int row, string caption, Control editor)
        {
            var label = new LabelControl { Text = caption, Dock = DockStyle.Fill, AutoSizeMode = LabelAutoSizeMode.None };
            label.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            editor.Dock = DockStyle.Fill;
            layout.Controls.Add(label, col, row);
            layout.Controls.Add(editor, col + 1, row);
        }

        private void LoadCandidates(string lot)
        {
            try
            {
                _requestKey = Guid.NewGuid();
                _returnCompleted = false;
                _btnReturn.Enabled = false;
                _candidates = _repository.GetReturnCandidates(lot);
                _grid.DataSource = _candidates;
                ConfigureColumns();
                _view.BestFitColumns();
                _lblSummary.Text = string.Format("Có {0:n0} dòng LOT còn có thể thu hồi.", _candidates.Rows.Count);
                CandidateChanged();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Không thể tải tồn nội bộ: " + ex.Message, Text);
            }
        }

        private void ConfigureColumns()
        {
            _view.Columns.Clear();
            AddColumn("TransferNo", "Phiếu điều chuyển", 130);
            AddColumn("LocationName", "Vị trí nội bộ", 160);
            AddColumn("ItemCode", "Mã hàng", 130);
            AddColumn("LotNo", "LOT", 180);
            AddColumn("TransferQuantity", "Đã chuyển", 85);
            AddColumn("ReturnedQuantity", "Đã thu hồi", 85);
            AddColumn("RemainingFromTransfer", "Còn được thu hồi", 110);
            AddColumn("InternalQuantity", "Tồn nội bộ", 95);
        }

        private void AddColumn(string field, string caption, int width)
        {
            _view.Columns.Add(new GridColumn { FieldName = field, Caption = caption, Width = width, Visible = true });
        }

        private void CandidateChanged()
        {
            DataRow row = _view.GetFocusedDataRow();
            _btnReturn.Enabled = false;
            _lookSlot.EditValue = null;
            _lookSlot.Properties.DataSource = null;
            if (row == null)
            {
                _lblSummary.Text = "Chọn một lần điều chuyển còn số lượng để thu hồi.";
                return;
            }

            string itemCode = Convert.ToString(row["ItemCode"]);
            int remaining = Convert.ToInt32(row["RemainingFromTransfer"]);
            int internalQty = Convert.ToInt32(row["InternalQuantity"]);
            int max = Math.Min(remaining, internalQty);
            _spinQuantity.Properties.MaxValue = Math.Max(1, max);
            if (_spinQuantity.Value < 1 || _spinQuantity.Value > max)
                _spinQuantity.Value = Math.Max(1, max);

            try
            {
                DataTable slots = _repository.GetReturnSlots(itemCode);
                _lookSlot.Properties.DataSource = slots;
                _lookSlot.Properties.ValueMember = "SlotId";
                _lookSlot.Properties.DisplayMember = "DisplayText";
                _lookSlot.Properties.NullText = "Tìm / chọn Slot nhận hàng";
                _lookSlot.Properties.Columns.Clear();
                _lookSlot.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("WarehouseName", "Kho", 130));
                _lookSlot.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("RackName", "Rack", 100));
                _lookSlot.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("SlotNumber", "Slot", 55));
                _lookSlot.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("Quantity", "Đang chứa", 75));
                _lookSlot.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("Capacity", "Sức chứa", 75));
                _lookSlot.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("SlotState", "Trạng thái", 150));
                _lookSlot.EditValue = null;
                _lblSummary.Text = string.Format("LOT {0} / {1}: tồn nội bộ {2:n0}; còn được thu hồi {3:n0}. Có {4:n0} Slot phù hợp (gồm Slot trống và Slot cùng mã hàng còn sức chứa).",
                    row["LotNo"], itemCode, internalQty, max, slots.Rows.Count);
                UpdateReturnButtonState();
            }
            catch (Exception ex)
            {
                _lblSummary.Text = "Không thể nạp Slot nhận: " + ex.Message;
            }
        }

        private void UpdateReturnButtonState()
        {
            DataRow row = _view.GetFocusedDataRow();
            if (row == null || _returnCompleted || _lookSlot.EditValue == null)
            {
                _btnReturn.Enabled = false;
                return;
            }
            int remaining = Convert.ToInt32(row["RemainingFromTransfer"]);
            int internalQty = Convert.ToInt32(row["InternalQuantity"]);
            _btnReturn.Enabled = remaining > 0 && internalQty > 0;
        }

        private void ReturnToWarehouse()
        {
            DataRow row = _view.GetFocusedDataRow();
            if (row == null) { XtraMessageBox.Show(this, "Chọn một lần điều chuyển cần thu hồi.", Text); return; }
            if (_lookSlot.EditValue == null) { XtraMessageBox.Show(this, "Tìm và chọn một Slot nhận hàng trong danh sách Slot hiện hữu.", Text); return; }
            DataTable slotChoices = _lookSlot.Properties.DataSource as DataTable;
            if (slotChoices == null || !slotChoices.AsEnumerable().Any(x =>
                Convert.ToInt32(x["SlotId"]) == Convert.ToInt32(_lookSlot.EditValue)))
            {
                XtraMessageBox.Show(this, "Slot nhận không nằm trong danh sách Slot hợp lệ vừa tải. Hãy chọn lại.", Text);
                return;
            }
            string reason = (_memoReason.Text ?? string.Empty).Trim();
            if (reason.Length == 0) { XtraMessageBox.Show(this, "Nhập lý do thu hồi.", Text); return; }

            int quantity = Convert.ToInt32(_spinQuantity.Value);
            int max = Math.Min(Convert.ToInt32(row["RemainingFromTransfer"]), Convert.ToInt32(row["InternalQuantity"]));
            if (quantity <= 0 || quantity > max) { XtraMessageBox.Show(this, "Số lượng vượt phần tồn có thể thu hồi.", Text); return; }

            _btnReturn.Enabled = false;
            try
            {
                var result = _repository.ExecuteReturn(
                    _module.UnitOfWork,
                    _module.StockMovement,
                    _module.StockHistoryRepo,
                    _module.SlotService,
                    Convert.ToInt32(row["TransferId"]),
                    Convert.ToInt32(_lookSlot.EditValue),
                    quantity,
                    reason,
                    Environment.UserName,
                    _requestKey);

                _returnCompleted = true;
                _btnReturn.Enabled = false;
                _lblSummary.Text = "Đã thu hồi thành công. Tải lại danh sách để cập nhật tồn và chọn lần thu hồi tiếp theo.";
                XtraMessageBox.Show(this,
                    string.Format("Đã thu hồi {0:n0} sản phẩm LOT {1} về kho. Tồn nội bộ đã giảm, Slot và STOCKTP đã tăng. Phiếu {2}.",
                        quantity, row["LotNo"], result.ReturnNo),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Thu hồi thất bại; toàn bộ giao dịch đã rollback.\r\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!_returnCompleted) UpdateReturnButtonState();
            }
        }
    }
}
