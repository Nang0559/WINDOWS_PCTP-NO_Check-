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

        public void CapNhapKho(string gioGiaoFcc, string nhaMay, string gioMa, bool isSP, bool isGiaoDb = false)
        {
            // Giao DB có bộ bảng riêng (TMPPHIEUGIAOHANGDB + DOCQRCODE_DB) và đi nhánh
            // MP (không phải SP). Không có config Giao DB -> giữ nguyên bảng MP/SP.
            bool useGiaoDbTables = isGiaoDb && _cfg.Delivery.CoConfigGiaoDB;
            if (useGiaoDbTables) isSP = false;

            string tmpTable = _cfg.Delivery.GetTmpTable(isSP, useGiaoDbTables);
            string docQrTable = _cfg.Delivery.GetDocQRTable(isSP, useGiaoDbTables);

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

            if (isSP)
            {
                // SP chỉ cập nhật kho cho các dòng ĐÃ có LOT (đã thống kê QR). Dòng chưa
                // có LOT bị proc bỏ qua, không tự gán LOT FIFO và không ghi đè LOT đã có.
                // Phần còn lại dùng chung pipeline QR/ledger/TMP và SlotLot movement service.
                soLot = _phieuRepo.CapNhapKhoSP(gioGiaoFcc, nhaMay, tmpTable, docQrTable, out errors);
            }
            else if (_cfg.Delivery.LoadTuBangRieng && !_cfg.Delivery.CoGear)
            {
                soLot = _phieuRepo.CapNhapKhoHTN(nhaMay, tmpTable, docQrTable, out errors);
            }
            else
            {
                soLot = _phieuRepo.CapNhapKho(gioGiaoFcc, nhaMay, tmpTable, docQrTable, out errors);
            }

            // Giao DB: đơn nguồn là FVN_Purchase_Order_DB_CT (STATUS='NG' = chưa giao). Dòng vừa giao phải được
            // ghi 'OK' + LOT về đó, nếu không lần Upload/nạp lại sau sẽ dựng lại nó thành NG, LOT trống.
            if (useGiaoDbTables)
            {
                try { _phieuRepo.DongBoDaGiaoVaoDonGiaoDB(tmpTable); }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Đã cập nhật kho nhưng chưa ghi được trạng thái đã giao vào đơn Giao DB: " + ex.Message, ex);
                }
            }

            _bus.Publish(new KhoUpdatedEvent(soLot, errors));
        }
    }
}
