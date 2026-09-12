using PCTP.Domain.Entities;
using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Shared.Models;
using System;
using System.Data;

namespace PCTP.Applications.Services
{
    /// <summary>
    /// Thin facade for QR delivery operations.
    /// Keeps the existing public API while delegating QR parsing/business rules
    /// to DocQRScanEngine.
    /// </summary>
    public sealed class DocQRService
    {
        private readonly IOrderCategoryResolver _categoryResolver;
        private readonly DocQRSessionState _session;
        private readonly DocQRScanEngine _engine;

        public bool IsBanSP => _session.IsBanSP;
        public bool IsBanOType => _session.IsBanOType;

        public DocQRService(IDocQRRepository repo, IEventBus bus, CustomerConfig cfg, IOrderCategoryResolver categoryResolver)
        {
            if (repo == null) throw new ArgumentNullException(nameof(repo));
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            _categoryResolver = categoryResolver ?? throw new ArgumentNullException(nameof(categoryResolver));
            _session = new DocQRSessionState(cfg);
            _engine = new DocQRScanEngine(repo, bus, cfg, _session);
        }

        // Backward compatible API.
        public void SetCheDoBanSP(bool isSP)
        {
            _session.SetCategory(isSP, _session.IsBanOType);
        }

        // Existing presenter entry point: resolve SP/O TYPE from gioMoTa.
        public void SetCheDoBan(string gioMoTa)
        {
            bool isSp = _categoryResolver.Resolve(new OrderLoadContext { GioFccMoTa = gioMoTa }) == OrderCategory.SP;
            bool isOType = GioMoTaCategoryResolver.IsLoaiOType(gioMoTa);
            _session.SetCategory(isSp, isOType);
        }

        public int CountChuaDG() => _engine.CountChuaDG();
        public bool CoDocQRNao() => _engine.CoDocQRNao();
        public DataTable LoadAll() => _engine.LoadAll();
        public void XoaDong(int stt) => _engine.XoaDong(stt);
        public void XoaToanBo() => _engine.XoaToanBo();
        public void CapNhapSlHvn(int stt, int slMoi) => _engine.CapNhapSlHvn(stt, slMoi);

        public ScanResult ProcessScan(string rawQr, Func<string, bool> kiemTraMaTrongPhieu, Func<string, int, bool> kiemTraSlDaBan)
            => _engine.ProcessScan(rawQr, kiemTraMaTrongPhieu, kiemTraSlDaBan);

        public ScanResult ProcessScanYMVN(string rawQr, Func<string, bool> kiemTraMaTrongPhieu, Func<string, int, bool> kiemTraSlDaBan)
            => _engine.ProcessScanYMVN(rawQr, kiemTraMaTrongPhieu, kiemTraSlDaBan);

        public ScanResult ConfirmSlKhacBiet(DocQRCode pending)
            => _engine.ConfirmSlKhacBiet(pending);

        public bool KiemTraSlDaBan(string maHang, int slBan)
            => _engine.KiemTraSlDaBan(maHang, slBan);
    }
}