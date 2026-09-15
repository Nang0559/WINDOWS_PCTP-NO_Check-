using DevExpress.XtraBars.Docking2010;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;

using PCTP.Infrastructure.Repositories;

using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Presentation.Presenters;
using PCTP.Presentation.Views;
using PCTP.Shared.Helpers;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.HVN
{
    public partial class HVN_PGH : XtraForm, IHVNView
    {
        public int SttDangSuaSl => _sttSuaSl;
        private readonly HVN_Presenter _presenter;
        private DataTable _addressTable = new DataTable();
        private int _hinhThucIn = 1;
        private string _tenpdb = "";
        private string _ggfccpdb = "";
        private CustomerConfig _cfg;
        private readonly string _customerNo;
        private DocQRCode _pendingSlKhacBiet = null;
        private bool _isLoading = false;
        public bool IsLoaiSP => _phieuHeaderControl != null && _phieuHeaderControl.IsLoaiSP;
        public GioXuat CurrentGioXuat => _phieuHeaderControl != null ? _phieuHeaderControl.CurrentGioXuat : null;
        public event EventHandler LoaiPhieuChanged = delegate { };
        private GioXuatRepository _gioRepo;
        private readonly IWaitFormService _waitForm;

        public HVN_PGH(string customerNo = "100001")
        {
            InitializeComponent();
            _waitForm = new WaitFormService(this);

            // Normalize once at the UI/module boundary. The actual Delivery config
            // is resolved by GiaoHangKhachModuleFactory so there is only one owner
            // for customer validation and module composition.
            _customerNo = CustomerTableConfig.NormalizeCustomerNo(customerNo);
            _presenter = BuildPresenter();
        }

        public void ShowReportWithGioHeader(DataTable reportData, string gioHeader) => _phieuDialogControl.ShowReportWithGioHeader(reportData, gioHeader);
        public void SetupNhaMayUI(CustomerConfig cfg) { if (_phieuHeaderControl != null) _phieuHeaderControl.ConfigureCustomer(cfg); }
        public void SetCheckedGiosYMVN(List<string> checkedGios) { if (_phieuHeaderControl != null) _phieuHeaderControl.SetCheckedGiosYMVN(checkedGios); }

        private HVN_Presenter BuildPresenter()
        {
            // ── Toàn bộ wiring (SQLPROVIDER, các Repository, Strategy, Source,
            //    Service...) giờ được GiaoHangKhachModuleFactory dựng tập trung,
            //    tránh lặp lại logic giữa HVN_PGH và các entry point khác. ──────
            var module = GiaoHangKhachModuleFactory.Build(_customerNo);

            _cfg = module.Cfg;
            _gioRepo = (GioXuatRepository)module.GioXuatRepo;

            return new HVN_Presenter(
                this,
                module.PhieuService,
                module.PhieuLotService,
                module.DocQRService,
                module.InPhieuService,
                module.HangThieuCaNgayService,
                module.GioXuatRepo,
                module.Bus,
                module.IsMayBanQR,
                module.TenBan,
                module.Cfg,
                module.CategoryResolver);
        }

        public void BindDonHang(DataTable dt) => _phieuGridControl.Bind(dt);
        public void BindHangThieu(DataTable dt) => _hangThieuControl.Bind(dt);
        public void BindLechIFS(DataTable dt) => _phieuBottomStateControl.BindLech(dt);
        public void BindDocQRCode(DataTable dt) { _docQrControl.Bind(dt); _docQrControl.MoveLastVisible(); }
        public void BindHoanThanhYMVN(DataTable dt)
        {
            if (InvokeRequired) { Invoke(new Action(() => BindHoanThanhYMVN(dt))); return; }
            if (dt == null || dt.Rows.Count == 0) { ShowInfo("Không có dữ liệu Hoàn Thành YMVN để hiển thị."); return; }
            _docQrControl.Bind(dt);
            _docQrControl.RefreshData();
        }
        public void BindGhepLot(DataTable dt) => _phieuBottomStateControl.BindGhepLot(dt);
        public void SetGridCaption(string caption) => _phieuGridControl.SetCaption(caption);
        public void RefreshLotRow(int stt, string lot) => _phieuGridControl.RefreshLotRow(stt, lot);

        private System.Threading.Timer _loadingTimeout;
        public void ShowLoading(bool show, string caption = "Đang xử lý...")
        {
            if (InvokeRequired) { Invoke(new Action(() => ShowLoading(show, caption))); return; }
            if (show)
            {
                _loadingTimeout?.Dispose(); _loadingTimeout = null;
                if (!_isLoading) { _isLoading = true; _waitForm.Show(caption); } else _waitForm.SetCaption(caption);
                _loadingTimeout = new System.Threading.Timer(_ => { try { Invoke(new Action(() => { System.Diagnostics.Debug.WriteLine("[ShowLoading] Auto-close sau 30s — có thể bị stuck"); ShowLoading(false); })); } catch { } }, null, 30000, System.Threading.Timeout.Infinite);
            }
            else
            {
                _loadingTimeout?.Dispose(); _loadingTimeout = null;
                if (!_isLoading) return;
                _isLoading = false; _waitForm.Close();
            }
        }
        public void ShowError(string msg) => XtraMessageBox.Show(msg, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        public void ShowInfo(string msg) => XtraMessageBox.Show(msg, "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        public void ShowWarning(string msg) => XtraMessageBox.Show(msg, "Cảnh Báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        public bool Confirm(string msg) => XtraMessageBox.Show(msg, "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes;
        public void ShowReport(DataTable reportData) => _phieuDialogControl.ShowReport(reportData);
        public void ShowHangThieuCaNgay(DataTable dt)
        {
            if (InvokeRequired) { Invoke(new Action(() => ShowHangThieuCaNgay(dt))); return; }
            _hangThieuControl.Bind(dt);
            _hangThieuControl.ShowAndBringToFront();
        }
        public void SwitchToDocQRView()
        {
            UIButtonHOME.Visible = true; _phieuHeaderControl.Visible = false; _docQrControl.BringToFront();
            try { _hangThieuControl.Visible = false; PN_DOCQR_SUASL1.Visible = true; PN_DOCQR_SUASL1.BringToFront(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SwitchToDocQRView] Lỗi set Visible: {ex.Message}"); }
            Invoke(new Action(() => { lblDocQrcode.Text = _cfg.Delivery?.LabelDocQR ?? "Đọc QRCode theo thứ tự: FCC → HVN"; }));
            _phieuBottomStateControl.HideSuaSoLuong();
            _phieuBottomStateControl.HideGhepLot();
            _phieuActionBarControl.ConfigureDocQr();
            _docQrInputControl.FocusInput();
        }
        public void SwitchToPhieuView()
        {
            UIButtonHOME.Visible = false; _phieuHeaderControl.Visible = true; _phieuGridControl.BringToFrontGrid();
            try { PN_DOCQR_SUASL1.Visible = false; _hangThieuControl.Visible = true; _hangThieuControl.BringToFront(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SwitchToPhieuView] Lỗi set Visible: {ex.Message}"); }
            _phieuBottomStateControl.HideSuaSoLuong();
            _phieuBottomStateControl.ShowGhepLot();
            _phieuBottomStateControl.HideLech();
            TXT_FCCTU.Text = ""; TXT_FCCTHANH.Text = ""; TXT_HVNTU.Text = ""; TXT_HVNTHANH.Text = ""; _sttSuaSl = 0;
            _phieuActionBarControl.ConfigurePhieuView(_cfg.Delivery.LoadTuBangRieng ? "Show Thông Tin Lệch IFS" : "Kiểm Tra Ghep Lot", imageBT);
        }
        public void BindGioXuatVP(IReadOnlyList<GioXuat> danhSach) { if (_phieuHeaderControl != null) _phieuHeaderControl.BindGioXuatVP(danhSach); }
        public void BindGioXuatHN(IReadOnlyList<GioXuat> danhSach) { if (_phieuHeaderControl != null) _phieuHeaderControl.BindGioXuatHN(danhSach); }
        public void SwitchToPhieuDBView() => _phieuActionBarControl.ConfigureGiaoDb();
        public void SetupPhieuButtons(bool showCapNhapKho, bool showKiemTraMaNG, bool showGhepLot, bool showDocQRCode, bool showLayLaiLot = false, bool showStop = false, bool showHangThieuCaNgay = true)
        {
            _phieuActionBarControl.ConfigureNormal(_cfg.Delivery.LoadTuBangRieng ? "Show Thông Tin Lệch IFS" : "Kiểm Tra Ghep Lot", showCapNhapKho, showKiemTraMaNG, showGhepLot, showDocQRCode, showLayLaiLot, showStop, showHangThieuCaNgay, imageBT);
        }
        public DateTime SelectedDate => _phieuHeaderControl != null ? _phieuHeaderControl.SelectedDate : DateTime.MinValue;
        public int SelectedTabAddNM => _phieuHeaderControl != null ? _phieuHeaderControl.SelectedTabAddNM : _cfg.Delivery.AddNmMacDinh;
        public string QRCodeInput => _docQrInputControl != null ? _docQrInputControl.Text.Trim() : string.Empty;
        public void ClearQRInput() { if (_docQrInputControl != null) _docQrInputControl.Clear(); }
        public int SelectedHinhThucIn => _hinhThucIn;
        public DataTable GetDonHangTable() => _phieuGridControl.GetDataTable();
        public DataTable GetAddressTable() => _addressTable;
        public IEnumerable<GhepLotItem> GetSelectedGhepLotRows()
        {
            foreach (DataRow row in _phieuBottomStateControl.GetSelectedGhepLotRows())
            {
                yield return new GhepLotItem
                {
                    MaHang = row[0].ToString(),
                    GioXuat = int.TryParse(row[1].ToString(), out int gio) ? gio : 0,
                    Lot = row[2].ToString()
                };
            }
        }
        public int GetFocusedDocQRStt() => _docQrControl != null ? _docQrControl.GetFocusedStt() : -1;
        public (string LotFcc, int SlFcc, int SlHvn) GetFocusedDocQRTemInfo() => _docQrControl != null ? _docQrControl.GetFocusedTemInfo() : (string.Empty, 0, 0);
        public void DeleteFocusedDocQRRow() { if (_docQrControl != null) _docQrControl.DeleteFocusedRow(); }
        public void ClearDocQRRows() { if (_docQrControl != null) _docQrControl.ClearRows(); }
        public string GetFocusedDonHangMaHang() => _phieuGridControl.GetFocusedMaHang();
        public bool CoLotDeLuuKho() => _phieuGridControl.HasLotToSave();
        public void ThemDongGiaoDB(DataTable danhSachMaHang) => _phieuGridControl.ConfigureGiaoDbRow(danhSachMaHang);
        public bool CoHangChuaOK() => _phieuGridControl.HasUnconfirmedRows();
