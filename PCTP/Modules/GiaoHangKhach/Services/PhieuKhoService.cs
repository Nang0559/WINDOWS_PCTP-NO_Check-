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
            string tmpTable = _cfg.Delivery.GetTmpTable(isSP);
            string docQrTable = _cfg.Delivery.GetDocQRTable(isSP);

            // ================================================================
            // FIFO IS THE FIRST-CLASS GATE
            // ================================================================
            // Evaluate first. This operation MUST NOT modify LOT/TMP/DOCQR.
            // If FIFO is wrong, stock validation is deliberately not executed.
            // The UI receives the complete affected-row list and decides whether
            // the invalid rows may be released.
            // ================================================================
            List<FifoViolation> fifoViolations =
                _phieuRepo.EvaluateFifoViolations(tmpTable, docQrTable)
                ?? new List<FifoViolation>();

            if (fifoViolations.Count > 0)
            {
                var confirmation = new FifoReleaseConfirmationRequestedEvent(fifoViolations);
                _bus.Publish(confirmation);

                // CANCEL (or no UI confirmation handler) means absolutely no
                // LOT reset and no CNK/stock stored procedure.
                if (!confirmation.Confirmed)
                    return;

                // The repository re-checks FIFO immediately before releasing.
                // Only after explicit OK are invalid rows reset so they are no
                // longer candidates for the stock update.
                _phieuRepo.ReleaseFifoViolations(tmpTable, docQrTable);
            }

            int soLot;
            DataTable errors;

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

            // FIFO has already been resolved before this point. Therefore any
            // stock shortage returned now belongs to the remaining valid rows
            // and must NOT be suppressed.
            _bus.Publish(new KhoUpdatedEvent(soLot, errors));
        }
    }
}
