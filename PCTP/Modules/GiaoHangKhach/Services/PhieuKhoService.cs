using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Models;
using System;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Services
{
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

            string tmpTable = _cfg.Delivery.GetTmpTable(isSP);
            string docQrTable = _cfg.Delivery.GetDocQRTable(isSP);

            // Final authoritative DB FIFO check/recovery immediately before CNK.
            // Invalid QR rows are released (LOT = '' + QR released) and are not
            // allowed to enter Usp_Qrcode_Update_Stock2405. The repository below
            // performs the same FIFO check once more as the final safety gate.
            _phieuRepo.ReleaseFifoViolations(tmpTable, docQrTable);

            if (_cfg.Delivery.LoadTuBangRieng && !_cfg.Delivery.CoGear)
            {
                soLot = _phieuRepo.CapNhapKhoHTN(
                    nhaMay,
                    tmpTable,
                    docQrTable,
                    out errors);
            }
            else
            {
                soLot = _phieuRepo.CapNhapKho(
                    gioGiaoFcc,
                    nhaMay,
                    tmpTable,
                    docQrTable,
                    out errors);
            }

            _bus.Publish(new KhoUpdatedEvent(soLot, errors));
        }
    }
}
