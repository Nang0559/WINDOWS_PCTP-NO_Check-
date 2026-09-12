using PCTP.Applications.Services;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Infrastructure.Repositories;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
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
        internal readonly PhieuService PhieuSvc;
        internal readonly DocQRService QrSvc;
        internal readonly InPhieuService InPhieuSvc;
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

        internal HVNPresenterContext(IHVNView view, PhieuService phieuSvc, DocQRService qrSvc, InPhieuService inPhieuSvc, IHangThieuCaNgayService hangThieuCaNgayService, IGioXuatRepository gioXuatRepo, IEventBus bus, bool isMayBanQR, string tenBan, CustomerConfig cfg, IOrderCategoryResolver categoryResolver)
        {
            View = view ?? throw new ArgumentNullException(nameof(view)); PhieuSvc = phieuSvc ?? throw new ArgumentNullException(nameof(phieuSvc)); QrSvc = qrSvc ?? throw new ArgumentNullException(nameof(qrSvc)); InPhieuSvc = inPhieuSvc ?? throw new ArgumentNullException(nameof(inPhieuSvc)); HangThieuCaNgayService = hangThieuCaNgayService ?? throw new ArgumentNullException(nameof(hangThieuCaNgayService)); GioXuatRepo = gioXuatRepo ?? throw new ArgumentNullException(nameof(gioXuatRepo)); Bus = bus ?? throw new ArgumentNullException(nameof(bus)); Cfg = cfg ?? throw new ArgumentNullException(nameof(cfg)); CategoryResolver = categoryResolver ?? throw new ArgumentNullException(nameof(categoryResolver)); CustomerBehavior = new CustomerDeliveryBehavior(cfg); IsMayBanQR = isMayBanQR; TenBan = tenBan; UiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        }
        internal void UpdateGioXuat(GioXuat gio) { GioXuatHienTai = gio; }
        internal string GetNhaMay() => !Cfg.Delivery.CoNhieuNhaMay ? Cfg.Delivery.TenNhaMay : (AddNM == 1 ? "HON DA - VIET NAM(NHA MAY VP)" : "HON DA - VIET NAM(NHA MAY HA NAM)");
        internal void RunWithLoading(Action action, string caption = "Đang xử lý...")
        {
            View.ShowLoading(true, caption); Task.Run(() => { try { action(); } catch (Exception ex) { UiContext.Post(_ => View.ShowError("Lỗi hệ thống: " + ex.Message), null); } finally { UiContext.Post(_ => View.ShowLoading(false), null); } });
        }
        internal void RunWithLoadingSync(Action action, string caption = "Đang xử lý...")
        {
            try { View.ShowLoading(true, caption); Application.DoEvents(); action(); }
            catch (Exception ex) { View.ShowError("Lỗi: " + ex.Message); }
            finally { HideLoadingUnlessAwaitingPhieuLoad(); }
        }
        internal void HideLoadingUnlessAwaitingPhieuLoad() { if (!AwaitingPhieuLoadedEvent) View.ShowLoading(false); }
        internal void LoadGioXuatYMVN()
        {
            var danhSachGio = PhieuSvc.GetDanhSachGioYMVN(View.SelectedDate.ToString("MM/dd/yyyy")); View.BindGioXuatCheckList(danhSachGio); if (danhSachGio.Count > 0) UpdateGioXuatFromCheckList(danhSachGio);
        }
        internal void UpdateGioXuatFromCheckList(List<string> danhSachGio)
        {
            var hours = danhSachGio.Select(g => g.Split(':')[0].PadLeft(2, '0')).Distinct().OrderBy(h => h).ToList();
            GioXuatHienTai = new GioXuat(string.Join(",", hours.Select(h => $"'{h}'")), string.Join("+", danhSachGio) + "H");
        }
        internal List<string> ParseGioYMVN(string gioDonTuDB)
        {
            if (string.IsNullOrWhiteSpace(gioDonTuDB)) return new List<string>();
            return gioDonTuDB.Replace("H", "").Split(new[] { ',', '+' }, StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).Where(g => !string.IsNullOrEmpty(g)).ToList();
        }
        internal void LoadPhieuHienTai()
        {
            if (IsLoadingPhieu) return; IsLoadingPhieu = true;
            string ngayGiao = "", nhaMay = ""; List<string> checkedGios = null; bool isLoaiSP = false; string gioMa = GioXuatHienTai.Ma, gioMoTa = GioXuatHienTai.MoTa;
            Action readUiAction = () => { ngayGiao = Cfg.Delivery.CoGear ? View.SelectedDate.ToString("MM/dd/yyyy") : View.SelectedDate.ToString("yyyy-MM-dd"); if (Cfg.Delivery.CoGear) { checkedGios = View.GetCheckedGioXuat(); isLoaiSP = View.IsLoaiSP; } else nhaMay = GetNhaMay(); };
            if (UiContext == SynchronizationContext.Current) readUiAction(); else UiContext.Send(_ => readUiAction(), null);
            try { AwaitingPhieuLoadedEvent = true; if (Cfg.Delivery.LoadTuBangRieng) PhieuSvc.LoadPhieuTuBangRieng_Internal(ngayGiao, Cfg.Delivery.CoGear ? checkedGios : null, isLoaiSP, IsMayBanQR, IsBanQR); else PhieuSvc.LoadPhieu(ngayGiao, nhaMay, gioMa, gioMoTa, AddNM, IsMayBanQR, IsBanQR); }
            catch (Exception ex) { AwaitingPhieuLoadedEvent = false; IsLoadingPhieu = false; UiContext.Post(_ => { View.ShowLoading(false); View.ShowError($"Lỗi tải phiếu: {ex.Message}"); }, null); }
        }
        internal void SetupPhieuButtonsDefault(bool showCapNhapKho = false, bool showKiemTraMaNG = false, bool showLayLaiLot = false, bool showStop = false)
        {
            bool coMaNG = showKiemTraMaNG || PhieuSvc.CheckCoMaNG(); View.SetupPhieuButtons(showCapNhapKho && IsMayBanQR, coMaNG && IsMayBanQR, IsMayBanQR, IsMayBanQR, showLayLaiLot && IsMayBanQR && !IsBanQR, showStop, !Cfg.Delivery.LoadTuBangRieng);
        }
        internal DataTable LoadPhieuGiaoDB() { DataTable dt = PhieuSvc.LoadTmpPhieuGiaoDB(View.SelectedDate, AddNM); View.BindDonHang(dt); View.SwitchToPhieuDBView(); return dt; }
    }
}
