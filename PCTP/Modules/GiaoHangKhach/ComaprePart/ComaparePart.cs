using DevExpress.Utils.Menu;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Localization;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Linq;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.QRCODE_HVN.ComaprePart
{
    public partial class ComaparePart : DevExpress.XtraEditors.XtraForm
    {
        private readonly SQLPROVIDER _sql = new SQLPROVIDER();

        private DataTable _listPart;
        private DataTable _compareParts;

        public ComaparePart()
        {
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
            GV_ListPart.Appearance.FocusedRow.BackColor =
                Color.FromArgb(255, 255, 192);

            GV_ListPart.Appearance.SelectedRow.BackColor =
                Color.FromArgb(255, 255, 192);

            GV_ListPart.Appearance.SelectedRow.Options.UseBackColor = true;

            GV_ListPart.OptionsSelection.MultiSelect = true;
            GV_ListPart.OptionsSelection.MultiSelectMode =
                DevExpress.XtraGrid.Views.Grid.GridMultiSelectMode.RowSelect;

            GV_ListPart.OptionsBehavior.Editable = false;

            GV_ListPart.OptionsView.ShowAutoFilterRow = true;
            GV_ListPart.OptionsView.ShowGroupPanel = false;
            GV_ListPart.OptionsView.ShowIndicator = true;
        }

        private void ConfigureControls()
        {
            lup_Ma.Properties.NullText = "";
            LuKToPart.Properties.NullText = "";

            DateE_NgayAPP.EditValue = DateTime.Now;
        }

        #endregion

        #region LOAD DATA

        private void LoadData()
        {
            LoadSourcePart();
            LoadComparePart();
        }

        private void LoadSourcePart()
        {
            const string sql = @"
            SELECT DISTINCT
                code,
                name
            FROM B20Item
            WHERE ISNULL(code, '') <> ''
            ORDER BY code";

            DataTable dt = _sql.LoadData1(
                _sql.B7R2_FCCdb,
                sql);

            lup_Ma.Properties.DataSource = dt;
            lup_Ma.Properties.ValueMember = "code";
            lup_Ma.Properties.DisplayMember = "code";
        }

        private void LoadComparePart()
        {
            const string sql = @"
            SELECT
                STT,
                PartNo,
                PartName,
                PartNoCompare AS ToPartNo,
                PartName AS ToPartName,
                timeSet,
                IsActive,
                timeSet AS DateApp
            FROM tbl_QR_ComparePart
            ORDER BY STT DESC";

            _compareParts = _sql.LoadData1(
                _sql.B7R2_FCCdb,
                sql);

            GT_ListPart.DataSource = _compareParts;
        }

        #endregion

        #region SOURCE PART

        private void lup_Ma_EditValueChanged(
            object sender,
            EventArgs e)
        {
            string maHang = Convert.ToString(lup_Ma.EditValue)?.Trim();

            if (string.IsNullOrWhiteSpace(maHang))
            {
                LuKToPart.Properties.DataSource = null;
                LuKToPart.EditValue = null;
                return;
            }

            LoadTargetParts(maHang);
        }

        private void LoadTargetParts(string maHang)
        {
            const string sql = @"
            SELECT DISTINCT
                code,
                name
            FROM B20Item
            WHERE code LIKE @Code
            ORDER BY code";

            DataTable dt = _sql.LoadData1(
                _sql.B7R2_FCCdb,
                sql,
                new SqlParameter("@Code", SqlDbType.NVarChar, 100)
                {
                    Value = maHang + "%"
                });

            _listPart = dt;

            LuKToPart.Properties.DataSource = _listPart;
            LuKToPart.Properties.ValueMember = "code";
            LuKToPart.Properties.DisplayMember = "code";
        }

        #endregion

        #region CREATE COMPARE PART

        private void cmd_Tao_Click(object sender, EventArgs e)
        {
            string partNo = Convert.ToString(lup_Ma.EditValue)?.Trim();
            string partNoCompare = Convert.ToString(LuKToPart.EditValue)?.Trim();

            if (string.IsNullOrWhiteSpace(partNo))
            {
                XtraMessageBox.Show(
                    "Vui lòng chọn mã hàng gốc.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                lup_Ma.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(partNoCompare))
            {
                XtraMessageBox.Show(
                    "Vui lòng chọn mã hàng thay thế.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                LuKToPart.Focus();
                return;
            }

            if (string.Equals(
                partNo,
                partNoCompare,
                StringComparison.OrdinalIgnoreCase))
            {
                XtraMessageBox.Show(
                    "Mã hàng gốc và mã hàng thay thế không được giống nhau.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            if (!CheckComparePartExists(partNo, partNoCompare))
            {
                InsertComparePart(
                    partNo,
                    partNoCompare);

                XtraMessageBox.Show(
                    "Đã tạo mapping mã hàng thành công.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                LoadComparePart();
            }
            else
            {
                XtraMessageBox.Show(
                    "Mapping mã hàng này đã tồn tại.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private bool CheckComparePartExists(
            string partNo,
            string partNoCompare)
        {
            const string sql = @"
            SELECT COUNT(*)
            FROM tbl_QR_ComparePart
            WHERE PartNo = @PartNo
              AND PartNoCompare = @PartNoCompare";

            object result = _sql.ExecuteScalar(
                _sql.B7R2_FCCdb,
                sql,
                new[]
                {
                new SqlParameter("@PartNo", SqlDbType.NVarChar, 100)
                {
                    Value = partNo
                },

                new SqlParameter("@PartNoCompare", SqlDbType.NVarChar, 100)
                {
                    Value = partNoCompare
                }
                });

            return result != null
                && result != DBNull.Value
                && Convert.ToInt32(result) > 0;
        }

        private void InsertComparePart(
            string partNo,
            string partNoCompare)
        {
            const string sql = @"
            INSERT INTO tbl_QR_ComparePart
            (
                PartNo,
                PartNoCompare,
                timeSet,
                IsActive
            )
            VALUES
            (
                @PartNo,
                @PartNoCompare,
                @TimeSet,
                1
            )";

            _sql.ExecuteNonQuery(
                _sql.B7R2_FCCdb,
                sql,
                new SqlParameter[]
                {
                new SqlParameter("@PartNo", SqlDbType.NVarChar, 100)
                {
                    Value = partNo
                },

                new SqlParameter("@PartNoCompare", SqlDbType.NVarChar, 100)
                {
                    Value = partNoCompare
                },

                new SqlParameter("@TimeSet", SqlDbType.DateTime)
                {
                    Value = DateE_NgayAPP.DateTime
                }
                });
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
            item.ImageOptions.Image = imageCollection1.Images[0];

            return item;
        }

        private DXMenuItem CreateRefreshMenuItem(GridView view)
        {
            DXMenuItem item = new DXMenuItem(
                "Làm mới",
                OnRefreshClick);

            item.Tag = view;
            item.ImageOptions.Image = imageCollection1.Images[1];

            return item;
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
                XtraMessageBox.Show(
                    "Vui lòng chọn ít nhất một dòng cần xóa.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            DialogResult confirm = XtraMessageBox.Show(
                $"Bạn có chắc muốn xóa {selectedRows.Length} dòng đã chọn?",
                "Xác nhận xóa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            foreach (int rowHandle in selectedRows)
            {
                if (rowHandle < 0)
                    continue;

                object sttValue =
                    view.GetRowCellValue(rowHandle, "STT");

                if (sttValue == null ||
                    sttValue == DBNull.Value)
                    continue;

                if (!int.TryParse(
                    sttValue.ToString(),
                    out int stt))
                    continue;

                DeleteComparePart(stt);
            }

            LoadComparePart();
        }

        private void DeleteComparePart(int stt)
        {
            const string sql = @"
            DELETE FROM tbl_QR_ComparePart
            WHERE STT = @STT";

            _sql.ExecuteNonQuery(
                _sql.B7R2_FCCdb,
                sql,
                new SqlParameter("@STT", SqlDbType.Int)
                {
                    Value = stt
                });
        }

        #endregion

        #region GRID

        private void GV_ListPart_RowUpdated(
            object sender,
            RowObjectEventArgs e)
        {
            // Không sử dụng SqlDataAdapter.Update ở đây.
            //
            // Form này đang sử dụng SQLPROVIDER để thao tác DB.
            // Nếu cần cho phép sửa trực tiếp trên Grid,
            // hãy viết UPDATE có parameter riêng.
        }

        #endregion
    }
}