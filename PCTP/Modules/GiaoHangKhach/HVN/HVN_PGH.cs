using DevExpress.XtraBars.Docking2010;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.BandedGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraReports.UI;
using PCTP.Applications.Services;
using PCTP.Applications.Services;
using PCTP.ClassSQL;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Infrastructure;
using PCTP.Infrastructure;
using PCTP.Infrastructure.Repositories;
using PCTP.Infrastructure.Repositories;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.GiaoHangKhach.Repositories;
using PCTP.Modules.GiaoHangKhach.SubForm;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Presentation.Presenters;
using PCTP.Presentation.Presenters;
using PCTP.Presentation.Views;
using PCTP.Presentation.Views;
using PCTP.QRCODE_HVN;
using PCTP.QRCODE_HVN.Report;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.QRCODE_HVN.PGH
{
    /// <summary>
    /// Form sau refactor:
    /// - Implement IHVNView
    /// - Chỉ xử lý DevExpress (bind grid, show dialog, radioGroup)
    /// - Không chứa SQL, không chứa logic nghiệp vụ
    /// - Khởi tạo dependency graph trong constructor (hoặc dùng DI container)
    /// </summary>
    public partial class HVN_PGH : XtraForm, IHVNView
    {

        public int SttDangSuaSl => _sttSuaSl;
        // ── Presenter ────────────────────────────────────────────────────────
        private readonly HVN_Presenter _presenter;

        // ── State nội bộ View ────────────────────────────────────────────────
        private DataTable _addressTable = new DataTable();
        private int _hinhThucIn = 1;
        private string _tenpdb = "";
        private string _ggfccpdb = "";
        private CustomerConfig _cfg;
        private readonly string _customerNo;
        // ── Pending QR khi SL không khớp (chờ user xác nhận) ────────────────
        private DocQRCode _pendingSlKhacBiet = null;
        private bool _isLoading = false;
        private bool _isLoaiSP = false;
        public bool IsLoaiSP => _isLoaiSP;
        private Button _btnToggleLoaiPhieu;
        public event EventHandler LoaiPhieuChanged = delegate { };
        // ════════════════════════════════════════════════════════════════════
        // Constructor
        // ════════════════════════════════════════════════════════════════════
        public HVN_PGH(string customerNo = "100001")
        {
            InitializeComponent();

            _cfg = CustomerTableConfig.Get(customerNo);
            // Gỡ event tránh trigger khi form chưa ready
            dateNX.EditValueChanged -= dateNX_EditValueChanged;
            tabPaneHVN.Click -= tabPaneHVN_Click;
            RDO_GXHN.SelectedIndexChanged -= RDO_GXHN_SelectedIndexChanged;
            radioGroup2.SelectedIndexChanged -= radioGroup2_SelectedIndexChanged;
            gridVDOCQRCODE.FocusedRowChanged += gridVDOCQRCODE_FocusedRowChanged;
            gridVSUASL.FocusedRowChanged += gridVSUASL_FocusedRowChanged;
            _presenter = BuildPresenter();
        }
        // ── Trong SetupNhaMayUI hoặc SwitchToDocQRView ───────────────────────────
        public void ShowReportWithGioHeader(DataTable reportData, string gioHeader)
        {
            var report = new rpPhieuGiaoHang();
            report.DataSource = reportData;
            report.SetGioHeader(gioHeader);  // ← đổi header trước khi show
            new ReportPrintTool(report).ShowPreviewDialog();
        }
        public void SetupNhaMayUI(CustomerConfig cfg)
        {
            if (cfg.CoNhieuNhaMay)
            {
                // 100001
                tabVP.PageVisible = true;
                tabHN.PageVisible = true;
                tabPaneHVN.Visible = true;
                radioGroup2.Visible = true;
                RDO_GXHN.Visible = true;
                CheckGX.Visible = false;  // ← ẩn CheckList
                btnUploadMilkrun.Visible = false;
            }
            else if (cfg.CoGear)
            {
                // 100002 YMVN: dùng CheckListBox thay radio
                tabPaneHVN.Visible = false;
                tabVP.PageVisible = false;
                tabHN.PageVisible = false;
                radioGroup2.Visible = false;
                RDO_GXHN.Visible = false;
                CheckGX.Visible = true;   // ← hiện CheckList
                CheckGX.BringToFront();
                btnUploadMilkrun.Visible = true;  // ← nút Upload Milkrun SP
                                                  // ── Thêm button Toggle MP/SP ─────────────────────────────────
                if (_btnToggleLoaiPhieu == null)
                {
                    _btnToggleLoaiPhieu = new Button
                    {
                        Text = "Xem: MP",
                        Width = 100,
                        Height = btnUploadMilkrun.Height,
                        Location = new System.Drawing.Point(
                            btnUploadMilkrun.Right + 8,   // ← cạnh phải btnUploadMilkrun
                            btnUploadMilkrun.Top),
                        BackColor = System.Drawing.Color.SteelBlue,
                        ForeColor = System.Drawing.Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold)
                    };
                    _btnToggleLoaiPhieu.Click += BtnToggleLoaiPhieu_Click;

                    // Thêm vào cùng container với btnUploadMilkrun
                    btnUploadMilkrun.Parent.Controls.Add(_btnToggleLoaiPhieu);
                }
                _btnToggleLoaiPhieu.Visible = true;
            }
            else if (cfg.LoadTheoNgay)
            {
                if (_btnToggleLoaiPhieu != null)
                    _btnToggleLoaiPhieu.Visible = false;
                // 100003
                tabPaneHVN.Visible = false;
                tabVP.PageVisible = false;
                tabHN.PageVisible = false;
                radioGroup2.Visible = false;
                RDO_GXHN.Visible = false;
                CheckGX.Visible = false;
                btnUploadMilkrun.Text = "Upload PO HTN";  // ← đổi text
                btnUploadMilkrun.Visible = true;              // ← dùng lại nút
                if (_btnToggleLoaiPhieu != null)
                    _btnToggleLoaiPhieu.Visible = false;
            }
            else
            {
                // Customer khác: 1 nhà máy, có chọn giờ
                tabVP.PageVisible = true;
                tabHN.PageVisible = false;
                tabPaneHVN.TabAlignment = Alignment.Far;
                tabPaneHVN.Visible = true;
                radioGroup2.Visible = true;
                RDO_GXHN.Visible = false;
                CheckGX.Visible = false;
                btnUploadMilkrun.Visible = false;
                if (_btnToggleLoaiPhieu != null)
                    _btnToggleLoaiPhieu.Visible = false;
            }
        }
        public void SetCheckedGiosYMVN(List<string> checkedGios)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetCheckedGiosYMVN(checkedGios)));
                return;
            }

            CheckGX.ItemCheck -= CheckGX_OnItemCheck;
            try
            {
                var gioSet = new HashSet<string>(
                    checkedGios ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < CheckGX.Items.Count; i++)
                {
                    string item = CheckGX.Items[i].ToString();
                    CheckGX.SetItemChecked(i, gioSet.Contains(item));
                }
            }
            finally
            {
                CheckGX.ItemCheck += CheckGX_OnItemCheck;
            }
        }
        // ── Khởi tạo dependency graph ────────────────────────────────────────
        private HVN_Presenter BuildPresenter()
        {
            var sql = new SQLPROVIDER();
            var bus = new InProcessEventBus();

            var phieuDb = new PhieuSqlExecutor(sql);
            var phieuUow = new UnitOfWork(sql);

            // Dependency bắt buộc của PhieuRepository (trừ kho ảo A0) — dùng chung
            // phieuDb/phieuUow để mọi thao tác trong CapNhapKho tham gia đúng 1 transaction.
            var bulkStockSlotRepo = new BulkStockSlotRepository(phieuDb, phieuUow);
            var historyRepo = new StockHistoryRepository(phieuDb, phieuUow);
            var hangChoGiaoRepo = new HangChoGiaoRepository(phieuDb, phieuUow);

            var phieuRepo = new PhieuRepository(
                phieuDb, phieuUow, _cfg,
                bulkStockSlotRepo, historyRepo, hangChoGiaoRepo);

            // PhieuTmpRepository sở hữu InsertTmpRow — TableOrderRepo uỷ quyền qua đây
            // thay vì tự viết lại SQL INSERT bảng TMP (xem ghi chú trong TableOrderRepo.cs).
            var phieuTmpRepo = new PhieuTmpRepository(phieuDb, phieuUow);
            var tableOrderRepo = new TableOrderRepo(phieuDb, phieuTmpRepo);

            var gioRepo = new GioXuatRepository(sql);
            var qrRepo = new DocQRRepository(sql, _cfg);
            var sqlRepo = new SqlRepository(sql);

            var gioVP = gioRepo.GetDictGioVP();
            var gioHN = gioRepo.GetDictGioHN();
            phieuRepo.EnsureTablesExist();
            var ifsRepo = IFSRepository.Create();
            // ── Tính isMayBanQR trước ────────────────────────────────────────────
            string tenMayBanQR = sql.ExecuteReader(sql.B7R2_FCCdb,
                "SELECT TenMay FROM tbl_QR_MAY_DOCQR WHERE TT = 1");
            bool isMayBanQR = string.Equals(
                Environment.MachineName,
                tenMayBanQR,
                StringComparison.OrdinalIgnoreCase);

            // ── Tính tenBan từ isMayBanQR — đây là nơi duy nhất biết cả hai ─────

            string tenBan = isMayBanQR
                 ? _cfg.TmpTable
                 : _cfg.GetTmpViewTable(Environment.MachineName);
            var phieuSvc = new PhieuService(phieuRepo, ifsRepo, bus, gioRepo, tenBan, _cfg, isMayBanQR, tableOrderRepo);
            var qrSvc = new DocQRService(qrRepo, bus, _cfg);
            var inPhieuSvc = new InPhieuService(ifsRepo, phieuRepo, sqlRepo, gioVP, gioHN, _cfg);



            return new HVN_Presenter(this, phieuSvc, qrSvc, inPhieuSvc,
                                      gioRepo, bus, isMayBanQR, tenBan, _cfg); // ← truyền vào
        }
        private static string SanitizeMachineName(string name)
    => System.Text.RegularExpressions.Regex.Replace(
           name ?? "LOCAL", @"[^A-Za-z0-9_]", "_");
        // ════════════════════════════════════════════════════════════════════
        // I. BIND DỮ LIỆU VÀO GRID
        // ════════════════════════════════════════════════════════════════════
        public void BindDonHang(DataTable dt)
        {
            gridCtrDONHANG.DataSource = dt;
            GridViewDONHANG.BestFitColumns();
        }
        private void BtnToggleLoaiPhieu_Click(object sender, EventArgs e)
        {
            // Toggle trạng thái
            _isLoaiSP = !_isLoaiSP;

            // Cập nhật text button
            _btnToggleLoaiPhieu.Text = _isLoaiSP ? "Xem: SP" : "Xem: MP";
            _btnToggleLoaiPhieu.BackColor = _isLoaiSP
                ? System.Drawing.Color.OrangeRed
                : System.Drawing.Color.SteelBlue;

            // Bắn event → Presenter sẽ lắng nghe và reload
            LoaiPhieuChanged.Invoke(this, EventArgs.Empty);
        }
        public void BindHangThieu(DataTable dt) => GCT_HT.DataSource = dt;
        public void BindDocQRCode(DataTable dt)
        {
            gridCtrDOCQrCODE.DataSource = dt;
            gridVDOCQRCODE.RefreshData();           // force grid refresh sau khi đổi DataSource
            gridVDOCQRCODE.MoveLastVisible();       // scroll xuống dòng mới nhất vừa scan
        }

        public void BindGhepLot(DataTable dt) => gridCTTGL.DataSource = dt;

        public void SetGridCaption(string caption) => gridBandDH.Caption = caption;

        public void RefreshLotRow(int stt, string lot)
        {
            for (int i = 0; i < GridViewDONHANG.RowCount; i++)
            {
                string sttStr = GridViewDONHANG.GetRowCellDisplayText(i, "STT").Trim();
                if (!int.TryParse(sttStr, out int rowStt) || rowStt != stt) continue;

                GridViewDONHANG.SetRowCellValue(i, "LOT", lot);

                // ── THÊM: refresh dòng ngay để màu green hiện lên ──
                GridViewDONHANG.RefreshRow(i);
                return;
            }
        }

        //public void RefreshDocQR() => gridCtrDOCQrCODE.RefreshDataSource();

        // ════════════════════════════════════════════════════════════════════
        // II. TRẠNG THÁI / CHUYỂN VIEW
        // ════════════════════════════════════════════════════════════════════

        private System.Threading.Timer _loadingTimeout;

        public void ShowLoading(bool show, string caption = "Đang xử lý...")
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowLoading(show, caption)));
                return;
            }

            if (show)
            {
                // ── Reset timeout nếu đang loading — tránh đóng sớm ────────────
                _loadingTimeout?.Dispose();
                _loadingTimeout = null;

                if (!_isLoading)
                {
                    _isLoading = true;
                    splashScreenManager1.ShowWaitForm();
                }

                // ── FIX 1: set caption vào WaitForm sau khi show ────────────────
                try
                {
                    splashScreenManager1.SetWaitFormCaption(caption);
                }
                catch { }

                // ── FIX 2: reset timeout mỗi lần gọi ShowLoading(true) ──────────
                _loadingTimeout = new System.Threading.Timer(_ =>
                {
                    try
                    {
                        this.Invoke(new Action(() =>
                        {
                            System.Diagnostics.Debug.WriteLine(
                                "[ShowLoading] Auto-close sau 30s — có thể bị stuck");
                            ShowLoading(false);
                        }));
                    }
                    catch { }
                }, null, 30000, System.Threading.Timeout.Infinite);
            }
            else
            {
                // ── Tắt timeout trước ───────────────────────────────────────────
                _loadingTimeout?.Dispose();
                _loadingTimeout = null;

                if (!_isLoading) return;

                _isLoading = false;
                splashScreenManager1.CloseWaitForm();
            }
        }

        public void ShowError(string msg) =>
            XtraMessageBox.Show(msg, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);

        public void ShowInfo(string msg) =>
            XtraMessageBox.Show(msg, "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);

        public bool Confirm(string msg) =>
            XtraMessageBox.Show(msg, "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            == DialogResult.Yes;

        public void ShowReport(DataTable reportData)
        {
            var report = new rpPhieuGiaoHang();
            report.DataSource = reportData;
            new ReportPrintTool(report).ShowPreviewDialog();
        }

        // ── Chuyển sang màn hình đọc QR ─────────────────────────────────────
        public void SwitchToDocQRView()
        {

            tabPaneHVN.Click -= tabPaneHVN_Click;

            UIButtonHOME.Visible = true;
            panelPhieu.Visible = false;
            gridCtrDOCQrCODE.BringToFront();
            PN_DOCQR_SUASL1.BringToFront();
            // Đảm bảo cập nhật trên UI Thread
            this.Invoke(new Action(() =>
            {
                lblDocQrcode.Text = _cfg?.LabelDocQR ?? "Đọc QRCode theo thứ tự: FCC → HVN";
            }));
            // ── Ban đầu ẩn SUASL, chờ user click dòng QR ────────────────────────
            gridCtrSUASL.Visible = false;
            gridCTTGL.Visible = false;  // cả 2 đều ẩn khi vào màn hình QR

            UIButton.AllowGlyphSkinning = false;
            UIButton.Buttons.Clear();
            UIButton.Buttons.AddRange(new WindowsUIButton[]
            {
        new WindowsUIButton { Caption = "Xóa Dòng Được Chọn", Style = ButtonStyle.PushButton, ImageUri = "Delete;Size16x16;Colored" },
        new WindowsUIButton { Caption = "Xóa Toàn Bộ Dữ Liệu", Style = ButtonStyle.PushButton, ImageUri = "clear;Size16x16;Colored"  },
        new WindowsUIButton { Caption = "Hoàn Thành",           Style = ButtonStyle.PushButton, ImageUri = "apply;Size16x16;Colored"  }
            });
            UIButton.Buttons.Insert(2, new WindowsUISeparator());

            txt_DOCQRCODE.Focus();
            tabPaneHVN.Click += tabPaneHVN_Click;
        }

        // ── Chuyển về màn hình phiếu thường ─────────────────────────────────
        public void SwitchToPhieuView()
        {
            tabPaneHVN.Click -= tabPaneHVN_Click;

            UIButtonHOME.Visible = false;
            panelPhieu.Visible = true;
            gridCtrDONHANG.BringToFront();
            GCT_HT.BringToFront();

            // ── Phục hồi: ẩn SUASL, hiện GHEPLOT ────────────────────────────────
            gridCtrSUASL.Visible = false;
            gridCTTGL.Visible = true;
            gridCTTGL.BringToFront();

            // Reset textbox sửa SL
            TXT_FCCTU.Text = "";
            TXT_FCCTHANH.Text = "";
            TXT_HVNTU.Text = "";
            TXT_HVNTHANH.Text = "";
            _sttSuaSl = 0;

            UIButton.AllowGlyphSkinning = false;
            UIButton.Buttons.Clear();
            var b1 = new WindowsUIButton { Caption = "Kiểm Tra Ghep Lot", Style = ButtonStyle.PushButton, Image = imageBT.Images[1] };
            var b2 = new WindowsUIButton { Caption = "In Phiếu", Style = ButtonStyle.PushButton, ImageUri = "Print;Size16x16;Colored" };
            var b3 = new WindowsUIButton { Caption = "DOC QRCODE", Style = ButtonStyle.PushButton, ImageUri = "IndentIncrease;Size16x16;Colored" };
            UIButton.Buttons.AddRange(new WindowsUIButton[] { b3, b1, b2 });
            UIButton.Buttons.Insert(1, new WindowsUISeparator());

            tabPaneHVN.Click += tabPaneHVN_Click;
        }
        // ── Bind radio từ DB — gọi TRƯỚC khi gắn event ──────────────────────────
        public void BindGioXuatVP(IReadOnlyList<GioXuat> danhSach)
        {
            radioGroup2.Properties.Items.Clear();
            for (int i = 0; i < danhSach.Count; i++)
            {
                var gio = danhSach[i];
                var item = new RadioGroupItem(i, gio.MoTa, true, null, gio.Ma);
                radioGroup2.Properties.Items.Add(item);
            }
            if (radioGroup2.Properties.Items.Count > 0)
                radioGroup2.EditValue = 0;
        }

        public void BindGioXuatHN(IReadOnlyList<GioXuat> danhSach)
        {
            RDO_GXHN.Properties.Items.Clear();
            for (int i = 0; i < danhSach.Count; i++)
            {
                var gio = danhSach[i];
                var item = new RadioGroupItem(i, gio.MoTa, true, null, gio.Ma);
                RDO_GXHN.Properties.Items.Add(item);
            }
            if (RDO_GXHN.Properties.Items.Count > 0)
                RDO_GXHN.EditValue = 0;
        }
        // ── Chuyển về màn hình phiếu GIAO DB ────────────────────────────────
        public void SwitchToPhieuDBView()
        {
            UIButton.AllowGlyphSkinning = false;
            UIButton.Buttons.Clear();
            UIButton.Buttons.AddRange(new WindowsUIButton[]
            {
        new WindowsUIButton { Caption = "Upload Đơn Hàng",  // ✅ thêm
            Style = ButtonStyle.PushButton,
            ImageUri = "Import;Size16x16;Colored" },
        new WindowsUIButton { Caption = "DOC QRCODE",
            Style = ButtonStyle.PushButton,
            ImageUri = "IndentIncrease;Size16x16;Colored" },
        new WindowsUIButton { Caption = "Thêm",
            Style = ButtonStyle.PushButton,
            ImageUri = "new;Size16x16;Colored" },
        new WindowsUIButton { Caption = "Xóa",
            Style = ButtonStyle.PushButton,
            ImageUri = "Delete;Size16x16;Colored" },
        new WindowsUIButton { Caption = "Lưu",
            Style = ButtonStyle.PushButton,
            ImageUri = "Save;Size16x16;Colored" },
        new WindowsUIButton { Caption = "In Phiếu",
            Style = ButtonStyle.PushButton,
            ImageUri = "Print;Size16x16;Colored" }
            });
        }

        // ── Cấu hình nút phiếu thường (có thể ẩn/hiện CNK và KiemTraMaNG) ──


        // HVN_PGH
        public void SetupPhieuButtons(bool showCapNhapKho, bool showKiemTraMaNG,
                                       bool showGhepLot, bool showDocQRCode,
                                       bool showLayLaiLot = false,
                                       bool showStop = false)
        {
            UIButton.AllowGlyphSkinning = false;
            UIButton.Buttons.Clear();

            // Nút luôn có trên tất cả máy
            UIButton.Buttons.Add(new WindowsUIButton
            {
                Caption = "In Phiếu",
                Style = ButtonStyle.PushButton,
                ImageUri = "Print;Size16x16;Colored"
            });
            UIButton.Buttons.Add(new WindowsUIButton
            {
                Caption = "In Ghép Lot",
                Style = ButtonStyle.PushButton,
                ImageUri = "Print;Size16x16;Colored"
            });
            UIButton.Buttons.Add(new WindowsUIButton
            {
                Caption = "In Tách Lot",
                Style = ButtonStyle.PushButton,
                ImageUri = "Print;Size16x16;Colored"
            });

            UIButton.Buttons.Insert(0, new WindowsUISeparator());

            // Nút chỉ máy bắn QR
            if (showDocQRCode)
                UIButton.Buttons.Insert(0, new WindowsUIButton
                {
                    Caption = "DOC QRCODE",
                    Style = ButtonStyle.PushButton,
                    ImageUri = "IndentIncrease;Size16x16;Colored"
                });
            if (showLayLaiLot)
                UIButton.Buttons.Insert(0, new WindowsUIButton
                {
                    Caption = "Lấy Lại Lot",
                    Style = ButtonStyle.PushButton,
                    ImageUri = "IndentIncrease;Size16x16;Colored"
                });
            if (showGhepLot)
                UIButton.Buttons.Add(new WindowsUIButton
                {
                    Caption = "Kiểm Tra Ghep Lot",
                    Style = ButtonStyle.PushButton,
                    Image = imageBT.Images[1]
                });

            if (showCapNhapKho)
                UIButton.Buttons.Add(new WindowsUIButton
                {
                    Caption = "Cập Nhập Kho",
                    Style = ButtonStyle.PushButton,
                    ImageUri = "Save;Size16x16;Colored"
                });

            if (showKiemTraMaNG)
                UIButton.Buttons.Add(new WindowsUIButton
                {
                    Caption = "Kiểm tra mã NG",
                    Style = ButtonStyle.PushButton,
                    ImageUri = "SpellCheckAsYouType;Size16x16;Colored"
                });

            if (showStop)
            {
                UIButton.Buttons.Add(new WindowsUISeparator());
                UIButton.Buttons.Add(new WindowsUIButton
                {
                    Caption = "Ghi Chú STOP",
                    Style = ButtonStyle.PushButton,
                    ImageUri = "Warning;Size16x16;Colored"
                });
                UIButton.Buttons.Add(new WindowsUIButton
                {
                    Caption = "Xóa Ghi Chú STOP",
                    Style = ButtonStyle.PushButton,
                    ImageUri = "Clear;Size16x16;Colored"
                });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // III. ĐỌC GIÁ TRỊ TỪ UI
        // ════════════════════════════════════════════════════════════════════
        public DateTime SelectedDate => dateNX.DateTime;
        //public int SelectedTabAddNM => tabPaneHVN.SelectedPage == tabHN ? 2 : 1;
        public int SelectedTabAddNM =>
        _cfg.CoNhieuNhaMay
        ? (tabPaneHVN.SelectedPage == tabHN ? 2 : 1)
        : _cfg.AddNmMacDinh;  // ← cố định cho 10003
        public string QRCodeInput => txt_DOCQRCODE.Text.Trim();
        public void ClearQRInput() => txt_DOCQRCODE.Text = "";
        public int SelectedHinhThucIn => _hinhThucIn;

        // ════════════════════════════════════════════════════════════════════
        // IV. ĐỌC DATA TỪ GRID
        // ════════════════════════════════════════════════════════════════════
        public DataTable GetDonHangTable() => gridCtrDONHANG.DataSource as DataTable;
        public DataTable GetAddressTable() => _addressTable;
        // Form gốc INGHEPLOT(): GridVTTGL.GetSelectedRows() → row[0]=MA, row[1]=GIO, row[2]=LOT
        public IEnumerable<GhepLotItem> GetSelectedGhepLotRows()
        {
            var list = new List<GhepLotItem>();
            foreach (int i in GridVTTGL.GetSelectedRows())
            {
                DataRow row = GridVTTGL.GetDataRow(i);
                if (row == null) continue;
                list.Add(new GhepLotItem
                {
                    MaHang = row[0].ToString(),
                    GioXuat = int.TryParse(row[1].ToString(), out int gio) ? gio : 0,
                    Lot = row[2].ToString()
                });
            }
            return list;
        }

        public int GetFocusedDocQRStt()
        {
            string s = gridVDOCQRCODE.GetFocusedRowCellDisplayText("STT");
            return int.TryParse(s, out int v) ? v : -1;
        }

        public (string LotFcc, int SlFcc, int SlHvn) GetFocusedDocQRTemInfo()
        {
            string lot = gridVDOCQRCODE.GetFocusedRowCellDisplayText("LOTFCC");
            int.TryParse(gridVDOCQRCODE.GetFocusedRowCellDisplayText("SLTEMFCC"), out int slFcc);
            int.TryParse(gridVDOCQRCODE.GetFocusedRowCellDisplayText("SLTEMHVN"), out int slHvn);
            return (lot, slFcc, slHvn);
        }

        public void DeleteFocusedDocQRRow() => gridVDOCQRCODE.DeleteSelectedRows();

        public void ClearDocQRRows() => gridCtrDOCQrCODE.DataSource = null;

        public string GetFocusedDonHangMaHang() =>
            GridViewDONHANG.GetFocusedRowCellDisplayText("MAHANG").Trim();

        // ════════════════════════════════════════════════════════════════════
        // V. KIỂM TRA TRẠNG THÁI GRID
        // ════════════════════════════════════════════════════════════════════
        public bool CoLotDeLuuKho()
        {
            for (int i = 0; i < GridViewDONHANG.RowCount; i++)
                if (GridViewDONHANG.GetRowCellDisplayText(i, "LOT").Trim() != "")
                    return true;
            return false;
        }
        // Form gốc PBD_ThemMoi(): AddNewRow + RepositoryItemLookUpEdit + RepositoryItemComboBox
        public void ThemDongGiaoDB(DataTable danhSachMaHang)
        {
            GridViewDONHANG.AddNewRow();

            // Bind LookUp mã hàng
            var riLookup = new RepositoryItemLookUpEdit();
            riLookup.DataSource = danhSachMaHang;
            riLookup.ValueMember = "Code";
            riLookup.DisplayMember = "Code";
            riLookup.BestFitMode = BestFitMode.BestFitResizePopup;
            riLookup.SearchMode = SearchMode.AutoSuggest;
            gridCtrDONHANG.RepositoryItems.Add(riLookup);
            GridViewDONHANG.Columns["MAHANG"].ColumnEdit = riLookup;
            GridViewDONHANG.BestFitColumns();

            // Bind ComboBox giờ giao 00-24
            var riCombo = new RepositoryItemComboBox();
            for (int i = 0; i <= 24; i++)
                riCombo.Items.Add(i.ToString("00"));
            gridCtrDONHANG.RepositoryItems.Add(riCombo);
            GridViewDONHANG.Columns["GIOGIAO"].ColumnEdit = riCombo;
        }
        public bool CoHangChuaOK()
        {
            if (GridViewDONHANG.RowCount == 0) return false;
            for (int i = 0; i < GridViewDONHANG.RowCount; i++)
                if (GridViewDONHANG.GetRowCellDisplayText(i, "STATUS").Trim() != "OK")
                    return true;
            return false;
        }

        // ════════════════════════════════════════════════════════════════════
        // VI. DIALOG PHỨC TẠP
        // ════════════════════════════════════════════════════════════════════


        // View load phiếu GIAO DB đã lưu để hiển thị lại
        private void LoadDBOKView()
        {
            // Gọi về Presenter nếu cần tải lại dữ liệu — hiện delegate lên event
            // Presenter lắng nghe FormLoaded / DateChanged sẽ tự reload
        }

        public int ShowChonSttTrungMa(ListView danhSachTrung)
        {
            // FRM_LISTRUNGMSL nhận ListView trực tiếp — không cần static field trung gian
            var frm = new FRM_LISTRUNGMSL(danhSachTrung);
            frm.ShowDialog();

            // FRM_LISTRUNGMSL.STTPHIEU là static string set khi user chọn dòng
            if (string.IsNullOrWhiteSpace(FRM_LISTRUNGMSL.STTPHIEU))
                return -1;   // user đóng dialog mà không chọn → Presenter sẽ bỏ qua dòng này

            return int.TryParse(FRM_LISTRUNGMSL.STTPHIEU, out int stt) ? stt : -1;
        }

        public void ShowKiemTraMaNG(string maHang)
        {
            var frm = new FRM_SUALOTHVN(maHang);
            frm.ShowDialog();
        }

        public void ShowTachLot()
        {
            var frm = new UF_TACHLOT();
            frm.Show();
        }

        public void ShowLoiCapNhapKho(DataTable errors)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowLoiCapNhapKho(errors)));
                return;
            }

            // 1. Khởi tạo Form lỗi (thay FormDanhSachLoi bằng tên class thật của bạn)
            var frmLoi = new frm_err_cnk(errors);

            // 2. Thiết lập vị trí hiển thị ở giữa Form HVN_PGH
            frmLoi.StartPosition = FormStartPosition.CenterParent;

            // 3. QUAN TRỌNG: Khóa cứng Form HVN_PGH lại, không cho người dùng click hay bấm nút nữa
            this.Enabled = false;

            // 4. Bắt sự kiện khi Form lỗi bị đóng (người dùng bấm X hoặc nút Đóng trên form lỗi)
            frmLoi.FormClosed += (senderForm, args) =>
            {
                // Mở khóa lại Form HVN_PGH để tiếp tục làm việc
                this.Enabled = true;
                this.Activate(); // Đưa Form HVN_PGH lên phía trước

                // Giải phóng bộ nhớ của Form lỗi sau khi dùng xong
                frmLoi.Dispose();
            };

            // 5. Hiển thị Form lỗi dưới dạng Modeless (Show thông thường) và gán Owner là Form này
            // Gán Owner giúp Form lỗi luôn luôn nằm ĐÈ lên trên Form HVN_PGH, không bị chìm xuống dưới
            frmLoi.Show(this);
        }

        public int? ShowSuaSoLuongTem(int sttBan, string lotFcc, int slFcc, int slHvn)
        {
            _sttSuaSl = sttBan;   // ← lưu STT, Presenter sẽ đọc qua SttDangSuaSl
            LOTFCCVN.Text = lotFcc;
            TXT_FCCTU.Text = slFcc.ToString();
            TXT_HVNTU.Text = slHvn.ToString();
            TXT_HVNTHANH.Text = "";     // ← xóa giá trị cũ mỗi lần mở mới
            return null;
        }

        // Presenter gọi hàm này sau khi SuaSoLuongTemClicked để lấy giá trị user đã nhập
        private int _sttSuaSl = 0;


        public int ShowChonHinhThucIn()
        {
            using (var frm = new FRM_HTIN())
            {
                frm.ShowDialog();
                _hinhThucIn = frm.HinhThucIn;
                return _hinhThucIn;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // VII. EVENTS — khai báo
        // ════════════════════════════════════════════════════════════════════
        public event EventHandler FormLoaded = delegate { };
        public event EventHandler DateChanged = delegate { };
        public event EventHandler GioXuatChanged = delegate { };
        public event EventHandler GioXuatCheckedChanged = delegate { };
        public event EventHandler CheckGX_ItemCheck = delegate { };
        public event EventHandler TabChanged = delegate { };
        public event EventHandler CapNhapKhoClicked = delegate { };
        public event EventHandler InPhieuClicked = delegate { };
        public event EventHandler InGhepLotClicked = delegate { };
        public event EventHandler InTachLotClicked = delegate { };
        public event EventHandler DocQRCodeClicked = delegate { };
        public event EventHandler KiemTraGhepLotClicked = delegate { };
        public event EventHandler KiemTraMaNGClicked = delegate { };
        public event EventHandler<string> QRCodeSubmitted = delegate { };
        public event EventHandler HoanThanhClicked = delegate { };
        public event EventHandler XoaDongQRClicked = delegate { };
        public event EventHandler XoaToanBoQRClicked = delegate { };
        public event EventHandler SuaSoLuongTemClicked = delegate { };
        public event EventHandler<LayLaiLotEventArgs> LayLaiLotNoClicked = delegate { };
        public event EventHandler UploadGiaoDBClicked = delegate { };
        public event EventHandler LuuGiaoDBClicked = delegate { };
        public event EventHandler<TTPHIEUEventArgs> CapNhapTTPHIEUClicked
        = delegate { };
        public event EventHandler<ChonLotThuCongEventArgs> ChonLotThuCongClicked
        = delegate { };
        public event EventHandler HoanThanhYMVNClicked = delegate { };
        public event EventHandler UploadMilkrunSPClicked = delegate { };

        // ════════════════════════════════════════════════════════════════════
        // Form Load
        // ════════════════════════════════════════════════════════════════════
        private void HVN_PGH_Load(object sender, EventArgs e)
        {
            // ── 1. Setup UI theo config customer ─────────────────────
            SetupNhaMayUI(_cfg);
            // ── 2. Cập nhật tiêu đề form ─────────────────────────────
            this.Text = $"Phiếu Giao Hàng — {_cfg.DisplayName}";
            // ── 3. Load địa chỉ theo customer ────────────────────────
            _addressTable = IFSRepository.Create()
                                .GetCustomerAddress(_cfg.CustomerNo)
                            ?? new DataTable();
            // ── 4. Bind radio giờ xuất ───────────────────────────────
            var gioRepo = new GioXuatRepository(new SQLPROVIDER());
            BindGioXuatVP(gioRepo.GetDanhSachGioVP());
            if (_cfg.CoNhieuNhaMay)
                BindGioXuatHN(gioRepo.GetDanhSachGioHN());

            // ── 4b. Setup grid cột theo customer ─────────────────────
            SetupGridDonHangYMVN(_cfg.LoadTuBangRieng);

            // ── 5. Gắn events ────────────────────────────────────────
            GridViewDONHANG.ShowingEditor += GridViewDONHANG_ShowingEditor_LOT;
            dateNX.EditValueChanged += dateNX_EditValueChanged;

            if (_cfg.CoNhieuNhaMay)
                tabPaneHVN.Click += tabPaneHVN_Click;

            RDO_GXHN.SelectedIndexChanged += RDO_GXHN_SelectedIndexChanged;
            radioGroup2.SelectedIndexChanged += radioGroup2_SelectedIndexChanged;

            if (_cfg.CoGear)
            {
                CheckGX.ItemCheck += CheckGX_OnItemCheck;
                btnUploadMilkrun.Click += btnUploadMilkrun_Click;
            }
            else if (_cfg.LoadTheoNgay)
            {
                btnUploadMilkrun.Click += btnUploadMilkrun_Click;
            }

            if (dateNX.DateTime == DateTime.MinValue || dateNX.DateTime.Year < 2000)
                dateNX.DateTime = DateTime.Now;

            FormLoaded.Invoke(this, EventArgs.Empty);
        }

        // ── Setup cột grid theo customer ─────────────────────────────────────────
        public void SetupGridDonHangYMVN(bool bangrieng)
        {
            // ← Cột GEAR đã có sẵn trong Designer, chỉ cần ẩn/hiện
            SetColumnVisible(GridViewDONHANG, "GEAR", bangrieng);
            SetColumnVisible(GridViewDONHANG, "PO_NO", bangrieng);

            if (bangrieng)
            {
                SetColumnCaption(GridViewDONHANG, "GEAR", "Gear Sử Dụng");
                SetColumnCaption(GridViewDONHANG, "CUA", "Cửa");
                SetColumnCaption(GridViewDONHANG, "TRUYEN", "Truyền");
                SetColumnCaption(GridViewDONHANG, "GIOGIAO", "Giờ");
                SetColumnCaption(GridViewDONHANG, "PO_NO", "Số PO");
            }
        }

        private void SetColumnVisible(BandedGridView view,
            string fieldName, bool visible)
        {

            var col = view.Columns.ColumnByFieldName(fieldName);
            if (col != null) col.Visible = visible;
        }

        private void SetColumnCaption(BandedGridView view,
            string fieldName, string caption)
        {
            var col = view.Columns.ColumnByFieldName(fieldName);
            if (col != null) col.Caption = caption;
        }
        // Implement BindGioXuatCheckList
        public void BindGioXuatCheckList(List<string> danhSachGio)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => BindGioXuatCheckList(danhSachGio)));
                return;
            }

            CheckGX.ItemCheck -= CheckGX_OnItemCheck;
            CheckGX.Items.Clear();
            foreach (var gio in danhSachGio)
                CheckGX.Items.Add(gio, true);
            CheckGX.ItemCheck += CheckGX_OnItemCheck;

            // DEBUG — xem CheckGX đang ở đâu
            System.Diagnostics.Debug.WriteLine(
                $"[CheckGX] Visible={CheckGX.Visible}" +
                $" | Location={CheckGX.Location}" +
                $" | Size={CheckGX.Size}" +
                $" | Parent={CheckGX.Parent?.Name}" +
                $" | ParentVisible={CheckGX.Parent?.Visible}" +
                $" | Items={CheckGX.Items.Count}" +
                $" | BoundsInForm={CheckGX.RectangleToScreen(CheckGX.ClientRectangle)}");
        }
        // Handler double-click vào cột LOT
        public void LockCheckListYMVN()
        {
            CheckGX.ItemCheck -= CheckGX_OnItemCheck; // ← tháo event tránh trigger
            CheckGX.Enabled = false;
        }

        public void UnlockCheckListYMVN()
        {
            CheckGX.Enabled = true;
            CheckGX.ItemCheck += CheckGX_OnItemCheck; // ← gắn lại event
        }
        private void GridViewDONHANG_ShowingEditor_LOT(object sender, CancelEventArgs e)
        {
            // Chặn edit trực tiếp cột LOT — chỉ cho nhập qua dialog
            if (GridViewDONHANG.FocusedColumn?.FieldName == "LOT")
            {
                e.Cancel = true;  // hủy edit mode

                // Lấy data và bắn event mở dialog
                int stt = GetFocusedDonHangStt();
                if (stt < 0) return;

                string status = GridViewDONHANG
                    .GetFocusedRowCellDisplayText("STATUS").Trim();
                if (status == "OK")
                {
                    ShowInfo("Dòng này đã được Cập Nhập Kho!");
                    return;
                }

                string maHang = GridViewDONHANG
                    .GetFocusedRowCellDisplayText("MAHANG").Trim();
                int.TryParse(
                    GridViewDONHANG.GetFocusedRowCellDisplayText("SOLUONG"),
                    out int soLuong);

                ChonLotThuCongClicked.Invoke(this,
                    new ChonLotThuCongEventArgs(stt, maHang, soLuong));
            }
        }
        public ChonLotResult ShowChonLotTuKho(int stt, string maHang,
    int soLuong, DataTable danhSachLot)
        {
            using (var frm = new FRM_CHON_LOT_KHO(maHang, soLuong, danhSachLot))
            {
                if (frm.ShowDialog() == DialogResult.OK)
                    return new ChonLotResult
                    {
                        LotGhep = frm.LotGhep,
                        Confirmed = true
                    };

                return new ChonLotResult { Confirmed = false };
            }
        }
        public List<string> GetCheckedGioXuat()
        {
            var list = new List<string>();
            foreach (object item in CheckGX.CheckedItems)
                list.Add(item.ToString());
            return list;
        }

        public void BindGhepLotYMVN(DataTable dt) => gridCTTGL.DataSource = dt;

        public void ShowReportYMVN(DataTable reportData)
        {
            var report = new rpPhieuGiaoHangYAM();
            report.DataSource = reportData;
            new ReportPrintTool(report).ShowPreviewDialog();
        }
        public void SetupYMVNButtons()
        {
            UIButton.AllowGlyphSkinning = false;
            UIButton.Buttons.Clear();

            UIButton.Buttons.Add(new WindowsUIButton
            {
                Caption = "In Phiếu",
                Style = ButtonStyle.PushButton,
                ImageUri = "Print;Size16x16;Colored"
            });

            UIButton.Buttons.Add(new WindowsUIButton
            {
                Caption = _isLoaiSP ? "Đang xem: SP" : "Đang xem: MP",
                Style = ButtonStyle.PushButton,
                ImageUri = "Refresh;Size16x16;Colored"
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // DevExpress event handlers → bắn interface events
        // ════════════════════════════════════════════════════════════════════
        private void dateNX_EditValueChanged(object sender, EventArgs e)
            => DateChanged.Invoke(this, EventArgs.Empty);

        private void tabPaneHVN_Click(object sender, EventArgs e)
            => TabChanged.Invoke(this, EventArgs.Empty);

        private void RDO_GXHN_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateGioXuatFromRadio();
            GioXuatChanged.Invoke(this, EventArgs.Empty);
        }

        private void radioGroup2_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateGioXuatFromRadio();
            GioXuatChanged.Invoke(this, EventArgs.Empty);
        }

        private void UpdateGioXuatFromRadio()
        {
            string ma, moTa;

            // ── 10003: không có tab, chỉ dùng radio VP ───────────────────────
            if (!_cfg.CoNhieuNhaMay)
            {
                // Luôn đọc từ radioGroup2 bất kể tab nào
                int idx = radioGroup2.SelectedIndex;
                if (idx < 0 || idx >= radioGroup2.Properties.Items.Count) return;
                var item = radioGroup2.Properties.Items[idx];
                ma = item.AccessibleName ?? "'06'";
                moTa = item.Description ?? "(6H)";
                _presenter.UpdateGioXuat(new GioXuat(ma, moTa));
                return;
            }

            // ── 100001: đọc theo tab đang chọn ───────────────────────────────
            if (tabPaneHVN.SelectedPage == tabHN)
            {
                int idx = RDO_GXHN.SelectedIndex;
                if (idx < 0 || idx >= RDO_GXHN.Properties.Items.Count) return;
                var item = RDO_GXHN.Properties.Items[idx];
                ma = item.AccessibleName ?? "'06'";
                moTa = item.Description ?? "(6H)";
            }
            else
            {
                int idx = radioGroup2.SelectedIndex;
                if (idx < 0 || idx >= radioGroup2.Properties.Items.Count) return;
                var item = radioGroup2.Properties.Items[idx];
                ma = item.AccessibleName ?? "'06'";
                moTa = item.Description ?? "(6H)";
            }
            _presenter.UpdateGioXuat(new GioXuat(ma, moTa));
        }

        // ── GIAO DB guard — chặn RadioGroup trước khi đổi sang "#" ──────────
        public void XoaDongGiaoDB() => GridViewDONHANG.DeleteSelectedRows();
        private void radioGroup2_EditValueChanging(object sender, ChangingEventArgs e)
        {
            if ((int)e.NewValue == 8)   // index của "(GIAO DB)" trong VP
                e.Cancel = !_presenter.OnGiaoDBChanging(_presenter.AddNM);
        }

        private void RDO_GXHN_EditValueChanging(object sender, ChangingEventArgs e)
        {
            if ((int)e.NewValue == 10)  // index của "(GIAO DB)" trong HN
                e.Cancel = !_presenter.OnGiaoDBChanging(_presenter.AddNM);
        }

        // ── QR Code input ────────────────────────────────────────────────────
        private void txt_DOCQRCODE_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
                QRCodeSubmitted.Invoke(this, txt_DOCQRCODE.Text);
        }
        // ════════════════════════════════════════════════════════════════════════════
        // FIX 4: HVN_PGH — implement các method mới
        // ════════════════════════════════════════════════════════════════════════════

        public void SetDate(DateTime date)
        {
            // Gỡ event tránh trigger DateChanged khi set programmatically
            dateNX.EditValueChanged -= dateNX_EditValueChanged;
            dateNX.DateTime = date;
            dateNX.EditValueChanged += dateNX_EditValueChanged;
        }
        public void SuspendGioXuatChanged()
        {
            radioGroup2.SelectedIndexChanged -= radioGroup2_SelectedIndexChanged;
            RDO_GXHN.SelectedIndexChanged -= RDO_GXHN_SelectedIndexChanged;
        }

        public void ResumeGioXuatChanged()
        {
            radioGroup2.SelectedIndexChanged += radioGroup2_SelectedIndexChanged;
            RDO_GXHN.SelectedIndexChanged += RDO_GXHN_SelectedIndexChanged;
        }
        public void SetTab(int addNM)
        {
            tabPaneHVN.Click -= tabPaneHVN_Click;
            if (addNM == 2)
            {
                tabPaneHVN.SelectedPage = tabHN;
                tabVP.PageVisible = false;          // ẩn tab còn lại
            }
            else
            {
                tabPaneHVN.SelectedPage = tabVP;
                tabHN.PageVisible = false;
            }
            tabPaneHVN.Click += tabPaneHVN_Click;
        }
        public void LockDatePicker() => dateNX.Enabled = false;
        public void UnlockDatePicker() => dateNX.Enabled = true;

        public void LockRadioExcept(string gioFCC)
        {
            var gioSet = new HashSet<string>(
                gioFCC.Split(',').Select(g => g.Trim().Trim('\'')),
                StringComparer.OrdinalIgnoreCase);

            LockRadioGroup(radioGroup2.Properties.Items, gioSet,
                           i => radioGroup2.SelectedIndex = i);
            LockRadioGroup(RDO_GXHN.Properties.Items, gioSet,
                           i => RDO_GXHN.SelectedIndex = i);
        }

        private void LockRadioGroup(RadioGroupItemCollection items,
                                      HashSet<string> gioSet,
                                      Action<int> setIndex)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = (RadioGroupItem)items[i];
                var itemSet = new HashSet<string>(
                    (item.AccessibleName ?? "").Split(',')
                                               .Select(g => g.Trim().Trim('\'')),
                    StringComparer.OrdinalIgnoreCase);

                if (itemSet.SetEquals(gioSet))
                {
                    setIndex(i);
                    item.Enabled = true;
                }
                else
                {
                    item.Enabled = false;
                }
            }
        }
        // Handler mới trong HVN_PGH:
        private void CheckGX_OnItemCheck(object sender, DevExpress.XtraEditors.Controls.ItemCheckEventArgs e)
        {
            this.BeginInvoke(new Action(() =>
                GioXuatCheckedChanged.Invoke(this, EventArgs.Empty)));
        }

        public void UnlockAllRadio()
        {
            foreach (RadioGroupItem item in radioGroup2.Properties.Items)
                item.Enabled = true;

            foreach (RadioGroupItem item in RDO_GXHN.Properties.Items)
                item.Enabled = true;

            tabVP.PageVisible = true;
            tabHN.PageVisible = true;
        }

        //public void LockRadioExcept(string gioFCC)
        //{
        //    for (int i = 0; i < radioGroup2.Properties.Items.Count; i++)
        //    {
        //        RadioGroupItem item = (RadioGroupItem)radioGroup2.Properties.Items[i];
        //        if (item.AccessibleName != null && item.AccessibleName == gioFCC)
        //            radioGroup2.SelectedIndex = i;
        //        else
        //            item.Enabled = false;
        //    }

        //    for (int i = 0; i < RDO_GXHN.Properties.Items.Count; i++)
        //    {
        //        RadioGroupItem item = (RadioGroupItem)RDO_GXHN.Properties.Items[i];
        //        if (item.AccessibleName != null && item.AccessibleName == gioFCC)
        //            RDO_GXHN.SelectedIndex = i;
        //        else
        //            item.Enabled = false;
        //    }
        //}

        public void UpdateGioXuatFromDB(string gioFCC)
        {
            // gioFCC = "'17','18','19'" → tách ra Set để compare
            var gioSet = new HashSet<string>(
                gioFCC.Split(',')
                      .Select(g => g.Trim().Trim('\'')),   // "17","18","19"
                StringComparer.OrdinalIgnoreCase);

            if (TrySelectRadio(radioGroup2.Properties.Items, gioSet, gioFCC,
                               i => radioGroup2.SelectedIndex = i)) return;

            TrySelectRadio(RDO_GXHN.Properties.Items, gioSet, gioFCC,
                           i => RDO_GXHN.SelectedIndex = i);
        }

        private bool TrySelectRadio(RadioGroupItemCollection items,
                                      HashSet<string> gioSet,
                                      string gioFCC,
                                      Action<int> setIndex)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = (RadioGroupItem)items[i];
                if (string.IsNullOrEmpty(item.AccessibleName)) continue;

                // AccessibleName = "'17','18','19'" → tách ra so sánh Set
                var itemSet = new HashSet<string>(
                    item.AccessibleName.Split(',')
                                       .Select(g => g.Trim().Trim('\'')),
                    StringComparer.OrdinalIgnoreCase);

                // Hai Set phải bằng nhau (không chỉ Contains)
                if (itemSet.SetEquals(gioSet))
                {
                    setIndex(i);
                    _presenter.UpdateGioXuat(
                        new GioXuat(gioFCC, item.Description ?? gioFCC));
                    return true;
                }
            }
            return false;
        }


        public bool HoiXoaDocQR()
            => XtraMessageBox.Show(
                "Dữ liệu không phù hợp:\n" +
                "Dữ liệu đọc QRCode không khớp với phiếu!\n" +
                "Bạn muốn xóa dữ liệu đọc?\n" +
                "(Nếu không xóa, phiếu giao hàng sẽ không được tải đúng)",
                "Thông Báo",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes;

        // ════════════════════════════════════════════════════════════════════════════
        // FIX 6: IHVNView — thêm UpdateGioXuatFromDB
        // HVN_PGH implement:
        // ════════════════════════════════════════════════════════════════════════════

        //public void UpdateGioXuatFromDB(string gioFCC)
        //{
        //    // Tìm radio item có AccessibleName khớp gioFCC rồi select
        //    // VP
        //    for (int i = 0; i < radioGroup2.Properties.Items.Count; i++)
        //    {
        //        var item = radioGroup2.Properties.Items[i];
        //        if (item.AccessibleName != null && item.AccessibleName == gioFCC)
        //        {
        //            radioGroup2.SelectedIndex = i;
        //            _presenter.UpdateGioXuat(new GioXuat(gioFCC, item.Description ?? gioFCC));
        //            return;
        //        }
        //    }
        //    // HN
        //    for (int i = 0; i < RDO_GXHN.Properties.Items.Count; i++)
        //    {
        //        var item = RDO_GXHN.Properties.Items[i];
        //        if (item.AccessibleName != null && item.AccessibleName == gioFCC)
        //        {
        //            RDO_GXHN.SelectedIndex = i;
        //            _presenter.UpdateGioXuat(new GioXuat(gioFCC, item.Description ?? gioFCC));
        //            return;
        //        }
        //    }
        //}
        // ════════════════════════════════════════════════════════════════════
        // UIButton click — routing
        // ════════════════════════════════════════════════════════════════════
        private void UIButton_ButtonClick(object sender, ButtonEventArgs e)
        {
            string cap = ((WindowsUIButton)e.Button).Caption;
            switch (cap)
            {
                // ── Phiếu thường ─────────────────────────────────────────────
                case "DOC QRCODE":
                    DocQRCodeClicked.Invoke(this, EventArgs.Empty);
                    break;

                case "Kiểm Tra Ghep Lot":
                    KiemTraGhepLotClicked.Invoke(this, EventArgs.Empty);
                    break;
                case "In Phiếu":
                    InPhieuClicked.Invoke(this, EventArgs.Empty);
                    break;
                case "In Ghép Lot":
                    InGhepLotClicked.Invoke(this, EventArgs.Empty);
                    break;
                case "In Tách Lot":
                    InTachLotClicked.Invoke(this, EventArgs.Empty);
                    break;
                case "Cập Nhập Kho":
                    if (_isLoading) return;
                    CapNhapKhoClicked.Invoke(this, EventArgs.Empty);
                    break;
                case "Kiểm tra mã NG":
                    KiemTraMaNGClicked.Invoke(this, EventArgs.Empty);
                    break;

                // ── Màn hình đọc QR ───────────────────────────────────────────
                //case "Hoàn Thành":
                //    HoanThanhClicked.Invoke(this, EventArgs.Empty);
                //    break;
                case "Xóa Dòng Được Chọn":
                    if (Confirm("Xóa dòng đang chọn?"))
                        XoaDongQRClicked.Invoke(this, EventArgs.Empty);
                    break;
                case "Xóa Toàn Bộ Dữ Liệu":
                    if (Confirm("Toàn bộ dữ liệu đọc sẽ bị xóa. Bạn chắc chắn?"))
                        XoaToanBoQRClicked.Invoke(this, EventArgs.Empty);
                    break;
                case "Sửa Số Lượng Tem":
                    SuaSoLuongTemClicked.Invoke(this, EventArgs.Empty);
                    break;
                case "Lấy Lại Lot":
                    {
                        int stt = GetFocusedDonHangStt();
                        if (stt < 0)
                        {
                            ShowInfo("Vui lòng chọn dòng cần lấy lại LOT trên danh sách đơn hàng!");
                            break;
                        }
                        // Kiểm tra dòng này có LOT không — nếu không có thì không cần reset
                        string lot = GridViewDONHANG.GetFocusedRowCellDisplayText("LOT").Trim();
                        if (string.IsNullOrEmpty(lot))
                        {
                            ShowInfo("Dòng này chưa có LOT, không cần lấy lại!");
                            break;
                        }
                        // Kiểm tra chưa CNK — nếu đã CNK thì không cho reset
                        string status = GridViewDONHANG.GetFocusedRowCellDisplayText("STATUS").Trim();
                        if (status == "OK")
                        {
                            ShowInfo("Dòng này đã được Cập Nhập Kho, không thể lấy lại LOT!");
                            break;
                        }
                        LayLaiLotNoClicked.Invoke(this, new LayLaiLotEventArgs(stt));
                        break;
                    }
                // ── GIAO DB ───────────────────────────────────────────────────
                case "Upload Đơn Hàng":
                    UploadGiaoDBClicked.Invoke(this, EventArgs.Empty);
                    break;
                case "Lưu":
                    LuuGiaoDBClicked.Invoke(this, EventArgs.Empty);
                    break;
                // Thêm ghi chú Phiếu SP 
                case "Ghi Chú STOP":
                    {
                        int stt = GetFocusedDonHangStt();
                        if (stt < 0) break;
                        CapNhapTTPHIEUClicked.Invoke(this, new TTPHIEUEventArgs(stt, "STOP"));
                        break;
                    }
                case "Xóa Ghi Chú STOP":
                    {
                        int stt = GetFocusedDonHangStt();
                        if (stt < 0) break;
                        CapNhapTTPHIEUClicked.Invoke(this, new TTPHIEUEventArgs(stt, ""));
                        break;
                    }
                // UIButton_ButtonClick — thêm case YMVN
                case "Hoàn Thành":
                    if (_cfg.CoHoanThanhYMVN)
                        HoanThanhYMVNClicked.Invoke(this, EventArgs.Empty);
                    else
                        HoanThanhClicked.Invoke(this, EventArgs.Empty);
                    break;

                case "Upload Milkrun SP":
                    UploadMilkrunSPClicked.Invoke(this, EventArgs.Empty);
                    break;
                case "Đang xem: MP":
                case "Đang xem: SP":
                    _isLoaiSP = !_isLoaiSP;
                    // Cập nhật caption button trong UIButton
                    foreach (var b in UIButton.Buttons.OfType<WindowsUIButton>())
                    {
                        if (b.Caption == "Đang xem: MP" || b.Caption == "Đang xem: SP")
                        {
                            b.Caption = _isLoaiSP ? "Đang xem: SP" : "Đang xem: MP";
                            break;
                        }
                    }
                    LoaiPhieuChanged.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        // ── UIButtonHOME ─────────────────────────────────────────────────────
        private void UIButtonHOME_ButtonClick(object sender, ButtonEventArgs e)
        {
            switch (((WindowsUIButton)e.Button).Caption)
            {
                case "HOME":
                    SwitchToPhieuView();
                    break;

            }
        }

        // ── Double click trên gridVDOCQRCODE → mở panel sửa SL tem ──────────
        private void gridVDOCQRCODE_FocusedRowChanged(object sender,
    DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            int stt = GetFocusedDocQRStt();
            if (stt < 0)
            {
                // Không có dòng hợp lệ → ẩn gridCtrSUASL, hiện gridCTTGL
                gridCtrSUASL.Visible = false;
                gridCTTGL.BringToFront();
                return;
            }

            var (lotFcc, slFcc, slHvn) = GetFocusedDocQRTemInfo();
            _sttSuaSl = stt;

            // Load data vào gridCtrSUASL
            gridCtrSUASL.DataSource = BuildSuaSlTable(stt, lotFcc, slFcc, slHvn);

            // ── Hiện gridCtrSUASL, ẩn gridCTTGL ─────────────────────────────────
            gridCTTGL.Visible = false;
            gridCtrSUASL.Visible = true;
            gridCtrSUASL.BringToFront();

            // Reset textbox
            TXT_FCCTU.Text = "";
            TXT_FCCTHANH.Text = "";
            TXT_HVNTU.Text = "";
            TXT_HVNTHANH.Text = "";
            LOTFCCVN.Text = lotFcc;
        }
        private DataTable BuildSuaSlTable(int stt, string lotFcc, int slFcc, int slHvn)
        {
            var tbl = new DataTable();
            tbl.Columns.Add("STT", typeof(int));
            tbl.Columns.Add("LOAI", typeof(string));  // "FCC" hoặc "HVN"
            tbl.Columns.Add("LOT", typeof(string));
            tbl.Columns.Add("SLHIEN", typeof(int));     // SL hiện tại
            tbl.Columns.Add("SLTHANH", typeof(int));     // SL muốn sửa

            // Các tem FCC (có thể ghép nhiều)
            if (!string.IsNullOrEmpty(lotFcc))
            {
                var parts = lotFcc.Split(',');
                foreach (var part in parts)
                {
                    var ls = part.Trim().Split('-');
                    string lot = ls[0].Trim();
                    int sl = ls.Length > 1 && int.TryParse(ls[1], out int v) ? v : slFcc;

                    var row = tbl.NewRow();
                    row["STT"] = stt;
                    row["LOAI"] = "FCC";
                    row["LOT"] = lot;
                    row["SLHIEN"] = sl;
                    row["SLTHANH"] = sl;
                    tbl.Rows.Add(row);
                }
            }

            // Tem HVN
            if (slHvn > 0)
            {
                var row = tbl.NewRow();
                row["STT"] = stt;
                row["LOAI"] = "HVN";
                row["LOT"] = "";       // HVN không có LOT riêng trong grid
                row["SLHIEN"] = slHvn;
                row["SLTHANH"] = slHvn;
                tbl.Rows.Add(row);
            }

            return tbl;
        }
        private void gridVSUASL_FocusedRowChanged(object sender,
    DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            var view = sender as DevExpress.XtraGrid.Views.Grid.GridView;
            if (view == null || view.FocusedRowHandle < 0) return;

            string loai = view.GetFocusedRowCellDisplayText("LOAI").Trim();
            string lot = view.GetFocusedRowCellDisplayText("LOT").Trim();
            string slHien = view.GetFocusedRowCellDisplayText("SLHIEN").Trim();

            if (loai == "FCC")
            {
                TXT_FCCTU.Text = slHien;
                TXT_FCCTHANH.Text = slHien;   // prefill để user chỉ sửa số
                                              // Clear HVN
                TXT_HVNTU.Text = "";
                TXT_HVNTHANH.Text = "";
            }
            else if (loai == "HVN")
            {
                TXT_HVNTU.Text = slHien;
                TXT_HVNTHANH.Text = slHien;   // prefill
                                              // Clear FCC
                TXT_FCCTU.Text = "";
                TXT_FCCTHANH.Text = "";
            }

            LOTFCCVN.Text = lot;
        }
        private DataTable BuildSuaSlTable(string lotFcc)
        {
            var tbl = new DataTable();
            tbl.Columns.Add("STT", typeof(int));
            tbl.Columns.Add("LOTFCC", typeof(string));
            tbl.Columns.Add("SLTEMFCC", typeof(int));
            tbl.Columns.Add("SUATHANH", typeof(int));

            var parts = lotFcc.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                var ls = parts[i].Split('-');
                var row = tbl.NewRow();
                row["STT"] = i;
                row["LOTFCC"] = ls[0];
                row["SLTEMFCC"] = int.TryParse(ls.Length > 1 ? ls[1] : "0", out int sl) ? sl : 0;
                row["SUATHANH"] = 0;
                tbl.Rows.Add(row);
            }
            return tbl;
        }

        // ── Nút cmd_SuaLTemFCC (Designer) ────────────────────────────────────
        private void cmd_SuaLTemFCC_Click(object sender, EventArgs e)
        {
            if (_sttSuaSl <= 0)
            {
                ShowInfo("Vui lòng chọn dòng QR cần sửa!");
                return;
            }
            if (!int.TryParse(TXT_FCCTHANH.Text, out int slMoi) || slMoi <= 0)
            {
                ShowInfo("Số lượng FCC không hợp lệ!");
                return;
            }
            // Bắn event để Presenter xử lý — truyền loại "FCC"
            SuaSoLuongTemClicked.Invoke(this, EventArgs.Empty);
        }
        public int? GetSuaSoLuongResult()
        {
            // Ưu tiên HVN nếu có giá trị — vì HVN thường được sửa sau FCC
            if (!string.IsNullOrWhiteSpace(TXT_HVNTHANH.Text)
                && int.TryParse(TXT_HVNTHANH.Text, out int slHvn)
                && slHvn > 0)
                return slHvn;

            if (!string.IsNullOrWhiteSpace(TXT_FCCTHANH.Text)
                && int.TryParse(TXT_FCCTHANH.Text, out int slFcc)
                && slFcc > 0)
                return slFcc;

            return null;
        }
        // ════════════════════════════════════════════════════════════════════
        // Row styling — chỉ UI
        // ════════════════════════════════════════════════════════════════════
        private void GridViewDONHANG_RowCellStyle(object sender, RowCellStyleEventArgs e)
        {
            var view = (GridView)sender;

            void Apply(string val, string okVal)
            {
                bool ok = val.Trim() == okVal;
                e.Appearance.BackColor = ok ? Color.Green : Color.Red;
                e.Appearance.ForeColor = Color.Yellow;
                if (ok) e.Appearance.Font = new Font("Arial", 9, FontStyle.Bold);
            }

            if (e.Column.FieldName == "LOT")
            {
                string lot = view.GetRowCellDisplayText(e.RowHandle, "LOT").Trim();
                e.Appearance.BackColor = lot == "" ? Color.Red : Color.Green;
                e.Appearance.ForeColor = Color.Yellow;
                if (lot != "") e.Appearance.Font = new Font("Arial", 9, FontStyle.Bold);
            }
            else if (e.Column.FieldName == "STATUS")
                Apply(view.GetRowCellDisplayText(e.RowHandle, "STATUS"), "OK");
            else if (e.Column.FieldName == "STATUSDOC")
                Apply(view.GetRowCellDisplayText(e.RowHandle, "STATUSDOC"), "OK");
        }

        // ════════════════════════════════════════════════════════════════════
        // Stub handlers — Designer yêu cầu
        // ════════════════════════════════════════════════════════════════════
        private void GridViewDONHANG_CellValueChanged(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e) { }
        private void GridViewDONHANG_CellValueChanging(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e) { }
        private void GridViewDONHANG_ClipboardRowCopying(object sender, DevExpress.XtraGrid.Views.Grid.ClipboardRowCopyingEventArgs e) { }
        private void GridViewDONHANG_PopupMenuShowing(object sender, DevExpress.XtraGrid.Views.Grid.PopupMenuShowingEventArgs e) { }
        private void GridViewDONHANG_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e) { }
        private void GridViewDONHANG_ValidateRow(object sender, DevExpress.XtraGrid.Views.Base.ValidateRowEventArgs e) { }
        private void GridViewDONHANG_ValidatingEditor(object sender, DevExpress.XtraEditors.Controls.BaseContainerValidateEditorEventArgs e) { }
        private void HVN_PGH_ContextMenuStripChanged(object sender, EventArgs e) { }
        private void btnUploadMilkrun_Click(object sender, EventArgs e)
    => UploadMilkrunSPClicked.Invoke(this, EventArgs.Empty);

        // ════════════════════════════════════════════════════════════════════
        // Dispose
        // ════════════════════════════════════════════════════════════════════
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_cfg.CoGear)
            {
                CheckGX.ItemCheck -= CheckGX_OnItemCheck;
                btnUploadMilkrun.Click -= btnUploadMilkrun_Click;
                if (_btnToggleLoaiPhieu != null)
                    _btnToggleLoaiPhieu.Click -= BtnToggleLoaiPhieu_Click;
            }
            else if (_cfg.LoadTheoNgay)  // ← HTN
            {
                btnUploadMilkrun.Click -= btnUploadMilkrun_Click;
            }
            _presenter.Dispose();
            base.OnFormClosed(e);
        }
        //---------------Help 
        private int GetFocusedDonHangStt()
        {
            string s = GridViewDONHANG.GetFocusedRowCellDisplayText("STT");
            return int.TryParse(s, out int v) ? v : -1;
        }

        private void cmd_SuaSLHVN_Click(object sender, EventArgs e)
        {
            if (_sttSuaSl <= 0)
            {
                ShowInfo("Vui lòng chọn dòng QR cần sửa!");
                return;
            }
            if (!int.TryParse(TXT_HVNTHANH.Text, out int slMoi) || slMoi <= 0)
            {
                ShowInfo("Số lượng HVN không hợp lệ!");
                return;
            }
            SuaSoLuongTemClicked.Invoke(this, EventArgs.Empty);
        }

        private void cmd_SuaLTemFCC_Click_1(object sender, EventArgs e)
        {
            if (_sttSuaSl <= 0)
            {
                ShowInfo("Vui lòng chọn dòng QR cần sửa!");
                return;
            }
            if (!int.TryParse(TXT_FCCTHANH.Text, out int slMoi) || slMoi <= 0)
            {
                ShowInfo("Số lượng FCC không hợp lệ!");
                return;
            }
            // Bắn event để Presenter xử lý — truyền loại "FCC"
            SuaSoLuongTemClicked.Invoke(this, EventArgs.Empty);
        }
    }

    // ── Helper class giữ nguyên ──────────────────────────────────────────────────
    public class MyWindowsUIButtonPanel : WindowsUIButtonPanel
    {
        public WindowsUIButtonsPanel GetButtonsPanel()
        {
            return ButtonsPanel;
        }
    }

}