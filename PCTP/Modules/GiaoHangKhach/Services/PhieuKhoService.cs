using PCTP.Domain.Entities;
using PCTP.Domain.Events;
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

            int soLot = 0;
            DataTable errors = new DataTable();
            var releasedFifoWarnings = new List<FifoViolation>();

            // DB is the authoritative final gate. Before each stock update attempt,
            // release QR rows that no longer satisfy the actual STOCKTP FIFO state.
            // Their LOT becomes empty, so Usp_Qrcode_Update_Stock2405 cannot process
            // those rows. Valid rows remain eligible for the same stock update.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                List<FifoViolation> released = _phieuRepo.ReleaseFifoViolations(tmpTable, docQrTable);
                if (released != null && released.Count > 0)
                    releasedFifoWarnings.AddRange(released);

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

            if (releasedFifoWarnings.Count > 0)
                errors = MergeErrors(releasedFifoWarnings.ToErrorTable(), errors);

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

        private static DataTable MergeErrors(DataTable fifoWarnings, DataTable errors)
        {
            if (fifoWarnings == null || fifoWarnings.Rows.Count == 0)
                return errors ?? new DataTable();
            if (errors == null || errors.Rows.Count == 0)
                return fifoWarnings;

            foreach (DataColumn column in fifoWarnings.Columns)
                if (!errors.Columns.Contains(column.ColumnName))
                    errors.Columns.Add(column.ColumnName, column.DataType);

            foreach (DataRow source in fifoWarnings.Rows)
            {
                DataRow target = errors.NewRow();
                foreach (DataColumn column in errors.Columns)
                {
                    if (source.Table.Columns.Contains(column.ColumnName))
                        target[column.ColumnName] = source[column.ColumnName];
                }
                errors.Rows.Add(target);
            }

            return errors;
        }
    }
}
