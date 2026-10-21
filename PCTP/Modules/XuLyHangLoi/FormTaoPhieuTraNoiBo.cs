using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi
{
    using DevExpress.XtraEditors;
    using DevExpress.XtraEditors.Repository;
    using DevExpress.XtraGrid.Views.Base;
    using PCTP.Modules.KhoVatLy.Application.Interfaces;
    using PCTP.Modules.XuLyHangLoi.Repository;
    using PCTP.Modules.XuLyHangLoi.Enums;
    using PCTP.Modules.XuLyHangLoi.Models;
    using PCTP.Modules.XuLyHangLoi.Services;
    using PCTP.Shared.Enums;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Forms;

    public partial class FormTaoPhieuTraNoiBo : XtraForm
    {
        private readonly ITraNoiBoService _traNoiBoService;
        private readonly INoiBoLookupRepository _lookupRepo;
        private readonly ISlotService _slotService;

        // Dữ liệu lookup (load 1 lần khi mở form)
        private List<DeptLookupItem> _depts = new List<DeptLookupItem>();
        private List<ItemLookupItem> _items = new List<ItemLookupItem>();
        private List<SlotNguonLookupItem> _slotItems = new List<SlotNguonLookupItem>();
        private bool _updatingRow;

        // ============================================================
        // HEADER
        // ============================================================

        private TextEdit _txtSoPhieu;
        private SearchLookUpEdit _lookPhongBan;
        private SearchLookUpEdit _lookBoPhanPhatHienLoi;
        private TextEdit _txtLyDo;
        private TextEdit _txtNguoiTao;

        // ============================================================
        // GRID
        // ============================================================

        private DevExpress.XtraGrid.GridControl _grid;
        private DevExpress.XtraGrid.Views.Grid.GridView _gridView;

        private RepositoryItemSearchLookUpEdit _repoSlot;
        private RepositoryItemSearchLookUpEdit _repoMaHang;

        private DevExpress.XtraEditors.SimpleButton _btnThem;
        private DevExpress.XtraEditors.SimpleButton _btnXoa;
        private DevExpress.XtraEditors.SimpleButton _btnLuu;
        private DevExpress.XtraEditors.SimpleButton _btnHuy;

        public FormTaoPhieuTraNoiBo(
            ITraNoiBoService traNoiBoService,
            INoiBoLookupRepository lookupRepo,
            ISlotService slotService)
        {
            _traNoiBoService = traNoiBoService
                ?? throw new ArgumentNullException(nameof(traNoiBoService));

            _lookupRepo = lookupRepo
                ?? throw new ArgumentNullException(nameof(lookupRepo));

            _slotService = slotService
                ?? throw new ArgumentNullException(nameof(slotService));

            BuiUI();
            LoadLookups();
        }

        // ============================================================
        // LOAD LOOKUP: B20Dept (bộ phận) + Slot đang chứa hàng
        // ============================================================

        private void LoadLookups()
        {
            try
            {
                _depts = _lookupRepo.GetActiveDepartments()
                    ?? new List<DeptLookupItem>();

                _items = _lookupRepo.GetItemsInSlots()
                    ?? new List<ItemLookupItem>();

                var nameByCode = _items
                    .GroupBy(x => x.ItemCode ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().ItemName,
                        StringComparer.OrdinalIgnoreCase);

                _slotItems = (_slotService.GetAllActiveSlotLots()
                        ?? new List<PCTP.Modules.KhoCore.Models.SlotLotViewInfo>())
                    .Where(x => x.Quantity > 0)
                    .Select(x => new SlotNguonLookupItem
                    {
                        SlotLotId = x.SlotLotId,
                        SlotId = x.SlotId,
                        ViTri = $"{x.WarehouseName} / {x.RackName} / Slot {x.SlotNumber}",
                        ItemCode = x.ItemCode,
                        TenHang = x.ItemCode != null
                                  && nameByCode.TryGetValue(x.ItemCode.Trim(), out var n)
                            ? n
                            : string.Empty,
                        LotNo = x.LotNo,
                        Quantity = x.Quantity,
                        TemCode = x.TemCode
                    })
                    .ToList();

                BindDeptLookup(_lookPhongBan);
                BindDeptLookup(_lookBoPhanPhatHienLoi);

                _repoSlot.DataSource = _slotItems;
                _repoSlot.PopulateViewColumns();
                CauHinhCotSlotLookup();

                _repoMaHang.DataSource = _items;
                _repoMaHang.PopulateViewColumns();
                CauHinhCotMaHangLookup();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    $"Không tải được danh sách bộ phận / slot.\r\n\r\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void BindDeptLookup(SearchLookUpEdit look)
        {
            look.Properties.DataSource = _depts;
            look.Properties.DisplayMember = nameof(DeptLookupItem.Display);
            look.Properties.ValueMember = nameof(DeptLookupItem.Code);
            look.Properties.PopulateViewColumns();

            var view = look.Properties.View;

            if (view.Columns[nameof(DeptLookupItem.Display)] != null)
                view.Columns[nameof(DeptLookupItem.Display)].Visible = false;

            var colCode = view.Columns[nameof(DeptLookupItem.Code)];
            if (colCode != null)
            {
                colCode.Caption = "Mã bộ phận";
                colCode.Width = 110;
            }

            var colName = view.Columns[nameof(DeptLookupItem.Name)];
            if (colName != null)
            {
                colName.Caption = "Tên bộ phận";
                colName.Width = 320;
            }
        }

        private static SearchLookUpEdit TaoDeptLookup()
        {
            var look = new SearchLookUpEdit { Dock = DockStyle.Fill };
            look.Properties.NullText = "";
            look.Properties.ShowClearButton = true;
            look.Properties.ImmediatePopup = true;
            look.Properties.PopupFormSize = new System.Drawing.Size(520, 360);
            look.Properties.View.OptionsView.ShowAutoFilterRow = false;
            // Ô tìm kiếm tìm trên cả Code và Name (các cột đang hiển thị).
            look.Properties.View.OptionsFind.AlwaysVisible = true;
            look.Properties.View.OptionsFind.FindNullPrompt = "Tìm theo mã hoặc tên...";
            look.Properties.View.OptionsFind.ShowCloseButton = false;
            return look;
        }

        private static string GetDeptName(SearchLookUpEdit look, List<DeptLookupItem> depts)
        {
            var code = look.EditValue as string;
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;

            var dept = depts.FirstOrDefault(d =>
                string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase));

            if (dept == null) return code;
            return string.IsNullOrWhiteSpace(dept.Name) ? dept.Code : dept.Name;
        }

        // ============================================================
        // UI
        // ============================================================

        private void BuiUI()
        {
            Text = "Tạo phiếu trả nội bộ";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1200;
            Height = 700;

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };

            main.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 150));

            main.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            main.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 45));

            // ========================================================
            // HEADER
            // ========================================================

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3
            };

            for (int i = 0; i < 4; i++)
            {
                header.ColumnStyles.Add(
                    new ColumnStyle(
                        i % 2 == 0
                            ? SizeType.Absolute
                            : SizeType.Percent,
                        i % 2 == 0 ? 140 : 50));
            }

            for (int i = 0; i < 3; i++)
            {
                header.RowStyles.Add(
                    new RowStyle(SizeType.Percent, 33.33f));
            }

            _txtSoPhieu = new TextEdit
            {
                Dock = DockStyle.Fill
            };

            _lookPhongBan = TaoDeptLookup();

            _lookBoPhanPhatHienLoi = TaoDeptLookup();

            _txtLyDo = new TextEdit
            {
                Dock = DockStyle.Fill
            };

            _txtNguoiTao = new TextEdit
            {
                Dock = DockStyle.Fill,
                Text = Environment.UserName
            };

            AddLabel(header, "Số phiếu", 0, 0);
            header.Controls.Add(_txtSoPhieu, 1, 0);

            AddLabel(header, "Phòng ban", 2, 0);
            header.Controls.Add(_lookPhongBan, 3, 0);

            AddLabel(header, "BP phát hiện lỗi", 0, 1);
            header.Controls.Add(_lookBoPhanPhatHienLoi, 1, 1);

            AddLabel(header, "Người tạo", 2, 1);
            header.Controls.Add(_txtNguoiTao, 3, 1);

            AddLabel(header, "Lý do", 0, 2);
            header.Controls.Add(_txtLyDo, 1, 2);
            header.SetColumnSpan(_txtLyDo, 3);

            main.Controls.Add(header, 0, 0);

            // ========================================================
            // GRID CHI TIET
            // ========================================================

            _grid = new DevExpress.XtraGrid.GridControl
            {
                Dock = DockStyle.Fill
            };

            _gridView = new DevExpress.XtraGrid.Views.Grid.GridView(_grid);

            _grid.MainView = _gridView;
            _grid.ViewCollection.Add(_gridView);

            _gridView.OptionsBehavior.Editable = true;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.NewItemRowPosition =
                DevExpress.XtraGrid.Views.Grid.NewItemRowPosition.None;

            TaoCotGrid();

            _gridView.ShownEditor += GridView_ShownEditor;
            _gridView.CellValueChanged += GridView_CellValueChanged;

            main.Controls.Add(_grid, 0, 1);

            // ========================================================
            // BUTTON
            // ========================================================

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            _btnHuy = new SimpleButton
            {
                Text = "Hủy",
                Width = 100
            };

            _btnLuu = new SimpleButton
            {
                Text = "💾 Lưu phiếu",
                Width = 130
            };

            _btnXoa = new SimpleButton
            {
                Text = "🗑 Xóa dòng",
                Width = 110
            };

            _btnThem = new SimpleButton
            {
                Text = "➕ Thêm dòng",
                Width = 110
            };

            _btnHuy.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _btnLuu.Click += (s, e) => LuuPhieu();

            _btnThem.Click += (s, e) => ThemDong();

            _btnXoa.Click += (s, e) => XoaDong();

            buttons.Controls.Add(_btnHuy);
            buttons.Controls.Add(_btnLuu);
            buttons.Controls.Add(_btnXoa);
            buttons.Controls.Add(_btnThem);

            main.Controls.Add(buttons, 0, 2);

            Controls.Add(main);
        }

        private void AddLabel(
            TableLayoutPanel panel,
            string text,
            int column,
            int row)
        {
            var label = new LabelControl
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Padding = new Padding(5, 8, 0, 0)
            };

            panel.Controls.Add(label, column, row);
        }

        // ============================================================
        // GRID
        // ============================================================

        private void TaoCotGrid()
        {
            _gridView.Columns.Clear();

            // ---- Repository: Slot nguồn (Slot đang chứa hàng) ----
            _repoSlot = new RepositoryItemSearchLookUpEdit
            {
                NullText = "",
                ValueMember = nameof(SlotNguonLookupItem.SlotLotId),
                DisplayMember = nameof(SlotNguonLookupItem.ViTri),
                ImmediatePopup = true,
                PopupFormSize = new System.Drawing.Size(820, 380)
            };
            _repoSlot.DataSource = _slotItems;
            _repoSlot.PopulateViewColumns();
            _repoSlot.View.OptionsFind.AlwaysVisible = true;
            _repoSlot.View.OptionsFind.FindNullPrompt = "Tìm theo vị trí, mã hàng, lot...";
            _repoSlot.View.OptionsFind.ShowCloseButton = false;
            CauHinhCotSlotLookup();

            // ---- Repository: Mã hàng (mã hàng có trong Slot) ----
            _repoMaHang = new RepositoryItemSearchLookUpEdit
            {
                NullText = "",
                ValueMember = nameof(ItemLookupItem.ItemCode),
                DisplayMember = nameof(ItemLookupItem.ItemCode),
                ImmediatePopup = true,
                PopupFormSize = new System.Drawing.Size(560, 360)
            };
            _repoMaHang.DataSource = _items;
            _repoMaHang.PopulateViewColumns();
            _repoMaHang.View.OptionsFind.AlwaysVisible = true;
            _repoMaHang.View.OptionsFind.FindNullPrompt = "Tìm theo mã hoặc tên hàng...";
            _repoMaHang.View.OptionsFind.ShowCloseButton = false;
            CauHinhCotMaHangLookup();

            var repoSoLuong = new RepositoryItemSpinEdit
            {
                IsFloatValue = false,
                MinValue = 0,
                MaxValue = int.MaxValue
            };

            _grid.RepositoryItems.AddRange(
                new RepositoryItem[] { _repoSlot, _repoMaHang, repoSoLuong });

            var colSlot = _gridView.Columns.AddField(nameof(PhieuTraHangCT.SlotLotIdNguon));
            colSlot.Caption = "Slot nguồn";
            colSlot.Visible = true;
            colSlot.Width = 230;
            colSlot.ColumnEdit = _repoSlot;

            var colSlotId = _gridView.Columns.AddField(nameof(PhieuTraHangCT.SlotIdNguon));
            colSlotId.Caption = "SlotId";
            colSlotId.Visible = false;

            var colMaHang = _gridView.Columns.AddField(nameof(PhieuTraHangCT.MaHang));
            colMaHang.Caption = "Mã hàng";
            colMaHang.Visible = true;
            colMaHang.Width = 140;
            colMaHang.ColumnEdit = _repoMaHang;

            var colTenHang = _gridView.Columns.AddField(nameof(PhieuTraHangCT.TenHang));
            colTenHang.Caption = "Tên hàng";
            colTenHang.Visible = true;
            colTenHang.Width = 180;
            colTenHang.OptionsColumn.AllowEdit = false;

            var colLot = _gridView.Columns.AddField(nameof(PhieuTraHangCT.LotNo));
            colLot.Caption = "Lot No";
            colLot.Visible = true;
            colLot.Width = 130;
            colLot.OptionsColumn.AllowEdit = false;

            var colSoLuong = _gridView.Columns.AddField(nameof(PhieuTraHangCT.SoLuong));
            colSoLuong.Caption = "Số lượng";
            colSoLuong.Visible = true;
            colSoLuong.Width = 90;
            colSoLuong.ColumnEdit = repoSoLuong;

            var colLyDo = _gridView.Columns.AddField(nameof(PhieuTraHangCT.LyDoNg));
            colLyDo.Caption = "Lý do NG";
            colLyDo.Visible = true;
            colLyDo.Width = 220;

            // Các cột đối chiếu phiếu giao:
            var colDinhDanh = _gridView.Columns.AddField(
                nameof(PhieuTraHangCT.DinhDanhPhieuGiao));

            colDinhDanh.Caption = "Phiếu giao gốc";
            colDinhDanh.Visible = false;

            var colPo = _gridView.Columns.AddField(
                nameof(PhieuTraHangCT.PoNo));

            colPo.Caption = "PO";
            colPo.Visible = false;

            var colNgayGiao = _gridView.Columns.AddField(
                nameof(PhieuTraHangCT.NgayGiao));

            colNgayGiao.Caption = "Ngày giao";
            colNgayGiao.Visible = false;

            var colNhaMay = _gridView.Columns.AddField(
                nameof(PhieuTraHangCT.NhaMay));

            colNhaMay.Caption = "Nhà máy";
            colNhaMay.Visible = false;
        }

        private void CauHinhCotSlotLookup()
        {
            var v = _repoSlot.View;

            void Cot(string field, string caption, int width, bool visible = true)
            {
                var c = v.Columns[field];
                if (c == null) return;
                c.Caption = caption;
                c.Width = width;
                c.Visible = visible;
            }

            Cot(nameof(SlotNguonLookupItem.SlotLotId), "SlotLotId", 0, false);
            Cot(nameof(SlotNguonLookupItem.SlotId), "SlotId", 0, false);
            Cot(nameof(SlotNguonLookupItem.TemCode), "Tem", 0, false);
            Cot(nameof(SlotNguonLookupItem.ViTri), "Vị trí", 230);
            Cot(nameof(SlotNguonLookupItem.ItemCode), "Mã hàng", 120);
            Cot(nameof(SlotNguonLookupItem.TenHang), "Tên hàng", 180);
            Cot(nameof(SlotNguonLookupItem.LotNo), "Lot No", 120);
            Cot(nameof(SlotNguonLookupItem.Quantity), "Tồn", 70);
        }

        private void CauHinhCotMaHangLookup()
        {
            var v = _repoMaHang.View;

            var c1 = v.Columns[nameof(ItemLookupItem.ItemCode)];
            if (c1 != null) { c1.Caption = "Mã hàng"; c1.Width = 140; }

            var c2 = v.Columns[nameof(ItemLookupItem.ItemName)];
            if (c2 != null) { c2.Caption = "Tên hàng"; c2.Width = 360; }
        }

        // Khi mở editor: lọc Slot theo Mã hàng của dòng, giới hạn Số lượng theo tồn.
        private void GridView_ShownEditor(object sender, EventArgs e)
        {
            var row = _gridView.GetFocusedRow() as PhieuTraHangCT;
            var field = _gridView.FocusedColumn?.FieldName;

            if (row == null || field == null)
                return;

            if (field == nameof(PhieuTraHangCT.SlotLotIdNguon)
                && _gridView.ActiveEditor is SearchLookUpEdit slotEdit)
            {
                slotEdit.Properties.DataSource =
                    string.IsNullOrWhiteSpace(row.MaHang)
                        ? _slotItems
                        : _slotItems
                            .Where(x => string.Equals(
                                x.ItemCode?.Trim(), row.MaHang.Trim(),
                                StringComparison.OrdinalIgnoreCase))
                            .ToList();
            }
            else if (field == nameof(PhieuTraHangCT.SoLuong)
                && _gridView.ActiveEditor is SpinEdit spin)
            {
                var slot = TimSlotItem(row.SlotLotIdNguon);
                spin.Properties.MaxValue = slot != null ? slot.Quantity : int.MaxValue;
            }
        }

        private void GridView_CellValueChanged(object sender, CellValueChangedEventArgs e)
        {
            if (_updatingRow)
                return;

            var row = _gridView.GetRow(e.RowHandle) as PhieuTraHangCT;
            if (row == null)
                return;

            _updatingRow = true;

            try
            {
                if (e.Column.FieldName == nameof(PhieuTraHangCT.SlotLotIdNguon))
                {
                    var slot = TimSlotItem(row.SlotLotIdNguon);

                    if (slot == null)
                    {
                        row.SlotLotIdNguon = null;
                        row.SlotIdNguon = null;
                        row.LotNo = string.Empty;
                        row.SoLuong = 0;
                    }
                    else
                    {
                        row.SlotIdNguon = slot.SlotId;
                        row.MaHang = slot.ItemCode;
                        row.TenHang = slot.TenHang;
                        row.LotNo = slot.LotNo;
                        row.SoLuong = slot.Quantity;
                    }
                }
                else if (e.Column.FieldName == nameof(PhieuTraHangCT.MaHang))
                {
                    var item = _items.FirstOrDefault(x => string.Equals(
                        x.ItemCode, row.MaHang, StringComparison.OrdinalIgnoreCase));

                    row.TenHang = item?.ItemName ?? string.Empty;

                    // Đổi mã hàng → Slot đã chọn trước đó không còn khớp thì bỏ.
                    var slot = TimSlotItem(row.SlotLotIdNguon);
                    if (slot != null && !string.Equals(
                            slot.ItemCode?.Trim(), row.MaHang?.Trim(),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        row.SlotLotIdNguon = null;
                        row.SlotIdNguon = null;
                        row.LotNo = string.Empty;
                        row.SoLuong = 0;
                    }
                }
            }
            finally
            {
                _updatingRow = false;
            }

            _gridView.RefreshRow(e.RowHandle);
        }

        private SlotNguonLookupItem TimSlotItem(int? slotLotId)
        {
            if (!slotLotId.HasValue)
                return null;

            return _slotItems.FirstOrDefault(x => x.SlotLotId == slotLotId.Value);
        }

        // ============================================================
        // THÊM DÒNG
        // ============================================================

        private void ThemDong()
        {
            var item = new PhieuTraHangCT
            {
                MaHang = "",
                TenHang = "",
                LotNo = "",
                SoLuong = 0,
                LyDoNg = "",
                SlotIdNguon = null,
                SlotLotIdNguon = null
            };

            var data = _grid.DataSource as List<PhieuTraHangCT>;

            if (data == null)
            {
                data = new List<PhieuTraHangCT>();
                _grid.DataSource = data;
            }

            data.Add(item);

            _grid.RefreshDataSource();

            int rowHandle = _gridView.GetRowHandle(data.Count - 1);

            if (rowHandle >= 0)
            {
                _gridView.FocusedRowHandle = rowHandle;
                _gridView.ShowEditor();
            }
        }

        // ============================================================
        // XÓA DÒNG
        // ============================================================

        private void XoaDong()
        {
            int rowHandle = _gridView.FocusedRowHandle;

            if (rowHandle < 0)
                return;

            var item = _gridView.GetRow(rowHandle) as PhieuTraHangCT;

            if (item == null)
                return;

            var data = _grid.DataSource as List<PhieuTraHangCT>;

            if (data == null)
                return;

            data.Remove(item);

            _grid.RefreshDataSource();
        }

        // ============================================================
        // VALIDATE
        // ============================================================

        private bool ValidateForm(
            List<PhieuTraHangCT> items)
        {
            if (string.IsNullOrWhiteSpace(_lookPhongBan.EditValue as string))
            {
                XtraMessageBox.Show(
                    "Vui lòng chọn Phòng ban.",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _lookPhongBan.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(_lookBoPhanPhatHienLoi.EditValue as string))
            {
                XtraMessageBox.Show(
                    "Vui lòng chọn Bộ phận phát hiện lỗi.",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _lookBoPhanPhatHienLoi.Focus();
                return false;
            }

            if (items == null || items.Count == 0)
            {
                XtraMessageBox.Show(
                    "Phiếu phải có ít nhất một dòng hàng.",
                    "Thiếu dữ liệu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                if (string.IsNullOrWhiteSpace(item.MaHang))
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: chưa nhập Mã hàng.",
                        "Thiếu dữ liệu",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }

                if (string.IsNullOrWhiteSpace(item.LotNo))
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: chưa nhập Lot No.",
                        "Thiếu dữ liệu",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }

                if (item.SoLuong <= 0)
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: Số lượng phải lớn hơn 0.",
                        "Dữ liệu không hợp lệ",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }

                if (item.SlotIdNguon == null)
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: chưa chọn Slot nguồn.",
                        "Thiếu dữ liệu",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }

                var slot = TimSlotItem(item.SlotLotIdNguon);

                if (slot != null && item.SoLuong > slot.Quantity)
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: Số lượng ({item.SoLuong}) vượt quá tồn trong Slot ({slot.Quantity}).",
                        "Dữ liệu không hợp lệ",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }

                if (items.Take(i).Any(x => x.SlotLotIdNguon == item.SlotLotIdNguon))
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: Slot/LOT này đã được chọn ở dòng khác.",
                        "Dữ liệu trùng",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }
            }

            return true;
        }

        // ============================================================
        // LƯU
        // ============================================================

        private void LuuPhieu()
        {
            try
            {
                _gridView.CloseEditor();
                _gridView.UpdateCurrentRow();

                var items = (_grid.DataSource as List<PhieuTraHangCT>)
                    ?? new List<PhieuTraHangCT>();

                if (!ValidateForm(items))
                    return;

                string soPhieu = _txtSoPhieu.Text.Trim();

                if (string.IsNullOrWhiteSpace(soPhieu))
                {
                    soPhieu =
                        $"TNBI-{DateTime.Now:yyyyMMddHHmmss}";
                }

                string nguoiTao = _txtNguoiTao.Text.Trim();

                if (string.IsNullOrWhiteSpace(nguoiTao))
                    nguoiTao = Environment.UserName;

                // ========================================================
                // HEADER
                // ========================================================

                var phieu = new PhieuTraHang
                {
                    Nguon = NguonXuLyBatThuong.TraNoiBo,

                    SoPhieu = soPhieu,

                    // TraNoiBo không dùng:
                    NguonKhachTra = null,
                    SoPhieuKhach = null,
                    NgayPhatHanh = null,
                    SlipNo = null,
                    Ca = null,
                    TenKhachHang = null,

                    // Dùng cho nội bộ
                    PhongBan = GetDeptName(_lookPhongBan, _depts),

                    LyDo = _txtLyDo.Text.Trim(),

                    BoPhanPhatHienLoi =
                        GetDeptName(_lookBoPhanPhatHienLoi, _depts),

                    XacNhanBPPhatHienLoi = null,
                    XacNhanQCKhach = null,
                    XacNhanNhaCungCap = null,

                    NgayNhanKho = null,

                    TongSoLuongNhan =
                        items.Sum(x => x.SoLuong),

                    // Service sẽ xử lý state ban đầu.
                    Status = PhieuTraHangStatus.Moi,

                    BoPhanNhanLai = null,
                    SoLuongGiaoLai = null,
                    NgayGiaoLaiBoPhan = null,
                    NguoiGiaoLaiBoPhan = null,

                    Note = null,

                    CreatedAt = DateTime.Now,
                    CreatedBy = nguoiTao,

                    UpdatedAt = null,
                    UpdatedBy = null,

                    ChiTiet = items
                };

                // ========================================================
                // DETAIL
                // ========================================================

                foreach (var item in phieu.ChiTiet)
                {
                    item.Id = 0;
                    item.PhieuTraHangId = 0;

                    // TraNoiBo dùng Slot nguồn.
                    // Các thông tin đối chiếu phiếu giao
                    // ban đầu chưa có.
                    item.DinhDanhPhieuGiao = null;
                    item.PoNo = null;
                    item.NgayGiao = null;
                    item.NhaMay = null;
                }

                // ========================================================
                // SERVICE
                // ========================================================

                int id =
                    _traNoiBoService.TaoPhieuTraNoiBo(phieu);

                XtraMessageBox.Show(
                    $"Đã tạo phiếu trả nội bộ thành công.\r\n\r\n" +
                    $"Số phiếu: {phieu.SoPhieu}\r\n" +
                    $"ID: {id}\r\n" +
                    $"Số dòng: {phieu.ChiTiet.Count}\r\n" +
                    $"Tổng số lượng: {phieu.TongSoLuongNhan}",
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    $"Không thể tạo phiếu trả nội bộ.\r\n\r\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
