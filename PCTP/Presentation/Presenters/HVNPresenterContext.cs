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
        internal readonly IEventBus Bus;
        internal readonly CustomerConfig Cfg;
        internal readonly IOrderCategoryResolver CategoryResolver;
        internal readonly CustomerDeliveryBehavior CustomerBehavior;
        internal readonly bool IsMayBanQR;
        internal readonly string TenBan;
        internal readonly SynchronizationContext UiContext;
        internal GioXuat GioXuatHienTai = new GioXuat("'06'", "(6H)");
        internal int AddNM = 1;
        internal bool IsBanQR;
        internal bool IsLoadingPhieu;
        internal bool AwaitingPhieuLoadedEvent;
        internal DeliverySessionIdentity DeliverySession { get; private set; }
        private int _busy;

        internal HVNPresenterContext(IHVNView view, IPhieuService phieuSvc, IPhieuLotService lotSvc, IDocQRService qrSvc, IInPhieuService inPhieuSvc, IHangThieuCaNgayService hangThieuCaNgayService, IGioXuatRepository gioXuatRepo, IEventBus bus, bool isMayBanQR, string tenBan, CustomerConfig cfg, IOrderCategoryResolver categoryResolver)
        {
            View = view ?? throw new ArgumentNullException(nameof(view)); PhieuView = View; DocQrView = View; GiaoDbView = View; YmvnView = View;
            PhieuSvc = phieuSvc ?? throw new ArgumentNullException(nameof(phieuSvc)); LotSvc = lotSvc ?? throw new ArgumentNullException(nameof(lotSvc)); QrSvc = qrSvc ?? throw new ArgumentNullException(nameof(qrSvc)); InPhieuSvc = inPhieuSvc ?? throw new ArgumentNullException(nameof(inPhieuSvc)); HangThieuCaNgayService = hangThieuCaNgayService ?? throw new ArgumentNullException(nameof(hangThieuCaNgayService)); GioXuatRepo = gioXuatRepo ?? throw new ArgumentNullException(nameof(gioXuatRepo)); Bus = bus ?? throw new ArgumentNullException(nameof(bus)); Cfg = cfg ?? throw new ArgumentNullException(nameof(cfg)); CategoryResolver = categoryResolver ?? throw new ArgumentNullException(nameof(categoryResolver));
            CustomerBehavior = new CustomerDeliveryBehavior(cfg); IsMayBanQR = isMayBanQR; TenBan = tenBan; UiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
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
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
            View.ShowLoading(true, caption);
            Task.Run(() => { try { action(); } catch (Exception ex) { UiContext.Post(_ => View.ShowError("Lỗi hệ thống: " + ex.Message), null); } finally { Interlocked.Exchange(ref _busy, 0); UiContext.Post(_ => View.ShowLoading(false), null); } });
        }
        internal void RunWithLoadingSync(Action action, string caption = "Đang xử lý...")
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
            try { View.ShowLoading(true, caption); action(); } catch (Exception ex) { View.ShowError("Lỗi: " + ex.Message); } finally { Interlocked.Exchange(ref _busy, 0); HideLoadingUnlessAwaitingPhieuLoad(); }
        }
        internal void HideLoadingUnlessAwaitingPhieuLoad() { if (!AwaitingPhieuLoadedEvent) View.ShowLoading(false); }

        internal void LoadGioXuatYMVN()
        {
            var danhSachGio = PhieuSvc.GetDanhSachGioYMVN(View.SelectedDate.ToString("MM/dd/yyyy"));
            View.BindGioXuatCheckList(danhSachGio);

            var delivered = PhieuSvc.GetGioDaGiao(GetNhaMay(), View.SelectedDate.ToString("yyyy-MM-dd"));
            var deliveredSet = new HashSet<string>(delivered ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var deliveredDisplay = danhSachGio
                .Where(g => deliveredSet.Contains(NormalizeHour(g)))
                .ToList();
            View.SetCheckedGiosYMVN(deliveredDisplay);

            var selectable = danhSachGio
                .Where(g => !deliveredSet.Contains(NormalizeHour(g)))
                .ToList();

            if (selectable.Count > 0)
                UpdateGioXuatFromCheckList(selectable);
            else
                GioXuatHienTai = new GioXuat("", "");
        }
        internal void UpdateGioXuatFromCheckList(List<string> danhSachGio)
        {
            var hours = danhSachGio.Select(g => NormalizeHour(g)).Where(h => !string.IsNullOrEmpty(h)).Distinct().OrderBy(h => h).ToList();
            GioXuatHienTai = new GioXuat(string.Join(",", hours.Select(h => $"'{h}'")), string.Join("+", danhSachGio) + "H");
        }
        internal List<string> ParseGioYMVN(string gioDonTuDB)
        {
            if (string.IsNullOrWhiteSpace(gioDonTuDB)) return new List<string>();
            return gioDonTuDB.Replace("H", "").Split(new[] { ',', '+' }, StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).Where(g => !string.IsNullOrEmpty(g)).ToList();
        }
        private static string NormalizeHour(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string s = value.Replace("H", "").Trim(); int colon = s.IndexOf(':'); if (colon >= 0) s = s.Substring(0, colon);
            int hour; return int.TryParse(s, out hour) ? hour.ToString("00") : "";
        }
        internal void LoadPhieuHienTai()
        {
            // QR mode owns an immutable session context. Never reload TMP/DOCQR
            // if the UI has drifted to another date/plant/hour/category.
            if (IsBanQR && !IsCurrentDeliverySessionValid())
            {
                System.Diagnostics.Debug.WriteLine(
                    "[LoadPhieuHienTai] BLOCKED: UI context does not match active QR delivery session.");
                return;
            }

            if (IsLoadingPhieu) return;
            IsLoadingPhieu = true;
            string ngayGiao = ""; string nhaMay = ""; List<string> checkedGios = null; bool isLoaiSP = false;
            string gioMa = GioXuatHienTai.Ma; string gioMoTa = GioXuatHienTai.MoTa;
            Action readUiAction = () =>
            {
                ngayGiao = Cfg.Delivery.CoGear ? View.SelectedDate.ToString("MM/dd/yyyy") : View.SelectedDate.ToString("yyyy-MM-dd");
                if (Cfg.Delivery.CoGear) { checkedGios = View.GetCheckedGioXuat(); } else nhaMay = GetNhaMay();
                if (Cfg.Delivery.CoGear || Cfg.Delivery.CoLoaiSP) isLoaiSP = View.IsLoaiSP;
            };
            if (UiContext == SynchronizationContext.Current) readUiAction(); else UiContext.Send(_ => readUiAction(), null);
            try
            {
                AwaitingPhieuLoadedEvent = true;
                PhieuSvc.LoadPhieu(ngayGiao, nhaMay, gioMa, gioMoTa, AddNM, IsMayBanQR, IsBanQR, checkedGios, isLoaiSP);
            }
            catch (Exception ex)
            {
                AwaitingPhieuLoadedEvent = false; IsLoadingPhieu = false;
                UiContext.Post(_ => { View.ShowLoading(false); View.ShowError($"Lỗi tải phiếu: {ex.Message}"); }, null);
            }
        }
        internal void SetupPhieuButtonsDefault(bool showCapNhapKho = false, bool showKiemTraMaNG = false, bool showLayLaiLot = false, bool showStop = false)
        {
            bool coMaNG = showKiemTraMaNG || PhieuSvc.CheckCoMaNG(); View.SetupPhieuButtons(showCapNhapKho && IsMayBanQR, coMaNG && IsMayBanQR, IsMayBanQR, IsMayBanQR, showLayLaiLot && IsMayBanQR && !IsBanQR, showStop, !Cfg.Delivery.LoadTuBangRieng);
        }
        internal DataTable LoadPhieuGiaoDB()
        {
            DataTable dt = PhieuSvc.LoadTmpPhieuGiaoDB(View.SelectedDate, AddNM); View.BindDonHang(dt); GiaoDbView.SwitchToPhieuDBView(); return dt;
        }
    }
}