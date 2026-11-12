using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.GiaoHangKhach.Repositories;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.Controls
{
    /// <summary>
    /// Xem lại / sửa / xóa các đơn ĐÃ upload vào bảng đơn riêng (Purchase_Order_YMVN, Purchase_Order_HTN, ...).
    /// - Hiển thị trạng thái Đã giao / Chưa giao.
    /// - Dòng ĐÃ GIAO bị khóa: không sửa ô, không xóa (repository cũng chặn ở tầng DB).
    /// - Thêm / sửa / xóa chỉ ghi xuống DB khi bấm Lưu (một transaction, lỗi một dòng → rollback tất cả).
    /// </summary>
    public class UploadedOrderManagerControl : XtraUserControl
    {
        /// <summary>Bắn ra sau khi Lưu thành công (form cha dùng để nạp lại lưới đơn).</summary>
        public event EventHandler DaLuu;

        private UploadedOrderRepository _repo;
        private UploadedOrderRepository.TableInfo _info;
        private DataTable _dt;

        private readonly DateEdit dtTu = new DateEdit();
        private readonly DateEdit dtDen = new DateEdit();
        private readonly ComboBoxEdit cboTrangThai = new ComboBoxEdit();
        private readonly SimpleButton btnTai = new SimpleButton();
        private readonly SimpleButton btnThem = new SimpleButton();
        private readonly SimpleButton btnXoa = new SimpleButton();
        private readonly SimpleButton btnLuu = new SimpleButton();
        private readonly LabelControl lblThongTin = new LabelControl();
        private readonly GridControl grid = new GridControl();
        private readonly GridView view = new GridView();

        public UploadedOrderManagerControl()
        {
            Dock = DockStyle.Fill;
            BuildUI();
        }

        /// <summary>Có thay đổi trên lưới mà chưa Lưu.</summary>
        public bool CoThayDoiChuaLuu
        {
            get
            {
                if (_dt == null) return false;
                view.CloseEditor();
                return _dt.GetChanges() != null;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Khởi tạo
        // ══════════════════════════════════════════════════════════════════
        public void Configure(string connectionString, string orderTable)
        {
            _repo = new UploadedOrderRepository(connectionString, orderTable);
            try
            {
                _info = _repo.GetInfo();
            }
            catch (Exception ex)
            {
                _info = null;
                SetEnabled(false);
                lblThongTin.Text = "Lỗi: " + ex.Message;
                lblThongTin.Appearance.ForeColor = Color.Firebrick;
                return;
            }

            if (!_info.HasIsDelivered)
            {
                // Không có cột IsDelivered → không biết dòng nào đã giao → chỉ cho XEM.
                btnThem.Enabled = btnXoa.Enabled = btnLuu.Enabled = false;
                view.OptionsBehavior.Editable = false;
                lblThongTin.Text = "Bảng [" + orderTable + "] chưa có cột IsDelivered: chỉ xem. " +
                                   "Chạy SQL/PCTP_OrderTables_AddIsDelivered.sql để bật sửa/xóa.";
                lblThongTin.Appearance.ForeColor = Color.DarkOrange;
            }

            TaiDuLieu(false);
        }

        private void BuildUI()
        {
            var pnl = new PanelControl
            {
                Dock = DockStyle.Top,
                Height = 44,
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder
            };

            int x = 8, y = 10;
            pnl.Controls.Add(new LabelControl { Text = "Từ ngày:", Location = new Point(x, y + 3), AutoSizeMode = LabelAutoSizeMode.None, Size = new Size(55, 18) });
            x += 58;
            Dat(dtTu, x, y, 105); x += 110;
            pnl.Controls.Add(new LabelControl { Text = "Đến:", Location = new Point(x, y + 3), AutoSizeMode = LabelAutoSizeMode.None, Size = new Size(30, 18) });
            x += 32;
            Dat(dtDen, x, y, 105); x += 112;

            cboTrangThai.Location = new Point(x, y);
            cboTrangThai.Size = new Size(100, 22);
            cboTrangThai.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            cboTrangThai.Properties.Items.AddRange(new object[] { "Tất cả", "Chưa giao", "Đã giao" });
            cboTrangThai.SelectedIndex = 0;
            cboTrangThai.SelectedIndexChanged += (s, e) => ApDungLoc();
            pnl.Controls.Add(cboTrangThai);
            x += 108;

            Nut(btnTai, "🔄 Tải", x, y, 70); btnTai.Click += (s, e) => TaiDuLieu(true); x += 76;
            Nut(btnThem, "➕ Thêm dòng", x, y, 100); btnThem.Click += BtnThem_Click; x += 106;
            Nut(btnXoa, "🗑 Xóa", x, y, 70); btnXoa.Click += BtnXoa_Click; x += 76;
            Nut(btnLuu, "💾 Lưu", x, y, 80); btnLuu.Click += BtnLuu_Click; x += 90;
            foreach (var b in new[] { btnTai, btnThem, btnXoa, btnLuu }) pnl.Controls.Add(b);

            lblThongTin.Location = new Point(x, y + 3);
            lblThongTin.AutoSizeMode = LabelAutoSizeMode.None;
            lblThongTin.Size = new Size(430, 18);
            lblThongTin.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnl.Controls.Add(lblThongTin);

            dtTu.DateTime = DateTime.Today.AddDays(-7);
            dtDen.DateTime = DateTime.Today.AddDays(30);

            // ── Lưới ────────────────────────────────────────────────────────
            grid.Dock = DockStyle.Fill;
            grid.MainView = view;
            grid.ViewCollection.Add(view);
            view.GridControl = grid;

            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ColumnAutoWidth = false;
            view.OptionsView.NewItemRowPosition = NewItemRowPosition.Top;
            view.OptionsSelection.MultiSelect = true;
            view.OptionsSelection.MultiSelectMode = GridMultiSelectMode.RowSelect;
            view.OptionsBehavior.EditingMode = GridEditingMode.Inplace;
            view.OptionsBehavior.Editable = true;

            view.ShowingEditor += View_ShowingEditor;
            view.RowStyle += View_RowStyle;
            view.InitNewRow += View_InitNewRow;
            view.ValidateRow += View_ValidateRow;

            Controls.Add(grid);
            Controls.Add(pnl);   // thêm sau để Dock=Top nằm trên lưới
        }

        private static void Dat(DateEdit d, int x, int y, int w)
        {
            d.Location = new Point(x, y);
            d.Size = new Size(w, 22);
            d.Properties.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            d.Properties.DisplayFormat.FormatString = "dd/MM/yyyy";
            d.Properties.EditFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            d.Properties.EditFormat.FormatString = "dd/MM/yyyy";
            d.Properties.Mask.EditMask = "dd/MM/yyyy";
            d.Properties.Mask.UseMaskAsDisplayFormat = true;
        }

        private static void Nut(SimpleButton b, string text, int x, int y, int w)
        {
            b.Text = text;
            b.Location = new Point(x, y - 1);
            b.Size = new Size(w, 24);
        }

        private void SetEnabled(bool on)
        {
            btnTai.Enabled = btnThem.Enabled = btnXoa.Enabled = btnLuu.Enabled = on;
            dtTu.Enabled = dtDen.Enabled = cboTrangThai.Enabled = on;
        }

        // ══════════════════════════════════════════════════════════════════
        // Nạp dữ liệu / lọc
        // ══════════════════════════════════════════════════════════════════
        private void TaiDuLieu(bool hoiNeuDangSua)
        {
            if (_repo == null || _info == null) return;

            if (hoiNeuDangSua && CoThayDoiChuaLuu)
            {
                var kq = XtraMessageBox.Show(FindForm(),
                    "Có thay đổi chưa lưu. Tải lại sẽ bỏ các thay đổi đó. Tiếp tục?",
                    "Tải lại", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (kq != DialogResult.Yes) return;
            }

            if (dtDen.DateTime.Date < dtTu.DateTime.Date)
            {
                XtraMessageBox.Show(FindForm(), "Đến ngày phải ≥ Từ ngày.", "Tải đơn",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                _dt = _repo.Load(dtTu.DateTime, dtDen.DateTime, _info);
                grid.DataSource = null;
                grid.DataSource = _dt.DefaultView;
                CauHinhCot();
                ApDungLoc();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(FindForm(), "Lỗi tải đơn: " + ex.Message, "Tải đơn",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        private void ApDungLoc()
        {
            if (_dt == null) return;
            string loc = "";
            switch (cboTrangThai.SelectedIndex)
            {
                case 1: loc = "DaGiao = false"; break;
                case 2: loc = "DaGiao = true"; break;
            }
            _dt.DefaultView.RowFilter = loc;
            CapNhatThongTin();
        }

        private void CapNhatThongTin()
        {
            if (_dt == null) return;
            if (_info != null && !_info.HasIsDelivered) return;   // giữ cảnh báo thiếu cột
            int tong = 0, daGiao = 0;
            foreach (DataRow r in _dt.Rows)
            {
                if (r.RowState == DataRowState.Deleted) continue;
                tong++;
                if (DaGiao(r)) daGiao++;
            }
            lblThongTin.Text = "Tổng " + tong + " dòng — Đã giao " + daGiao + " / Chưa giao " + (tong - daGiao);
            lblThongTin.Appearance.ForeColor = Color.DimGray;
        }

        // ══════════════════════════════════════════════════════════════════
        // Cột
        // ══════════════════════════════════════════════════════════════════
        private void CauHinhCot()
        {
            view.PopulateColumns();

            foreach (GridColumn c in view.Columns)
            {
                c.OptionsColumn.AllowEdit = true;
                c.OptionsColumn.AllowSort = DevExpress.Utils.DefaultBoolean.True;
            }

            Cot("Oder_no", "PO", 110);
            Cot("Part_no", "Mã hàng", 120);
            Cot("Part_name", "Tên hàng", 220);
            Cot("NgayGiao", "Ngày giờ giao", 130);
            Cot("Slgiao", "SL giao", 80);
            Cot("QCDG", "QC đóng gói", 90);
            Cot("Gear", "Gear", 90);
            Cot("CUA", "Cửa", 70);
            Cot("TrangThai", "Trạng thái", 90, soleOnly: true);
            Cot("DeliveredDate", "Ngày đã giao", 130, soleOnly: true);

            view.Columns["DaGiao"].Visible = false;
            view.Columns["o_Oder_no"].Visible = false;
            view.Columns["o_Part_no"].Visible = false;
            view.Columns["o_NgayGiao"].Visible = false;

            // Ngày giờ: nhập đến phút (smalldatetime không có giây).
            var rpDate = new RepositoryItemDateEdit();
            rpDate.Mask.EditMask = "dd/MM/yyyy HH:mm";
            rpDate.Mask.UseMaskAsDisplayFormat = true;
            rpDate.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            rpDate.DisplayFormat.FormatString = "dd/MM/yyyy HH:mm";
            rpDate.EditFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            rpDate.EditFormat.FormatString = "dd/MM/yyyy HH:mm";
            rpDate.VistaDisplayMode = DevExpress.Utils.DefaultBoolean.False;
            grid.RepositoryItems.Add(rpDate);
            view.Columns["NgayGiao"].ColumnEdit = rpDate;

            var rpDateRo = new RepositoryItemDateEdit { ReadOnly = true };
            rpDateRo.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            rpDateRo.DisplayFormat.FormatString = "dd/MM/yyyy HH:mm";
            grid.RepositoryItems.Add(rpDateRo);
            view.Columns["DeliveredDate"].ColumnEdit = rpDateRo;

            // Số nguyên không âm.
            var rpSo = new RepositoryItemSpinEdit { IsFloatValue = false, MinValue = 0, MaxValue = int.MaxValue };
            rpSo.Mask.EditMask = "N0";
            rpSo.Mask.UseMaskAsDisplayFormat = true;
            grid.RepositoryItems.Add(rpSo);
            view.Columns["Slgiao"].ColumnEdit = rpSo;
            view.Columns["QCDG"].ColumnEdit = rpSo;

            // Cột chữ: giới hạn độ dài theo cột trong DB.
            foreach (string ten in new[] { "Oder_no", "Part_no", "Part_name", "Gear", "CUA" })
            {
                var rp = new RepositoryItemTextEdit();
                int max;
                if (_info != null && _info.MaxLength.TryGetValue(ten, out max)) rp.MaxLength = max;
                grid.RepositoryItems.Add(rp);
                view.Columns[ten].ColumnEdit = rp;
            }

            view.BestFitColumns();
        }

        private void Cot(string ten, string caption, int rong, bool soleOnly = false)
        {
            GridColumn c = view.Columns[ten];
            if (c == null) return;
            c.Caption = caption;
            c.Width = rong;
            c.Visible = true;
            if (soleOnly) c.OptionsColumn.AllowEdit = false;
        }

        // ══════════════════════════════════════════════════════════════════
        // Khóa dòng đã giao
        // ══════════════════════════════════════════════════════════════════
        private static bool DaGiao(DataRow r)
        {
            if (r == null || r.RowState == DataRowState.Deleted || r.RowState == DataRowState.Detached) return false;
            object v = r["DaGiao"];
            return v != null && v != DBNull.Value && Convert.ToBoolean(v);
        }

        private void View_ShowingEditor(object sender, CancelEventArgs e)
        {
            var v = (GridView)sender;
            if (DaGiao(v.GetDataRow(v.FocusedRowHandle))) e.Cancel = true;
        }

        private void View_RowStyle(object sender, RowStyleEventArgs e)
        {
            if (e.RowHandle < 0) return;
            var v = (GridView)sender;
            if (DaGiao(v.GetDataRow(e.RowHandle)))
            {
                e.Appearance.BackColor = Color.FromArgb(235, 235, 235);
                e.Appearance.ForeColor = Color.Gray;
                e.HighPriority = true;
            }
        }

        private void View_InitNewRow(object sender, InitNewRowEventArgs e)
        {
            var v = (GridView)sender;
            v.SetRowCellValue(e.RowHandle, "NgayGiao", DateTime.Today.AddHours(8));
            v.SetRowCellValue(e.RowHandle, "Slgiao", 0);
            v.SetRowCellValue(e.RowHandle, "QCDG", 0);
            v.SetRowCellValue(e.RowHandle, "DaGiao", false);
            v.SetRowCellValue(e.RowHandle, "TrangThai", "Chưa giao");
        }

        private void View_ValidateRow(object sender, ValidateRowEventArgs e)
        {
            var v = (GridView)sender;
            DataRow r = v.GetDataRow(e.RowHandle);
            string loi = KiemTraDong(r);
            if (loi != null)
            {
                e.Valid = false;
                e.ErrorText = loi;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Nút
        // ══════════════════════════════════════════════════════════════════
        private void BtnThem_Click(object sender, EventArgs e)
        {
            if (_dt == null) return;
            // Dòng mới là Chưa giao → bỏ lọc "Đã giao" để người dùng thấy dòng vừa thêm.
            if (cboTrangThai.SelectedIndex == 2) cboTrangThai.SelectedIndex = 0;
            view.AddNewRow();
            view.FocusedColumn = view.Columns["Oder_no"];
            view.ShowEditor();
        }

        private void BtnXoa_Click(object sender, EventArgs e)
        {
            if (_dt == null) return;
            view.CloseEditor();

            int[] handles = view.GetSelectedRows();
            if (handles.Length == 0 && view.FocusedRowHandle >= 0)
                handles = new[] { view.FocusedRowHandle };

            var xoa = new List<DataRow>();
            int boQuaDaGiao = 0;
            foreach (int h in handles)
            {
                if (h < 0) continue;
                DataRow r = view.GetDataRow(h);
                if (r == null) continue;
                if (DaGiao(r)) { boQuaDaGiao++; continue; }   // đã giao → không xóa
                xoa.Add(r);
            }

            if (xoa.Count == 0)
            {
                XtraMessageBox.Show(FindForm(),
                    boQuaDaGiao > 0 ? "Các dòng đã giao không được xóa." : "Chưa chọn dòng nào để xóa.",
                    "Xóa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string msg = "Xóa " + xoa.Count + " dòng chưa giao? (Chỉ xóa khỏi DB khi bấm Lưu.)";
            if (boQuaDaGiao > 0) msg += "\n" + boQuaDaGiao + " dòng đã giao sẽ được giữ nguyên.";
            if (XtraMessageBox.Show(FindForm(), msg, "Xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes) return;

            foreach (DataRow r in xoa) r.Delete();
            CapNhatThongTin();
        }

        private void BtnLuu_Click(object sender, EventArgs e)
        {
            if (_dt == null || _repo == null || _info == null) return;

            if (!view.PostEditor()) return;
            view.UpdateCurrentRow();
            view.CloseEditor();

            DataTable thayDoi = _dt.GetChanges();
            if (thayDoi == null || thayDoi.Rows.Count == 0)
            {
                XtraMessageBox.Show(FindForm(), "Không có thay đổi để lưu.", "Lưu",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string loi = KiemTraTatCa();
            if (loi != null)
            {
                XtraMessageBox.Show(FindForm(), loi, "Lưu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int them = thayDoi.Select("", "", DataViewRowState.Added).Length;
            int sua = thayDoi.Select("", "", DataViewRowState.ModifiedCurrent).Length;
            int xoa = thayDoi.Select("", "", DataViewRowState.Deleted).Length;
            if (XtraMessageBox.Show(FindForm(),
                    "Lưu thay đổi?\nThêm: " + them + "   Sửa: " + sua + "   Xóa: " + xoa,
                    "Lưu", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                Cursor = Cursors.WaitCursor;
                int soGhi = _repo.Save(_dt, _info);
                XtraMessageBox.Show(FindForm(), "Đã lưu " + soGhi + " dòng.", "Lưu",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                TaiDuLieu(false);              // nạp lại → khóa gốc o_* và trạng thái luôn khớp DB
                DaLuu?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                // Repository đã rollback; giữ nguyên thay đổi trên lưới để người dùng sửa lại.
                XtraMessageBox.Show(FindForm(), "Không lưu được (đã hoàn tác toàn bộ):\n" + ex.Message, "Lưu",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        // ══════════════════════════════════════════════════════════════════
        // Kiểm tra dữ liệu
        // ══════════════════════════════════════════════════════════════════
        private string KiemTraDong(DataRow r)
        {
            if (r == null || r.RowState == DataRowState.Deleted) return null;

            string po = Convert.ToString(r["Oder_no"]).Trim();
            string part = Convert.ToString(r["Part_no"]).Trim();
            if (po.Length == 0) return "PO không được để trống.";
            if (part.Length == 0) return "Mã hàng không được để trống.";
            if (r["NgayGiao"] == DBNull.Value) return "Ngày giờ giao không được để trống.";

            int sl, qc;
            if (!int.TryParse(Convert.ToString(r["Slgiao"]), out sl) || sl < 0) return "SL giao phải là số nguyên ≥ 0.";
            if (!int.TryParse(Convert.ToString(r["QCDG"]), out qc) || qc < 0) return "QC đóng gói phải là số nguyên ≥ 0.";

            foreach (string ten in new[] { "Oder_no", "Part_no", "Part_name", "Gear", "CUA" })
            {
                int max;
                string val = Convert.ToString(r[ten]).Trim();
                if (_info != null && _info.MaxLength.TryGetValue(ten, out max) && val.Length > max)
                    return "Cột " + ten + " tối đa " + max + " ký tự (đang " + val.Length + ").";
            }
            return null;
        }

        private string KiemTraTatCa()
        {
            var khoa = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in _dt.Rows)
            {
                if (r.RowState == DataRowState.Deleted) continue;

                if (r.RowState == DataRowState.Added || r.RowState == DataRowState.Modified)
                {
                    string loi = KiemTraDong(r);
                    if (loi != null)
                        return "PO " + Convert.ToString(r["Oder_no"]) + " / " + Convert.ToString(r["Part_no"]) + ": " + loi;
                }

                // Trùng khóa PO + mã hàng + ngày giờ (đến phút) ngay trong lưới.
                DateTime n = Convert.ToDateTime(r["NgayGiao"]);
                string k = Convert.ToString(r["Oder_no"]).Trim() + "|" + Convert.ToString(r["Part_no"]).Trim() + "|" +
                           n.ToString("yyyyMMddHHmm");
                if (!khoa.Add(k))
                    return "Trùng khóa (PO + mã hàng + ngày giờ giao): " + k.Replace("|", " / ");
            }
            return null;
        }
    }
}