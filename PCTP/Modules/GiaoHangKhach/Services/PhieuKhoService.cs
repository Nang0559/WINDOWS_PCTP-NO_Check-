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
                // The dialog receives the exact violation snapshot that was evaluated.
                // OK/Cancel is therefore deterministic and cannot silently release a
                // different set of rows because FIFO was re-evaluated afterwards.
                var confirmation = new FifoReleaseConfirmationRequestedEvent(fifoViolations);
                _bus.Publish(confirmation);

                // Fail closed if the UI did not answer. Never continue to CNK in this state.
                if (!confirmation.IsCompleted || !confirmation.WaitForDecision())
                    return;

                // Phase 2: mutate only the rows the user actually confirmed.
                _phieuRepo.ReleaseFifoViolations(tmpTable, docQrTable, fifoViolations);
            }

            // Phase 3: CNK sees only the remaining valid rows. FIFO-invalid rows are
            // explicitly marked NG/detached from DOCQR by ReleaseFifoViolations.
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

            // Any shortage reported here belongs to the remaining valid candidates;
            // FIFO-invalid rows are no longer part of the CNK candidate set.
            _bus.Publish(new KhoUpdatedEvent(soLot, errors));
        }
    }
}
