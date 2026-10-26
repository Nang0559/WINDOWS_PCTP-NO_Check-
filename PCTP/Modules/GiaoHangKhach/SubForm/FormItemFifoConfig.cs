using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach.Intefaces;
using PCTP.Modules.GiaoHangKhach.Repositories;
using PCTP.Shared.Common;   // ★ THÊM — IUnitOfWork
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.SubForm
{
    /// <summary>
    /// Màn hình quản trị: bật/tắt bắt buộc FIFO theo từng mã hàng (FVN_ItemFifoConfig).
    /// Không phụ thuộc file Designer.cs — toàn bộ UI dựng bằng code.
    /// </summary>
    public partial class FormItemFifoConfig : XtraForm
    {
        private readonly IItemFifoConfigRepository _repo;
        private GridControl _grid;
        private GridView _gridView;
        private DataTable _dt;
        private SimpleButton _btnThem;
        private SimpleButton _btnXoa;
        private SimpleButton _btnLuu;
        private SimpleButton _btnDong;
        private SimpleButton _btnLamMoi;

        // ── Ctor mặc định: tự dựng dependency, dùng khi mở form từ menu ─────
        public FormItemFifoConfig()
            : this(CreateDefaultRepo())
        {
        }

        // ★ SỬA — tách factory riêng, dùng CHUNG 1 SQLPROVIDER cho cả
        // PhieuSqlExecutor và UnitOfWork (đúng nguyên tắc "1 phiên làm việc =
        // 1 kết nối logic" áp dụng xuyên suốt project).
        private static IItemFifoConfigRepository CreateDefaultRepo()
        {
            var provider = new SQLPROVIDER();
            var sql = new PhieuSqlExecutor(provider);
            var uow = new UnitOfWork(provider);
            return new ItemFifoConfigRepository(sql, uow);
        }

        // ── Ctor cho phép inject repo (test/tái sử dụng) ─────────────────────
        public FormItemFifoConfig(IItemFifoConfigRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            BuildUI();
            LoadData();
        }

        // ★ THÊM — người thực hiện = tên máy, dùng chung cho Upsert/Delete
        private static string NguoiThucHien => System.Net.Dns.GetHostName();

        // ════════════════════════════════════════════════════════════════
        // Dựng UI
        // ════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            this.Text = "Cấu hình bắt buộc FIFO theo mã hàng";
            this.Size = new Size(720, 520);
            this.StartPosition = FormStartPosition.CenterScreen;

            // ── Panel nút phía trên ─────────────────────────────────────
            var panelTop = new PanelControl
            {
                Dock = DockStyle.Top,
                Height = 44
            };

            _btnThem = new SimpleButton
            {
                Text = "Thêm Mã Hàng",
                Location = new Point(8, 8),
                Width = 120
            };
            _btnThem.Click += BtnThem_Click;

            _btnXoa = new SimpleButton
            {
                Text = "Xóa Dòng Chọn",
                Location = new Point(_btnThem.Right + 8, 8),
                Width = 120
            };
            _btnXoa.Click += BtnXoa_Click;

            _btnLamMoi = new SimpleButton
            {
                Text = "Làm Mới",
                Location = new Point(_btnXoa.Right + 8, 8),
                Width = 90
            };
            _btnLamMoi.Click += (s, e) => LoadData();

            _btnLuu = new SimpleButton
            {
                Text = "Lưu",
                Location = new Point(_btnLamMoi.Right + 8, 8),
                Width = 90
            };
            _btnLuu.Click += BtnLuu_Click;

            _btnDong = new SimpleButton
            {
                Text = "Đóng",
                Location = new Point(_btnLuu.Right + 8, 8),
                Width = 90
            };
            _btnDong.Click += (s, e) => this.Close();

            panelTop.Controls.AddRange(new Control[]
            {
                _btnThem, _btnXoa, _btnLamMoi, _btnLuu, _btnDong
            });

            // ── Grid chính ───────────────────────────────────────────────
            _grid = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _grid.ViewCollection.Add(_gridView);
            _gridView.OptionsBehavior.Editable = true;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.ColumnAutoWidth = false;

            this.Controls.Add(_grid);
            this.Controls.Add(panelTop);
        }

        // ════════════════════════════════════════════════════════════════
        // Load dữ liệu
        // ════════════════════════════════════════════════════════════════
        private void LoadData()
        {
            try
            {
                _dt = _repo.GetAll();
                if (!_dt.Columns.Contains("_IsNew"))
                    _dt.Columns.Add("_IsNew", typeof(bool));

                _grid.DataSource = _dt;
                SetupColumns();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    $"Lỗi load dữ liệu FIFO Config: {ex.Message}",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetupColumns()
        {
            _gridView.Columns.Clear();

            GridColumn colItemCode = _gridView.Columns.AddField("ItemCode");
            colItemCode.Caption = "Mã Hàng";
            colItemCode.Visible = true;
            colItemCode.OptionsColumn.AllowEdit = false; // sửa mã hàng qua "Thêm", không edit trực tiếp
            colItemCode.Width = 150;

            GridColumn colTenHang = _gridView.Columns.AddField("TenHang");
            colTenHang.Caption = "Tên Hàng";
            colTenHang.Visible = true;
            colTenHang.OptionsColumn.AllowEdit = false;
            colTenHang.Width = 320;

            GridColumn colEnforce = _gridView.Columns.AddField("EnforceFifo");
            colEnforce.Caption = "Bắt Buộc FIFO";
            colEnforce.Visible = true;
            colEnforce.Width = 120;

            var riCheck = new RepositoryItemCheckEdit();
            _grid.RepositoryItems.Add(riCheck);
            colEnforce.ColumnEdit = riCheck;

            // ★ THÊM — hiển thị thông tin cập nhật gần nhất (giờ đã có sẵn từ
            // GetAll() sau khi ItemFifoConfigRepository trả thêm UpdatedAt/UpdatedBy)
            if (_dt.Columns.Contains("UpdatedAt"))
            {
                GridColumn colUpdatedAt = _gridView.Columns.AddField("UpdatedAt");
                colUpdatedAt.Caption = "Cập nhật lúc";
                colUpdatedAt.Visible = true;
                colUpdatedAt.OptionsColumn.AllowEdit = false;
                colUpdatedAt.Width = 130;
                colUpdatedAt.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
                colUpdatedAt.DisplayFormat.FormatString = "dd/MM/yyyy HH:mm";
            }

            if (_dt.Columns.Contains("UpdatedBy"))
            {
                GridColumn colUpdatedBy = _gridView.Columns.AddField("UpdatedBy");
                colUpdatedBy.Caption = "Máy cập nhật";
                colUpdatedBy.Visible = true;
                colUpdatedBy.OptionsColumn.AllowEdit = false;
                colUpdatedBy.Width = 130;
            }

            //_gridView.Columns["_IsNew"].Visible = false;
            _gridView.BestFitColumns();
        }

        // ════════════════════════════════════════════════════════════════
        // Thêm mã hàng mới — chọn từ B20Item, mặc định EnforceFifo = false
        // ════════════════════════════════════════════════════════════════
        private void BtnThem_Click(object sender, EventArgs e)
        {
            try
            {
                // ★ SỬA — qua repository thay vì tự new SQLPROVIDER() query trực tiếp
                // trong Form (vi phạm Form → Service/Repository). Thêm method mới
                // GetDanhSachMaHangKhaDung() vào IItemFifoConfigRepository.
                DataTable dsMaHang = _repo.GetDanhSachMaHangKhaDung();

                using (var frm = new FormChonMaHang(dsMaHang))
                {
                    if (frm.ShowDialog() != DialogResult.OK) return;
                    if (string.IsNullOrWhiteSpace(frm.SelectedItemCode)) return;

                    string ma = frm.SelectedItemCode.Trim();

                    // Không cho thêm trùng
                    bool trung = _dt.AsEnumerable()
                        .Any(r => string.Equals(
                            r["ItemCode"].ToString().Trim(), ma,
                            StringComparison.OrdinalIgnoreCase));

                    if (trung)
                    {
                        XtraMessageBox.Show("Mã hàng này đã có trong danh sách.",
                            "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    DataRow row = _dt.NewRow();
                    row["ItemCode"] = ma;
                    row["TenHang"] = frm.SelectedItemName ?? "";
                    row["EnforceFifo"] = false;
                    row["_IsNew"] = true;

                    _dt.Rows.Add(row);
                    _gridView.RefreshData();
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Lỗi thêm mã hàng: {ex.Message}",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Xóa dòng đang chọn — xóa cả trong DB nếu đã tồn tại
        // ════════════════════════════════════════════════════════════════
        private void BtnXoa_Click(object sender, EventArgs e)
        {
            int handle = _gridView.FocusedRowHandle;
            if (handle < 0) return;

            string ma = _gridView.GetRowCellDisplayText(handle, "ItemCode").Trim();
            if (string.IsNullOrEmpty(ma)) return;

            if (XtraMessageBox.Show($"Xóa cấu hình FIFO cho mã hàng '{ma}'?",
                "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                // ★ SỬA — thêm NguoiThucHien (tên máy) để ghi vào bảng lịch sử
                _repo.Delete(ma, NguoiThucHien);
                _gridView.DeleteRow(handle);

                XtraMessageBox.Show("Đã xóa.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Lỗi xóa: {ex.Message}",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Lưu toàn bộ — Upsert từng dòng trong grid
        // ════════════════════════════════════════════════════════════════
        private void BtnLuu_Click(object sender, EventArgs e)
        {
            _gridView.CloseEditor();
            _gridView.UpdateCurrentRow();

            int soLuong = 0;
            try
            {
                foreach (DataRow row in _dt.Rows)
                {
                    if (row.RowState == DataRowState.Deleted) continue;

                    string ma = row["ItemCode"]?.ToString().Trim() ?? "";
                    if (string.IsNullOrEmpty(ma)) continue;

                    bool enforce = row["EnforceFifo"] != DBNull.Value
                        && Convert.ToBoolean(row["EnforceFifo"]);

                    // ★ SỬA — thêm NguoiThucHien (tên máy)
                    _repo.Upsert(ma, enforce, NguoiThucHien);
                    soLuong++;
                }

                XtraMessageBox.Show($"Đã lưu {soLuong} mã hàng.",
                    "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

                LoadData(); // reload để đồng bộ lại _IsNew, thứ tự, UpdatedAt/UpdatedBy...
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Lỗi lưu cấu hình: {ex.Message}",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Dialog chọn mã hàng đơn giản — dùng cho nút "Thêm Mã Hàng"
    // (giữ nguyên 100%, không có thay đổi gì liên quan)
    // ════════════════════════════════════════════════════════════════════
    internal sealed class FormChonMaHang : XtraForm
    {
        private readonly GridControl _grid;
        private readonly GridView _gridView;

        public string SelectedItemCode { get; private set; }
        public string SelectedItemName { get; private set; }

        public FormChonMaHang(DataTable dsMaHang)
        {
            this.Text = "Chọn Mã Hàng";
            this.Size = new Size(480, 520);
            this.StartPosition = FormStartPosition.CenterParent;

            var txtSearch = new TextEdit { Dock = DockStyle.Top };
            txtSearch.EditValueChanged += (s, e) =>
            {
                string kw = txtSearch.Text?.Trim() ?? "";
                _grid.DataSource = string.IsNullOrEmpty(kw)
                    ? dsMaHang
                    : dsMaHang.AsEnumerable()
                        .Where(r => r["Code"].ToString().IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0
                                 || r["Name"].ToString().IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                        .CopyToDataTable2();
            };

            _grid = new GridControl { Dock = DockStyle.Fill, DataSource = dsMaHang };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _grid.ViewCollection.Add(_gridView);
            _gridView.OptionsBehavior.Editable = false;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsBehavior.ReadOnly = true;
            _gridView.DoubleClick += (s, e) => ChonDongHienTai();

            var btnOk = new SimpleButton { Text = "Chọn", Dock = DockStyle.Bottom, Height = 32 };
            btnOk.Click += (s, e) => ChonDongHienTai();

            this.Controls.Add(_grid);
            this.Controls.Add(btnOk);
            this.Controls.Add(txtSearch);
        }

        private void ChonDongHienTai()
        {
            if (_gridView.FocusedRowHandle < 0) return;
            SelectedItemCode = _gridView.GetFocusedRowCellDisplayText("Code");
            SelectedItemName = _gridView.GetFocusedRowCellDisplayText("Name");
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    internal static class DataTableExtensions
    {
        public static DataTable CopyToDataTable2(this System.Collections.Generic.IEnumerable<DataRow> rows)
        {
            var list = rows.ToList();
            if (list.Count == 0) return new DataTable();
            return list.CopyToDataTable();
        }
    }
}