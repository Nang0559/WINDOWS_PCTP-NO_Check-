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

            // PHASE 1: FIFO is a hard business gate. Nothing that can update stock
            // is allowed to execute before this phase has completed successfully.
            List<FifoViolation> fifoViolations =
                _phieuRepo.EvaluateFifoViolations(tmpTable, docQrTable)
                ?? new List<FifoViolation>();

            if (fifoViolations.Count > 0)
            {
                // Publish() is asynchronous. FIFO confirmation is a blocking gate,
                // therefore the confirmation event MUST be synchronous here.
                var confirmation = new FifoReleaseConfirmationRequestedEvent(fifoViolations);
                _bus.PublishSynchronous(confirmation);

                // Fail closed: Cancel, missing UI handler, or incomplete confirmation
                // means NO stock validation and NO stock update.
                if (!confirmation.IsCompleted || !confirmation.WaitForDecision())
                    return;

                // Release only the exact STT values returned by FIFO evaluation.
                // This clears the invalid QR rows from the current CNK candidate set.
                _phieuRepo.ReleaseFifoViolations(tmpTable, docQrTable, fifoViolations);

                // PHASE 2: rebuild the candidate set after release.
                // This is intentionally re-evaluated immediately before stock update.
                // If any FIFO violation remains, fail closed and DO NOT call the stock SP.
                List<FifoViolation> remainingViolations =
                    _phieuRepo.EvaluateFifoViolations(tmpTable, docQrTable)
                    ?? new List<FifoViolation>();

                if (remainingViolations.Count > 0)
                    return;
            }

            // PHASE 3: only now is stock update allowed.
            // Usp_Qrcode_Update_Stock2405 (inside CapNhapKho/CapNhapKhoHTN)
            // is therefore downstream of the FIFO gate.
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
