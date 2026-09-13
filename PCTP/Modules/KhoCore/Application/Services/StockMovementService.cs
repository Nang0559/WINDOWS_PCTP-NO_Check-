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
        private readonly IStockBalanceRepository _balance;
        private readonly IStockSlotRepository _slots;
        private readonly IStockReceivingRepository _receiving;

        public StockMovementService(
            IStockBalanceRepository balance,
            IStockSlotRepository slots)
            : this(balance, slots, null)
        {
        }

        public StockMovementService(
            IStockBalanceRepository balance,
            IStockSlotRepository slots,
            IStockReceivingRepository receiving)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _slots = slots ?? throw new ArgumentNullException(nameof(slots));
            _receiving = receiving;
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

                if (string.IsNullOrWhiteSpace(itemCode))
                    return StockMovementResult.Fail("Không xác định được ItemCode của SlotLot nguồn.");

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
                bool isQuarantineReceive = string.Equals(
                    request.MovementType,
                    StockMovementRequest.Types.ReworkNgReceive,
                    StringComparison.OrdinalIgnoreCase);

                bool isNormalReceive = string.Equals(
                    request.MovementType,
                    StockMovementRequest.Types.Receive,
                    StringComparison.OrdinalIgnoreCase);

                // Normal finished-goods receiving owns STOCKTP creation/update here.
                // Rework receive paths intentionally use the balance adapter rules below.
                if (isNormalReceive && _receiving != null)
                {
                    if (string.IsNullOrWhiteSpace(request.LotNo))
                        return StockMovementResult.Fail("LotNo không được rỗng khi nhập thành phẩm.");

                    int status = request.ReceivingStatus ?? 0;
                    if (_receiving.Exists(request.LotNo))
                    {
                        _receiving.Update(
                            request.LotNo,
                            request.Quantity,
                            status);
                    }
                    else
                    {
                        _receiving.Insert(new StockReceivingRecord
                        {
                            LotNo = request.LotNo,
                            ItemCode = request.ItemCode,
                            ItemName = request.ItemName,
                            Model = request.Model,
                            ProductionCase = request.ProductionCase,
                            ProductionDate = request.ProductionDate,
                            ProductionQuantity = request.ProductionQuantity,
                            ReceivedQuantity = request.Quantity,
                            Status = status
                        });
                    }
                }

                _slots.AddQuantity(
                    request.TargetSlotId.Value,
                    request.Quantity,
                    request.ItemCode);

                if (adjustAvailable &&
                    !isQuarantineReceive &&
                    !(isNormalReceive && _receiving != null) &&
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
