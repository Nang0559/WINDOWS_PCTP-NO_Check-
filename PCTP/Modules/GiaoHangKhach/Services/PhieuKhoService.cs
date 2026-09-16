using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Phase 7: business service cho nghiệp vụ cập nhật kho của phiếu giao.
    /// Không chứa UI; persistence vẫn do IPhieuRepository đảm nhiệm.
    ///
    /// FIFO policy:
    /// - FIFO kiểm tra nhanh tại UI/QR có thể dùng snapshot trong RAM.
    /// - Trước khi cập nhật kho phải reconcile lại với tồn kho thực tế.
    /// - Nếu FIFO đã thay đổi trong lúc đang đọc QR, các LOT hiện không còn hợp lệ
    ///   sẽ được release về trạng thái có thể đọc lại, thay vì để Usp_Update_Stock
    ///   reject toàn bộ phiếu và bắt người dùng xoá thủ công.
    /// - Stored procedure vẫn là lớp bảo vệ cuối cùng cho concurrency/bypass.
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
            DataTable fifoErrors;

            string tmpTable = _cfg.Delivery.GetTmpTable(isSP);
            string docQRTable = _cfg.Delivery.GetDocQRTable(isSP);

            try
            {
                // ============================================================
                // FINAL FIFO RECONCILE
                // ============================================================
                // Không chạy FIFO SQL ở từng lần quét QR vì sẽ làm máy quét lag.
                // Nhưng ngay trước khi update stock phải đối chiếu lại tồn kho thật.
                //
                // Ví dụ:
                //   lúc scan: B100,C100 -> RAM cho B
                //   trong lúc scan: A100 rework nhập lại
                //   lúc CNK: DB = A100,B100,C100 -> B không còn đúng FIFO
                //
                // Khi đó tự release B + QR liên quan để người dùng quét lại A.
                fifoErrors = ReconcileFifoBeforeStockUpdate(tmpTable, docQRTable);

                if (_cfg.Delivery.LoadTuBangRieng && !_cfg.Delivery.CoGear)
                {
                    soLot = _phieuRepo.CapNhapKhoHTN(
                        nhaMay,
                        tmpTable,
                        docQRTable,
                        out errors);
                }
                else
                {
                    soLot = _phieuRepo.CapNhapKho(
                        gioGiaoFcc,
                        nhaMay,
                        tmpTable,
                        docQRTable,
                        out errors);
                }

                errors = MergeErrors(fifoErrors, errors);

                if (errors != null && errors.Rows.Count > 0)
                {
                    foreach (DataRow r in errors.Rows)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[CapNhapKho ERROR] MH={r["MH"]}, LOT={r["LOT"]}, STATUS={r["STATUS"]}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CapNhapKho EXCEPTION] {ex.Message}");
                throw;
            }

            _bus.Publish(new KhoUpdatedEvent(soLot, errors));
        }

        /// <summary>
        /// Đối chiếu FIFO với tồn kho thực tế ngay trước CNK.
        ///
        /// Đây là điểm reconcile cuối của Application layer:
        /// 1. Đọc danh sách vi phạm FIFO một lần.
        /// 2. Map violation về STT thực tế trong TMP.
        /// 3. Release LOT + QR bằng primitive LayLaiLotNo hiện có.
        ///
        /// Không tự DELETE QR. LayLaiLotNo chịu trách nhiệm reset:
        /// TMP.LOT/STATUSDOC/TTPHIEU và DOCQR.GIO/KETQUA/STTBAN.
        /// </summary>
        private DataTable ReconcileFifoBeforeStockUpdate(
            string tmpTable,
            string docQRTable)
        {
            var violations = _phieuRepo.CheckFifoViolations(tmpTable);
            if (violations == null || violations.Count == 0)
                return null;

            DataTable current = _phieuRepo.GetDonHangHienTai(tmpTable);
            if (current == null || current.Rows.Count == 0)
                return violations.ToErrorTable();

            var released = new HashSet<int>();

            foreach (FifoViolation violation in violations)
            {
                if (violation == null)
                    continue;

                string maHang = (violation.MaHang ?? string.Empty).Trim();
                string lot = (violation.LotDaChon ?? string.Empty).Trim();

                foreach (DataRow row in current.Rows)
                {
                    string rowMaHang = row["MAHANG"]?.ToString()?.Trim() ?? string.Empty;
                    string rowLot = row["LOT"]?.ToString()?.Trim() ?? string.Empty;

                    if (!string.Equals(rowMaHang, maHang, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.Equals(rowLot, lot, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!int.TryParse(row["STT"]?.ToString(), out int stt) || stt <= 0)
                        continue;

                    if (!released.Add(stt))
                        continue;

                    _phieuRepo.LayLaiLotNo(stt, tmpTable, docQRTable);
                }
            }

            return violations.ToErrorTable();
        }

        private static DataTable MergeErrors(DataTable fifoErrors, DataTable stockErrors)
        {
            if (fifoErrors == null || fifoErrors.Rows.Count == 0)
                return stockErrors;

            if (stockErrors == null || stockErrors.Rows.Count == 0)
                return fifoErrors;

            DataTable merged = stockErrors.Clone();

            foreach (DataRow row in fifoErrors.Rows)
                merged.ImportRow(row);

            foreach (DataRow row in stockErrors.Rows)
                merged.ImportRow(row);

            return merged;
        }
    }
}
