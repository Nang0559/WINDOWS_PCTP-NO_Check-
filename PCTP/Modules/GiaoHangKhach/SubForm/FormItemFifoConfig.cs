using DevExpress.Data.Filtering;
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
                // Qua repository (Form -> Repository), không query trực tiếp trong Form.
                DataTable dsMaHang = _repo.GetDanhSachMaHangKhaDung();

                using (var frm = new FormChonMaHang(dsMaHang))
                {
                    if (frm.ShowDialog() != DialogResult.OK) return;
                    if (frm.SelectedItems.Count == 0) return;

                    // Tập mã đã có trong lưới, dùng để bỏ qua mã trùng
                    var daCo = new HashSet<string>(
                        _dt.AsEnumerable()
                           .Where(r => r.RowState != DataRowState.Deleted)
                           .Select(r => (r["ItemCode"]?.ToString() ?? "").Trim()),
                        StringComparer.OrdinalIgnoreCase);

                    int soThem = 0, soTrung = 0;
                    foreach (var item in frm.SelectedItems)
                    {
                        string ma = (item.Key ?? "").Trim();
                        if (ma.Length == 0) continue;

                        if (!daCo.Add(ma)) { soTrung++; continue; }

                        DataRow row = _dt.NewRow();
                        row["ItemCode"] = ma;
                        row["TenHang"] = item.Value ?? "";
                        row["EnforceFifo"] = false;
                        row["_IsNew"] = true;
                        _dt.Rows.Add(row);
                        soThem++;
                    }

                    _gridView.RefreshData();

                    if (soTrung > 0)
                    {
                        XtraMessageBox.Show(
                            $"Đã thêm {soThem} mã hàng. Bỏ qua {soTrung} mã đã có trong danh sách.\n" +
                            "Bấm \"Lưu\" để ghi vào cơ sở dữ liệu.",
                            "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
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
    // Dialog chọn mã hàng — cho phép TICK chọn NHIỀU mã.
    // Lọc bằng ActiveFilterCriteria của GridView (không dựng lại DataTable) nên các
    // mã đã tick vẫn được giữ khi đổi từ khóa tìm kiếm.
    // ════════════════════════════════════════════════════════════════════
    internal sealed class FormChonMaHang : XtraForm
    {
        private const string ColChon = "_Chon";

        private readonly DataTable _dt;
        private readonly GridControl _grid;
        private readonly GridView _gridView;
        private readonly LabelControl _lblDem;
        private readonly SimpleButton _btnOk;

        /// <summary>Danh sách (Code, Name) đã chọn.</summary>
        public List<KeyValuePair<string, string>> SelectedItems { get; private set; }
            = new List<KeyValuePair<string, string>>();

        // Giữ tương thích code cũ: mã đầu tiên được chọn.
        public string SelectedItemCode => SelectedItems.Count > 0 ? SelectedItems[0].Key : null;
        public string SelectedItemName => SelectedItems.Count > 0 ? SelectedItems[0].Value : null;

        public FormChonMaHang(DataTable dsMaHang)
        {
            _dt = dsMaHang ?? new DataTable();
            if (!_dt.Columns.Contains("Code")) _dt.Columns.Add("Code", typeof(string));
            if (!_dt.Columns.Contains("Name")) _dt.Columns.Add("Name", typeof(string));
            if (!_dt.Columns.Contains(ColChon))
            {
                var c = _dt.Columns.Add(ColChon, typeof(bool));
                c.DefaultValue = false;
                foreach (DataRow r in _dt.Rows) r[ColChon] = false;
            }

            this.Text = "Chọn Mã Hàng (tick để chọn nhiều)";
            this.Size = new Size(560, 600);
            this.StartPosition = FormStartPosition.CenterParent;

            // ── Ô tìm kiếm ──────────────────────────────────────────
            var txtSearch = new TextEdit { Dock = DockStyle.Top };
            txtSearch.Properties.NullValuePrompt = "Nhập mã hoặc tên hàng để lọc...";
            txtSearch.Properties.NullValuePromptShowForEmptyValue = true;
            txtSearch.EditValueChanged += (s, e) => ApDungLoc(txtSearch.Text);

            // ── Thanh nút chọn nhanh ────────────────────────────────
            var panelTools = new PanelControl { Dock = DockStyle.Top, Height = 40 };
            var btnChonLoc = new SimpleButton
            {
                Text = "Chọn tất cả (đang lọc)",
                Location = new Point(8, 6),
                Width = 160
            };
            btnChonLoc.Click += (s, e) => DatCheckDangLoc(true);
            var btnBoChon = new SimpleButton
            {
                Text = "Bỏ chọn tất cả",
                Location = new Point(btnChonLoc.Right + 8, 6),
                Width = 120
            };
            btnBoChon.Click += (s, e) => DatCheckTatCa(false);
            _lblDem = new LabelControl
            {
                Location = new Point(btnBoChon.Right + 12, 12),
                Text = "Đã chọn: 0"
            };
            panelTools.Controls.AddRange(new Control[] { btnChonLoc, btnBoChon, _lblDem });

            // ── Lưới ────────────────────────────────────────────────
            _grid = new GridControl { Dock = DockStyle.Fill, DataSource = _dt };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _grid.ViewCollection.Add(_gridView);
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.ColumnAutoWidth = false;
            _gridView.OptionsBehavior.Editable = true;   // chỉ cột tick được sửa

            var colChon = _gridView.Columns.AddField(ColChon);
            colChon.Caption = "Chọn";
            colChon.Visible = true;
            colChon.VisibleIndex = 0;
            colChon.Width = 60;
            var riCheck = new RepositoryItemCheckEdit();
            riCheck.CheckedChanged += (s, e) => { _gridView.PostEditor(); CapNhatDem(); };
            _grid.RepositoryItems.Add(riCheck);
            colChon.ColumnEdit = riCheck;

            var colCode = _gridView.Columns.AddField("Code");
            colCode.Caption = "Mã Hàng";
            colCode.Visible = true;
            colCode.VisibleIndex = 1;
            colCode.Width = 180;
            colCode.OptionsColumn.AllowEdit = false;

            var colName = _gridView.Columns.AddField("Name");
            colName.Caption = "Tên Hàng";
            colName.Visible = true;
            colName.VisibleIndex = 2;
            colName.Width = 280;
            colName.OptionsColumn.AllowEdit = false;

            // Bấm đúp vào dòng = đổi trạng thái tick
            _gridView.DoubleClick += (s, e) =>
            {
                int h = _gridView.FocusedRowHandle;
                if (h < 0) return;
                bool cur = Convert.ToBoolean(_gridView.GetRowCellValue(h, ColChon));
                _gridView.SetRowCellValue(h, ColChon, !cur);
                _gridView.PostEditor();
                CapNhatDem();
            };

            // ── Nút dưới ────────────────────────────────────────────
            var panelBottom = new PanelControl { Dock = DockStyle.Bottom, Height = 44 };
            _btnOk = new SimpleButton
            {
                Text = "Thêm đã chọn",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Width = 140,
                Height = 30
            };
            _btnOk.Click += (s, e) => XacNhan();
            var btnHuy = new SimpleButton
            {
                Text = "Hủy",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Width = 90,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };
            panelBottom.Controls.AddRange(new Control[] { _btnOk, btnHuy });
            panelBottom.Resize += (s, e) =>
            {
                btnHuy.Location = new Point(panelBottom.Width - btnHuy.Width - 10, 7);
                _btnOk.Location = new Point(btnHuy.Left - _btnOk.Width - 8, 7);
            };
            btnHuy.Location = new Point(panelBottom.Width - btnHuy.Width - 10, 7);
            _btnOk.Location = new Point(btnHuy.Left - _btnOk.Width - 8, 7);
            this.CancelButton = btnHuy;

            this.Controls.Add(_grid);
            this.Controls.Add(panelBottom);
            this.Controls.Add(panelTools);
            this.Controls.Add(txtSearch);
            CapNhatDem();
        }

        // ── Lọc theo mã/tên, KHÔNG đụng tới trạng thái tick ───────────
        private void ApDungLoc(string keyword)
        {
            string kw = (keyword ?? "").Trim();
            if (kw.Length == 0)
            {
                _gridView.ActiveFilterCriteria = null;
                return;
            }

            _gridView.ActiveFilterCriteria = new GroupOperator(
                GroupOperatorType.Or,
                new FunctionOperator(FunctionOperatorType.Contains,
                    new OperandProperty("Code"), new OperandValue(kw)),
                new FunctionOperator(FunctionOperatorType.Contains,
                    new OperandProperty("Name"), new OperandValue(kw)));
        }

        // Tick/bỏ tick mọi dòng đang HIỂN THỊ sau khi lọc
        private void DatCheckDangLoc(bool value)
        {
            _gridView.CloseEditor();
            _gridView.BeginUpdate();
            try
            {
                for (int i = 0; i < _gridView.DataRowCount; i++)
                {
                    int src = _gridView.GetDataSourceRowIndex(i);
                    if (src >= 0) _dt.Rows[src][ColChon] = value;
                }
            }
            finally { _gridView.EndUpdate(); }
            CapNhatDem();
        }

        private void DatCheckTatCa(bool value)
        {
            _gridView.CloseEditor();
            _gridView.BeginUpdate();
            try { foreach (DataRow r in _dt.Rows) r[ColChon] = value; }
            finally { _gridView.EndUpdate(); }
            CapNhatDem();
        }

        private void CapNhatDem()
        {
            int n = _dt.AsEnumerable().Count(r => r[ColChon] != DBNull.Value && (bool)r[ColChon]);
            _lblDem.Text = $"Đã chọn: {n}";
            _btnOk.Text = n > 0 ? $"Thêm đã chọn ({n})" : "Thêm đã chọn";
        }

        private void XacNhan()
        {
            _gridView.CloseEditor();
            _gridView.UpdateCurrentRow();

            var ds = _dt.AsEnumerable()
                .Where(r => r[ColChon] != DBNull.Value && (bool)r[ColChon])
                .Select(r => new KeyValuePair<string, string>(
                    Convert.ToString(r["Code"]).Trim(),
                    Convert.ToString(r["Name"]).Trim()))
                .Where(kv => kv.Key.Length > 0)
                .ToList();

            // Chưa tick gì nhưng đang đứng ở 1 dòng -> coi như chọn dòng đó (giữ hành vi cũ)
            if (ds.Count == 0 && _gridView.FocusedRowHandle >= 0)
            {
                string code = (_gridView.GetFocusedRowCellDisplayText("Code") ?? "").Trim();
                string name = (_gridView.GetFocusedRowCellDisplayText("Name") ?? "").Trim();
                if (code.Length > 0) ds.Add(new KeyValuePair<string, string>(code, name));
            }

            if (ds.Count == 0)
            {
                XtraMessageBox.Show("Vui lòng tick chọn ít nhất một mã hàng.",
                    "Chưa chọn", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedItems = ds;
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