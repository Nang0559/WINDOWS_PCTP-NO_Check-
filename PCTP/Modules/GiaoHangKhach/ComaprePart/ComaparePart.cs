using DevExpress.Utils.Menu;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.GiaoHangKhach.Intefaces;
using PCTP.Modules.GiaoHangKhach.Repositories;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.QRCODE_HVN.ComaprePart
{
    /// <summary>
    /// Quản lý mapping mã hàng HVN (PartNo) -> mã FCC (PartNoCompare) trong tbl_QR_ComparePart.
    /// Form chỉ lo giao diện; mọi truy cập SQL đi qua IComparePartRepository.
    /// </summary>
    public partial class ComaparePart : DevExpress.XtraEditors.XtraForm
    {
        // timeSet là smalldatetime -> chỉ nhận 1900-01-01 .. 2079-06-06.
        private static readonly DateTime MinTimeSet = new DateTime(1900, 1, 1);
        private static readonly DateTime MaxTimeSet = new DateTime(2079, 6, 6);

        // Vị trí ảnh trong imageCollection1 (xem ComaparePart.resx).
        private const int IconDelete = 0;
        private const int IconRefresh = 1;

        private readonly IComparePartRepository _repo;

        // ── Ctor mặc định: tự dựng dependency, dùng khi mở form từ menu ─────
        public ComaparePart()
            : this(CreateDefaultRepo())
        {
        }

        // Dùng CHUNG 1 SQLPROVIDER cho PhieuSqlExecutor và UnitOfWork
        // (đúng nguyên tắc "1 phiên làm việc = 1 kết nối logic" của project).
        private static IComparePartRepository CreateDefaultRepo()
        {
            var provider = new SQLPROVIDER();
            return new ComparePartRepository(
                new PhieuSqlExecutor(provider),
                new UnitOfWork(provider));
        }

        // ── Ctor cho phép inject repo (test/tái sử dụng) ─────────────────────
        public ComaparePart(IComparePartRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            InitializeComponent();

            ConfigureGrid();
            ConfigureControls();

            Load += ComaparePart_Load;
        }

        #region FORM

        private void ComaparePart_Load(object sender, EventArgs e)
        {
            LoadData();
        }

        #endregion

        #region CONFIGURATION

        private void ConfigureGrid()
        {
            // Dùng chữ đen trên nền vàng cho cả dòng focus và dòng được chọn.
            // Phải bật UseBackColor/UseForeColor để không bị DevExpress skin ghi đè.
            Color focusedBackColor = Color.FromArgb(255, 255, 192);
            Color focusedForeColor = Color.FromArgb(32, 32, 32);

            GV_ListPart.Appearance.FocusedRow.BackColor = focusedBackColor;
            GV_ListPart.Appearance.FocusedRow.ForeColor = focusedForeColor;
            GV_ListPart.Appearance.FocusedRow.Options.UseBackColor = true;
            GV_ListPart.Appearance.FocusedRow.Options.UseForeColor = true;

            GV_ListPart.Appearance.SelectedRow.BackColor = focusedBackColor;
            GV_ListPart.Appearance.SelectedRow.ForeColor = focusedForeColor;
            GV_ListPart.Appearance.SelectedRow.Options.UseBackColor = true;
            GV_ListPart.Appearance.SelectedRow.Options.UseForeColor = true;

            GV_ListPart.OptionsSelection.MultiSelect = true;
            GV_ListPart.OptionsSelection.MultiSelectMode =
                GridMultiSelectMode.RowSelect;

            GV_ListPart.OptionsBehavior.Editable = false;

            GV_ListPart.OptionsView.ShowAutoFilterRow = true;
            GV_ListPart.OptionsView.ShowGroupPanel = false;
            GV_ListPart.OptionsView.ShowIndicator = true;
        }

        private void ConfigureControls()
        {
            ConfigureLookUp(lup_Ma);
            ConfigureLookUp(LuKToPart);

            DateE_NgayAPP.Properties.MinValue = MinTimeSet;
            DateE_NgayAPP.Properties.MaxValue = MaxTimeSet;
            DateE_NgayAPP.EditValue = DateTime.Now;
        }

        private static void ConfigureLookUp(LookUpEdit edit)
        {
            edit.Properties.NullText = "";
            edit.Properties.ValueMember = "code";
            edit.Properties.DisplayMember = "code";

            // Hiện cả mã lẫn tên để chọn đúng mã hàng.
            edit.Properties.Columns.Clear();
            edit.Properties.Columns.Add(new LookUpColumnInfo("code", "Mã hàng", 120));
            edit.Properties.Columns.Add(new LookUpColumnInfo("name", "Tên hàng", 260));
        }

        #endregion

        #region MESSAGES

        private static void ShowWarning(string message)
        {
            XtraMessageBox.Show(
                message,
                "Thông báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private static void ShowInfo(string message)
        {
            XtraMessageBox.Show(
                message,
                "Thông báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static void ShowError(string action, Exception ex)
        {
            XtraMessageBox.Show(
                $"{action}: {ex.Message}",
                "Lỗi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        #endregion

        #region LOAD DATA

        private void LoadData()
        {
            try
            {
                LoadSourcePart();
                LoadComparePart();
            }
            catch (Exception ex)
            {
                ShowError("Lỗi tải dữ liệu mã hàng", ex);
            }
        }

        private void LoadSourcePart()
        {
            lup_Ma.Properties.DataSource = _repo.GetSourceParts();
        }

        private void LoadComparePart()
        {
            GT_ListPart.DataSource = _repo.GetAll();
        }

        #endregion

        #region SOURCE PART

        private void lup_Ma_EditValueChanged(
            object sender,
            EventArgs e)
        {
            // Đổi mã gốc thì mã thay thế đã chọn trước đó không còn hợp lệ:
            // nếu không xóa, EditValue cũ vẫn còn dù không hiển thị trong danh sách mới.
            LuKToPart.EditValue = null;

            string maHang = Convert.ToString(lup_Ma.EditValue)?.Trim();

            if (string.IsNullOrWhiteSpace(maHang))
            {
                LuKToPart.Properties.DataSource = null;
                return;
            }

            try
            {
                LoadTargetParts();
            }
            catch (Exception ex)
            {
                LuKToPart.Properties.DataSource = null;
                ShowError("Lỗi tải danh sách mã thay thế", ex);
            }
        }

        private void LoadTargetParts()
        {
            // Mã kho không bắt buộc cùng tiền tố với mã khách.
            // Một mã khách có thể được ánh xạ tới nhiều mã kho khác nhau.
            LuKToPart.Properties.DataSource = _repo.GetTargetParts();
        }

        #endregion

        #region CREATE COMPARE PART

        private void cmd_Tao_Click(object sender, EventArgs e)
        {
            string partNo = Convert.ToString(lup_Ma.EditValue)?.Trim();
            string partNoCompare = Convert.ToString(LuKToPart.EditValue)?.Trim();

            if (string.IsNullOrWhiteSpace(partNo))
            {
                ShowWarning("Vui lòng chọn mã hàng gốc.");
                lup_Ma.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(partNoCompare))
            {
                ShowWarning("Vui lòng chọn mã hàng thay thế.");
                LuKToPart.Focus();
                return;
            }

            // Mã giống nhau được kiểm tra trực tiếp theo mặc định khi đọc QR.
            // ComparePart chỉ dành cho mã FCC khác mã HVN, tránh khai báo mapping A -> A.
            if (string.Equals(partNo, partNoCompare, StringComparison.OrdinalIgnoreCase))
            {
                ShowWarning("Mã hàng gốc và mã hàng thay thế giống nhau. Trường hợp này không cần khai báo ComparePart.");
                LuKToPart.Focus();
                return;
            }

            DateTime timeSet = DateE_NgayAPP.DateTime;

            if (DateE_NgayAPP.EditValue == null
                || timeSet < MinTimeSet
                || timeSet > MaxTimeSet)
            {
                ShowWarning("Vui lòng chọn ngày áp dụng hợp lệ.");
                DateE_NgayAPP.Focus();
                return;
            }

            bool inserted;

            cmd_Tao.Enabled = false;
            try
            {
                inserted = _repo.TryInsert(partNo, partNoCompare, timeSet);
            }
            catch (Exception ex)
            {
                ShowError("Lỗi tạo mapping mã hàng", ex);
                return;
            }
            finally
            {
                cmd_Tao.Enabled = true;
            }

            if (inserted)
            {
                ShowInfo("Đã tạo mapping mã hàng thành công.");
                RefreshCompareList();
            }
            else
            {
                ShowInfo("Mapping mã hàng này đã tồn tại.");
            }
        }

        private void cmd_Huy_Click(object sender, EventArgs e)
        {
            // Đặt lại vùng nhập liệu; đổi lup_Ma sẽ tự xóa mã thay thế (xem lup_Ma_EditValueChanged).
            lup_Ma.EditValue = null;
            LuKToPart.EditValue = null;
            DateE_NgayAPP.EditValue = DateTime.Now;

            lup_Ma.Focus();
        }

        private void RefreshCompareList()
        {
            try
            {
                LoadComparePart();
            }
            catch (Exception ex)
            {
                ShowError("Lỗi tải danh sách mapping", ex);
            }
        }

        #endregion

        #region DELETE

        private void GV_ListPart_PopupMenuShowing(
            object sender,
            PopupMenuShowingEventArgs e)
        {
            if (e.MenuType != GridMenuType.Row)
                return;

            GridView view = sender as GridView;

            if (view == null)
                return;

            e.Menu.Items.Clear();

            DXMenuItem deleteItem = CreateDeleteMenuItem(view);
            DXMenuItem refreshItem = CreateRefreshMenuItem(view);

            e.Menu.Items.Add(deleteItem);
            e.Menu.Items.Add(refreshItem);
        }

        private DXMenuItem CreateDeleteMenuItem(GridView view)
        {
            DXMenuItem item = new DXMenuItem(
                "Xóa dòng chọn",
                OnDeleteClick);

            item.Tag = view;
            item.ImageOptions.Image = GetMenuImage(IconDelete);

            return item;
        }

        private DXMenuItem CreateRefreshMenuItem(GridView view)
        {
            DXMenuItem item = new DXMenuItem(
                "Làm mới",
                OnRefreshClick);

            item.Tag = view;
            item.ImageOptions.Image = GetMenuImage(IconRefresh);

            return item;
        }

        private Image GetMenuImage(int index)
        {
            if (index < 0 || index >= imageCollection1.Images.Count)
                return null;

            return imageCollection1.Images[index];
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            LoadData();
        }

        private void OnDeleteClick(object sender, EventArgs e)
        {
            GridView view = null;

            if (sender is DXMenuItem menuItem)
                view = menuItem.Tag as GridView;

            if (view == null)
                return;

            int[] selectedRows = view.GetSelectedRows();

            if (selectedRows == null || selectedRows.Length == 0)
            {
                ShowWarning("Vui lòng chọn ít nhất một dòng cần xóa.");
                return;
            }

            DialogResult confirm = XtraMessageBox.Show(
                $"Bạn có chắc muốn xóa {selectedRows.Length} dòng đã chọn?",
                "Xác nhận xóa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            var keys = new List<ComparePartKey>();
            foreach (int rowHandle in selectedRows)
            {
                // rowHandle < 0: dòng group / auto-filter / new-item, không phải dữ liệu.
                if (rowHandle < 0)
                    continue;

                keys.Add(ReadKey(view, rowHandle));
            }

            if (keys.Count == 0)
            {
                ShowWarning("Không có dòng dữ liệu hợp lệ để xóa.");
                return;
            }

            try
            {
                int deleted = _repo.Delete(keys);

                RefreshCompareList();

                if (deleted == 0)
                    ShowWarning("Không có dòng nào được xóa (dữ liệu có thể đã bị người khác thay đổi). Danh sách đã được làm mới.");
            }
            catch (Exception ex)
            {
                ShowError("Lỗi xóa mapping mã hàng", ex);
            }
        }

        private static ComparePartKey ReadKey(GridView view, int rowHandle)
        {
            int? stt = null;
            object sttValue = view.GetRowCellValue(rowHandle, "STT");

            if (sttValue != null
                && sttValue != DBNull.Value
                && int.TryParse(Convert.ToString(sttValue), out int parsed))
            {
                stt = parsed;
            }

            return new ComparePartKey(
                stt,
                Convert.ToString(view.GetRowCellValue(rowHandle, "PartNo")),
                Convert.ToString(view.GetRowCellValue(rowHandle, "ToPartNo")));
        }

        #endregion
    }
}
