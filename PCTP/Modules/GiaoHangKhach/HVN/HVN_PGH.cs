using DevExpress.XtraBars.Docking2010;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.BandedGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraReports.UI;
using PCTP.Applications.Services;
using PCTP.ClassSQL;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Infrastructure;
using PCTP.Infrastructure.Repositories;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.GiaoHangKhach.OrderLoading;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Modules.GiaoHangKhach.OrderLoading.GiaoDB;
using PCTP.Modules.GiaoHangKhach.OrderLoading.IFS;
using PCTP.Modules.GiaoHangKhach.Repositories;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Modules.GiaoHangKhach.SubForm;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Presentation.Presenters;
using PCTP.Presentation.Views;
using PCTP.QRCODE_HVN;
using PCTP.QRCODE_HVN.Report;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.Shared.Models;
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
            _cfg = CustomerTableConfig.Get(customerNo);
            gridVDOCQRCODE.FocusedRowChanged += gridVDOCQRCODE_FocusedRowChanged;
            _phieuBottomStateControl.SuaSlView.FocusedRowChanged += gridVSUASL_FocusedRowChanged;
            _presenter = BuildPresenter();
        }

        public void ShowReportWithGioHeader(DataTable reportData, string gioHeader) => _phieuDialogControl.ShowReportWithGioHeader(reportData, gioHeader);
        public void SetupNhaMayUI(CustomerConfig cfg) { if (_phieuHeaderControl != null) _phieuHeaderControl.ConfigureCustomer(cfg); }
        public void SetCheckedGiosYMVN(List<string> checkedGios) { if (_phieuHeaderControl != null) _phieuHeaderControl.SetCheckedGiosYMVN(checkedGios); }

        private HVN_Presenter BuildPresenter()
        {
            var sql = new SQLPROVIDER();
            var bus = new InProcessEventBus();
            var phieuDb = new PhieuSqlExecutor(sql);
            var phieuUow = new UnitOfWork(sql);
            var bulkStockSlotRepo = new BulkStockSlotRepository(phieuDb, phieuUow);
            var historyRepo = new StockHistoryRepository(phieuDb, phieuUow);
            var hangChoGiaoRepo = new HangChoGiaoRepository(phieuDb, phieuUow);
            var phieugiaDBRepo = new PhieuGiaoDBRepository(phieuDb, phieuUow);
            var phieuRepo = new PhieuRepository(phieuDb, phieuUow, _cfg, bulkStockSlotRepo, historyRepo, hangChoGiaoRepo);
            var phieuTmpRepo = new PhieuTmpRepository(phieuDb, phieuUow);
            var tableOrderRepo = new TableOrderRepo(phieuDb, phieuTmpRepo);
            _gioRepo = new GioXuatRepository(phieuDb, phieuUow);
            var qrRepo = new DocQRRepository(sql, _cfg);
            var sqlRepo = new SqlRepository(phieuDb, phieuUow);
            var luuTruRepo = new PhieuLuuTruRepository(phieuDb, phieuUow);
            var rowCategoryFilter = new DockCodeRowCategoryFilter();
            var categoryResolver = new GioMoTaCategoryResolver();
            var gioVP = _gioRepo.GetDictGioVP();
            var gioHN = _gioRepo.GetDictGioHN();
            phieuRepo.EnsureTablesExist();
            var ifsRepo = IFSRepository.Create();
            string tenMayBanQR = sql.ExecuteReader(sql.B7R2_FCCdb, "SELECT TenMay FROM tbl_QR_MAY_DOCQR WHERE TT = 1");
            bool isMayBanQR = string.Equals(Environment.MachineName, tenMayBanQR, StringComparison.OrdinalIgnoreCase);
            string tenBan = isMayBanQR ? _cfg.Delivery.TmpTable : _cfg.Delivery.GetTmpViewTable(Environment.MachineName);
            var ifsStrategy = new IfsOrderLoadStrategy(ifsRepo, luuTruRepo, phieuTmpRepo);
            var tableOrderStrategy = new OrderTableLoadStrategy(tableOrderRepo, phieuTmpRepo, ifsRepo, rowCategoryFilter);
            var giaoDbStrategy = new GiaoDbOrderLoadStrategy(phieugiaDBRepo);
            var ifsSource = new IfsOrderSource(ifsStrategy);
            var tableOrderSource = new TableOrderSource(tableOrderStrategy);
            var giaoDbSource = new GiaoDbOrderSource(giaoDbStrategy);
            var orderSourceFactory = new OrderSourceFactory(ifsSource, tableOrderSource, giaoDbSource);
            var phieuSvc = new PhieuService(phieuRepo, ifsRepo, bus, _gioRepo, tenBan, _cfg, isMayBanQR, tableOrderRepo, phieugiaDBRepo, orderSourceFactory, rowCategoryFilter);
            var lotSvc = new PhieuLotService(phieuRepo, phieuRepo, bus);
            var hangthieucangaySvc = new HangThieuCaNgayService(ifsRepo, luuTruRepo, phieuDb);
            var qrSvc = new DocQRService(qrRepo, bus, _cfg, categoryResolver);
            var inPhieuSvc = new InPhieuService(ifsRepo, phieuRepo, sqlRepo, gioVP, gioHN, _cfg);
            return new HVN_Presenter(this, phieuSvc, lotSvc, qrSvc, inPhieuSvc, hangthieucangaySvc, _gioRepo, bus, isMayBanQR, tenBan, _cfg, categoryResolver);
        }

        private static string SanitizeMachineName(string name) => System.Text.RegularExpressions.Regex.Replace(name ?? "LOCAL", @"[^A-Za-z0-9_]", "_");
        public void BindDonHang(DataTable dt) => _phieuGridControl.Bind(dt);
        public void BindHangThieu(DataTable dt) => _hangThieuControl.Bind(dt);
        public void BindLechIFS(DataTable dt) => _phieuBottomStateControl.LechGrid.DataSource = dt;
        public void BindDocQRCode(DataTable dt) { _docQrControl.Bind(dt); gridVDOCQRCODE.MoveLastVisible(); }
        public void BindHoanThanhYMVN(DataTable dt)
        {
            if (InvokeRequired) { Invoke(new Action(() => BindHoanThanhYMVN(dt))); return; }
            if (dt == null || dt.Rows.Count == 0) { ShowInfo("Không có dữ liệu Hoàn Thành YMVN để hiển thị."); return; }
            _docQrControl.Bind(dt);
            gridVDOCQRCODE.RefreshData();
        }
        public void BindGhepLot(DataTable dt) => _phieuBottomStateControl.GhepLotGrid.DataSource = dt;
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
        public bool Confirm(string msg) => XtraMessageBox.Show(msg, "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        public void ShowReport(DataTable reportData) => _phieuDialogControl.ShowReport(reportData);
        public void ShowHangThieuCaNgay(DataTable dt)
        {
            if (InvokeRequired) { Invoke(new Action(() => ShowHangThieuCaNgay(dt))); return; }
            _hangThieuControl.Bind(dt);
            _hangThieuControl.ShowAndBringToFront();
        }
        public void SwitchToDocQRView()
        {
            UIButtonHOME.Visible = true; panelPhieu.Visible = false; _docQrControl.BringToFront();
            try { _hangThieuControl.Visible = false; PN_DOCQR_SUASL1.Visible = true; PN_DOCQR_SUASL1.BringToFront(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SwitchToDocQRView] Lỗi set Visible: {ex.Message}"); }
            Invoke(new Action(() => { lblDocQrcode.Text = _cfg.Delivery?.LabelDocQR ?? "Đọc QRCode theo thứ tự: FCC → HVN"; }));
            _phieuBottomStateControl.SuaSlGrid.Visible = false;
            _phieuBottomStateControl.GhepLotGrid.Visible = false;
            _phieuActionBarControl.ConfigureDocQr();
            _docQrInputControl.FocusInput();
        }
        public void SwitchToPhieuView()
        {
            UIButtonHOME.Visible = false; panelPhieu.Visible = true; _phieuGridControl.BringToFrontGrid();
            try { PN_DOCQR_SUASL1.Visible = false; _hangThieuControl.Visible = true; _hangThieuControl.BringToFront(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SwitchToPhieuView] Lỗi set Visible: {ex.Message}"); }
            _phieuBottomStateControl.SuaSlGrid.Visible = false;
            _phieuBottomStateControl.GhepLotGrid.Visible = true;
            _phieuBottomStateControl.GhepLotGrid.BringToFront();
            _phieuBottomStateControl.LechGrid.Visible = false;
            TXT_FCCTU.Text = ""; TXT_FCCTHANH.Text = ""; TXT_HVNTU.Text = ""; TXT_HVNTHANH.Text = ""; _sttSuaSl = 0;
            _phieuActionBarControl.ConfigurePhieuView(_cfg.Delivery.LoadTuBangRieng ? "Show Thông Tin Lệch IFS" : "Kiểm Tra Ghep Lot", imageBT.Images);
        }
        public void BindGioXuatVP(IReadOnlyList<GioXuat> danhSach) { if (_phieuHeaderControl != null) _phieuHeaderControl.BindGioXuatVP(danhSach); }
        public void BindGioXuatHN(IReadOnlyList<GioXuat> danhSach) { if (_phieuHeaderControl != null) _phieuHeaderControl.BindGioXuatHN(danhSach); }
        public void SwitchToPhieuDBView() => _phieuActionBarControl.ConfigureGiaoDb();
        public void SetupPhieuButtons(bool showCapNhapKho, bool showKiemTraMaNG, bool showGhepLot, bool showDocQRCode, bool showLayLaiLot = false, bool showStop = false, bool showHangThieuCaNgay = true)
        {
            _phieuActionBarControl.ConfigureNormal(_cfg.Delivery.LoadTuBangRieng ? "Show Thông Tin Lệch IFS" : "Kiểm Tra Ghep Lot", showCapNhapKho, showKiemTraMaNG, showGhepLot, showDocQRCode, showLayLaiLot, showStop, showHangThieuCaNgay, imageBT.Images);
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
            var list = new List<GhepLotItem>();
            foreach (int i in _phieuBottomStateControl.GhepLotView.GetSelectedRows())
            {
                DataRow row = _phieuBottomStateControl.GhepLotView.GetDataRow(i);
                if (row == null) continue;
                list.Add(new GhepLotItem { MaHang = row[0].ToString(), GioXuat = int.TryParse(row[1].ToString(), out int gio) ? gio : 0, Lot = row[2].ToString() });
            }
            return list;
        }
        public int GetFocusedDocQRStt() => _docQrControl != null ? _docQrControl.GetFocusedStt() : -1;
        public (string LotFcc, int SlFcc, int SlHvn) GetFocusedDocQRTemInfo() => _docQrControl != null ? _docQrControl.GetFocusedTemInfo() : (string.Empty, 0, 0);
        public void DeleteFocusedDocQRRow() { if (_docQrControl != null) _docQrControl.DeleteFocusedRow(); }
        public void ClearDocQRRows() { if (_docQrControl != null) _docQrControl.ClearRows(); }
        public string GetFocusedDonHangMaHang() => _phieuGridControl.GetFocusedMaHang();
        public bool CoLotDeLuuKho() => _phieuGridControl.HasLotToSave();
        public void ThemDongGiaoDB(DataTable danhSachMaHang) => _phieuGridControl.ConfigureGiaoDbRow(danhSachMaHang);
        public bool CoHangChuaOK() => _phieuGridControl.HasUnconfirmedRows();
        private void LoadDBOKView() { }

        public int ShowChonSttTrungMa(ListView danhSachTrung) => _phieuDialogControl.ShowChonSttTrungMa(danhSachTrung);
        public void ShowKiemTraMaNG(string maHang) => _phieuDialogControl.ShowKiemTraMaNG(maHang);
        public void ShowTachLot() => _phieuDialogControl.ShowTachLot();
        public void ShowLoiCapNhapKho(DataTable errors) => _phieuDialogControl.ShowLoiCapNhapKho(errors, () => Enabled = false);
        public int? ShowSuaSoLuongTem(int sttBan, string lotFcc, int slFcc, int slHvn)
        {
            _sttSuaSl = sttBan; LOTFCCVN.Text = lotFcc; TXT_FCCTU.Text = slFcc.ToString(); TXT_HVNTU.Text = slHvn.ToString(); TXT_HVNTHANH.Text = ""; return null;
        }
        private int _sttSuaSl = 0;
        public int ShowChonHinhThucIn() { _hinhThucIn = _phieuDialogControl.ShowChonHinhThucIn(); return _hinhThucIn; }

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
        public event EventHandler<TTPHIEUEventArgs> CapNhapTTPHIEUClicked = delegate { };
        public event EventHandler<ChonLotThuCongEventArgs> ChonLotThuCongClicked = delegate { };
        public event EventHandler HoanThanhYMVNClicked = delegate { };
        public event EventHandler UploadMilkrunSPClicked = delegate { };
        public event EventHandler XemHangThieuCaNgayClicked = delegate { };

        private void HVN_PGH_Load(object sender, EventArgs e)
        {
            SetupNhaMayUI(_cfg); Text = $"Phiếu Giao Hàng — {_cfg.DisplayName}";
            _addressTable = IFSRepository.Create().GetCustomerAddress(_cfg.CustomerNo) ?? new DataTable();
            BindGioXuatVP(_gioRepo.GetDanhSachGioVP()); if (_cfg.Delivery.CoNhieuNhaMay) BindGioXuatHN(_gioRepo.GetDanhSachGioHN());
            SetupGridDonHangYMVN(_cfg.Delivery.LoadTuBangRieng);
            _phieuGridControl.OrderView.ShowingEditor += GridViewDONHANG_ShowingEditor_LOT;
            if (dateNX.DateTime == DateTime.MinValue || dateNX.DateTime.Year < 2000) dateNX.DateTime = DateTime.Now;
            if (_cfg.Delivery.CoGear) { CheckGX.ItemCheck += CheckGX_OnItemCheck; btnUploadMilkrun.Click += btnUploadMilkrun_Click; }
            else if (_cfg.Delivery.LoadTheoNgay) btnUploadMilkrun.Click += btnUploadMilkrun_Click;
            FormLoaded.Invoke(this, EventArgs.Empty);
            try { PN_DOCQR_SUASL1.Visible = false; _hangThieuControl.Visible = true; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HVN_PGH_Load] Lỗi set Visible GCT_HT/PN_DOCQR_SUASL1: {ex.Message}"); }
        }
        public void SetupGridDonHangYMVN(bool bangrieng) => _phieuGridControl.SetupForCustomer(bangrieng);
        public void BindGioXuatCheckList(List<string> danhSachGio) { if (_phieuHeaderControl != null) _phieuHeaderControl.BindGioXuatCheckList(danhSachGio); }
        public void LockCheckListYMVN() { if (_phieuHeaderControl != null) _phieuHeaderControl.LockCheckListYMVN(); }
        public void UnlockCheckListYMVN() { if (_phieuHeaderControl != null) _phieuHeaderControl.UnlockCheckListYMVN(); }
        private void GridViewDONHANG_ShowingEditor_LOT(object sender, CancelEventArgs e)
        {
            if (!_phieuGridControl.IsFocusedLotColumn()) return;
            e.Cancel = true; int stt = GetFocusedDonHangStt(); if (stt < 0) return;
            string status = _phieuGridControl.GetFocusedStatus(); if (status == "OK") { ShowInfo("Dòng này đã được Cập Nhập Kho!"); return; }
            string maHang = _phieuGridControl.GetFocusedMaHang(); int soLuong = _phieuGridControl.GetFocusedQuantity();
            ChonLotThuCongClicked.Invoke(this, new ChonLotThuCongEventArgs(stt, maHang, soLuong));
        }
        public ChonLotResult ShowChonLotTuKho(int stt, string maHang, int soLuong, DataTable danhSachLot) => _phieuDialogControl.ShowChonLotTuKho(maHang, soLuong, danhSachLot);
        public List<string> GetCheckedGioXuat() => _phieuHeaderControl != null ? _phieuHeaderControl.GetCheckedGioXuat() : new List<string>();
        public void BindGhepLotYMVN(DataTable dt) => _phieuBottomStateControl.GhepLotGrid.DataSource = dt;
        public void ShowReportYMVN(DataTable reportData) => _phieuDialogControl.ShowReportYMVN(reportData);
        public void XoaDongGiaoDB() => _phieuGridControl.DeleteSelectedRows();
        private void radioGroup2_EditValueChanging(object sender, ChangingEventArgs e) { if ((int)e.NewValue == 8) e.Cancel = !_presenter.OnGiaoDBChanging(_presenter.AddNM); }
        private void RDO_GXHN_EditValueChanging(object sender, ChangingEventArgs e) { if ((int)e.NewValue == 10) e.Cancel = !_presenter.OnGiaoDBChanging(_presenter.AddNM); }
        public void SetDate(DateTime date) { if (_phieuHeaderControl != null) { _phieuHeaderControl.SetDate(date); return; } dateNX.DateTime = date; }
        public void SuspendGioXuatChanged() { if (_phieuHeaderControl != null) _phieuHeaderControl.SuspendGioXuatChanged(); }
        public void ResumeGioXuatChanged() { if (_phieuHeaderControl != null) _phieuHeaderControl.ResumeGioXuatChanged(); }
        public void SetTab(int addNM) { if (_phieuHeaderControl != null) _phieuHeaderControl.SetTab(addNM); }
        public void LockDatePicker() { if (_phieuHeaderControl != null) _phieuHeaderControl.LockDatePicker(); }
        public void UnlockDatePicker() { if (_phieuHeaderControl != null) _phieuHeaderControl.UnlockDatePicker(); }
        public void LockRadioExcept(string gioFCC) { if (_phieuHeaderControl != null) _phieuHeaderControl.LockRadioExcept(gioFCC); }
        public void UnlockAllRadio() { if (_phieuHeaderControl != null) _phieuHeaderControl.UnlockAllRadio(); }
        private void LockRadioGroup(RadioGroupItemCollection items, HashSet<string> gioSet, Action<int> setIndex)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = (RadioGroupItem)items[i];
                var itemSet = new HashSet<string>((item.AccessibleName ?? "").Split(',').Select(g => g.Trim().Trim('\'')), StringComparer.OrdinalIgnoreCase);
                if (itemSet.SetEquals(gioSet)) { setIndex(i); item.Enabled = true; } else item.Enabled = false;
            }
        }
        public void UpdateGioXuatFromDB(string gioFCC) { if (_phieuHeaderControl != null) _phieuHeaderControl.UpdateGioXuatFromDB(gioFCC); }
        public bool HoiXoaDocQR() => XtraMessageBox.Show("Dữ liệu không phù hợp:\n" + "Dữ liệu đọc QRCode không khớp với phiếu!\n" + "Bạn muốn xóa dữ liệu đọc?\n" + "(Nếu không xóa, phiếu giao hàng sẽ không được tải đúng)", "Thông Báo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

        private void UIButtonHOME_ButtonClick(object sender, ButtonEventArgs e)
        {
            switch (((WindowsUIButton)e.Button).Caption)
            {
                case "HOME": SwitchToPhieuView(); break;
            }
        }
        private void gridVDOCQRCODE_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            int stt = GetFocusedDocQRStt();
            if (stt < 0) { _phieuBottomStateControl.SuaSlGrid.Visible = false; _phieuBottomStateControl.GhepLotGrid.BringToFront(); return; }
            var (lotFcc, slFcc, slHvn) = GetFocusedDocQRTemInfo();
            _sttSuaSl = stt; _phieuBottomStateControl.SuaSlGrid.DataSource = BuildSuaSlTable(stt, lotFcc, slFcc, slHvn);
            _phieuBottomStateControl.GhepLotGrid.Visible = false; _phieuBottomStateControl.SuaSlGrid.Visible = true; _phieuBottomStateControl.SuaSlGrid.BringToFront();
            TXT_FCCTU.Text = ""; TXT_FCCTHANH.Text = ""; TXT_HVNTU.Text = ""; TXT_HVNTHANH.Text = ""; LOTFCCVN.Text = lotFcc;
        }
        private DataTable BuildSuaSlTable(int stt, string lotFcc, int slFcc, int slHvn)
        {
            var tbl = new DataTable(); tbl.Columns.Add("STT", typeof(int)); tbl.Columns.Add("LOAI", typeof(string)); tbl.Columns.Add("LOT", typeof(string)); tbl.Columns.Add("SLHIEN", typeof(int)); tbl.Columns.Add("SLTHANH", typeof(int));
            if (!string.IsNullOrEmpty(lotFcc)) foreach (var part in lotFcc.Split(',')) { var ls = part.Trim().Split('-'); string lot = ls[0].Trim(); int sl = ls.Length > 1 && int.TryParse(ls[1], out int v) ? v : slFcc; var row = tbl.NewRow(); row["STT"] = stt; row["LOAI"] = "FCC"; row["LOT"] = lot; row["SLHIEN"] = sl; row["SLTHANH"] = sl; tbl.Rows.Add(row); }
            if (slHvn > 0) { var row = tbl.NewRow(); row["STT"] = stt; row["LOAI"] = "HVN"; row["LOT"] = ""; row["SLHIEN"] = slHvn; row["SLTHANH"] = slHvn; tbl.Rows.Add(row); }
            return tbl;
        }
        private void gridVSUASL_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            var view = sender as DevExpress.XtraGrid.Views.Grid.GridView; if (view == null || view.FocusedRowHandle < 0) return;
            string loai = view.GetFocusedRowCellDisplayText("LOAI").Trim(); string lot = view.GetFocusedRowCellDisplayText("LOT").Trim(); string slHien = view.GetFocusedRowCellDisplayText("SLHIEN").Trim();
            if (loai == "FCC") { TXT_FCCTU.Text = slHien; TXT_FCCTHANH.Text = slHien; TXT_HVNTU.Text = ""; TXT_HVNTHANH.Text = ""; }
            else if (loai == "HVN") { TXT_HVNTU.Text = slHien; TXT_HVNTHANH.Text = slHien; TXT_FCCTU.Text = ""; TXT_FCCTHANH.Text = ""; }
            LOTFCCVN.Text = lot;
        }
        private DataTable BuildSuaSlTable(string lotFcc)
        {
            var tbl = new DataTable(); tbl.Columns.Add("STT", typeof(int)); tbl.Columns.Add("LOTFCC", typeof(string)); tbl.Columns.Add("SLTEMFCC", typeof(int)); tbl.Columns.Add("SUATHANH", typeof(int));
            var parts = lotFcc.Split(',');
            for (int i = 0; i < parts.Length; i++) { var ls = parts[i].Split('-'); var row = tbl.NewRow(); row["STT"] = i; row["LOTFCC"] = ls[0]; row["SLTEMFCC"] = int.TryParse(ls.Length > 1 ? ls[1] : "0", out int sl) ? sl : 0; row["SUATHANH"] = 0; tbl.Rows.Add(row); }
            return tbl;
        }
        private void cmd_SuaLTemFCC_Click(object sender, EventArgs e)
        {
            if (_sttSuaSl <= 0) { ShowInfo("Vui lòng chọn dòng QR cần sửa!"); return; }
            if (!int.TryParse(TXT_FCCTHANH.Text, out int slMoi) || slMoi <= 0) { ShowInfo("Số lượng FCC không hợp lệ!"); return; }
            SuaSoLuongTemClicked.Invoke(this, EventArgs.Empty);
        }
        public int? GetSuaSoLuongResult()
        {
            if (!string.IsNullOrWhiteSpace(TXT_HVNTHANH.Text) && int.TryParse(TXT_HVNTHANH.Text, out int slHvn) && slHvn > 0) return slHvn;
            if (!string.IsNullOrWhiteSpace(TXT_FCCTHANH.Text) && int.TryParse(TXT_FCCTHANH.Text, out int slFcc) && slFcc > 0) return slFcc;
            return null;
        }
        private void GridViewDONHANG_RowCellStyle(object sender, RowCellStyleEventArgs e) => _phieuGridControl.ApplyRowCellStyle(e);
        private void GridViewDONHANG_CellValueChanged(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e) { }
        private void GridViewDONHANG_CellValueChanging(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e) { }
        private void GridViewDONHANG_ClipboardRowCopying(object sender, DevExpress.XtraGrid.Views.Grid.ClipboardRowCopyingEventArgs e) { }
        private void GridViewDONHANG_PopupMenuShowing(object sender, DevExpress.XtraGrid.Views.Grid.PopupMenuShowingEventArgs e) { }
        private void GridViewDONHANG_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e) { }
        private void GridViewDONHANG_ValidateRow(object sender, DevExpress.XtraGrid.Views.Base.ValidateRowEventArgs e) { }
        private void GridViewDONHANG_ValidatingEditor(object sender, DevExpress.XtraEditors.Controls.BaseContainerValidateEditorEventArgs e) { }
        private void HVN_PGH_ContextMenuStripChanged(object sender, EventArgs e) { }
        private void btnUploadMilkrun_Click(object sender, EventArgs e) => UploadMilkrunSPClicked.Invoke(this, EventArgs.Empty);

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_cfg.Delivery.CoGear) { CheckGX.ItemCheck -= CheckGX_OnItemCheck; btnUploadMilkrun.Click -= btnUploadMilkrun_Click; if (_btnToggleLoaiPhieu != null) _btnToggleLoaiPhieu.Click -= BtnToggleLoaiPhieu_Click; }
            else if (_cfg.Delivery.LoadTheoNgay) btnUploadMilkrun.Click -= btnUploadMilkrun_Click;
            _presenter.Dispose(); base.OnFormClosed(e);
        }
        private int GetFocusedDonHangStt() => _phieuGridControl.GetFocusedStt();
        private void cmd_SuaSLHVN_Click(object sender, EventArgs e)
        {
            if (_sttSuaSl <= 0) { ShowInfo("Vui lòng chọn dòng QR cần sửa!"); return; }
            if (!int.TryParse(TXT_HVNTHANH.Text, out int slMoi) || slMoi <= 0) { ShowInfo("Số lượng HVN không hợp lệ!"); return; }
            SuaSoLuongTemClicked.Invoke(this, EventArgs.Empty);
        }
        private void cmd_SuaLTemFCC_Click_1(object sender, EventArgs e)
        {
            if (_sttSuaSl <= 0) { ShowInfo("Vui lòng chọn dòng QR cần sửa!"); return; }
            if (!int.TryParse(TXT_FCCTHANH.Text, out int slMoi) || slMoi <= 0) { ShowInfo("Số lượng FCC không hợp lệ!"); return; }
            SuaSoLuongTemClicked.Invoke(this, EventArgs.Empty);
        }
    }

    public class MyWindowsUIButtonPanel : WindowsUIButtonPanel
    {
        public WindowsUIButtonsPanel GetButtonsPanel() { return ButtonsPanel; }
    }
}
