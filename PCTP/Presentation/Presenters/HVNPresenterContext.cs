using PCTP.Applications.Services;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Infrastructure.Repositories;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Presentation.Views;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Presentation.Presenters
{
    internal sealed class HVNPresenterContext
    {
        internal readonly IHVNView View;
        internal readonly IPhieuView PhieuView;
        internal readonly IDocQrView DocQrView;
        internal readonly IGiaoDbView GiaoDbView;
        internal readonly IYmvnView YmvnView;
        internal readonly IPhieuService PhieuSvc;
        internal readonly IPhieuLotService LotSvc;
        internal readonly IDocQRService QrSvc;
        internal readonly IInPhieuService InPhieuSvc;
        internal readonly IHangThieuCaNgayService HangThieuCaNgayService;
        internal readonly IGioXuatRepository GioXuatRepo;

        /// <summary>
        /// Session QR có thể chứa nhiều giờ giao cụ thể (khung (17+18+19)H có dòng giờ 17 và 19).
        /// Hợp lệ khi tất cả các giờ đó thuộc cùng MỘT khung giờ của nhà máy đang bắn.
        /// </summary>
        internal bool SessionHoursShareOneFrame(TrangThaiBan tt, bool isSP)
        {
            if (tt == null || tt.GioGiaoTatCa == null || tt.GioGiaoTatCa.Count <= 1)
                return true;
            if (isSP || Cfg.Delivery.CoGear || Cfg.Delivery.LoadTuBangRieng)
                return true;

            int addNm = Cfg.Delivery.CoNhieuNhaMay ? tt.AddNM : Cfg.Delivery.AddNmMacDinh;
            var frames = addNm == 1 ? GioXuatRepo.GetDanhSachGioVP() : GioXuatRepo.GetDanhSachGioHN();
            foreach (var frame in frames)
            {
                if (DeliverySessionIdentity.ContainsAllHours(frame.Ma, tt.GioGiaoTatCa))
                    return true;
            }

            return false;
        }
        internal readonly IEventBus Bus;
        internal readonly CustomerConfig Cfg;
        internal readonly IOrderCategoryResolver CategoryResolver;
        internal readonly CustomerDeliveryBehavior CustomerBehavior;
        internal readonly bool IsMayBanQR;
        /// <summary>Khoá đọc QR theo khách; null khi dựng presenter trong test/không có khoá.</summary>
        internal readonly IMachinePermissionService MachinePermission;
        internal readonly string TenBan;
        internal readonly SynchronizationContext UiContext;
        internal GioXuat GioXuatHienTai = new GioXuat("'06'", "(6H)");
        internal int AddNM = 1;
        internal bool IsBanQR;
        internal bool IsLoadingPhieu;
        internal bool AwaitingPhieuLoadedEvent;

        // ── LoadRequestId: "trọng tài" duy nhất chọn kết quả tải phiếu nào
        // được áp dụng lên UI. LoadPhieuHienTai() KHÔNG chặn lệnh gọi chồng
        // (không còn early-return khi IsLoadingPhieu=true) — mọi lệnh gọi đều
        // được phép bắn PhieuSvc.LoadPhieu(...), và OnPhieuLoaded (bên
        // PhieuPresenter) chỉ bind lên UI kết quả có LoadRequestId khớp với
        // LatestLoadRequestId tại thời điểm event đến; kết quả trễ của yêu cầu
        // cũ bị bỏ qua. Đánh đổi: 2 (hoặc hơn) lệnh SQL có thể chạy THẬT SỰ
        // song song trên cùng 1 PhieuService/SQLPROVIDER — nếu SQLPROVIDER
        // không thread-safe, đây là đường mở lại nguy cơ "already open
        // DataReader" khi 2 connection cùng lúc đụng pool. Cân nhắc thêm hàng
        // đợi tuần tự (channel/queue) ở tầng PhieuService nếu vẫn còn gặp lỗi
        // connection sau khi đã xác nhận LoadRequestId hoạt động đúng.
        private long _loadRequestSequence;
        internal long BeginLoadRequest() => Interlocked.Increment(ref _loadRequestSequence);
        internal long LatestLoadRequestId => Interlocked.Read(ref _loadRequestSequence);

        internal DeliverySessionIdentity DeliverySession { get; private set; }

        private int _busy;

        internal HVNPresenterContext(IHVNView view, IPhieuService phieuSvc, IPhieuLotService lotSvc, IDocQRService qrSvc, IInPhieuService inPhieuSvc, IHangThieuCaNgayService hangThieuCaNgayService, IGioXuatRepository gioXuatRepo, IEventBus bus, bool isMayBanQR, string tenBan, CustomerConfig cfg, IOrderCategoryResolver categoryResolver, IMachinePermissionService machinePermission = null)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            PhieuView = View;
            DocQrView = View;
            GiaoDbView = View;
            YmvnView = View;
            PhieuSvc = phieuSvc ?? throw new ArgumentNullException(nameof(phieuSvc));
            LotSvc = lotSvc ?? throw new ArgumentNullException(nameof(lotSvc));
            QrSvc = qrSvc ?? throw new ArgumentNullException(nameof(qrSvc));
            InPhieuSvc = inPhieuSvc ?? throw new ArgumentNullException(nameof(inPhieuSvc));
            HangThieuCaNgayService = hangThieuCaNgayService ?? throw new ArgumentNullException(nameof(hangThieuCaNgayService));
            GioXuatRepo = gioXuatRepo ?? throw new ArgumentNullException(nameof(gioXuatRepo));
            Bus = bus ?? throw new ArgumentNullException(nameof(bus));
            Cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            CategoryResolver = categoryResolver ?? throw new ArgumentNullException(nameof(categoryResolver));
            CustomerBehavior = new CustomerDeliveryBehavior(cfg);
            IsMayBanQR = isMayBanQR;
            MachinePermission = machinePermission;
            TenBan = tenBan;
            UiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        }

        internal void UpdateGioXuat(GioXuat gio)
        {
            GioXuatHienTai = gio;
        }

        internal void BeginDeliverySession(DateTime ngayGiao, int addNM, string gioGiao, string nhaMay, bool isSP)
        {
            DeliverySession = new DeliverySessionIdentity(addNM, ngayGiao, gioGiao, nhaMay, isSP);
        }

        internal void ClearDeliverySession()
        {
            DeliverySession = null;
        }

        internal bool IsCurrentDeliverySessionValid()
        {
            if (!IsBanQR)
                return true;

            if (DeliverySession == null)
                return false;

            return DeliverySession.Matches(
                View.SelectedDate,
                AddNM,
                GioXuatHienTai != null ? GioXuatHienTai.Ma : string.Empty,
                GetNhaMay(),
                Cfg.Delivery.CoGear || Cfg.Delivery.CoLoaiSP && PhieuView.IsLoaiSP);
        }

        internal string GetNhaMay() => !Cfg.Delivery.CoNhieuNhaMay ? Cfg.Delivery.TenNhaMay : (AddNM == 1 ? "HON DA - VIET NAM(NHA MAY VP)" : "HON DA - VIET NAM(NHA MAY HA NAM)");

        internal void RunWithLoading(Action action, string caption = "Đang xử lý...")
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                return;

            View.ShowLoading(true, caption);
            Task.Run(() =>
            {
                try { action(); }
                catch (Exception ex) { UiContext.Post(_ => View.ShowError("Lỗi hệ thống: " + ex.Message), null); }
                finally
                {
                    Interlocked.Exchange(ref _busy, 0);
                    UiContext.Post(_ => View.ShowLoading(false), null);
                }
            });
        }

        internal void RunWithLoadingSync(Action action, string caption = "Đang xử lý...")
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                return; // đang có thao tác khác chạy dở -> bỏ qua để tránh chồng lệnh SQL

            try
            {
                View.ShowLoading(true, caption);
                action();
            }
            catch (Exception ex) { View.ShowError("Lỗi: " + ex.Message); }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
                HideLoadingUnlessAwaitingPhieuLoad();
            }
        }

        internal void HideLoadingUnlessAwaitingPhieuLoad() { if (!AwaitingPhieuLoadedEvent) View.ShowLoading(false); }

        internal void LoadGioXuatYMVN()
        {
            // This method may run on a Task.Run thread (RunWithLoading): read UI state on the UI thread.
            DateTime selectedDate = DateTime.MinValue;
            if (UiContext == SynchronizationContext.Current) selectedDate = View.SelectedDate;
            else UiContext.Send(_ => selectedDate = View.SelectedDate, null);

            var danhSachGio = PhieuSvc.GetDanhSachGioYMVN(PCTP.Shared.Helpers.DateText.ToMdy(selectedDate));
            View.BindGioXuatCheckList(danhSachGio);

            var delivered = PhieuSvc.GetGioDaGiao(GetNhaMay(), PCTP.Shared.Helpers.DateText.ToIso(selectedDate));
            var deliveredSet = new HashSet<string>(
                (delivered ?? new List<string>()).Select(NormalizeYmvTime).Where(g => !string.IsNullOrEmpty(g)),
                StringComparer.OrdinalIgnoreCase);
            var deliveredDisplay = danhSachGio
                .Where(g => deliveredSet.Contains(NormalizeYmvTime(g)))
                .ToList();
            View.SetCheckedGiosYMVN(deliveredDisplay);

            // Keep the internal hour context in sync with the actual checked
            // items. The UI checklist is the single source of truth; do not
            // calculate a different set by excluding delivered slots here.
            var selected = View.GetCheckedGioXuat();
            UpdateGioXuatFromCheckList(selected);
        }

        internal void UpdateGioXuatFromCheckList(List<string> danhSachGio)
        {
            // YMVN checkbox represents a concrete delivery slot (HH:mm), not
            // only an integer hour. Preserve minutes in the QR session identity
            // so two slots such as 11:20 and 11:40 cannot be treated as one.
            var slots = (danhSachGio ?? new List<string>())
                .Select(NormalizeYmvTime)
                .Where(h => !string.IsNullOrEmpty(h))
                .Distinct()
                .OrderBy(h => h, StringComparer.OrdinalIgnoreCase)
                .ToList();

            GioXuatHienTai = new GioXuat(
                string.Join(",", slots.Select(h => $"'{h}'")),
                slots.Count == 0 ? string.Empty : string.Join("+", slots) + "H");
        }

        internal List<string> ParseGioYMVN(string gioDonTuDB)
        {
            if (string.IsNullOrWhiteSpace(gioDonTuDB)) return new List<string>();
            return gioDonTuDB.Replace("H", "").Split(new[] { ',', '+' }, StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).Where(g => !string.IsNullOrEmpty(g)).ToList();
        }

        private static string NormalizeYmvTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            string text = value.Replace("H", string.Empty).Trim().Trim('\'');
            TimeSpan time;
            if (text.IndexOf(':') >= 0 && TimeSpan.TryParse(text, out time))
                return time.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);

            int hour;
            return int.TryParse(text, out hour) && hour >= 0 && hour <= 23
                ? hour.ToString("00")
                : string.Empty;
        }

        private static string NormalizeHour(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string s = value.Replace("H", "").Trim(); int colon = s.IndexOf(':'); if (colon >= 0) s = s.Substring(0, colon);
            int hour; return int.TryParse(s, out hour) ? hour.ToString("00") : "";
        }

        internal void LoadPhieuHienTai()
        {
            // Chế độ QR sở hữu 1 "phiên giao hàng" bất biến. Không được tải
            // lại TMP/DOCQR nếu UI đã trôi sang ngày/nhà máy/giờ/loại khác so
            // với phiên đang khoá.
            if (IsBanQR && !IsCurrentDeliverySessionValid())
            {
                System.Diagnostics.Debug.WriteLine(
                    "[LoadPhieuHienTai] BLOCKED: UI context does not match active QR delivery session.");
                return;
            }

            // Không chặn lệnh gọi mới chỉ vì còn 1 kết quả cũ đang chờ bind lên
            // UI — LoadRequestId sẽ quyết định kết quả nào là "chính thức".
            IsLoadingPhieu = true;

            string ngayGiao = "";
            string nhaMay = "";
            List<string> checkedGios = null;
            bool isLoaiSP = false;

            // Do NOT snapshot GioXuatHienTai before reading the category.
            // For customer 100001, MP owns the hour (e.g. 14H/15H), while
            // SP is date-only. When the user clicks XEM SP, CurrentGioXuat
            // must be removed from this load context before PhieuService
            // creates OrderLoadContext.
            string gioMa = string.Empty;
            string gioMoTa = string.Empty;
            long loadRequestId = BeginLoadRequest();

            Action readUiAction = () =>
            {
                ngayGiao = Cfg.Delivery.CoGear
                    ? PCTP.Shared.Helpers.DateText.ToMdy(View.SelectedDate)
                    : PCTP.Shared.Helpers.DateText.ToIso(View.SelectedDate);

                if (Cfg.Delivery.CoGear)
                    checkedGios = View.GetCheckedGioXuat();
                else
                    nhaMay = GetNhaMay();

                if (Cfg.Delivery.CoGear || Cfg.Delivery.CoLoaiSP)
                    isLoaiSP = View.IsLoaiSP;

                if (Cfg.Delivery.CoLoaiSP && isLoaiSP)
                {
                    // SP has no delivery-hour identity.
                    gioMa = string.Empty;
                    gioMoTa = string.Empty;
                    GioXuatHienTai = new GioXuat(string.Empty, string.Empty);
                }
                else
                {
                    gioMa = GioXuatHienTai != null ? GioXuatHienTai.Ma : string.Empty;
                    gioMoTa = GioXuatHienTai != null ? GioXuatHienTai.MoTa : string.Empty;
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[LoadPhieuHienTai] category={(isLoaiSP ? "SP" : "MP")}, " +
                    $"gioMa='{gioMa}', gioMoTa='{gioMoTa}', " +
                    $"ngay='{ngayGiao}', nhaMay='{nhaMay}', addNM={AddNM}");
            };

            if (UiContext == SynchronizationContext.Current)
                readUiAction();
            else
                UiContext.Send(_ => readUiAction(), null);

            try
            {
                AwaitingPhieuLoadedEvent = true;
                PhieuSvc.LoadPhieu(
                    ngayGiao,
                    nhaMay,
                    gioMa,
                    gioMoTa,
                    AddNM,
                    IsMayBanQR,
                    IsBanQR,
                    checkedGios,
                    isLoaiSP,
                    loadRequestId);
            }
            catch (Exception ex)
            {
                AwaitingPhieuLoadedEvent = false;
                IsLoadingPhieu = false;

                UiContext.Post(_ =>
                {
                    View.ShowLoading(false);
                    View.ShowError($"Lỗi tải phiếu: {ex.Message}");
                }, null);
            }
        }

        internal void SetupPhieuButtonsDefault(bool showCapNhapKho = false, bool showKiemTraMaNG = false, bool showLayLaiLot = false, bool showStop = false)
        {
            bool coMaNG = showKiemTraMaNG || PhieuSvc.CheckCoMaNG();
            // YMVN ghép LOT is a planning/check action, not a QR-scanner-only action.
            // Keep it available for MP on read-only/view terminals as well; SP has
            // a different schema and LoadGhepLot() intentionally returns an empty table.
            bool showGhepLot = IsMayBanQR || (Cfg.Delivery.CoGear && !PhieuView.IsLoaiSP);
            View.SetupPhieuButtons(
                showCapNhapKho && IsMayBanQR,
                coMaNG && IsMayBanQR,
                showGhepLot,
                IsMayBanQR,
                showLayLaiLot && IsMayBanQR && !IsBanQR,
                showStop,
                !Cfg.Delivery.LoadTuBangRieng);
        }

        internal DataTable LoadPhieuGiaoDB()
        {
            DataTable dt = PhieuSvc.LoadTmpPhieuGiaoDB(View.SelectedDate, AddNM);
            View.BindDonHang(dt);
            GiaoDbView.SwitchToPhieuDBView();
            return dt;
        }
    }
}