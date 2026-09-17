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

            // Phase 1: read-only authoritative FIFO evaluation.
            List<FifoViolation> fifoViolations =
                _phieuRepo.EvaluateFifoViolations(tmpTable, docQrTable)
                ?? new List<FifoViolation>();

            if (fifoViolations.Count > 0)
            {
                // IMPORTANT: normal Publish() is asynchronous in InProcessEventBus.
                // FIFO confirmation is a blocking business gate, so it must wait until
                // the UI handler has actually returned OK/Cancel.
                var confirmation = new FifoReleaseConfirmationRequestedEvent(fifoViolations);
                _bus.PublishSynchronous(confirmation);

                // Fail closed if no UI handler exists or the user cancelled.
                if (!confirmation.IsCompleted || !confirmation.WaitForDecision())
                    return;

                // Phase 2: mutate only the exact rows shown and confirmed by the user.
                _phieuRepo.ReleaseFifoViolations(tmpTable, docQrTable, fifoViolations);
            }

            // Phase 3: CNK sees only the remaining valid rows.
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

            _bus.Publish(new KhoUpdatedEvent(soLot, errors));
        }
    }
}
