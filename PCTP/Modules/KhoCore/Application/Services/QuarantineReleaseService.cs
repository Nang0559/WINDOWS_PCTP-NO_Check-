using System;
using PCTP.Common;
using PCTP.Shared.Common;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Repositories;

namespace PCTP.Modules.KhoCore.Application.Services
{
    /// <summary>
    /// Atomic quarantine release boundary.
    /// A release is allowed only after QC disposition and authorization. The
    /// quarantined row is converted to an active SlotLot in the same UnitOfWork,
    /// STOCKTP is increased once, and the audit row is written before commit.
    /// </summary>
    public sealed class QuarantineReleaseService
    {
        public const byte ActiveStatus = 0;
        public const byte QuarantineStatus = 3;

        private readonly IStockSlotRepository _slots;
        private readonly IStockBalanceRepository _balance;
        private readonly IQuarantineReleaseAuditRepository _audit;
        private readonly IQuarantineReleaseAuthorization _authorization;
        private readonly IUnitOfWork _uow;

        public QuarantineReleaseService(
            IStockSlotRepository slots,
            IStockBalanceRepository balance,
            IQuarantineReleaseAuditRepository audit,
            IQuarantineReleaseAuthorization authorization,
            IUnitOfWork uow)
        {
            _slots = slots ?? throw new ArgumentNullException(nameof(slots));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        }

        public QuarantineReleaseResult Release(QuarantineReleaseRequest request)
        {
            if (request == null) return QuarantineReleaseResult.Fail("Release request không được null.");
            if (request.SlotLotId <= 0) return QuarantineReleaseResult.Fail("SlotLotId không hợp lệ.");
            if (string.IsNullOrWhiteSpace(request.PerformedBy)) return QuarantineReleaseResult.Fail("Người thực hiện release là bắt buộc.");
            if (!request.QcDispositionApproved)
                return QuarantineReleaseResult.Fail("Chưa có kết luận QC được phép release.");
            if (!_authorization.CanRelease(request.PerformedBy.Trim()))
                return QuarantineReleaseResult.Fail("Tài khoản không có quyền release hàng cách ly.", unauthorized: true);

            // Read the source Slot once to determine the lock target. The status and
            // quantity are re-read only after that Slot is locked inside the transaction.
            var initialDetails = _slots.GetLotDetails(request.SlotLotId);
            if (initialDetails == null)
                return QuarantineReleaseResult.Fail($"Không tìm thấy SlotLot {request.SlotLotId}.");
            var slotId = _slots.GetSlotId(request.SlotLotId);
            if (!slotId.HasValue || slotId.Value <= 0)
                return QuarantineReleaseResult.Fail("Không xác định được Slot của LOT cách ly.");

            try
            {
                _uow.Begin();
                _slots.LockSlotForUpdate(slotId.Value);

                // A second operator may have released this SlotLot while this request
                // waited for the Slot lock. Re-read under the lock before any mutation.
                var details = _slots.GetLotDetails(request.SlotLotId);
                if (details == null || details.PhieuStatus != QuarantineStatus)
                {
                    _uow.Rollback();
                    return QuarantineReleaseResult.Fail($"SlotLot {request.SlotLotId} không còn ở trạng thái cách ly; có thể đã được xử lý bởi giao dịch khác.");
                }
                if (details.Quantity <= 0)
                {
                    _uow.Rollback();
                    return QuarantineReleaseResult.Fail("LOT cách ly không còn số lượng để release.");
                }
                var currentSlotId = _slots.GetSlotId(request.SlotLotId);
                if (!currentSlotId.HasValue || currentSlotId.Value != slotId.Value)
                {
                    _uow.Rollback();
                    return QuarantineReleaseResult.Fail("Vị trí SlotLot đã thay đổi trong lúc release. Vui lòng tải lại.");
                }

                // Remove the quarantined quantity first. AddLot below creates the active
                // representation in the same transaction; a failure anywhere rolls back both.
                _slots.DecreaseLotQuantity(request.SlotLotId, details.Quantity);
                _slots.AddLot(slotId.Value, new StockSlotLot
                {
                    LotNo = details.LotNo,
                    ItemCode = details.ItemCode,
                    Quantity = details.Quantity,
                    TemCode = details.TemCode,
                    RawQr = details.RawQr,
                    ImportDate = details.ImportDate,
                    PhieuStatus = ActiveStatus
                });
                _balance.AdjustAvailableQuantity(details.LotNo, details.Quantity);
                _audit.RecordRelease(
                    request.SlotLotId,
                    slotId.Value,
                    details.LotNo,
                    details.ItemCode,
                    details.Quantity,
                    request.PerformedBy.Trim(),
                    request.ReferenceId,
                    request.Reason);

                _uow.Commit();
                return QuarantineReleaseResult.Ok("Đã release LOT cách ly và đồng bộ tồn kho trong cùng transaction.");
            }
            catch (Exception ex)
            {
                try { _uow.Rollback(); } catch { }
                return QuarantineReleaseResult.Fail("Release thất bại; toàn bộ thay đổi đã được rollback: " + ex.Message);
            }
        }
    }
}
