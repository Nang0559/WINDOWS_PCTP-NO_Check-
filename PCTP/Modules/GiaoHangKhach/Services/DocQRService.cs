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

        public DocQRService(IDocQRRepository repo, IEventBus bus, CustomerConfig cfg, IOrderCategoryResolver categoryResolver, FifoSessionService fifoService)
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

        public void SetCheDoBanSP(bool isSP) => _session.SetCategory(isSP, _session.IsBanOType);

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

        public void XoaDong(int stt)
        {
            _fifoState.Release(stt);
            _engine.XoaDong(stt);
        }

        public void XoaToanBo()
        {
            _engine.XoaToanBo();
            ResetFifo();
        }

        public void CapNhapSlHvn(int stt, int slMoi)
        {
            EnsureFifoInitialized();
            DataTable current = _engine.LoadAll();
            DataRow row = null;
            foreach (DataRow item in current.Rows)
            {
                if (!item.Table.Columns.Contains("STT")) continue;
                if (int.TryParse(item["STT"]?.ToString(), out int currentStt) && currentStt == stt) { row = item; break; }
            }

            if (row == null) { _engine.CapNhapSlHvn(stt, slMoi); return; }

            string maHang = GetFirstValue(row, "MAHANGFCC", "MAHANGHVN");
            string lot = GetFirstValue(row, "LOTFCC", "LOTHVN");
            int oldQty = GetInt(row, "SLTEMFCC", GetInt(row, "SLTEMHVN", 0));

            _fifoState.Release(stt);
            string message;
            if (!_fifoState.TryConsume(stt, maHang, lot, slMoi, out message))
            {
                _fifoState.TryConsume(stt, maHang, lot, oldQty, out message);
                throw new InvalidOperationException(message);
            }

            try { _engine.CapNhapSlHvn(stt, slMoi); }
            catch
            {
                _fifoState.Release(stt);
                _fifoState.TryConsume(stt, maHang, lot, oldQty, out message);
                throw;
            }
        }

        public ScanResult ProcessScan(string rawQr, Func<string, bool> kiemTraMaTrongPhieu, Func<string, int, bool> kiemTraSlDaBan)
        {
            EnsureFifoInitialized();
            return ApplyRamFifo(_engine.ProcessScan(rawQr, kiemTraMaTrongPhieu, kiemTraSlDaBan));
        }

        public ScanResult ProcessScanYMVN(string rawQr, Func<string, bool> kiemTraMaTrongPhieu, Func<string, int, bool> kiemTraSlDaBan)
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
            if (result == null || !result.IsOK || result.Pending == null) return result;

            DocQRCode item = result.Pending;
            string maHang = !string.IsNullOrWhiteSpace(item.MaHangFCC) ? item.MaHangFCC : item.MaHangHVN;
            string lot = !string.IsNullOrWhiteSpace(item.LotFCC) ? item.LotFCC : item.LotHVN;
            int quantity = item.SlTemFCC > 0 ? item.SlTemFCC : item.SlTemHVN;

            string message;
            if (_fifoState.TryConsume(item.STT, maHang, lot, quantity, out message)) return result;

            _engine.XoaDong(item.STT);
            // Refresh the current QR grid after the rejected row has been removed.
            _bus.Publish(new QRScannedEvent(item, "FIFO_REJECTED"));
            return ScanResult.FifoFail(item, message);
        }

        private static string GetFirstValue(DataRow row, string first, string second)
        {
            if (row.Table.Columns.Contains(first) && row[first] != DBNull.Value && !string.IsNullOrWhiteSpace(row[first].ToString())) return row[first].ToString().Trim();
            if (row.Table.Columns.Contains(second) && row[second] != DBNull.Value) return row[second].ToString().Trim();
            return string.Empty;
        }

        private static int GetInt(DataRow row, string column, int fallback)
        {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return fallback;
            return int.TryParse(row[column].ToString(), out int value) ? value : fallback;
        }

        private void OnKhoUpdated(KhoUpdatedEvent e) => ResetFifo();
        public bool KiemTraSlDaBan(string maHang, int slBan) => _engine.KiemTraSlDaBan(maHang, slBan);
    }
}
