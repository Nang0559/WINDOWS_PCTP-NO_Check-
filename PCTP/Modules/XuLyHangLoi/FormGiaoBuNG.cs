using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Domain.Entities;
using PCTP.Models;
using PCTP.Shared.Helpers;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK.ViewForm
{
    using DevExpress.XtraEditors;
    using DevExpress.XtraGrid;
    using DevExpress.XtraGrid.Views.Grid;
    using PCTP.Modules.XuLyHangLoi.Models;
    using PCTP.Modules.XuLyHangLoi.Services;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Windows.Forms;

    public partial class FormGiaoBuNG : XtraForm
    {
        // ============================================================
        // SERVICE
        // ============================================================

        private readonly IGiaoBuNGService _service;

        // ============================================================
        // CONTEXT
        // ============================================================

        private readonly int _phieuKhachTraId;
        private readonly string _soPhieuKhachTra;

        // ============================================================
        // UI
        // ============================================================

        private TextEdit _txtSoPhieu;
        private TextEdit _txtQr;
        private TextEdit _txtNguoiGiao;

        private SimpleButton _btnScan;
        private SimpleButton _btnReload;
        private SimpleButton _btnHoanTat;
        private SimpleButton _btnDong;

        private GridControl _grid;
        private GridView _gridView;

        private LabelControl _lblTrangThai;
        private LabelControl _lblTong;
        private LabelControl _lblDaGiao;
        private LabelControl _lblConLai;
        private LabelControl _lblHint;

        private bool _dangXuLy;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public FormGiaoBuNG(
        IGiaoBuNGService service,
        int phieuKhachTraId,
        string soPhieuKhachTra)
        {
            _service = service
                ?? throw new ArgumentNullException(nameof(service));

            if (phieuKhachTraId <= 0)
                throw new ArgumentException(
                    "PhieuKhachTraId không hợp lệ.",
                    nameof(phieuKhachTraId));

            _phieuKhachTraId = phieuKhachTraId;
            _soPhieuKhachTra = soPhieuKhachTra?.Trim() ?? "";

            BuildUI();

            _txtSoPhieu.Text = _soPhieuKhachTra;

            // Người giao nhập trực tiếp trên Form
            _txtNguoiGiao.Text = Environment.UserName;

            LoadData();
        }

        // ============================================================
        // UI
        // ============================================================

        private void BuildUI()
        {
            Text = "Giao bù hàng NG";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1250, 720);
            MinimumSize = new Size(1000, 600);

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(8)
            };

            main.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 65));

            main.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 70));

            main.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            main.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 42));

            main.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 42));

            // ========================================================
            // HEADER
            // ========================================================

            var pnlHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1
            };

            pnlHeader.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 110));

            pnlHeader.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 40));

            pnlHeader.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 110));

            pnlHeader.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 60));

            var lblPhieu = new LabelControl
            {
                Text = "Số phiếu khách:",
                Dock = DockStyle.Fill,
                Appearance =
            {
                Font = new Font("Tahoma", 9F, FontStyle.Bold)
            }
            };

            _txtSoPhieu = new TextEdit
            {
                Dock = DockStyle.Fill,
                ReadOnly = true
            };

            var lblNguoiGiao = new LabelControl
            {
                Text = "Người giao:",
                Dock = DockStyle.Fill,
                Appearance =
            {
                Font = new Font("Tahoma", 9F, FontStyle.Bold)
            }
            };

            _txtNguoiGiao = new TextEdit
            {
                Dock = DockStyle.Fill
            };

            pnlHeader.Controls.Add(lblPhieu, 0, 0);
            pnlHeader.Controls.Add(_txtSoPhieu, 1, 0);
            pnlHeader.Controls.Add(lblNguoiGiao, 2, 0);
            pnlHeader.Controls.Add(_txtNguoiGiao, 3, 0);

            main.Controls.Add(pnlHeader, 0, 0);

            // ========================================================
            // SCAN
            // ========================================================

            var pnlScan = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1
            };

            pnlScan.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 100));

            pnlScan.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            pnlScan.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 110));

            pnlScan.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 110));

            var lblQr = new LabelControl
            {
                Text = "QR / Barcode:",
                Dock = DockStyle.Fill,
                Appearance =
            {
                Font = new Font("Tahoma", 9F, FontStyle.Bold)
            }
            };

            _txtQr = new TextEdit
            {
                Dock = DockStyle.Fill
            };

            _txtQr.Properties.NullValuePrompt =
                "Quét QR phiếu giao / lot / mã định danh...";

            _txtQr.Properties.Appearance.Font =
                new Font("Tahoma", 11F);

            _txtQr.KeyDown += TxtQr_KeyDown;

            _btnScan = new SimpleButton
            {
                Text = "📷 Giao bù",
                Dock = DockStyle.Fill
            };

            _btnScan.Appearance.Font =
                new Font("Tahoma", 9F, FontStyle.Bold);

            _btnScan.Click += (s, e) => GiaoBuTheoQr();

            _btnReload = new SimpleButton
            {
                Text = "🔄 Làm mới",
                Dock = DockStyle.Fill
            };

            _btnReload.Click += (s, e) => LoadData();

            pnlScan.Controls.Add(lblQr, 0, 0);
            pnlScan.Controls.Add(_txtQr, 1, 0);
            pnlScan.Controls.Add(_btnScan, 2, 0);
            pnlScan.Controls.Add(_btnReload, 3, 0);

            main.Controls.Add(pnlScan, 0, 1);

            // ========================================================
            // GRID
            // ========================================================

            _grid = new GridControl
            {
                Dock = DockStyle.Fill
            };

            _gridView = new GridView(_grid);

            _grid.MainView = _gridView;

            _gridView.OptionsBehavior.Editable = false;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.ShowIndicator = false;
            _gridView.OptionsView.RowAutoHeight = true;

            _gridView.OptionsSelection.MultiSelect = false;

            _gridView.RowStyle += GridView_RowStyle;

            main.Controls.Add(_grid, 0, 2);

            // ========================================================
            // STATUS
            // ========================================================

            var pnlStatus = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1
            };

            pnlStatus.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 25));

            pnlStatus.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 25));

            pnlStatus.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 25));

            pnlStatus.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 25));

            _lblTrangThai = TaoLabelStatus("Trạng thái: Đang tải...");

            _lblTong = TaoLabelStatus("Tổng: 0");

            _lblDaGiao = TaoLabelStatus("Đã giao: 0");

            _lblConLai = TaoLabelStatus("Còn lại: 0");

            pnlStatus.Controls.Add(_lblTrangThai, 0, 0);
            pnlStatus.Controls.Add(_lblTong, 1, 0);
            pnlStatus.Controls.Add(_lblDaGiao, 2, 0);
            pnlStatus.Controls.Add(_lblConLai, 3, 0);

            main.Controls.Add(pnlStatus, 0, 3);

            // ========================================================
            // FOOTER
            // ========================================================

            var pnlFooter = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };

            pnlFooter.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            pnlFooter.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 330));

            _lblHint = new LabelControl
            {
                Text =
                    "💡 Quét QR để ghi nhận giao bù. " +
                    "Chỉ xác nhận hoàn tất sau khi đã giao đủ.",
                Dock = DockStyle.Fill,
                Appearance =
            {
                Font = new Font(
                    "Tahoma",
                    9F,
                    FontStyle.Italic),
                ForeColor = Color.DimGray
            },
                Padding = new Padding(5, 8, 0, 0)
            };

            var pnlButtons = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };

            pnlButtons.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 60));

            pnlButtons.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 40));

            _btnHoanTat = new SimpleButton
            {
                Text = "✅ Xác nhận hoàn tất",
                Dock = DockStyle.Fill
            };

            _btnHoanTat.Appearance.Font =
                new Font("Tahoma", 9F, FontStyle.Bold);

            _btnHoanTat.Click += (s, e) =>
                XacNhanHoanTat();

            _btnDong = new SimpleButton
            {
                Text = "Đóng",
                Dock = DockStyle.Fill
            };

            _btnDong.Click += (s, e) =>
                Close();

            pnlButtons.Controls.Add(_btnHoanTat, 0, 0);
            pnlButtons.Controls.Add(_btnDong, 1, 0);

            pnlFooter.Controls.Add(_lblHint, 0, 0);
            pnlFooter.Controls.Add(pnlButtons, 1, 0);

            main.Controls.Add(pnlFooter, 0, 4);

            Controls.Add(main);
        }

        private LabelControl TaoLabelStatus(string text)
        {
            return new LabelControl
            {
                Text = text,
                Dock = DockStyle.Fill,
                Appearance =
            {
                Font = new Font(
                    "Tahoma",
                    9F,
                    FontStyle.Bold)
            },
                Padding = new Padding(5, 8, 0, 0)
            };
        }

        // ============================================================
        // LOAD DATA
        // ============================================================

        private void LoadData()
        {
            try
            {
                SetBusy(true);

                List<HangChoGiao> rows =
                    _service.GetHangSanSangGiaoBu(
                        _phieuKhachTraId);

                _grid.DataSource = rows;

                ConfigureGrid();

                CapNhatThongKe(rows);

                _lblTrangThai.Text =
                    rows != null && rows.Count > 0
                        ? "Trạng thái: Đang chờ giao bù"
                        : "Trạng thái: Không còn hàng chờ giao";

                _lblTrangThai.Appearance.ForeColor =
                    rows != null && rows.Count > 0
                        ? Color.DarkOrange
                        : Color.DarkGreen;

                _btnHoanTat.Enabled =
                    rows != null && rows.Count > 0;

                if (rows == null || rows.Count == 0)
                {
                    _lblHint.Text =
                        "✅ Không còn hàng chờ giao bù cho phiếu này.";
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Không thể tải danh sách hàng chờ giao bù.\r\n\r\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // GRID
        // ============================================================

        private void ConfigureGrid()
        {
            _gridView.Columns.Clear();

            /*
             * Không phụ thuộc vào các column cứng của repository cũ.
             * PopulateColumns() dựa trực tiếp vào HangChoGiao.
             */

            _gridView.PopulateColumns();

            HideColumn("Id");
            HideColumn("PhieuKhachTraId");
            HideColumn("PhieuGiaoId");

            SetCaption(
                "LotNo",
                "Số lô");

            SetCaption(
                "ItemCode",
                "Mã hàng");

            SetCaption(
                "Quantity",
                "Số lượng");

            SetCaption(
                "DinhDanhPhieuGiao",
                "Định danh phiếu giao");

            SetCaption(
                "PoNo",
                "PO");

            SetCaption(
                "NgayGiao",
                "Ngày giao");

            SetCaption(
                "NhaMay",
                "Nhà máy");

            SetCaption(
                "TrangThai",
                "Trạng thái");

            FormatNumber("Quantity");

            FormatDate("NgayGiao");

            _gridView.BestFitColumns();
        }

        private void HideColumn(string fieldName)
        {
            var col = _gridView.Columns[fieldName];

            if (col != null)
                col.Visible = false;
        }

        private void SetCaption(
            string fieldName,
            string caption)
        {
            var col = _gridView.Columns[fieldName];

            if (col != null)
                col.Caption = caption;
        }

        private void FormatNumber(string fieldName)
        {
            var col = _gridView.Columns[fieldName];

            if (col == null)
                return;

            col.DisplayFormat.FormatType =
                DevExpress.Utils.FormatType.Numeric;

            col.DisplayFormat.FormatString = "n0";
        }

        private void FormatDate(string fieldName)
        {
            var col = _gridView.Columns[fieldName];

            if (col == null)
                return;

            col.DisplayFormat.FormatType =
                DevExpress.Utils.FormatType.DateTime;

            col.DisplayFormat.FormatString =
                "dd/MM/yyyy HH:mm";
        }

        private void GridView_RowStyle(
            object sender,
            RowStyleEventArgs e)
        {
            if (e.RowHandle < 0)
                return;

            var row =
                _gridView.GetRow(e.RowHandle)
                as HangChoGiao;

            if (row == null)
                return;

            /*
             * Không phụ thuộc vào tên trạng thái cụ thể.
             * Nếu HangChoGiao hiện tại của bạn chưa có Status,
             * đoạn này đơn giản không làm gì.
             */
        }

        // ============================================================
        // SCAN QR
        // ============================================================

        private void TxtQr_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;

            GiaoBuTheoQr();
        }

        private void GiaoBuTheoQr()
        {
            if (_dangXuLy)
                return;

            string rawQr =
                _txtQr.Text.Trim();

            if (string.IsNullOrWhiteSpace(rawQr))
            {
                XtraMessageBox.Show(
                    "Vui lòng quét hoặc nhập QR.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _txtQr.Focus();
                return;
            }

            string nguoiGiao =
                _txtNguoiGiao.Text.Trim();

            if (string.IsNullOrWhiteSpace(nguoiGiao))
            {
                XtraMessageBox.Show(
                    "Vui lòng nhập người giao.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _txtNguoiGiao.Focus();
                return;
            }

            try
            {
                SetBusy(true);

                ScanResult result =
                    _service.GiaoBuTheoQR(
                        _phieuKhachTraId,
                        rawQr,
                        nguoiGiao);

                XuLyScanResult(
                    result,
                    "Giao bù");
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Lỗi khi giao bù:\r\n\r\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);

                _txtQr.SelectAll();
                _txtQr.Focus();
            }
        }

        // ============================================================
        // XỬ LÝ SCAN RESULT
        // ============================================================

        private void XuLyScanResult(
            ScanResult result,
            string actionName)
        {
            if (result == null)
            {
                XtraMessageBox.Show(
                    $"{actionName} không trả về kết quả.",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }

            /*
             * Phần này cố tình không truy cập các property chưa được
             * bạn cung cấp của ScanResult.
             *
             * Nếu ScanResult hiện tại của bạn có:
             *
             *     Success
             *     Message
             *
             * thì thay bằng:
             *
             * if (!result.Success)
             * {
             *     ...
             * }
             */

            string message =
                result.ToString();

            XtraMessageBox.Show(
                message,
                actionName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            LoadData();
        }

        // ============================================================
        // XÁC NHẬN HOÀN TẤT
        // ============================================================

        private void XacNhanHoanTat()
        {
            if (_dangXuLy)
                return;

            string nguoiGiao =
                _txtNguoiGiao.Text.Trim();

            if (string.IsNullOrWhiteSpace(nguoiGiao))
            {
                XtraMessageBox.Show(
                    "Vui lòng nhập người giao.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _txtNguoiGiao.Focus();
                return;
            }

            var rows =
                _service.GetHangSanSangGiaoBu(
                    _phieuKhachTraId);

            if (rows != null && rows.Count > 0)
            {
                XtraMessageBox.Show(
                    "Phiếu vẫn còn hàng chưa giao bù.\r\n\r\n" +
                    "Hãy tiếp tục quét QR cho đến khi không còn hàng chờ giao.",
                    "Chưa thể hoàn tất",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                LoadData();
                return;
            }

            var confirm =
                XtraMessageBox.Show(
                    "Danh sách hàng chờ giao bù hiện đã hết.\r\n\r\n" +
                    "Bạn có chắc chắn muốn xác nhận hoàn tất giao bù?",
                    "Xác nhận hoàn tất",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                SetBusy(true);

                ScanResult result =
                    _service.XacNhanHoanTatGiaoBu(
                        _phieuKhachTraId,
                        nguoiGiao);

                XuLyKetQuaHoanTat(result);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Lỗi xác nhận hoàn tất giao bù:\r\n\r\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // KẾT QUẢ HOÀN TẤT
        // ============================================================

        private void XuLyKetQuaHoanTat(
            ScanResult result)
        {
            if (result == null)
            {
                XtraMessageBox.Show(
                    "Không nhận được kết quả xác nhận hoàn tất.",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }

            XtraMessageBox.Show(
                result.ToString(),
                "Hoàn tất giao bù",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }

        // ============================================================
        // THỐNG KÊ
        // ============================================================

        private void CapNhatThongKe(
            List<HangChoGiao> rows)
        {
            int tong = 0;

            if (rows != null)
            {
                foreach (var row in rows)
                {
                    tong += LaySoLuong(row);
                }
            }

            _lblTong.Text =
                $"Tổng: {tong:n0}";

            /*
             * GetHangSanSangGiaoBu() chỉ trả về phần còn chờ giao.
             *
             * Vì vậy ở đây:
             *
             *     Còn lại = tổng rows
             *
             * Còn "Đã giao" không thể suy ra chính xác nếu service
             * không trả tổng lịch sử giao.
             *
             * Không tự tính sai từ dữ liệu hiện tại.
             */

            _lblConLai.Text =
                $"Còn lại: {tong:n0}";
        }

        private int LaySoLuong(
            HangChoGiao row)
        {
            if (row == null)
                return 0;

            /*
             * Interface hiện tại chỉ cho biết HangChoGiao,
             * nhưng chưa có định nghĩa class trong phần code bạn gửi.
             *
             * Nếu Quantity là int thì đoạn dưới dùng trực tiếp.
             */

            return row.SoLuong;
        }

        // ============================================================
        // BUSY
        // ============================================================

        private void SetBusy(bool busy)
        {
            _dangXuLy = busy;

            if (_btnScan != null)
                _btnScan.Enabled = !busy;

            if (_btnReload != null)
                _btnReload.Enabled = !busy;

            if (_btnHoanTat != null)
                _btnHoanTat.Enabled = !busy;

            if (_txtQr != null)
                _txtQr.Enabled = !busy;

            if (_txtNguoiGiao != null)
                _txtNguoiGiao.Enabled = !busy;

            Cursor =
                busy
                    ? Cursors.WaitCursor
                    : Cursors.Default;
        }
    }
}