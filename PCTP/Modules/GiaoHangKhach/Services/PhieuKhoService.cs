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

            int soLot;
            DataTable errors;

            // ================================================================
            // FIFO IS THE FIRST-CLASS GATE
            // ================================================================
            // For FIFO-enabled parts, an invalid FIFO selection is a hard
            // business condition. ReleaseFifoViolations() clears LOT on the
            // invalid TMP rows so they cannot enter the stock update SP.
            //
            // IMPORTANT:
            // Do not run/retry the old CheckFifoViolations hard-stop here.
            // The new flow is exactly:
            //     FIFO release -> stock update -> stock validation
            //
            // A FIFO-blocked part is NOT allowed to produce a secondary
            // "insufficient stock" message. Otherwise the user can interpret
            // the problem as merely a stock shortage and miss the real gate:
            // the LOT selection is not FIFO-compliant.
            // ================================================================
            List<FifoViolation> fifoViolations =
                _phieuRepo.ReleaseFifoViolations(tmpTable, docQrTable)
                ?? new List<FifoViolation>();

            HashSet<string> fifoBlockedParts = BuildFifoBlockedParts(fifoViolations);

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

            // FIFO has priority over inventory validation for FIFO-enabled
            // parts. Keep stock errors for non-FIFO parts, but suppress only
            // inventory-shortage errors belonging to a part that was blocked
            // by FIFO in this CNK operation.
            errors = SuppressStockErrorsForFifoBlockedParts(
                errors,
                fifoBlockedParts);

            // FIFO errors are appended last so the UI presents the real root
            // cause after any stock rows have been filtered.
            if (fifoViolations.Count > 0)
                errors = MergeErrors(fifoViolations.ToErrorTable(), errors);

            _bus.Publish(new KhoUpdatedEvent(soLot, errors));
        }

        private static HashSet<string> BuildFifoBlockedParts(
            IEnumerable<FifoViolation> violations)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (violations == null) return result;

            foreach (FifoViolation violation in violations)
            {
                string maHang = violation?.MaHang?.Trim();
                if (!string.IsNullOrEmpty(maHang))
                    result.Add(maHang);
            }

            return result;
        }

        private static DataTable SuppressStockErrorsForFifoBlockedParts(
            DataTable errors,
            HashSet<string> fifoBlockedParts)
        {
            if (errors == null || errors.Rows.Count == 0 ||
                fifoBlockedParts == null || fifoBlockedParts.Count == 0)
                return errors ?? new DataTable();

            string partColumn = FindColumn(
                errors,
                "MAHANG",
                "MH",
                "Mã Hàng",
                "MaHang");

            string errorColumn = FindColumn(
                errors,
                "STATUS",
                "Lỗi",
                "LOI",
                "ERROR",
                "Ms");

            if (string.IsNullOrEmpty(partColumn) || string.IsNullOrEmpty(errorColumn))
                return errors;

            var remove = new List<DataRow>();
            foreach (DataRow row in errors.Rows)
            {
                string maHang = row[partColumn]?.ToString()?.Trim() ?? string.Empty;
                if (!fifoBlockedParts.Contains(maHang))
                    continue;

                string message = row[errorColumn]?.ToString()?.Trim() ?? string.Empty;
                if (IsInventoryShortageError(message))
                    remove.Add(row);
            }

            foreach (DataRow row in remove)
                errors.Rows.Remove(row);

            return errors;
        }

        private static bool IsInventoryShortageError(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            return message.IndexOf("tồn kho", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("ton kho", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("thiếu tồn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("thieu ton", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FindColumn(DataTable table, params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                foreach (DataColumn column in table.Columns)
                {
                    if (string.Equals(
                        column.ColumnName,
                        candidate,
                        StringComparison.OrdinalIgnoreCase))
                        return column.ColumnName;
                }
            }

            return string.Empty;
        }

        private static DataTable MergeErrors(DataTable fifoErrors, DataTable errors)
        {
            if (fifoErrors == null || fifoErrors.Rows.Count == 0)
                return errors ?? new DataTable();
            if (errors == null || errors.Rows.Count == 0)
                return fifoErrors;

            foreach (DataColumn column in fifoErrors.Columns)
                if (!errors.Columns.Contains(column.ColumnName))
                    errors.Columns.Add(column.ColumnName, column.DataType);

            foreach (DataRow source in fifoErrors.Rows)
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
