using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Shared.Helpers;
using PCTP.Shared.Models;
using System;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public class DocQRService : IDocQRService
    {
        private readonly IOrderCategoryResolver _categoryResolver;
        private readonly DocQRSessionState _session;
        private readonly DocQRScanEngine _engine;
        private readonly FifoSessionService _fifoService;
        private readonly FifoSessionState _fifoState;
        private readonly IEventBus _bus;
        private bool _fifoInitialized;

        public bool IsBanSP => _session.IsBanSP;
        public bool IsBanOType => _session.IsBanOType;

        public DocQRService(
            IDocQRRepository repo,
            IEventBus bus,
            CustomerConfig cfg,
            IOrderCategoryResolver categoryResolver,
            FifoSessionService fifoService)
        {
            if (repo == null) throw new ArgumentNullException(nameof(repo));
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            _categoryResolver = categoryResolver ?? throw new ArgumentNullException(nameof(categoryResolver));
            _fifoService = fifoService ?? throw new ArgumentNullException(nameof(fifoService));
            _bus = bus;
            _session = new DocQRSessionState(cfg);
            _fifoState = new FifoSessionState();
            _engine = new DocQRScanEngine(repo, bus, cfg, _session);
            _bus.Subscribe<KhoUpdatedEvent>(OnKhoUpdated);
        }

        public void SetCheDoBanSP(bool isSP)
        {
            _session.SetCategory(isSP, _session.IsBanOType);
        }

        public void SetCheDoBan(string gioMoTa)
        {
            bool isSp = _categoryResolver.Resolve(new OrderLoadContext { GioFccMoTa = gioMoTa }) == OrderCategory.SP;
            bool isOType = GioMoTaCategoryResolver.IsLoaiOType(gioMoTa);
            _session.SetCategory(isSp, isOType);
        }

        public int CountChuaDG() => _engine.CountChuaDG();
        public bool CoDocQRNao() => _engine.CoDocQRNao();

        public DataTable LoadAll()
        {
            EnsureFifoInitialized();
            return _engine.LoadAll();
        }

        public void InitializeFifo(DataTable orderRows)
        {
            _fifoService.Initialize(_fifoState, orderRows);
            _fifoInitialized = true;
        }

        public void ResetFifo()
        {
            _fifoState.Reset();
            _fifoInitialized = false;
        }

        public void XoaDong(int stt) => _engine.XoaDong(stt);

        public void XoaToanBo()
        {
            _engine.XoaToanBo();
            ResetFifo();
        }

        public void CapNhapSlHvn(int stt, int slMoi)
            => _engine.CapNhapSlHvn(stt, slMoi);

        public ScanResult ProcessScan(
            string rawQr,
            Func<string, bool> kiemTraMaTrongPhieu,
            Func<string, int, bool> kiemTraSlDaBan)
        {
            EnsureFifoInitialized();
            return ApplyRamFifo(_engine.ProcessScan(rawQr, kiemTraMaTrongPhieu, kiemTraSlDaBan));
        }

        public ScanResult ProcessScanYMVN(
            string rawQr,
            Func<string, bool> kiemTraMaTrongPhieu,
            Func<string, int, bool> kiemTraSlDaBan)
        {
            EnsureFifoInitialized();
            return ApplyRamFifo(_engine.ProcessScanYMVN(rawQr, kiemTraMaTrongPhieu, kiemTraSlDaBan));
        }

        public ScanResult ConfirmSlKhacBiet(DocQRCode pending)
        {
            EnsureFifoInitialized();
            return ApplyRamFifo(_engine.ConfirmSlKhacBiet(pending));
        }

        private void EnsureFifoInitialized()
        {
            if (_fifoInitialized) return;
            _fifoService.InitializeFromTables(_fifoState, _session.TmpTable, _session.DocQrTable);
            _fifoInitialized = true;
        }

        private ScanResult ApplyRamFifo(ScanResult result)
        {
            if (result == null || !result.IsOK || result.Pending == null)
                return result;

            DocQRCode item = result.Pending;
            string maHang = !string.IsNullOrWhiteSpace(item.MaHangFCC) ? item.MaHangFCC : item.MaHangHVN;
            string lot = !string.IsNullOrWhiteSpace(item.LotFCC) ? item.LotFCC : item.LotHVN;
            int quantity = item.SlTemFCC > 0 ? item.SlTemFCC : item.SlTemHVN;

            string message;
            if (_fifoState.TryConsume(maHang, lot, quantity, out message))
                return result;

            _engine.XoaDong(item.STT);
            return ScanResult.FifoFail(item, message);
        }

        private void OnKhoUpdated(KhoUpdatedEvent e)
        {
            ResetFifo();
        }

        public bool KiemTraSlDaBan(string maHang, int slBan)
            => _engine.KiemTraSlDaBan(maHang, slBan);
    }
}
