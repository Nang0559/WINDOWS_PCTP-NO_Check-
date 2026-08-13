using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using OfficeOpenXml;
using PCTP.ClassSQL;
using PCTP.FuctionMain;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.YMN
{
    public class FRM_UploadMikrun : XtraForm
    {
        private readonly CustomerConfig _cfg;
        private readonly SQLPROVIDER _sql;
        private ExcelPackage _excelPkg;
        private DataTable _previewDt;

        private LabelControl lblFile, lblSheet, lblCotBD, lblCotKT,
                              lblDongBD, lblDongKT, lblHint, lblSoDong;
        private TextEdit txtDuongDan, txtCotBD, txtCotKT,
                              txtDongBD, txtDongKT;
        private ComboBoxEdit cboSheet;
        private CheckEdit chkXoaCu;
        private SimpleButton btnChonFile, btnXemTruoc, btnUpload, btnDong;
        private GridControl gridPreview;
        private GridView gridViewPreview;

        private readonly string _targetTable;
        private readonly string _title;

        // ── Constructor YMVN (mặc định) ──────────────────────────────────────
        public FRM_UploadMikrun(SQLPROVIDER sql, CustomerConfig cfg)
            : this(sql, cfg,
                   targetTable: cfg?.OrderTable ?? "Purchase_Order_YMVN",
                   title: $"Upload Milkrun — {cfg?.OrderTable ?? "Purchase_Order_YMVN"}")
        { }

        // ── Constructor dùng chung ────────────────────────────────────────────
        public FRM_UploadMikrun(SQLPROVIDER sql, CustomerConfig cfg,
                                string targetTable, string title)
        {
            _sql = sql;
            _cfg = cfg;
            _targetTable = targetTable;
            _title = title;

            OfficeOpenXml.ExcelPackage.LicenseContext =
                OfficeOpenXml.LicenseContext.NonCommercial;

            BuildUI();
        }

        private void BuildUI()
        {
            this.Text = _title;
            this.Size = new System.Drawing.Size(1000, 760);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            int y = 15;

            lblFile = Lbl("File Excel/CSV:", 15, y + 3, 110);
            txtDuongDan = new TextEdit
            {
                Location = new System.Drawing.Point(130, y),
                Size = new System.Drawing.Size(640, 28)
            };
            txtDuongDan.Properties.ReadOnly = true;
            btnChonFile = Btn("📂 Chọn File", 780, y, 180, 28);
            btnChonFile.Click += BtnChonFile_Click;
            y += 40;

            lblSheet = Lbl("Sheet:", 15, y + 3, 110);
            cboSheet = new ComboBoxEdit
            {
                Location = new System.Drawing.Point(130, y),
                Size = new System.Drawing.Size(250, 28)
            };
            y += 40;

            var grp = new DevExpress.XtraEditors.GroupControl
            {
                Text = "Cấu hình đọc dữ liệu",
                Location = new System.Drawing.Point(15, y),
                Size = new System.Drawing.Size(960, 105)
            };

            lblCotBD = Lbl("Cột bắt đầu:", 10, 28, 90);
            txtCotBD = TxtNum("1", 105, 25, 55);
            lblCotKT = Lbl("Cột kết thúc:", 175, 28, 90);
            txtCotKT = TxtNum("8", 270, 25, 55);

            lblDongBD = Lbl("Dòng bắt đầu:", 10, 68, 90);
            txtDongBD = TxtNum("2", 105, 65, 55);
            lblDongKT = Lbl("Dòng kết thúc:", 175, 68, 95);
            txtDongKT = TxtNum("", 275, 65, 55);

            var lblKT2 = Lbl("(để trống = đọc hết)", 340, 68, 160);
            lblKT2.Appearance.ForeColor = System.Drawing.Color.Gray;

            grp.Controls.AddRange(new System.Windows.Forms.Control[]
            {
            lblCotBD, txtCotBD, lblCotKT, txtCotKT,
            lblDongBD, txtDongBD, lblDongKT, txtDongKT, lblKT2
            });
            y += 120;

            lblHint = new LabelControl
            {
                Text = "📋 Thứ tự cột: [1] Oder_no  [2] Part_no  [3] Part_name  " +
                       "[4] DateOder(yyyymmdd) (Ngày giao)  [5] SlGiao  [6] Quy cách đóng gói" +
                       $" [7] Gear  [8] CUA  " +
                       $"→ Bảng đích: {_targetTable}",
                Location = new System.Drawing.Point(15, y)
            };
            lblHint.Appearance.ForeColor = System.Drawing.Color.DarkBlue;
            y += 30;

            // ── chkXoaCu với tooltip giải thích logic IsDelivered ────────────
            chkXoaCu = new CheckEdit
            {
                Text = "Xóa dữ liệu cũ (chưa giao) trước khi upload",
                Location = new System.Drawing.Point(15, y + 5),
                Size = new System.Drawing.Size(420, 25)
            };
            chkXoaCu.Checked = true;

            btnXemTruoc = Btn("👁 Xem Trước", 460, y, 130, 32);
            btnXemTruoc.Click += BtnXemTruoc_Click;

            btnUpload = Btn("⬆ Upload DB", 600, y, 130, 32);
            btnUpload.Appearance.BackColor =
                System.Drawing.Color.FromArgb(0, 120, 212);
            btnUpload.Appearance.ForeColor = System.Drawing.Color.White;
            btnUpload.Click += BtnUpload_Click;

            btnDong = Btn("✕ Đóng", 740, y, 100, 32);
            btnDong.Click += (s, e) => this.Close();

            lblSoDong = new LabelControl
            {
                Text = "",
                Location = new System.Drawing.Point(15, y + 38)
            };
            lblSoDong.Appearance.ForeColor = System.Drawing.Color.DarkGreen;
            y += 75;

            gridViewPreview = new GridView();
            gridViewPreview.OptionsBehavior.Editable = false;
            gridViewPreview.OptionsView.ShowGroupPanel = false;

            gridPreview = new GridControl
            {
                Location = new System.Drawing.Point(15, y),
                Size = new System.Drawing.Size(965, 290),
                MainView = gridViewPreview
            };
            gridViewPreview.GridControl = gridPreview;

            this.Controls.AddRange(new System.Windows.Forms.Control[]
            {
            lblFile, txtDuongDan, btnChonFile,
            lblSheet, cboSheet,
            grp, lblHint,
            chkXoaCu, btnXemTruoc, btnUpload, btnDong, lblSoDong,
            gridPreview
            });
        }

        // ════════════════════════════════════════════════════════════
        // Chọn file
        // ════════════════════════════════════════════════════════════
        private void BtnChonFile_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Chọn file Excel";
                dlg.Filter = "Excel/CSV|*.xlsx;*.xls;*.csv|All|*.*";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                txtDuongDan.Text = dlg.FileName;
                LoadSheets(dlg.FileName);
            }
        }

        private void LoadSheets(string path)
        {
            try
            {
                _excelPkg?.Dispose();
                cboSheet.Properties.Items.Clear();

                if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    cboSheet.Properties.Items.Add("(CSV)");
                    cboSheet.SelectedIndex = 0;
                    cboSheet.Enabled = false;
                    return;
                }

                cboSheet.Enabled = true;
                using (var fs = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.ReadWrite))
                    _excelPkg = new ExcelPackage(fs);

                foreach (var ws in _excelPkg.Workbook.Worksheets)
                    cboSheet.Properties.Items.Add(ws.Name);

                if (cboSheet.Properties.Items.Count > 0)
                    cboSheet.SelectedIndex = 0;
            }
            catch (Exception ex) { ShowErr($"Lỗi đọc file: {ex.Message}"); }
        }

        // ════════════════════════════════════════════════════════════
        // Xem trước
        // ════════════════════════════════════════════════════════════
        private void BtnXemTruoc_Click(object sender, EventArgs e)
        {
            try
            {
                _previewDt = DocDuLieu();
                if (_previewDt == null) return;
                gridPreview.DataSource = _previewDt;
                lblSoDong.Text = $"✅ Tìm thấy {_previewDt.Rows.Count} dòng";
            }
            catch (Exception ex) { ShowErr($"Lỗi xem trước: {ex.Message}"); }
        }

        // ════════════════════════════════════════════════════════════
        // Upload — logic IsDelivered
        // ════════════════════════════════════════════════════════════
        private void BtnUpload_Click(object sender, EventArgs e)
        {
            if (_previewDt == null || _previewDt.Rows.Count == 0)
            { ShowWarn("Chưa có dữ liệu. Bấm Xem Trước trước!"); return; }

            try
            {
                // ── Kiểm tra bảng có cột IsDelivered không ───────────────────────
                bool hasIsDelivered = CoIsDelivered(_targetTable);

                if (hasIsDelivered)
                {
                    // ── Disable chkXoaCu — logic xóa được kiểm soát tự động ──────
                    chkXoaCu.Enabled = false;
                    chkXoaCu.Checked = false;

                    // ── Kiểm tra trùng theo 4 điều kiện ─────────────────────────
                    var trungInfo = KiemTraTrungPO(_previewDt, _targetTable);

                    if (trungInfo.CoTrung)
                    {
                        if (trungInfo.CoDelivered)
                        {
                            // ── Có đơn đã giao → BLOCK ───────────────────────────
                            XtraMessageBox.Show(
                                $"Phát hiện {trungInfo.SoDongTrung} dòng trùng " +
                                $"đã được giao hàng:\n\n" +
                                $"{trungInfo.ThongTinTrung}\n\n" +
                                "Không thể upload đè lên đơn đã giao!",
                                "Lỗi", MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            return;
                        }

                        // ── Trùng nhưng chưa giao → hỏi xóa cũ ─────────────────
                        var rs = XtraMessageBox.Show(
                            $"Phát hiện {trungInfo.SoDongTrung} dòng trùng " +
                            $"(chưa giao hàng):\n\n" +
                            $"{trungInfo.ThongTinTrung}\n\n" +
                            "Bạn có muốn XÓA dữ liệu cũ và upload lại không?",
                            "Xác Nhận",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (rs != DialogResult.Yes) return;

                        // ── Xóa dữ liệu cũ chưa giao ────────────────────────────
                        XoaDuLieuCuChuaGiao(_previewDt, _targetTable);
                    }
                }
                else
                {
                    // ── Chưa có IsDelivered → logic cũ ──────────────────────────
                    if (XtraMessageBox.Show(
                            $"Upload {_previewDt.Rows.Count} dòng vào " +
                            $"[{_targetTable}]?",
                            "Xác nhận", MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question) != DialogResult.Yes) return;

                    if (chkXoaCu.Checked)
                        _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                            $"DELETE FROM [{_targetTable}]");
                }

                // ── Thực hiện INSERT ─────────────────────────────────────────────
                int n = 0;
                foreach (DataRow row in _previewDt.Rows)
                {
                    _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                        $"INSERT INTO [{_targetTable}] " +
                        "(Oder_no, Part_no, Part_name, NgayGiao, " +
                        " Slgiao, QCDG, Gear, CUA) " +
                        "VALUES(@o,@p,@pn,@d,@s,@q,@g,@c)",
                        new SqlParameter("@o", row["Oder_no"]),
                        new SqlParameter("@p", row["Part_no"]),
                        new SqlParameter("@pn", row["Part_name"]),
                        new SqlParameter("@d", row["NgayGiao"] == DBNull.Value
                            ? (object)DBNull.Value : row["NgayGiao"]),
                        new SqlParameter("@s", row["Slgiao"]),
                        //new SqlParameter("@tq", row["TotalQty"]),
                        new SqlParameter("@q", row["QCDG"]),
                        new SqlParameter("@g", row["Gear"]),
                        new SqlParameter("@c", row["CUA"]));
                    n++;
                }

                XtraMessageBox.Show($"✅ Upload thành công {n} dòng!",
                    "Thành công", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // ── Enable lại chkXoaCu sau khi upload xong ─────────────────────
                chkXoaCu.Enabled = true;
            }
            catch (Exception ex) { ShowErr($"Lỗi Upload: {ex.Message}"); }
        }

        // ── Kiểm tra bảng có cột IsDelivered không ───────────────────────────────
        private bool CoIsDelivered(string orderTable)
        {
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                $"WHERE TABLE_NAME  = '{orderTable}' " +
                $"  AND COLUMN_NAME = 'IsDelivered'");
            return raw == "1";
        }

        // ── Kiểm tra trùng: PO_NO + NgayGiao + MaHang + GioGiao ─────────────────
        private KiemTraTrungResult KiemTraTrungPO(DataTable dtUpload,
            string orderTable)
        {
            var result = new KiemTraTrungResult();
            var sbInfo = new System.Text.StringBuilder();

            foreach (DataRow row in dtUpload.Rows)
            {
                string poNo = row["Oder_no"]?.ToString().Trim() ?? "";
                string maHang = row["Part_no"]?.ToString().Trim() ?? "";
                if (string.IsNullOrEmpty(poNo) || string.IsNullOrEmpty(maHang))
                    continue;

                string ngay = row["NgayGiao"] != DBNull.Value
                    ? Convert.ToDateTime(row["NgayGiao"]).ToString("yyyy-MM-dd")
                    : "";
                string gio = row["NgayGiao"] != DBNull.Value
                    ? Convert.ToDateTime(row["NgayGiao"]).ToString("HH")
                    : "";

                if (string.IsNullOrEmpty(ngay)) continue;

                DataTable dtCheck = _sql.ExecuteQuery(_sql.B7R2_FCCdb, $@"
            SELECT TOP 1 IsDelivered
            FROM [{orderTable}]
            WHERE Oder_no = '{SqlHelper.Esc(poNo)}'
              AND Part_no  = '{SqlHelper.Esc(maHang)}'
              AND CAST(NgayGiao AS DATE) = '{ngay}'
              AND DATEPART(HH, NgayGiao) = {gio}");

                if (dtCheck.Rows.Count == 0) continue;

                result.CoTrung = true;
                result.SoDongTrung++;

                bool delivered = dtCheck.Rows[0]["IsDelivered"] != DBNull.Value
                    && Convert.ToBoolean(dtCheck.Rows[0]["IsDelivered"]);
                if (delivered) result.CoDelivered = true;

                sbInfo.AppendLine(
                    $"  PO={poNo} | {maHang} | {ngay} {gio}:xx" +
                    $" | {(delivered ? "✗ Đã giao" : "○ Chưa giao")}");
            }

            result.ThongTinTrung = sbInfo.ToString();
            return result;
        }

        // ── Xóa dữ liệu cũ chưa giao (IsDelivered=0) ────────────────────────────
        private void XoaDuLieuCuChuaGiao(DataTable dtUpload, string orderTable)
        {
            foreach (DataRow row in dtUpload.Rows)
            {
                string poNo = row["Oder_no"]?.ToString().Trim() ?? "";
                string maHang = row["Part_no"]?.ToString().Trim() ?? "";
                string ngay = row["NgayGiao"] != DBNull.Value
                    ? Convert.ToDateTime(row["NgayGiao"]).ToString("yyyy-MM-dd")
                    : "";
                string gio = row["NgayGiao"] != DBNull.Value
                    ? Convert.ToDateTime(row["NgayGiao"]).ToString("HH")
                    : "";

                if (string.IsNullOrEmpty(poNo) || string.IsNullOrEmpty(ngay))
                    continue;

                _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, $@"
            DELETE FROM [{orderTable}]
            WHERE Oder_no = '{SqlHelper.Esc(poNo)}'
              AND Part_no  = '{SqlHelper.Esc(maHang)}'
              AND CAST(NgayGiao AS DATE) = '{ngay}'
              AND DATEPART(HH, NgayGiao) = {gio}
              AND IsDelivered = 0");
            }
        }

        // ── Result class ──────────────────────────────────────────────────────────
        private class KiemTraTrungResult
        {
            public bool CoTrung { get; set; }
            public bool CoDelivered { get; set; }
            public int SoDongTrung { get; set; }
            public string ThongTinTrung { get; set; } = "";
        }

        // ════════════════════════════════════════════════════════════
        // Helpers — kiểm tra IsDelivered
        // ════════════════════════════════════════════════════════════

        // Đếm tổng đơn đã giao trong bảng đích
        private int DemDonDaGiao()
        {
            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                $"SELECT COUNT(*) FROM [{_targetTable}] WHERE IsDelivered = 1");
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        // ════════════════════════════════════════════════════════════
        // Đọc dữ liệu Excel / CSV
        // ════════════════════════════════════════════════════════════
        private DataTable DocDuLieu()
        {
            string path = txtDuongDan.Text.Trim();
            if (!File.Exists(path))
            { ShowWarn("Vui lòng chọn file hợp lệ!"); return null; }

            if (!int.TryParse(txtCotBD.Text, out int c1) || c1 < 1)
            { ShowWarn("Cột bắt đầu không hợp lệ!"); return null; }
            if (!int.TryParse(txtCotKT.Text, out int c2) || c2 < c1)
            { ShowWarn("Cột kết thúc không hợp lệ!"); return null; }
            if (!int.TryParse(txtDongBD.Text, out int r1) || r1 < 1)
            { ShowWarn("Dòng bắt đầu không hợp lệ!"); return null; }

            int r2 = int.TryParse(txtDongKT.Text, out int rx) && rx >= r1
                     ? rx : int.MaxValue;

            return path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? DocCSV(path, c1, c2, r1, r2)
                : DocExcel(c1, c2, r1, r2);
        }

        private DataTable DocExcel(int c1, int c2, int r1, int r2)
        {
            if (_excelPkg == null) { ShowWarn("Chưa mở file!"); return null; }

            var ws = _excelPkg.Workbook.Worksheets[cboSheet.Text];
            if (ws == null)
            { ShowWarn($"Không tìm thấy sheet [{cboSheet.Text}]!"); return null; }

            var dt = TaoBang();
            int rEnd = Math.Min(r2, ws.Dimension?.End.Row ?? r1);

            for (int r = r1; r <= rEnd; r++)
            {
                string G(int col) => col <= c2
                    ? ws.Cells[r, col].Text?.Trim() ?? "" : "";

                // Bỏ qua dòng trống
                bool empty = true;
                for (int c = c1; c <= c2; c++)
                    if (!string.IsNullOrWhiteSpace(ws.Cells[r, c].Text))
                    { empty = false; break; }
                if (empty) continue;

                // Bỏ qua nếu Oder_no rỗng
                if (string.IsNullOrWhiteSpace(G(c1 + 3))) continue;

                int ci = c1;
                var row = dt.NewRow();
                row["Oder_no"] = Trunc(G(ci++), 50);
                row["Part_no"] = Trunc(G(ci++), 50);
                row["Part_name"] = Trunc(G(ci++), 100);
                string dateStr = G(ci++);
              
                row["NgayGiao"] = ParseDateOnly(dateStr);
                row["Slgiao"] = ToInt(G(ci++));
                row["QCDG"] = ToInt(G(ci++));
                row["Gear"] = Trunc(G(ci++), 200);
                row["CUA"] = Trunc(G(ci), 20);
                dt.Rows.Add(row);
            }
            return dt;
        }

        private DataTable DocCSV(string path, int c1, int c2, int r1, int r2)
        {
            var dt = TaoBang();
            var lines = File.ReadAllLines(path);

            for (int i = r1 - 1; i < Math.Min(lines.Length, r2); i++)
            {
                var cols = lines[i].Split(',');
                string G(int idx) => idx < cols.Length
                    ? cols[idx]?.Trim() ?? "" : "";

                if (string.IsNullOrWhiteSpace(G(c1 + 3))) continue;

                int ci = c1 - 1;
                var row = dt.NewRow();
                row["Oder_no"] = G(ci++);
                row["Part_no"] = G(ci++);
                row["Part_name"] = G(ci++);
                string dateStr = G(ci++);
                //string timeStr = G(ci++);
                row["NgayGiao"] = ParseDateOnly(dateStr);
                row["Slgiao"] = ToInt(G(ci++));
                row["QCDG"] = ToInt(G(ci++));
                row["Gear"] = G(ci++);
                row["CUA"] = G(ci);
                dt.Rows.Add(row);
            }
            return dt;
        }

        // ════════════════════════════════════════════════════════════
        // Helpers chung
        // ════════════════════════════════════════════════════════════
        private static DataTable TaoBang()
        {
            var dt = new DataTable();
            dt.Columns.Add("Oder_no", typeof(string));
            dt.Columns.Add("Part_no", typeof(string));
            dt.Columns.Add("Part_name", typeof(string));
            dt.Columns.Add("NgayGiao", typeof(DateTime));
            dt.Columns.Add("Slgiao", typeof(int));
            dt.Columns.Add("QCDG", typeof(int));
            dt.Columns.Add("Gear", typeof(string));
            dt.Columns.Add("CUA", typeof(string));
            return dt;
        }

        //private static object ParseDateTimeVBA(string dateStr, string timeStr)
        //{
        //    if (string.IsNullOrWhiteSpace(dateStr)) return DBNull.Value;
        //    try
        //    {
        //        if (!DateTime.TryParseExact(dateStr, "yyyyMMdd",
        //            System.Globalization.CultureInfo.InvariantCulture,
        //            System.Globalization.DateTimeStyles.None, out DateTime date))
        //            return DBNull.Value;

        //        if (!string.IsNullOrWhiteSpace(timeStr))
        //        {
        //            string[] fmts = { "h:mm:ss tt","hh:mm:ss tt",
        //                          "H:mm:ss",   "HH:mm:ss" };
        //            if (DateTime.TryParseExact(timeStr.Trim(), fmts,
        //                System.Globalization.CultureInfo.InvariantCulture,
        //                System.Globalization.DateTimeStyles.None, out DateTime t))
        //                return date.Date + t.TimeOfDay;
        //        }
        //        return date;
        //    }
        //    catch { return DBNull.Value; }
        //}
        private static object ParseDateOnly(string dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return DBNull.Value;
            if (DateTime.TryParseExact(dateStr, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime dt))
                return dt;
            if (DateTime.TryParse(dateStr, out DateTime dt2))
                return dt2.Date;
            return DBNull.Value;
        }

        private static int ToInt(string s) =>
            int.TryParse(s?.Replace(",", "").Trim(), out int v) ? v : 0;

        private static string Trunc(string s, int maxLen) =>
            string.IsNullOrEmpty(s) ? s
            : s.Length <= maxLen ? s
            : s.Substring(0, maxLen);

        private static LabelControl Lbl(string text, int x, int y, int w) =>
            new LabelControl
            {
                Text = text,
                Location = new System.Drawing.Point(x, y),
                Size = new System.Drawing.Size(w, 20)
            };

        private static TextEdit TxtNum(string val, int x, int y, int w) =>
            new TextEdit
            {
                Text = val,
                Location = new System.Drawing.Point(x, y),
                Size = new System.Drawing.Size(w, 28)
            };

        private static SimpleButton Btn(string text, int x, int y, int w, int h) =>
            new SimpleButton
            {
                Text = text,
                Location = new System.Drawing.Point(x, y),
                Size = new System.Drawing.Size(w, h)
            };

        private void ShowErr(string msg) =>
            XtraMessageBox.Show(msg, "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

        private void ShowWarn(string msg) =>
            XtraMessageBox.Show(msg, "Cảnh báo",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _excelPkg?.Dispose();
            base.OnFormClosed(e);
        }
        
    }

}
