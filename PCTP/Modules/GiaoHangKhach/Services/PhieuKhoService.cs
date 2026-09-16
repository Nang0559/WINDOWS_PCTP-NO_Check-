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
            string tmpTable = _cfg.Delivery.GetTmpTable(isSP);
            string docQrTable = _cfg.Delivery.GetDocQRTable(isSP);

            int soLot = 0;
            DataTable errors = new DataTable();

            // DB is the authoritative final gate. If STOCKTP changed between
            // the first re-check and the repository's final re-check, release
            // the newly invalid rows and retry once with the remaining valid QR.
            for (int attempt = 0; attempt < 2; attempt++)
            {
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

                if (!ContainsFifoError(errors))
                    break;
            }

            _bus.Publish(new KhoUpdatedEvent(soLot, errors));
        }

        private static bool ContainsFifoError(DataTable errors)
        {
            if (errors == null || !errors.Columns.Contains("STATUS"))
                return false;

            foreach (DataRow row in errors.Rows)
            {
                string status = row["STATUS"]?.ToString() ?? string.Empty;
                if (status.IndexOf("FIFO:", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
