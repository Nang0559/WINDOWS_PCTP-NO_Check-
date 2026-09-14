using System;
using PCTP.Common;
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

        public StockMovementService(IStockBalanceRepository balance, IStockSlotRepository slots)
            : this(balance, slots, null) { }

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
            if (request != null &&
                request.SlotId.HasValue &&
                request.SlotId.Value > 0 &&
                !string.IsNullOrWhiteSpace(request.LotNo))
            {
                if (request.Quantity <= 0)
                    return StockMovementResult.Fail("Quantity phải lớn hơn 0.");
                if (string.IsNullOrWhiteSpace(request.ItemCode))
                    return StockMovementResult.Fail("ItemCode không được rỗng khi Pick theo Slot + LOT.");

                try
                {
                    var take = _slots.TakeLot(
                        request.SlotId.Value,
                        request.LotNo,
                        request.ItemCode,
                        request.Quantity);

                    return StockMovementResult.OkWithConsumedLots(
                        take == null ? null : take.ExportLots,
                        "Đã pick LOT khỏi Slot.");
                }
                catch (Exception ex)
                {
                    return StockMovementResult.Fail("Lỗi pick LOT khỏi Slot: " + ex.Message);
                }
            }

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
                string sourceLotNo = _slots.GetLotNo(request.SlotLotId.Value);
                string sourceItemCode = _slots.GetItemCode(request.SlotLotId.Value);

                var sourceValidation = ValidateSourceLotIdentity(
                    request,
                    sourceLotNo,
                    sourceItemCode,
                    "Move");
                if (!sourceValidation.Success)
                    return sourceValidation;

                int available = _slots.GetLotQuantity(request.SlotLotId.Value);
                if (available < request.Quantity)
                    return StockMovementResult.Fail(
                        string.Format("SlotLot {0} chỉ còn {1}, không đủ {2}.",
                            request.SlotLotId.Value, available, request.Quantity));

                string itemCode = request.ItemCode;
                if (string.IsNullOrWhiteSpace(itemCode))
                    itemCode = sourceItemCode;
                if (string.IsNullOrWhiteSpace(itemCode))
                    return StockMovementResult.Fail("Không xác định được ItemCode của SlotLot nguồn.");

                string lotNo = request.LotNo;
                if (string.IsNullOrWhiteSpace(lotNo))
                    lotNo = sourceLotNo;

                _slots.DecreaseLotQuantity(request.SlotLotId.Value, request.Quantity);

                if (!string.IsNullOrWhiteSpace(lotNo))
                {
                    _slots.AddLot(request.TargetSlotId.Value, new StockSlotLot
                    {
                        LotNo = lotNo,
                        ItemCode = itemCode,
                        Quantity = request.Quantity,
                        ImportDate = request.OccurredAt ?? DateTime.Now
                    });
                }
                else
                {
                    _slots.AddQuantity(request.TargetSlotId.Value, request.Quantity, itemCode);
                }

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

        private StockMovementResult AddToSlot(StockMovementRequest request, bool adjustAvailable)
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

                if (isNormalReceive && _receiving != null)
                {
                    if (string.IsNullOrWhiteSpace(request.LotNo))
                        return StockMovementResult.Fail("LotNo không được rỗng khi nhập thành phẩm.");

                    int status = request.ReceivingStatus ?? 0;
                    if (_receiving.Exists(request.LotNo))
                        _receiving.Update(request.LotNo, request.Quantity, status);
                    else
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

                if (!string.IsNullOrWhiteSpace(request.LotNo))
                {
                    _slots.AddLot(request.TargetSlotId.Value, new StockSlotLot
                    {
                        LotNo = request.LotNo,
                        ItemCode = request.ItemCode,
                        Quantity = request.Quantity,
                        RawQr = null,
                        ImportDate = request.ProductionDate ?? request.OccurredAt ?? DateTime.Now
                    });
                }
                else
                {
                    _slots.AddQuantity(request.TargetSlotId.Value, request.Quantity, request.ItemCode);
                }

                if (adjustAvailable &&
                    !isQuarantineReceive &&
                    !(isNormalReceive && _receiving != null) &&
                    !string.IsNullOrWhiteSpace(request.LotNo))
                {
                    _balance.AdjustAvailableQuantity(request.LotNo, request.Quantity);
                }

                return StockMovementResult.Ok("Đã nhập tồn kho.");
            }
            catch (Exception ex)
            {
                return StockMovementResult.Fail("Lỗi nhập tồn kho: " + ex.Message);
            }
        }

        private StockMovementResult RemoveFromSlot(StockMovementRequest request, bool adjustAvailable)
        {
            if (request == null)
                return StockMovementResult.Fail("Stock movement request không được null.");
            if (request.Quantity <= 0)
                return StockMovementResult.Fail("Quantity phải lớn hơn 0.");
            if (string.IsNullOrWhiteSpace(request.LotNo))
                return StockMovementResult.Fail("LotNo không được rỗng.");

            try
            {
                if (!request.SlotLotId.HasValue)
                {
                    if (!adjustAvailable)
                        return StockMovementResult.Fail("Pick cần SlotLotId hoặc SlotId + LotNo.");

                    if (!_balance.TryDecreaseAvailableQuantity(request.LotNo, request.Quantity))
                        return StockMovementResult.Fail(
                            string.Format("STOCKTP LOT [{0}] không đủ hoặc đã thay đổi.", request.LotNo));

                    return StockMovementResult.Ok("Đã xuất tồn STOCKTP.");
                }

                if (request.SlotLotId.Value <= 0)
                    return StockMovementResult.Fail("SlotLotId không hợp lệ.");

                string sourceLotNo = _slots.GetLotNo(request.SlotLotId.Value);
                string sourceItemCode = _slots.GetItemCode(request.SlotLotId.Value);
                var sourceValidation = ValidateSourceLotIdentity(
                    request,
                    sourceLotNo,
                    sourceItemCode,
                    adjustAvailable ? "Export" : "Pick");
                if (!sourceValidation.Success)
                    return sourceValidation;

                int availableSlot = _slots.GetLotQuantity(request.SlotLotId.Value);
                if (availableSlot < request.Quantity)
                    return StockMovementResult.Fail(
                        string.Format("SlotLot {0} chỉ còn {1}, không đủ {2}.",
                            request.SlotLotId.Value, availableSlot, request.Quantity));

                if (adjustAvailable &&
                    !_balance.TryDecreaseAvailableQuantity(request.LotNo, request.Quantity))
                    return StockMovementResult.Fail(
                        string.Format("STOCKTP LOT [{0}] không đủ hoặc đã thay đổi.", request.LotNo));

                _slots.DecreaseLotQuantity(request.SlotLotId.Value, request.Quantity);
                return StockMovementResult.Ok("Đã xuất tồn kho.");
            }
            catch (Exception ex)
            {
                return StockMovementResult.Fail("Lỗi xuất tồn kho: " + ex.Message);
            }
        }

        private static StockMovementResult ValidateSourceLotIdentity(
            StockMovementRequest request,
            string sourceLotNo,
            string sourceItemCode,
            string operation)
        {
            if (string.IsNullOrWhiteSpace(sourceLotNo))
                return StockMovementResult.Fail(
                    string.Format("{0}: không xác định được LOT của SlotLot nguồn.", operation));

            if (!string.IsNullOrWhiteSpace(request.LotNo) &&
                !LotCodeHelper.AreLotKeysEquivalent(request.LotNo, sourceLotNo))
            {
                return StockMovementResult.Fail(
                    string.Format(
                        "{0}: LotNo request [{1}] không khớp LOT nguồn [{2}] của SlotLot {3}.",
                        operation,
                        request.LotNo,
                        sourceLotNo,
                        request.SlotLotId));
            }

            if (!string.IsNullOrWhiteSpace(request.ItemCode) &&
                !string.Equals(request.ItemCode.Trim(), sourceItemCode == null ? null : sourceItemCode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return StockMovementResult.Fail(
                    string.Format(
                        "{0}: ItemCode request [{1}] không khớp ItemCode nguồn [{2}] của SlotLot {3}.",
                        operation,
                        request.ItemCode,
                        sourceItemCode,
                        request.SlotLotId));
            }

            return StockMovementResult.Ok();
        }
    }
}
