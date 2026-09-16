using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Models;
using System;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Business service cho nghiệp vụ cập nhật kho của phiếu giao.
    /// FIFO được enforce tại repository boundary trước khi gọi stored procedure.
    /// </summary>
    public class PhieuKhoService : IPhieuKhoService
    {
        private readonly IPhieuRepository _phieuRepo;
        private readonly IEventBus _bus;
        private readonly CustomerConfig _cfg;

        public PhieuKhoService(
            IPhieuRepository phieuRepo,
            IEventBus bus,
            CustomerConfig cfg)
        {
            _phieuRepo = phieuRepo ?? throw new ArgumentNullException(nameof(phieuRepo));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        }

        public void CapNhapKho(string gioGiaoFcc, string nhaMay, string gioMa, bool isSP)
        {
            int soLot;
            DataTable errors;

            if (_cfg.Delivery.LoadTuBangRieng && !_cfg.Delivery.CoGear)
            {
                soLot = _phieuRepo.CapNhapKhoHTN(
                    nhaMay,
                    _cfg.Delivery.GetTmpTable(isSP),
                    _cfg.Delivery.GetDocQRTable(isSP),
                    out errors);
            }
            else
            {
                soLot = _phieuRepo.CapNhapKho(
                    gioGiaoFcc,
                    nhaMay,
                    _cfg.Delivery.GetTmpTable(isSP),
                    _cfg.Delivery.GetDocQRTable(isSP),
                    out errors);
            }

            _bus.Publish(new KhoUpdatedEvent(soLot, errors));
        }
    }
}