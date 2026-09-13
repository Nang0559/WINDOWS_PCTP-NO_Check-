using System;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;

namespace PCTP.Modules.KhoCore.Application.Services
{
    /// <summary>
    /// Central stock mutation service.
    /// It owns stock invariants while storage adapters remain outside KhoCore.
    /// The caller owns the surrounding transaction so workflow audit writes can
    /// participate in the same UnitOfWork.
    /// </summary>
    public sealed class StockMovementService : IStockMovementService
    {
        private const string MovementReworkNgReceive = "REWORK_NG_RECEIVE";

        private readonly IStockBalanceRepository _balance;
        private readonly IStockSlotRepository _slots;

        public StockMovementService(
            IStockBalanceRepository balance,
            IStockSlotRepository slots)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _slots = slots ?? throw new ArgumentNullException(nameof(slots));
        }

        public StockMovementResult Receive(StockMovementRequest request)
        {
            return AddToSlot(request, true);
        }

        public StockMovementResult Reserve(StockMovementRequest request)
        {
            return StockMovementResult.Fail("Reserve chưa được triển khai cho storage hiện tại.");
        }

        public StockMovementResult Pick(StockMovementRequest request)
        {
            return RemoveFromSlot(request, false);
        }

        public StockMovementResult Export(StockMovementRequest request)
        {
            return RemoveFromSlot(request, true);
        }

        public StockMovementResult Move(StockMovementRequest request)
        {
            if (request == null)
                return StockMovementResult.Fail("Stock movement request không được null.");
            if (!request.SlotLotId.HasValue || request.SlotLotId.Value <= 0)
                return StockMovementResult.Fail("SlotLotId không hợp lệ.");
            if (!request.TargetSlotId.HasValue || request.TargetSlotId.Value <= 0)
                return StockMovementResult.Fail("TargetSlotId không hợp lệ.");
            if (request.Quantity <= 0)
                return StockMovementResult.Fail("Quantity phải lớn hơn 0.");

            try
            {
                int available = _slots.GetLotQuantity(request.SlotLotId.Value);
                if (available < request.Quantity)
                    return StockMovementResult.Fail(
                        string.Format("SlotLot {0} chỉ còn {1}, không đủ {2}.",
                            request.SlotLotId.Value, available, request.Quantity));

                string itemCode = request.ItemCode;
                if (string.IsNullOrWhiteSpace(itemCode))
                    itemCode = _slots.GetItemCode(request.SlotLotId.Value);

                _slots.DecreaseLotQuantity(request.SlotLotId.Value, request.Quantity);
                _slots.AddQuantity(request.TargetSlotId.Value, request.Quantity, itemCode);

                return StockMovementResult.Ok("Đã di chuyển tồn kho.");
            }
            catch (Exception ex)
            {
                return StockMovementResult.Fail("Lỗi di chuyển tồn kho: " + ex.Message);
            }
        }

        public StockMovementResult ReturnFromRework(StockMovementRequest request)
        {
            return AddToSlot(request, true);
        }

        public StockMovementResult Correct(StockMovementRequest request)
        {
            if (request == null)
                return StockMovementResult.Fail("Stock movement request không được null.");
            if (string.IsNullOrWhiteSpace(request.LotNo))
                return StockMovementResult.Fail("LotNo không được rỗng.");
            if (request.Quantity == 0)
                return StockMovementResult.Fail("Quantity không được bằng 0.");

            try
            {
                _balance.AdjustAvailableQuantity(request.LotNo, request.Quantity);
                return StockMovementResult.Ok("Đã điều chỉnh tồn STOCKTP.");
            }
            catch (Exception ex)
            {
                return StockMovementResult.Fail("Lỗi điều chỉnh tồn: " + ex.Message);
            }
        }

        private StockMovementResult AddToSlot(
            StockMovementRequest request,
            bool adjustAvailable)
        {
            if (request == null)
                return StockMovementResult.Fail("Stock movement request không được null.");
            if (!request.TargetSlotId.HasValue || request.TargetSlotId.Value <= 0)
                return StockMovementResult.Fail("TargetSlotId không hợp lệ.");
            if (request.Quantity <= 0)
                return StockMovementResult.Fail("Quantity phải lớn hơn 0.");
            if (string.IsNullOrWhiteSpace(request.ItemCode))
                return StockMovementResult.Fail("ItemCode không được rỗng.");

            try
            {
                _slots.AddQuantity(
                    request.TargetSlotId.Value,
                    request.Quantity,
                    request.ItemCode);

                // NG sau rework được giữ trong khu NG/quarantine và tuyệt đối
                // không làm tăng STOCKTP khả dụng.
                bool isQuarantineReceive =
                    string.Equals(
                        request.MovementType,
                        MovementReworkNgReceive,
                        StringComparison.OrdinalIgnoreCase);

                if (adjustAvailable &&
                    !isQuarantineReceive &&
                    !string.IsNullOrWhiteSpace(request.LotNo))
                {
                    _balance.AdjustAvailableQuantity(
                        request.LotNo,
                        request.Quantity);
                }

                return StockMovementResult.Ok("Đã nhập tồn kho.");
            }
            catch (Exception ex)
            {
                return StockMovementResult.Fail("Lỗi nhập tồn kho: " + ex.Message);
            }
        }

        private StockMovementResult RemoveFromSlot(
            StockMovementRequest request,
            bool adjustAvailable)
        {
            if (request == null)
                return StockMovementResult.Fail("Stock movement request không được null.");
            if (!request.SlotLotId.HasValue || request.SlotLotId.Value <= 0)
                return StockMovementResult.Fail("SlotLotId không hợp lệ.");
            if (request.Quantity <= 0)
                return StockMovementResult.Fail("Quantity phải lớn hơn 0.");
            if (string.IsNullOrWhiteSpace(request.LotNo))
                return StockMovementResult.Fail("LotNo không được rỗng.");

            try
            {
                int availableSlot = _slots.GetLotQuantity(request.SlotLotId.Value);
                if (availableSlot < request.Quantity)
                    return StockMovementResult.Fail(
                        string.Format("SlotLot {0} chỉ còn {1}, không đủ {2}.",
                            request.SlotLotId.Value, availableSlot, request.Quantity));

                if (adjustAvailable &&
                    !_balance.TryDecreaseAvailableQuantity(request.LotNo, request.Quantity))
                {
                    return StockMovementResult.Fail(
                        string.Format("STOCKTP LOT [{0}] không đủ hoặc đã thay đổi.", request.LotNo));
                }

                _slots.DecreaseLotQuantity(request.SlotLotId.Value, request.Quantity);
                return StockMovementResult.Ok("Đã xuất tồn kho.");
            }
            catch (Exception ex)
            {
                return StockMovementResult.Fail("Lỗi xuất tồn kho: " + ex.Message);
            }
        }
    }
}
