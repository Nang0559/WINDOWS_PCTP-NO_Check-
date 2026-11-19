using DevExpress.XtraReports.UI;
using PCTP.Applications.Services;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Infrastructure.Repositories;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Modules.GiaoHangKhach.SubForm;
using PCTP.Presentation.Views;
using PCTP.QRCODE_HVN.Report;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Presentation.Presenters
{
    internal sealed class PhieuPresenter : IDisposable
    {
        private readonly HVNPresenterContext _c;
        private readonly IPhieuView _v;

        // Immutable UI restore snapshot for an active DOCQRCODE session.
        // TMP/DOCQRCODE is the source of truth; normal UI loads must never
        // replace this header context with the previous screen selection.
        private DateTime _qrRestoreDate = DateTime.MinValue;
        private int _qrRestoreAddNM;
        private string _qrRestoreConcreteHour = string.Empty;
        private bool _qrRestoreIsSP;
        // Initial control binding can synchronously raise context-change events.
        // Those events must not acquire _busy before the persisted QR session
        // has been restored.
        private bool _initializing;
        // Ignore header events raised before FormLoaded. DevExpress can raise
        // Date/Tab/GioXuat/LoaiPhieuChanged while controls are being created,
        // before OnFormLoaded has a chance to establish the QR session.
        private bool _formLoaded;
        internal PhieuPresenter(HVNPresenterContext context)
        {
            _c = context; _v = _c.PhieuView; var v = _v;
            v.FormLoaded += OnFormLoaded; v.DateChanged += OnDateChanged; v.GioXuatChanged += OnGioXuatChanged; v.TabChanged += OnTabChanged; v.CapNhapKhoClicked += OnCapNhapKho; v.InPhieuClicked += OnInPhieu; v.InGhepLotClicked += OnInGhepLot; v.InTachLotClicked += OnInTachLot; v.KiemTraGhepLotClicked += OnKiemTraGhepLot; v.KiemTraMaNGClicked += OnKiemTraMaNG; v.HoanThanhClicked += OnHoanThanh; v.LoaiPhieuChanged += OnLoaiPhieuChanged; v.ChonLotThuCongClicked += OnChonLotThuCong; v.XemHangThieuCaNgayClicked += OnXemHangThieuCaNgay; v.LayLaiLotNoClicked += OnLayLaiLotNo; v.CapNhapTTPHIEUClicked += OnCapNhapTTPHIEU; v.NoteEdited += OnNoteEdited;
            _c.Bus.Subscribe<PhieuLoadedEvent>(OnPhieuLoaded); _c.Bus.Subscribe<KhoUpdatedEvent>(OnKhoUpdated); _c.Bus.Subscribe<TinhTongCompletedEvent>(OnTinhTongCompleted);
        }
        private void OnFormLoaded(object sender, EventArgs e)
        {
            // DevExpress binding may synchronously raise Date/Tab/GioXuat/
            // LoaiPhieuChanged while the form is being initialized. These are
            // not user changes and must not start a competing async load.
            _initializing = true;
            try
            {
                if (_c.Cfg.Delivery.CoGear)
                {
                    _c.AddNM = _c.Cfg.Delivery.AddNmMacDinh;
                    _c.LoadGioXuatYMVN();
                }
                else
                {
                    _c.AddNM = _c.Cfg.Delivery.CoNhieuNhaMay
                        ? _v.SelectedTabAddNM
                        : _c.Cfg.Delivery.AddNmMacDinh;
                }
            }
            finally
            {
                _initializing = false;
            }

            // From this point header events are real user/UI changes. Events
            // raised before this point are only control initialization noise.
            _formLoaded = true;
            XetTrangThai();
        }
        private void OnDateChanged(object sender, EventArgs e)
        {
            if (!_formLoaded || _initializing) return;

            // QR session identity = ngày + nhà máy + giờ. Không được reload TMP
            // bằng một context mới khi đang có DOCQRCODE.
            if (_c.IsBanQR) return;

            _c.RunWithLoading(() =>
            {
                if (_c.Cfg.Delivery.CoGear) _c.LoadGioXuatYMVN();
                if (_c.GioXuatHienTai.Ma == "#")
                {
                    _c.LoadPhieuGiaoDB();
                    return;
                }
                _c.LoadPhieuHienTai();
            }, "Đang chuyển ngày...");
        }
        private void OnTabChanged(object sender, EventArgs e)
        {
            if (!_formLoaded || _initializing) return;

            // MP: nhà máy là một phần của QR session identity -> không đổi tab.
            // SP: nhà máy dùng chung với MP nhưng KHÔNG thuộc hour context.
            // Cho phép chuyển HN <-> VP khi đang ở màn hình SP; phiên MP/QR
            // trước đó không được ép UI quay lại tab cũ.
            if (_c.IsBanQR && !_v.IsLoaiSP) return;
            if (!_c.Cfg.Delivery.CoNhieuNhaMay) return;

            int selectedTab = _v.SelectedTabAddNM;
            _c.RunWithLoading(() =>
            {
                _c.AddNM = selectedTab;

                if (_c.IsBanQR && _v.IsLoaiSP)
                {
                    // Rời QR session hiện tại để nạp lại SP theo nhà máy mới.
                    // Không xóa DOCQRCODE/TMP của session cũ.
                    _c.IsBanQR = false;
                    _c.ClearDeliverySession();
                    _c.DocQrView.UnlockDocQrDeliveryContext();
                    _c.QrSvc.SetCheDoBanSP(true);
                }

                _c.LoadPhieuHienTai();
            }, "Chuyển nhà máy...");
        }
        private void OnGioXuatChanged(object sender, EventArgs e)
        {
            if (!_formLoaded || _initializing) return;

            // Không cho thay đổi hour context trong một QR session đang mở.
            if (_c.IsBanQR) return;

            _c.UpdateGioXuat(_v.CurrentGioXuat);
            _c.RunWithLoading(() =>
            {
                if (_c.Cfg.Delivery.CoGear)
                {
                    _c.LoadPhieuHienTai();
                    return;
                }
                if (_c.GioXuatHienTai.Ma == "#")
                {
                    _c.UiContext.Post(_ => _c.LoadPhieuGiaoDB(), null);
                    return;
                }
                _c.UiContext.Send(_ => _c.Bus.Publish(new GioXuatChangedEvent(_c.GioXuatHienTai, _c.AddNM)), null);
                _c.LoadPhieuHienTai();
            }, "Chuyển giờ xuất...");
        }
        private void OnCapNhapKho(object sender, EventArgs e) => _c.RunWithLoadingSync(() => { if (_c.Cfg.Delivery.CoGear) { var gs = _c.YmvnView.GetCheckedGioXuat(); if (!gs.Any()) { _v.ShowInfo("Bạn chưa chọn giờ xuất!"); return; } _c.PhieuSvc.LuuGhiChuTuLuoi(_v.GetDonHangTable()); _c.PhieuSvc.CapNhapKhoYMVN(_v.SelectedDate.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture), string.Join(",", gs.Select(g => $"'{g}'")), _c.GetNhaMay(), _v.GetDonHangTable()); return; } if (_c.Cfg.Delivery.LoadTuBangRieng) { if (!_v.CoLotDeLuuKho()) { _v.ShowInfo("Không có dữ liệu cho CNK !!!!!"); return; } _c.PhieuSvc.LuuGhiChuTuLuoi(_v.GetDonHangTable()); _c.PhieuSvc.CapNhapKho("", _c.GetNhaMay()); return; } bool isLoaiSP = _c.CategoryResolver.Resolve(new OrderLoadContext { GioFccMoTa = _c.GioXuatHienTai.MoTa }) == OrderCategory.SP; if (!_v.CoLotDeLuuKho()) { _v.ShowInfo("Không có dữ liệu cho CNK !!!!!"); return; } string nhaMay = _c.GetNhaMay(); string ngayGiao = _v.SelectedDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture); if (isLoaiSP) _c.PhieuSvc.LuuPhieuSP(nhaMay, ngayGiao, _c.GioXuatHienTai.MoTa, _c.GioXuatHienTai.Ma); _c.PhieuSvc.LuuGhiChuTuLuoi(_v.GetDonHangTable()); _c.PhieuSvc.CapNhapKho(_c.GioXuatHienTai.MoTa, nhaMay, _c.GioXuatHienTai.Ma); }, "Đang cập nhật kho...");
        // Lưu Ghi Chú ngay khi người dùng sửa xong ô; lỗi ở đây không chặn thao tác (CNK sẽ lưu lại lần nữa).
        private void OnNoteEdited(object sender, TTPHIEUEventArgs e)
        {
            try { _c.PhieuSvc.LuuGhiChuMotDong(e.Stt, e.GhiChu); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[OnNoteEdited] STT={e.Stt}: {ex.Message}"); }
        }
        private void OnCapNhapTTPHIEU(object sender, TTPHIEUEventArgs e) => _c.RunWithLoadingSync(() => { if (_c.CategoryResolver.Resolve(new OrderLoadContext { GioFccMoTa = _c.GioXuatHienTai.Ma }) != OrderCategory.SP) return; _c.PhieuSvc.CapNhapTTPHIEU(_c.GetNhaMay(), _v.SelectedDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), _c.GioXuatHienTai.MoTa, e.Stt, e.GhiChu); }, "Đang cập nhật thông tin phiếu...");
        private void OnInPhieu(object sender, EventArgs e)
        {
            int hinhThucIn = 0; if (!_c.Cfg.Delivery.CoGear && !_c.Cfg.Delivery.LoadTuBangRieng && _c.GioXuatHienTai.Ma != "#") { hinhThucIn = _v.ShowChonHinhThucIn(); if (hinhThucIn == -1) return; }
            // Preview là dialog modal (ShowPreviewDialog chặn tới khi đóng) nên KHÔNG được mở bên trong RunWithLoadingSync:
            // wait form "Đang khởi tạo biểu mẫu in..." chỉ đóng ở finally => treo trên phiếu. Dựng dữ liệu trong loading, mở preview sau.
            Action showReport = null;
            _c.RunWithLoadingSync(() =>
            {
                string ngayDdMMyyyy = _v.SelectedDate.ToString("ddMMyyyy", System.Globalization.CultureInfo.InvariantCulture);
                if (_c.Cfg.Delivery.CoGear) { DataTable d = _c.InPhieuSvc.BuildReportDataYMVN(_v.GetDonHangTable(), _c.YmvnView.GetCheckedGioXuat()); showReport = () => _c.YmvnView.ShowReportYMVN(d); return; }
                if (_c.Cfg.Delivery.LoadTuBangRieng) { DataTable d = _c.InPhieuSvc.BuildReportDataTuBangRieng(_v.GetDonHangTable(), _v.GetAddressTable(), ngayDdMMyyyy); string cot = _c.Cfg.Delivery.LoadTheoNgay ? "PO No" : "Giờ"; showReport = () => _v.ShowReportWithGioHeader(d, cot); return; }
                if (_c.GioXuatHienTai.Ma == "#") { DataTable d = _c.InPhieuSvc.BuildReportDataGiaoDB(_v.GetDonHangTable()); showReport = () => _v.ShowReport(d); return; }
                DataTable r = _c.InPhieuSvc.BuildReportData(ngayDdMMyyyy, _c.GioXuatHienTai.Ma, _c.GioXuatHienTai.MoTa, _c.GetNhaMay(), _c.AddNM, hinhThucIn, _v.GetAddressTable());
                showReport = () => _v.ShowReport(r);
            }, "Đang khởi tạo biểu mẫu in...");
            if (showReport != null) showReport();
        }
        /// <summary>True khi mọi LOT trong chuỗi ghép (cách nhau bởi ',', dạng LOT20-SL) có ký tự thứ 13 = '0' (không có Gear).</summary>
        private static bool IsLotNoGear(string lotGhep)
        {
            if (string.IsNullOrWhiteSpace(lotGhep)) return false;
            string[] lots = lotGhep.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (lots.Length == 0) return false;
            foreach (string raw in lots)
            {
                string lot = raw.Trim();
                int dash = lot.IndexOf('-');
                if (dash > 0) lot = lot.Substring(0, dash);
                if (lot.Length < 13 || lot[12] != '0') return false;
            }
            return true;
        }
        /// <summary>
        /// Tìm Gear cho một dòng tem in. Khớp chính xác theo chuỗi LOT ghép; nếu SP in trả chuỗi LOT khác dạng
        /// (vd. không có đuôi -SL, hoặc chỉ một LOT trong chuỗi) thì khớp theo từng LOT 20 ký tự.
        /// Hàng không Gear (ký tự 13 của LOT = '0') luôn in được với ô Gear trống, không báo lỗi.
        /// </summary>
        private static bool TryResolveGear(Dictionary<string, string> gearByPayload, string payload, out string gear)
        {
            if (gearByPayload.TryGetValue(payload, out gear)) return true;

            string norm = NormalizeLotKey(payload);
            foreach (KeyValuePair<string, string> kv in gearByPayload)
            {
                string k = NormalizeLotKey(kv.Key);
                if (k.Length > 0 && (k == norm || k.IndexOf(norm, StringComparison.OrdinalIgnoreCase) >= 0 || norm.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    gear = kv.Value;
                    return true;
                }
            }

            if (IsLotNoGear(payload))
            {
                gear = string.Empty;
                return true;
            }
            gear = null;
            return false;
        }
        /// <summary>Bỏ khoảng trắng và đuôi "-SL" của từng LOT: "A-5, B-3" -> "A,B".</summary>
        private static string NormalizeLotKey(string lotGhep)
        {
            if (string.IsNullOrWhiteSpace(lotGhep)) return string.Empty;
            var parts = new List<string>();
            foreach (string raw in lotGhep.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = raw.Trim();
                int dash = t.IndexOf('-');
                parts.Add(dash > 0 ? t.Substring(0, dash) : t);
            }
            return string.Join(",", parts);
        }
        private void OnInGhepLot(object sender, EventArgs e)
        {
            List<GhepLotItem> selectedRows = _v.GetSelectedGhepLotRows().ToList();
            bool ymvn = _c.Cfg.Delivery.CoGear;

            // YMVN: tem phải dùng mẫu GHEPLOT_YMVN và điền GEAR vào ô dưới nhãn "Gear" (mẫu đọc [Model]).
            // Usp_gheplotPrint chỉ trả B20Item.model nên nếu không thay thì tem in Model thay vì Gear.
            var gearByPayload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (ymvn)
            {
                if (selectedRows.Count == 0)
                {
                    MessageBox.Show("Hãy chọn dòng ghép LOT cần in.", "In ghép LOT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                foreach (GhepLotItem item in selectedRows)
                {
                    // Ký tự 13 của LOT = '0' nghĩa là "không phân loại Gear" (B20Gear.Code=0 có Name rỗng theo thiết kế):
                    // vẫn in tem ghép, ô Gear để trống. Chỉ báo lỗi khi Gear rỗng mà LOT lại có mã Gear khác 0 (dữ liệu chưa tải đủ).
                    string gearName = (item.GearName ?? string.Empty).Trim();
                    if (gearName.Length == 0 && !IsLotNoGear(item.Lot))
                    {
                        // Không chặn cứng: cho người dùng chọn in tiếp với ô Gear trống hoặc quay lại tải lại ghép LOT.
                        DialogResult dr = MessageBox.Show("Dòng ghép LOT của mã " + item.MaHang + " không có Gear.\r\n"
                            + "Chọn Yes để vẫn in tem với ô Gear trống, hoặc No rồi bấm \"Kiểm tra ghép LOT\" để tải lại.",
                            "In ghép LOT", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                        if (dr != DialogResult.Yes) return;
                    }
                    gearByPayload[(item.Lot ?? string.Empty).Trim()] = gearName;
                }
            }

            DataTable reportData = null;
            _c.RunWithLoadingSync(() => reportData = _c.InPhieuSvc.InGhepLot(selectedRows.Count > 0 ? selectedRows : null, Environment.MachineName), "Đang tổng hợp dữ liệu ghép LOT...");
            if (reportData == null) return;

            XtraReport report;
            if (ymvn)
            {
                if (!reportData.Columns.Contains("Model") || !reportData.Columns.Contains("ItemLotCode"))
                {
                    MessageBox.Show("Dữ liệu in thiếu cột Model/ItemLotCode nên không điền được Gear.", "In ghép LOT", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                foreach (DataRow row in reportData.Rows)
                {
                    string payload = Convert.ToString(row["ItemLotCode"]).Trim();
                    string gear;
                    if (!TryResolveGear(gearByPayload, payload, out gear))
                    {
                        MessageBox.Show("Không tìm thấy Gear cho tem LOT '" + payload + "'. Hãy bấm \"Kiểm tra ghép LOT\" rồi in lại.", "In ghép LOT", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    row["Model"] = gear;
                }
                report = new GHEPLOT_YMVN { DataSource = reportData };
            }
            else
            {
                report = new GHEPLOT { DataSource = reportData };
            }
            // Preview là dialog modal nên mở NGOÀI RunWithLoadingSync (xem chú thích ở OnInPhieu).
            new ReportPrintTool(report).ShowPreviewDialog();
        }
        private void OnInTachLot(object sender, EventArgs e) => _v.ShowTachLot();
        private void OnKiemTraGhepLot(object sender, EventArgs e) => _c.RunWithLoading(() => { DataTable dt = _c.PhieuSvc.LoadGhepLot(); _c.UiContext.Post(_ => _v.BindGhepLot(dt), null); }, "Đang kiểm tra ghép LOT...");
        private void OnKiemTraMaNG(object sender, EventArgs e) { string ma = _v.GetFocusedDonHangMaHang(); if (!string.IsNullOrWhiteSpace(ma)) _v.ShowKiemTraMaNG(ma); }
        private void OnHoanThanh(object sender, EventArgs e) => _c.RunWithLoadingSync(() =>
        {
            int n = _c.QrSvc.CountChuaDG();
            if (n > 0)
            {
                // Resolve the active category's physical TMP/DOCQR pair once.
                // _c.TenBan is the MP/view binding and must not be used for SP:
                // SP candidates live in TMPPHIEUGIAOHANG_SP.
                bool isSp = _c.QrSvc.IsBanSP;
                // Giao DB: TMPPHIEUGIAOHANGDB + DOCQRCODE_DB (phiên QR riêng).
                bool isGiaoDb = _c.QrSvc.IsGiaoDB;
                string activeTmpTable = _c.Cfg.Delivery.GetTmpTable(isSp, isGiaoDb);
                string activeDocQrTable = _c.Cfg.Delivery.GetDocQRTable(isSp, isGiaoDb);

                DataTable t = _c.PhieuSvc.GetDonHangChuaLot(isSp);
                if (t != null && t.Rows.Count > 0)
                    _c.LotSvc.TinhTongLot(
                        t,
                        activeTmpTable,
                        activeDocQrTable,
                        activeTmpTable,
                        rows =>
                        {
                            // Đóng wait form TRƯỚC khi mở popup chọn dòng trùng (ShowDialog chặn luồng
                            // UI nên wait form nếu còn mở sẽ đè lên popup); xong thì bật lại để tiếp tục
                            // tổng hợp. ShowLoading(false) cũng hủy timer auto-close 30s trong lúc chờ chọn.
                            _v.ShowLoading(false);
                            try { return _v.ShowChonSttTrungMa(rows); }
                            finally { _v.ShowLoading(true, "Đang tổng hợp dữ liệu hoàn thành..."); }
                        });
                int conLai = _c.QrSvc.CountChuaDG();
                if (conLai > 0)
                {
                    // Còn dòng QR chưa đóng gói: GIỮ khóa phiên QR (IsBanQR,
                    // DeliverySession, khóa ngày/giờ/nhà máy) nhưng vẫn phải
                    // quay về màn hình Phiếu + GridView Đơn hàng, giống nút HOME.
                    // Trước đây chỗ này chỉ `return` nên màn hình kẹt ở DocQR.
                    _v.SwitchToPhieuView();
                    DataTable dangHien = _v.GetDonHangTable();
                    DataTable moiNhat = _c.PhieuSvc.GetDonHangHienTai(activeTmpTable);
                    dangHien = MergeDocQrResultIntoOrderTable(dangHien, moiNhat);
                    _v.BindDonHang(dangHien);
                    _v.ShowInfo("Còn " + conLai + " dòng QR chưa đóng gói vào phiếu. Phiên đọc QR vẫn được giữ, bấm DOC QRCODE để quay lại đọc tiếp.");
                    return;
                }

                _c.IsBanQR = false;
                _c.QrSvc.SetCheDoBan("");
                _c.ClearDeliverySession();
                _c.DocQrView.UnlockDocQrDeliveryContext();
                if (_c.Cfg.Delivery.CoGear)
                    _c.YmvnView.UnlockCheckListYMVN();
                _v.UnlockAllRadio();
                _v.UnlockDatePicker();
                _v.SwitchToPhieuView();
                DataTable current = _v.GetDonHangTable();
                DataTable latest = _c.PhieuSvc.GetDonHangHienTai(activeTmpTable);
                current = MergeDocQrResultIntoOrderTable(current, latest);
                _v.BindDonHang(current);
                _c.SetupPhieuButtonsDefault(true, false, _c.PhieuSvc.CheckCoLotChuaCNK(current));
                return;
            }
            _c.IsBanQR = false;
            _c.QrSvc.SetCheDoBan("");
            _c.ClearDeliverySession();
            _c.DocQrView.UnlockDocQrDeliveryContext();
            if (_c.Cfg.Delivery.CoGear)
                _c.YmvnView.UnlockCheckListYMVN();
            _v.UnlockAllRadio();
            _v.UnlockDatePicker();
            if (_c.GioXuatHienTai.Ma == "#")
            {
                DataTable d = _c.LoadPhieuGiaoDB();
                _c.SetupPhieuButtonsDefault(true, false, _c.PhieuSvc.CheckCoLotChuaCNK(d));
            }
            else
            {
                _v.SwitchToPhieuView();
                _c.LoadPhieuHienTai();
            }
        }, "Đang tổng hợp dữ liệu hoàn thành...");

        private static DataTable MergeDocQrResultIntoOrderTable(DataTable current, DataTable latest)
        {
            if (latest == null || latest.Rows.Count == 0)
                return current;
            // Lưới chưa có bảng nào: dùng luôn bảng TMP đầy đủ cột thay vì bind bảng rỗng.
            if (current == null)
                return latest;
            if (current.Rows.Count == 0)
                return current;

            foreach (DataRow source in latest.Rows)
            {
                string stt = source.Table.Columns.Contains("STT") ? source["STT"]?.ToString().Trim() : "";
                if (string.IsNullOrEmpty(stt)) continue;
                DataRow target = current.AsEnumerable().FirstOrDefault(r => r.Table.Columns.Contains("STT") && string.Equals(r["STT"]?.ToString().Trim(), stt, StringComparison.OrdinalIgnoreCase));
                if (target == null) continue;
                CopyIfBothColumnsExist(source, target, "LOT");
                CopyIfBothColumnsExist(source, target, "STATUS");
                CopyIfBothColumnsExist(source, target, "STATUSDOC");
                FillMissingFromTmp(source, target);
            }
            return current;
        }

        // Bảng đang bind lên lưới có thể thiếu cột hoặc trống (vd được nạp lại từ bản rút gọn trong lúc đọc QR).
        // TMP luôn giữ đủ thông tin dòng (GIOGIAO/CUA/TRUYEN/TENHANG/GEAR/DV/SOLUONG/PO_NO/...), nên chỉ điền
        // vào cột chưa có hoặc ô đang trống; không bao giờ ghi đè ô đã có giá trị.
        private static void FillMissingFromTmp(DataRow source, DataRow target)
        {
            foreach (DataColumn column in source.Table.Columns)
            {
                object value = source[column];
                if (value == null || value == DBNull.Value) continue;
                if (string.IsNullOrWhiteSpace(Convert.ToString(value))) continue;

                string name = column.ColumnName;
                if (!target.Table.Columns.Contains(name))
                    target.Table.Columns.Add(name, column.DataType);

                object existing = target[name];
                if (existing != null && existing != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(existing)))
                    continue;

                try { target[name] = value; }
                catch (Exception)
                {
                    try { target[name] = Convert.ChangeType(value, target.Table.Columns[name].DataType, System.Globalization.CultureInfo.InvariantCulture); }
                    catch (Exception) { /* kiểu không tương thích: bỏ qua ô này */ }
                }
            }
        }

        private static void CopyIfBothColumnsExist(DataRow source, DataRow target, string columnName)
        {
            if (!source.Table.Columns.Contains(columnName) || !target.Table.Columns.Contains(columnName)) return;
            target[columnName] = source[columnName] == DBNull.Value ? (object)DBNull.Value : source[columnName];
        }

        private void OnLoaiPhieuChanged(object sender, EventArgs e)
        {
            if (!_formLoaded || _initializing)
                return;

            // MP/SP is a view mode, but each mode has its own DOCQR/TMP
            // session. Re-run the same persisted-session detection whenever
            // the user switches the view:
            //
            //   MP -> check DOCQRCODE + TMPPHIEUGIAOHANG
            //   SP -> check DOCQRCODE_SP + TMPPHIEUGIAOHANG_SP
            //
            // If the selected mode has no DOCQR data, that mode is NOT in a
            // QR session: unlock Date/Plant/Hour and load its normal orders.
            // If DOCQR exists, restore its TMP context and lock exactly as
            // during FormLoaded.
            XetTrangThai();
        }
        private void OnChonLotThuCong(object sender, ChonLotThuCongEventArgs e) { if (!_c.IsMayBanQR) return; DataTable lots = _c.PhieuSvc.GetDanhSachLotTuKho(e.MaHang); ChonLotResult r = _v.ShowChonLotTuKho(e.Stt, e.MaHang, e.SoLuong, lots); if (!r.Confirmed || string.IsNullOrWhiteSpace(r.LotGhep)) return; _c.PhieuSvc.NhapLotThuCong(e.Stt, r.LotGhep, _c.TenBan);
            _v.RefreshLotRow(e.Stt, r.LotGhep);
            _c.IsBanQR = true;
            bool isSpSession = _c.Cfg.Delivery.CoLoaiSP && _v.IsLoaiSP;
            _c.BeginDeliverySession(
                _v.SelectedDate,
                _c.AddNM,
                isSpSession ? string.Empty : _c.GioXuatHienTai.Ma,
                _c.GetNhaMay(),
                isSpSession);
            _v.LockRadioExcept(_c.GioXuatHienTai.Ma);
            _v.LockDatePicker();
            _c.DocQrView.LockDocQrDeliveryContext(
                isSpSession,
                isSpSession ? string.Empty : _c.GioXuatHienTai.Ma);
            DataTable dt = _c.PhieuSvc.GetDonHangHienTai(_c.TenBan); _c.SetupPhieuButtonsDefault(_c.PhieuSvc.CheckCanCapNhapKho(dt), false, _c.PhieuSvc.CheckCoLotChuaCNK(dt)); }
        private void OnLayLaiLotNo(object sender, LayLaiLotEventArgs e) { if (!_v.Confirm($"Bạn có chắc chắn muốn reset dữ liệu LOT của dòng có STT {e.Stt} không?")) return; _c.RunWithLoadingSync(() => { _c.PhieuSvc.LayLaiLotNo(e.Stt, _c.QrSvc.IsBanSP); _c.LoadPhieuHienTai(); }, "Đang xử lý lấy lại số LOT..."); }
        private void OnXemHangThieuCaNgay(object sender, EventArgs e) => _c.RunWithLoading(() => { DataTable dt = _c.HangThieuCaNgayService.TinhHangThieuCaNgay(_v.SelectedDate, _c.GetNhaMay(), _c.AddNM, _c.Cfg); _c.UiContext.Post(_ => _v.ShowHangThieuCaNgay(dt), null); }, "Đang tính hàng thiếu cả ngày...");
        private void XetTrangThai()
        {
            System.Diagnostics.Debug.WriteLine(
             $"[XetTrangThai] ENTER Thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
            _c.RunWithLoadingSync(() => {
                System.Diagnostics.Debug.WriteLine(
            $"[XetTrangThai] INSIDE RunWithLoadingSync Thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
                if (!_c.IsMayBanQR)
                {
                    _c.IsBanQR = false; _v.UnlockAllRadio(); _v.UnlockDatePicker();
                    _c.LoadPhieuHienTai(); return;
                }
                // The selected MP/SP view owns its own DOCQR table.
                // Never fall back from one category to the other here:
                // switching MP -> SP must re-evaluate DOCQRCODE_SP, and
                // switching SP -> MP must re-evaluate DOCQRCODE.
                bool selectedIsSP = _v.IsLoaiSP && _c.Cfg.Delivery.CoConfigSP;
                var tt = selectedIsSP
                    ? _c.PhieuSvc.GetTrangThaiDangBanSP()
                    : _c.PhieuSvc.GetTrangThaiDangBan();

                if (!tt.DangBan)
                {
                    // No DOCQR in the currently selected mode means there is
                    // no immutable QR context for THIS view. Do not delete the
                    // other mode's DOCQR/TMP session. Just unlock the header
                    // and load the selected mode normally.
                    _c.IsBanQR = false;
                    _c.ClearDeliverySession();
                    _c.QrSvc.SetCheDoBanSP(selectedIsSP);
                    _v.UnlockAllRadio();
                    _v.UnlockDatePicker();
                    _c.DocQrView.UnlockDocQrDeliveryContext();
                    _c.LoadPhieuHienTai();
                    return;
                }
                if (!tt.DataKhongKhop && !_c.SessionHoursShareOneFrame(tt, selectedIsSP))
                    tt.DataKhongKhop = true;
                if (tt.DataKhongKhop)
                {
                    if (_c.DocQrView.HoiXoaDocQR()) _c.PhieuSvc.XoaDocQRCode(selectedIsSP); _c.IsBanQR = false;
                    _c.QrSvc.SetCheDoBanSP(false); _v.UnlockAllRadio(); _v.UnlockDatePicker(); _c.LoadPhieuHienTai(); return;
                }
                if (!PCTP.Shared.Helpers.DateText.TryParseApp(tt.NgayGiao, out DateTime ngay))
                {
                    _c.IsBanQR = false;
                    _c.ClearDeliverySession();
                    _c.QrSvc.SetCheDoBanSP(false);
                    _v.UnlockAllRadio();
                    _v.UnlockDatePicker();
                    _c.DocQrView.UnlockDocQrDeliveryContext();
                    _c.LoadPhieuHienTai();
                    return;
                }

                // IMPORTANT:
                // Mark QR mode BEFORE touching Date/Tab. SetDate/SetTab can
                // raise their events synchronously. If IsBanQR is still false,
                // those handlers start a normal IFS load and race the restore,
                // which can reset the context immediately after we lock it.
                _c.AddNM = _c.Cfg.Delivery.CoNhieuNhaMay ? tt.AddNM : _c.Cfg.Delivery.AddNmMacDinh;
                _c.IsBanQR = true;

                // Snapshot the TMP identity before touching any UI control.
                // This snapshot is reapplied after every PhieuLoadedEvent so a
                // later grid/header refresh cannot revert date/tab/hour to the
                // previous user context.
                _qrRestoreDate = ngay.Date;
                _qrRestoreAddNM = _c.AddNM;
                _qrRestoreConcreteHour = tt.GioGiaoFCC ?? string.Empty;

                if (_c.Cfg.Delivery.CoNhieuNhaMay)
                    _v.SetTab(_qrRestoreAddNM);
                _v.SetDate(_qrRestoreDate);

                // DIAGNOSTIC #1: đọc ngược lại control thật ngay sau khi gán.
                // Nếu dòng này in ra -> DevExpress không "nhận" giá trị gán
                // (rất có thể do control chưa HandleCreated lúc Form_Load chạy).
                if (_v.SelectedTabAddNM != _qrRestoreAddNM || _v.SelectedDate.Date != _qrRestoreDate.Date)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[XetTrangThai] Header did not accept restore: " +
                        $"expected ADDNM={_qrRestoreAddNM}/Date={_qrRestoreDate:yyyy-MM-dd}, " +
                        $"but control now shows ADDNM={_v.SelectedTabAddNM}/Date={_v.SelectedDate:yyyy-MM-dd}.");
                }

                // SP has its own DOCQR/TMP session. Do not infer the
                // category from the currently selected MP/SP radio description:
                // the selected DOCQR table is the authoritative category.
                // SP TMP can contain multiple/blank GIOGIAO values, so its
                // immutable identity is Date + ADDNM only.
                if (selectedIsSP)
                {
                    _qrRestoreIsSP = true;
                    _c.QrSvc.SetCheDoBanSP(true);

                    // Some legacy SP sessions still have a single concrete
                    // GIOGIAO. Restore it when it maps to a real radio item,
                    // but never require an hour for an SP session.
                    if (!_c.Cfg.Delivery.CoGear && !string.IsNullOrWhiteSpace(tt.GioGiaoFCC))
                    {
                        _v.SuspendGioXuatChanged();
                        try
                        {
                            _v.SelectGioXuatByConcreteHour(tt.GioGiaoFCC);
                        }
                        finally
                        {
                            _v.ResumeGioXuatChanged();
                        }
                    }

                    _c.BeginDeliverySession(
                        ngay.Date,
                        _c.AddNM,
                        string.Empty,
                        _c.GetNhaMay(),
                        true);

                    _c.DocQrView.LockDocQrDeliveryContext(true, string.Empty);
                    _c.LoadPhieuHienTai();
                    return;
                }

                if (_c.Cfg.Delivery.CoGear)
                {
                    var gs = _c.ParseGioYMVN(tt.GioGiaoFCC);
                    _c.QrSvc.SetCheDoBanSP(false);
                    _c.BeginDeliverySession(
                        ngay.Date,
                        _c.AddNM,
                        tt.GioGiaoFCC,
                        _c.GetNhaMay(),
                        false);
                    _v.SuspendGioXuatChanged();
                    try
                    {
                        _c.YmvnView.SetCheckedGiosYMVN(gs);
                        // Phiếu đã giao: không khóa danh sách giờ nữa — cho chọn giờ để IN phiếu theo giờ.
                        _c.YmvnView.EnablePrintHourSelectionYMVN();
                    }
                    finally { _v.ResumeGioXuatChanged(); }

                    _c.DocQrView.LockDocQrDeliveryContext(false, tt.GioGiaoFCC);
                    _c.LoadPhieuHienTai();
                    return;
                }

                // TMP.GIOGIAO is a concrete delivery hour (for example "15").
                // The Radio may represent a group (for example "'15','16'").
                // Resolve the Radio by its Ma/hour-set, never by parsing MoTa.
                string gio = tt.GioGiaoFCC, ma = "", mota = "";
                var ds = _c.AddNM == 1
                    ? _c.GioXuatRepo.GetDanhSachGioVP()
                    : _c.GioXuatRepo.GetDanhSachGioHN();

                foreach (var g in ds)
                {
                    if (DeliverySessionIdentity.ContainsHour(g.Ma, gio))
                    {
                        ma = g.Ma;
                        mota = g.MoTa;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(ma))
                {
                    // Preserve special/non-standard hours, but do not invent a
                    // grouped radio identity when the repository has no match.
                    ma = $"'{gio.Trim().Trim('\'')}'";
                    mota = gio + "H";
                }

                bool isSpSession = _c.CategoryResolver.Resolve(
                    new OrderLoadContext { GioFccMoTa = mota }) == OrderCategory.SP;
                _qrRestoreIsSP = isSpSession;

                _c.QrSvc.SetCheDoBanSP(isSpSession);

                _v.SuspendGioXuatChanged();
                try
                {
                    // IMPORTANT: update the real DevExpress RadioGroup selection,
                    // not only HVNPresenterContext.GioXuatHienTai. TMP stores a
                    // concrete hour (15), while the Radio item can be a group
                    // ('15','16'). The header resolves the containing item and
                    // becomes the single source of truth for the visible UI.
                    bool radioRestored = _v.SelectGioXuatByConcreteHour(gio);
                    System.Diagnostics.Debug.WriteLine(
                    $"[XetTrangThai] AFTER SelectGioXuatByConcreteHour " +
                    $"gio={gio}, " +
                    $"radioRestored={radioRestored}, " +
                    $"AddNM={_c.AddNM}, " +
                    $"Time={DateTime.Now:HH:mm:ss.fff}");
                    // FIX: luôn đồng bộ GioXuatHienTai, không chỉ khi restore thất bại.
                    _c.GioXuatHienTai = new GioXuat(ma, mota);

                    if (!radioRestored)
                    {
                        // DIAGNOSTIC #2: đây chính là dấu hiệu Radio bị "kẹt" ở item
                        // mặc định (ví dụ 6H) thay vì giờ thật từ TMP.
                        System.Diagnostics.Debug.WriteLine(
                            $"[XetTrangThai] SelectGioXuatByConcreteHour('{gio}') FAILED " +
                            $"for ADDNM={_c.AddNM} — Radio still shows its default item.");
                    }
                }
                finally { _v.ResumeGioXuatChanged(); }

                // The immutable session keeps the concrete TMP hour. Validation
                // accepts the selected Radio only when that hour belongs to its
                // hour-set.
                _c.BeginDeliverySession(
                    ngay.Date,
                    _c.AddNM,
                    gio,
                    _c.GetNhaMay(),
                    isSpSession);

                // QR + TMP metadata now define the exact date/factory/hour.
                _c.DocQrView.LockDocQrDeliveryContext(
                    isSpSession,
                    isSpSession ? string.Empty : ma);
                _c.LoadPhieuHienTai();
            }, "Đang kiểm tra trạng thái phiên làm việc cũ...");
        }

        private void RestoreQrHeaderFromSnapshot()
        {
            if (!_c.IsBanQR || _qrRestoreDate == DateTime.MinValue)
                return;

            _v.SuspendGioXuatChanged();
            try
            {
                if (_c.Cfg.Delivery.CoNhieuNhaMay)
                    _v.SetTab(_qrRestoreAddNM);

                _v.SetDate(_qrRestoreDate);

                if (!_qrRestoreIsSP && !string.IsNullOrWhiteSpace(_qrRestoreConcreteHour))
                {
                    bool radioRestored = _v.SelectGioXuatByConcreteHour(_qrRestoreConcreteHour);
                    if (!radioRestored)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[RestoreQrHeaderFromSnapshot] SelectGioXuatByConcreteHour('{_qrRestoreConcreteHour}') " +
                            $"FAILED for ADDNM={_qrRestoreAddNM}, Date={_qrRestoreDate:yyyy-MM-dd}.");
                    }
                }
            }
            finally
            {
                _v.ResumeGioXuatChanged();
            }
        }

        private void OnPhieuLoaded(PhieuLoadedEvent e)
        {
            if (e == null)
                return;

            // EventBus may publish from a worker thread. Validate and bind only
            // on the WinForms UI thread, against the latest load request.
            _c.UiContext.Post(_ =>
            {
                bool currentIsSP = _v.IsLoaiSP && _c.Cfg.Delivery.CoConfigSP;
                long latestLoadRequestId = _c.LatestLoadRequestId;

                // Category alone is insufficient for MP -> SP -> MP. The request
                // sequence makes every older non-zero result stale, even if its
                // category happens to match the current one again.
                //
                // LoadRequestId == 0 is intentionally treated as a legacy/untracked
                // event so existing PublishEmptyPhieuLoaded/other callers remain
                // backward-compatible. Such an event is still protected by IsSP.
                if ((e.LoadRequestId != 0 && e.LoadRequestId != latestLoadRequestId) ||
                    e.IsSP != currentIsSP)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[OnPhieuLoaded] IGNORE STALE: eventId={e.LoadRequestId}, latestId={latestLoadRequestId}, eventSP={e.IsSP}, currentSP={currentIsSP}, caption='{e.Caption}'");
                    return;
                }

                DataTable data = e.DonHangTable;
                _v.SetGridCaption(e.Caption ?? string.Empty);
                _v.BindDonHang(data ?? new DataTable());
                _c.SetupPhieuButtonsDefault(true, e.CoMaNG, _c.PhieuSvc.CheckCoLotChuaCNK(data));
                _c.IsLoadingPhieu = false;
                _c.AwaitingPhieuLoadedEvent = false;
                _c.HideLoadingUnlessAwaitingPhieuLoad();
                RestoreQrHeaderFromSnapshot();

                if (_c.IsBanQR)
                    _c.UiContext.Post(__ => RestoreQrHeaderFromSnapshot(), null);
            }, null);
        }
        private void OnKhoUpdated(KhoUpdatedEvent e)
        {
            _c.UiContext.Post(_ =>
            {
                if (e != null && e.Errors != null && e.Errors.Rows.Count > 0)
                {
                    bool onlyFifo = true;
                    foreach (DataRow row in e.Errors.Rows)
                    {
                        string status = row.Table.Columns.Contains("STATUS") ? row["STATUS"]?.ToString() ?? string.Empty : string.Empty;
                        if (status.IndexOf("FIFO:", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            onlyFifo = false;
                            break;
                        }
                    }

                    if (onlyFifo)
                    {
                        _v.ShowWarning(BuildFifoWarning(e.Errors));
                        _c.LoadPhieuHienTai();
                        return;
                    }

                    _v.ShowLoiCapNhapKho(e.Errors);
                    return;
                }
                _c.LoadPhieuHienTai();
            }, null);
        }

        private static string BuildFifoWarning(DataTable errors)
        {
            var lines = new List<string>();
            foreach (DataRow row in errors.Rows)
            {
                string stt = row.Table.Columns.Contains("STT") ? row["STT"]?.ToString() ?? "" : "";
                string mh = row.Table.Columns.Contains("MH") ? row["MH"]?.ToString() ?? "" : "";
                string lot = row.Table.Columns.Contains("LOT") ? row["LOT"]?.ToString() ?? "" : "";
                string status = row.Table.Columns.Contains("STATUS") ? row["STATUS"]?.ToString() ?? "" : "";
                lines.Add(string.Format("STT: {0}\nMã hàng: {1}\nLOT đã chọn: {2}\n{3}", stt, mh, lot, status));
            }

            return "CẢNH BÁO FIFO – TỒN KHO ĐÃ THAY ĐỔI\n\n" +
                   string.Join("\n\n--------------------\n\n", lines) +
                   "\n\nCác dòng FIFO không hợp lệ đã được lấy lại LOT và không được đưa vào lần cập nhật kho này. Vui lòng quét/chọn lại LOT FIFO hiện tại.";
        }
        private void OnTinhTongCompleted(TinhTongCompletedEvent e)
        {
            var results = e?.Results;
            _c.UiContext.Post(_ =>
            {
                DataTable current = _v.GetDonHangTable();
                ApplyTinhTongResults(current, results);
                _v.BindDonHang(current);
                _c.SetupPhieuButtonsDefault(true, false, _c.PhieuSvc.CheckCoLotChuaCNK(current));
            }, null);
        }

        private static void ApplyTinhTongResults(DataTable table, IReadOnlyList<(int Stt, string Lot)> results)
        {
            if (table == null || results == null || results.Count == 0 || !table.Columns.Contains("STT") || !table.Columns.Contains("LOT")) return;
            foreach (var result in results)
            {
                DataRow target = table.AsEnumerable().FirstOrDefault(r => string.Equals(r["STT"]?.ToString().Trim(), result.Stt.ToString(), StringComparison.OrdinalIgnoreCase));
                if (target != null) target["LOT"] = result.Lot ?? string.Empty;
            }
        }

        public void Dispose() { var v = _v; v.FormLoaded -= OnFormLoaded; v.DateChanged -= OnDateChanged; v.GioXuatChanged -= OnGioXuatChanged; v.TabChanged -= OnTabChanged; v.CapNhapKhoClicked -= OnCapNhapKho; v.InPhieuClicked -= OnInPhieu; v.InGhepLotClicked -= OnInGhepLot; v.InTachLotClicked -= OnInTachLot; v.KiemTraGhepLotClicked -= OnKiemTraGhepLot; v.KiemTraMaNGClicked -= OnKiemTraMaNG; v.HoanThanhClicked -= OnHoanThanh; v.LoaiPhieuChanged -= OnLoaiPhieuChanged; v.ChonLotThuCongClicked -= OnChonLotThuCong; v.XemHangThieuCaNgayClicked -= OnXemHangThieuCaNgay; v.LayLaiLotNoClicked -= OnLayLaiLotNo; v.CapNhapTTPHIEUClicked -= OnCapNhapTTPHIEU; v.NoteEdited -= OnNoteEdited; _c.Bus.Unsubscribe<PhieuLoadedEvent>(OnPhieuLoaded); _c.Bus.Unsubscribe<KhoUpdatedEvent>(OnKhoUpdated); _c.Bus.Unsubscribe<TinhTongCompletedEvent>(OnTinhTongCompleted); }
    }
}
