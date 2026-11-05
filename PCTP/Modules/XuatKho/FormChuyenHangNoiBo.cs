using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.XuLyHangLoi.Application;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Common;
using PCTP.Modules.KhoVatLy.Kho.Models;

namespace PCTP.Modules.XuatKho
{
    /// <summary>
    /// Chuyển tồn từ Slot sang khu vực nội bộ không thuộc bản đồ Slot.
    /// Không tạo phiếu giao hàng. STOCKTP giảm và tồn khu vực nội bộ tăng
    /// trong cùng UnitOfWork transaction.
    /// </summary>
    public sealed class FormChuyenHangNoiBo : XtraForm
    {
        private readonly XuLyHangLoiModuleFactory.Module _module;
        private readonly SlotRepository _slotRepository;
        private readonly PCTP.Modules.XuatKho.Repositories.StockExportRepository _stockRepository;
        private readonly PCTP.Modules.XuatKho.Repositories.InternalTransferRepository _transferRepository;
        private readonly TextEdit _txtLot = new TextEdit();
        private readonly TextEdit _txtDestination = new TextEdit();
        private readonly TextEdit _txtReason = new TextEdit();
        private readonly SpinEdit _numQuantity = new SpinEdit();
        private readonly GridControlHost _grid = new GridControlHost();
        private readonly LabelControl _lblStock = new LabelControl();
        private readonly SimpleButton _btnSearch = new SimpleButton();
        private readonly SimpleButton _btnTransfer = new SimpleButton();
        private readonly SimpleButton _btnInternalStock = new SimpleButton();
        private List<SlotLotViewInfo> _sourceRows = new List<SlotLotViewInfo>();
        private Guid _requestKey = Guid.NewGuid();
        private bool _transferCompleted;
        private bool _sourceBalanceConsistent;

        public FormChuyenHangNoiBo()
        {
            Text = "Chuyển hàng sang vị trí nội bộ";
            Width = 1120;
            Height = 720;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new System.Drawing.Size(900, 600);

            var provider = new PCTP.ClassSQL.SQLPROVIDER();
            var sql = new PhieuSqlExecutor(provider);
            var uow = new UnitOfWork(provider);
            _module = XuLyHangLoiModuleFactory.Build(sql, uow);
            _slotRepository = new SlotRepository(sql, uow);
            _stockRepository = new PCTP.Modules.XuatKho.Repositories.StockExportRepository(sql, uow);
            _transferRepository = new PCTP.Modules.XuatKho.Repositories.InternalTransferRepository(sql, uow);

            BuildUi();
        }

        private void BuildUi()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(12) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(layout);

            AddRow(layout, 0, "LOT cần chuyển", _txtLot);
            _txtLot.Properties.NullValuePrompt = "Nhập LOTNO để tìm trong tồn Slot";
            AddRow(layout, 1, "Vị trí nội bộ đích", _txtDestination);
            _txtDestination.Properties.NullValuePrompt = "Ví dụ: LINE-A / KHU-CHO-SUA / QC-INTERNAL";
            AddRow(layout, 2, "Số lượng chuyển", _numQuantity);
            _numQuantity.Properties.MinValue = 1;
            _numQuantity.Properties.MaxValue = int.MaxValue;
            _numQuantity.Properties.IsFloatValue = false;
            AddRow(layout, 3, "Lý do", _txtReason);
            _txtReason.Properties.NullValuePrompt = "Bắt buộc nhập lý do điều chuyển";

            _grid.Dock = DockStyle.Fill;
            layout.Controls.Add(_grid, 0, 4);
            layout.SetColumnSpan(_grid, 2);

            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            _btnSearch.Text = "Tìm LOT trong kho";
            _btnSearch.Width = 150;
            _btnSearch.Click += (s, e) => SearchLot();
            _btnTransfer.Text = "Xác nhận chuyển";
            _btnTransfer.Width = 150;
            _btnTransfer.Click += (s, e) => Transfer();
            _lblStock.AutoSize = true;
            footer.Controls.Add(_btnSearch);
            footer.Controls.Add(_btnTransfer);
            _btnInternalStock.Text = "Xem tồn nội bộ";
            _btnInternalStock.Width = 130;
            _btnInternalStock.Click += (s, e) => ShowInternalStock();
            footer.Controls.Add(_btnInternalStock);
            footer.Controls.Add(_lblStock);
            layout.Controls.Add(footer, 0, 5);
            layout.SetColumnSpan(footer, 2);
        }

        private static void AddRow(TableLayoutPanel layout, int row, string label, Control editor)
        {
            var caption = new LabelControl { Text = label, Dock = DockStyle.Fill, AutoSizeMode = LabelAutoSizeMode.None };
            caption.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            editor.Dock = DockStyle.Fill;
            layout.Controls.Add(caption, 0, row);
            layout.Controls.Add(editor, 1, row);
        }

        private void SearchLot()
        {
            string lot = (_txtLot.Text ?? string.Empty).Trim();
            if (lot.Length == 0)
            {
                XtraMessageBox.Show(this, "Vui lòng nhập LOTNO.", Text);
                return;
            }

            try
            {
                _requestKey = Guid.NewGuid();
                _transferCompleted = false;
                _sourceBalanceConsistent = false;
                _btnTransfer.Enabled = false;
                _sourceRows = _slotRepository.GetAllActiveSlotLots()
                    .Where(x => string.Equals((x.LotNo ?? string.Empty).Trim(), lot, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.WarehouseName).ThenBy(x => x.RackName).ThenBy(x => x.SlotNumber)
                    .ToList();

                _grid.SetRows(_sourceRows);
                int stock = _stockRepository.GetSlConLai(lot);
                int slotTotal = _sourceRows.Sum(x => x.Quantity);
                _sourceBalanceConsistent = _sourceRows.Count > 0 && stock == slotTotal;
                _btnTransfer.Enabled = _sourceBalanceConsistent;
                _lblStock.Text = string.Format("STOCKTP còn: {0:N0} | Tồn Slot khả dụng: {1:N0}", stock, slotTotal);
                if (_sourceRows.Count == 0)
                    XtraMessageBox.Show(this, "Không tìm thấy LOT đang hoạt động trong Slot. Hàng cách ly không được phép chuyển.", Text);
                else if (stock != slotTotal)
                    XtraMessageBox.Show(this, "Cảnh báo: tổng tồn Slot khác STOCKTP. Không thực hiện chuyển cho đến khi đối soát.", Text);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Không thể tra cứu LOT: " + ex.Message, Text);
            }
        }

        private void ShowInternalStock()
        {
            try
            {
                _btnTransfer.Enabled = false;
                _sourceBalanceConsistent = false;
                _grid.SetTable(_transferRepository.GetInternalStock((_txtLot.Text ?? string.Empty).Trim()));
                _lblStock.Text = "Tồn tại vị trí nội bộ (lọc theo LOT nếu đã nhập).";
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Không thể đọc tồn nội bộ: " + ex.Message, Text);
            }
        }

        private void Transfer()
        {
            var source = _grid.SelectedRow;
            string destination = (_txtDestination.Text ?? string.Empty).Trim();
            string reason = (_txtReason.Text ?? string.Empty).Trim();
            int quantity = Convert.ToInt32(_numQuantity.Value);

            if (!_sourceBalanceConsistent) { XtraMessageBox.Show(this, "Chưa có kết quả LOT hợp lệ hoặc tổng Slot không khớp STOCKTP. Không thể chuyển.", Text); return; }
            if (source == null) { XtraMessageBox.Show(this, "Chọn một dòng LOT/Slot nguồn.", Text); return; }
            if (destination.Length == 0) { XtraMessageBox.Show(this, "Nhập vị trí nội bộ đích.", Text); return; }
            if (reason.Length == 0) { XtraMessageBox.Show(this, "Nhập lý do chuyển hàng.", Text); return; }
            if (quantity <= 0 || quantity > source.Quantity) { XtraMessageBox.Show(this, "Số lượng phải lớn hơn 0 và không vượt tồn Slot đã chọn.", Text); return; }

            _btnTransfer.Enabled = false;
            try
            {
                var result = _transferRepository.ExecuteTransfer(
                    _module.UnitOfWork,
                    _module.StockMovement,
                    _module.StockHistoryRepo,
                    _module.SlotService,
                    source,
                    destination,
                    reason,
                    quantity,
                    Environment.UserName,
                    _requestKey);

                if (!result.Success)
                    throw new InvalidOperationException(result.Message);

                XtraMessageBox.Show(this,
                    string.Format("Đã chuyển {0:N0} sản phẩm LOT {1} từ Slot {2} sang {3}. STOCKTP đã giảm và tồn nội bộ đã tăng.",
                        quantity, source.LotNo, source.SlotId, destination),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);

                _transferCompleted = true;
                _btnTransfer.Enabled = false;
                _lblStock.Text = "Đã chuyển thành công. Bấm Tìm LOT để bắt đầu một giao dịch mới.";
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Chuyển hàng thất bại; transaction đã rollback.\r\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { _btnTransfer.Enabled = !_transferCompleted && _sourceBalanceConsistent; }
        }

        /// <summary>Small DataGridView wrapper to keep this screen independent of a designer file.</summary>
        private sealed class GridControlHost : UserControl
        {
            private readonly DataGridView _view = new DataGridView();
            public GridControlHost()
            {
                _view.Dock = DockStyle.Fill;
                _view.ReadOnly = true;
                _view.AllowUserToAddRows = false;
                _view.AllowUserToDeleteRows = false;
                _view.MultiSelect = false;
                _view.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                _view.AutoGenerateColumns = true;
                Controls.Add(_view);
            }
            public SlotLotViewInfo SelectedRow
            {
                get { return _view.CurrentRow == null ? null : _view.CurrentRow.DataBoundItem as SlotLotViewInfo; }
            }
            public void SetRows(List<SlotLotViewInfo> rows) { _view.DataSource = null; _view.DataSource = rows; }
            public void SetTable(System.Data.DataTable table) { _view.DataSource = null; _view.DataSource = table; }
        }
    }
}
