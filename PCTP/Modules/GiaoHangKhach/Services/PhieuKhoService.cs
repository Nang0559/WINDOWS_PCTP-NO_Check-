using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public class PhieuKhoService : IPhieuKhoService
    {
        private readonly IPhieuRepository _phieuRepo;
        private readonly IEventBus _bus;
        private readonly CustomerConfig _cfg;

        public PhieuKhoService(IPhieuRepository phieuRepo, IEventBus bus, CustomerConfig cfg)
        {
            _phieuRepo = phieuRepo ?? throw new ArgumentNullException(nameof(phieuRepo));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        }

        public void CapNhapKho(string gioGiaoFcc, string nhaMay, string gioMa, bool isSP)
        {
            string tmpTable = _cfg.Delivery.GetTmpTable(isSP);
            string docQrTable = _cfg.Delivery.GetDocQRTable(isSP);

            // FIFO is the first-class gate. Evaluation is read-only.
            List<FifoViolation> fifoViolations =
                _phieuRepo.EvaluateFifoViolations(tmpTable, docQrTable)
                ?? new List<FifoViolation>();

            if (fifoViolations.Count > 0)
            {
                var confirmation = new FifoReleaseConfirmationRequestedEvent(fifoViolations);
                _bus.Publish(confirmation);

                // CANCEL means no LOT reset and no CNK/stock update.
                if (!confirmation.WaitForDecision())
                    return;

                // Re-check and release only after explicit OK. Invalid FIFO rows
                // are removed from the stock-update candidates by the repository.
                _phieuRepo.ReleaseFifoViolations(tmpTable, docQrTable);
            }

            int soLot;
            DataTable errors;

            if (_cfg.Delivery.LoadTuBangRieng && !_cfg.Delivery.CoGear)
            {
                soLot = _phieuRepo.CapNhapKhoHTN(nhaMay, tmpTable, docQrTable, out errors);
            }
            else
            {
                soLot = _phieuRepo.CapNhapKho(gioGiaoFcc, nhaMay, tmpTable, docQrTable, out errors);
            }

            // FIFO has already been resolved. Any stock shortage here belongs to
            // the remaining valid rows and must not be suppressed.
            _bus.Publish(new KhoUpdatedEvent(soLot, errors));
        }
    }
}
