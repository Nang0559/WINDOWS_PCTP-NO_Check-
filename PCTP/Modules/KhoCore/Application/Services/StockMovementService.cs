using System;
using PCTP.Common;
using PCTP.Shared.Common;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Infrastructure.Security;

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
        private readonly IUnitOfWork _uow;

        public StockMovementService(IStockBalanceRepository balance, IStockSlotRepository slots)
            : this(balance, slots, null, null) { }

        public StockMovementService(
            IStockBalanceRepository balance,
            IStockSlotRepository slots,
            IStockReceivingRepository receiving)
            : this(balance, slots, receiving, null) { }

        public StockMovementService(
            IStockBalanceRepository balance,
            IStockSlotRepository slots,
            IStockReceivingRepository receiving,
            IUnitOfWork uow)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _slots = slots ?? throw new ArgumentNullException(nameof(slots));
            _receiving = receiving;
            _uow = uow;
        }

        public StockMovementResult Receive(StockMovementRequest request)
        {
            AuthorizationGuard.DemandActionAndScope("WAREHOUSE.RECEIPT", "CONFIRM", "SITE", _balance.SiteCode, _balance.SiteCode);
            return AddToSlot(request, true);
        }

        public StockMovementResult Reserve(StockMovementRequest request)
        {
            AuthorizationGuard.DemandActionAndScope("WAREHOUSE.STOCK_VIEW", "CONFIRM", "SITE", _balance.SiteCode, _balance.SiteCode);
            return StockMovementResult.Fail("Reserve chưa được triển khai cho storage hiện tại.");
        }

        public StockMovementResult Pick(StockMovementRequest request)
        {
            AuthorizationGuard.DemandActionAndScope("SHIPPING.DISPATCH", "CONFIRM", "SITE", _balance.SiteCode, _balance.SiteCode);
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
            AuthorizationGuard.DemandActionAndScope("SHIPPING.DISPATCH", "CONFIRM", "SITE", _balance.SiteCode, _balance.SiteCode);
            return RemoveFromSlot(request, true);
        }

        public StockMovementResult Move(StockMovementRequest request)
        {
            AuthorizationGuard.DemandActionAndScope("WAREHOUSE.MOVE", "CONFIRM", "SITE", _balance.SiteCode, _balance.SiteCode);
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
                byte sourceStatus = _slots.GetPhieuStatus(request.SlotLotId.Value);
                if (sourceStatus != 0)
                    return StockMovementResult.Fail($"Không thể di chuyển SlotLot {request.SlotLotId.Value}: trạng thái {sourceStatus} không phải Active; hàng NG cách ly không được xuất hoặc di chuyển như tồn khả dụng.");

                var sourceDetails = _slots.GetLotDetails(request.SlotLotId.Value);
                if (sourceDetails == null)
                    return StockMovementResult.Fail($"Không tìm thấy SlotLot nguồn {request.SlotLotId.Value}.");
                string sourceLotNo = sourceDetails.LotNo;
                string sourceItemCode = sourceDetails.ItemCode;

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

                if (!TryClaimRequest(request, false))
                    return StockMovementResult.AlreadyProcessed("Mã tham chiếu di chuyển kho đã được xử lý trước đó.");

                string itemCode = request.ItemCode;
                if (string.IsNullOrWhiteSpace(itemCode))
                    itemCode = sourceItemCode;
                if (string.IsNullOrWhiteSpace(itemCode))
                    return StockMovementResult.Fail("Không xác định được ItemCode của SlotLot nguồn.");

                string lotNo = request.LotNo;
                if (string.IsNullOrWhiteSpace(lotNo))
                    lotNo = sourceLotNo;

                // MES ledger là nguồn tồn: MOVE (SiteCode, PartNo, MaterialLotId) từ Slot nguồn sang Slot đích.
                var moveKey = _balance.ResolveLot(itemCode, sourceLotNo, false);
                if (moveKey == null)
                    return StockMovementResult.Fail($"LOT [{sourceLotNo}] của mã [{itemCode}] chưa có trong MES.FVN_MaterialLot tại site {_balance.SiteCode}.");
                int? moveFromSlot = _slots.GetSlotId(request.SlotLotId.Value);
                if (!moveFromSlot.HasValue || moveFromSlot.Value <= 0)
                    return StockMovementResult.Fail("Không xác định được Slot nguồn của SlotLot.");
                long moveTxn = _balance.Post(new StockLedgerEntry
                {
                    MovementType = "MOVE",
                    Lot = moveKey,
                    Quantity = request.Quantity,
                    FromSlotId = moveFromSlot,
                    ToSlotId = request.TargetSlotId,
                    ReferenceType = LedgerReferenceType(request, "MOVE"),
                    ReferenceNo = request.ReferenceId,
                    IdempotencyKey = LedgerIdempotencyKey(request, "MOVE", moveKey, moveFromSlot, request.TargetSlotId),
                    ActorCode = LedgerActor(request),
                    OccurredAtUtc = ToUtc(request.OccurredAt)
                });

                _slots.DecreaseLotQuantity(request.SlotLotId.Value, request.Quantity);

                if (!string.IsNullOrWhiteSpace(lotNo))
                {
                    _slots.AddLot(request.TargetSlotId.Value, new StockSlotLot
                    {
                        LotNo = lotNo,
                        ItemCode = itemCode,
                        Quantity = request.Quantity,
                        // Giữ QR/tem chỉ khi chuyển toàn bộ dòng; chuyển một phần không được nhân bản tem.
                        TemCode = request.Quantity == available ? sourceDetails.TemCode : null,
                        RawQr = request.Quantity == available ? sourceDetails.RawQr : null,
                        ImportDate = sourceDetails.ImportDate ?? request.OccurredAt ?? DateTime.Now,
                        PhieuStatus = 0
                    });
                }
                else
                {
                    _slots.AddQuantity(request.TargetSlotId.Value, request.Quantity, itemCode);
                }

                return StockMovementResult.Ok("Đã di chuyển tồn kho.").WithTransaction(moveTxn);
            }
            catch (Exception ex)
            {
                return StockMovementResult.Fail("Lỗi di chuyển tồn kho: " + ex.Message);
            }
        }

        public StockMovementResult ReturnFromRework(StockMovementRequest request)
        {
            AuthorizationGuard.DemandActionAndScope("QUALITY.RETURN", "CONFIRM", "SITE", _balance.SiteCode, _balance.SiteCode);
            return AddToSlot(request, true);
        }

        public StockMovementResult Correct(StockMovementRequest request)
        {
            AuthorizationGuard.DemandActionAndScope("WAREHOUSE.STOCK_ADJUST", "CONFIRM", "SITE", _balance.SiteCode, _balance.SiteCode);
            if (request == null)
                return StockMovementResult.Fail("Stock movement request không được null.");
            if (string.IsNullOrWhiteSpace(request.LotNo))
                return StockMovementResult.Fail("LotNo không được rỗng.");
            if (string.IsNullOrWhiteSpace(request.ItemCode))
                return StockMovementResult.Fail("ItemCode không được rỗng khi điều chỉnh tồn.");
            if (request.Quantity == 0)
                return StockMovementResult.Fail("Quantity không được bằng 0.");

            if (!request.SlotId.HasValue || request.SlotId.Value <= 0)
                return StockMovementResult.Fail("Điều chỉnh tồn phải chỉ định Slot đích/nguồn để đồng bộ SlotLot.");
            if (_uow == null)
                return StockMovementResult.Fail("Chưa cấu hình UnitOfWork; từ chối chỉnh riêng STOCKTP để tránh lệch tồn.");

            try
            {
                _uow.Begin();
                if (!TryClaimRequest(request, false))
                {
                    _uow.Rollback();
                    return StockMovementResult.AlreadyProcessed("Mã tham chiếu điều chỉnh tồn đã được xử lý trước đó.");
                }

                if (request.Quantity > 0)
                {
                    _slots.AddLot(request.SlotId.Value, new StockSlotLot
                    {
                        LotNo = request.LotNo.Trim(),
                        ItemCode = request.ItemCode.Trim(),
                        Quantity = request.Quantity,
                        TemCode = null, // dòng điều chỉnh không được tái sử dụng TemCode/QrData của tem khác
                        RawQr = null,
                        ImportDate = request.OccurredAt ?? DateTime.Now,
                        PhieuStatus = 0
                    });
                }
                else
                {
                    _slots.TakeLot(request.SlotId.Value, request.LotNo, request.ItemCode, Math.Abs(request.Quantity));
                }

                StockLotKey adjustKey = _balance.ResolveLot(request.ItemCode, request.LotNo, request.Quantity > 0);
                if (adjustKey == null)
                    throw new InvalidOperationException($"LOT [{request.LotNo}] chưa có trong MES.FVN_MaterialLot; không thể điều chỉnh giảm.");
                bool adjustIn = request.Quantity > 0;
                long adjustTxn = _balance.Post(new StockLedgerEntry
                {
                    MovementType = adjustIn ? "ADJUST_IN" : "ADJUST_OUT",
                    Lot = adjustKey,
                    Quantity = Math.Abs(request.Quantity),
                    FromSlotId = adjustIn ? (int?)null : request.SlotId,
                    ToSlotId = adjustIn ? request.SlotId : (int?)null,
                    ReferenceType = LedgerReferenceType(request, "ADJUST"),
                    ReferenceNo = request.ReferenceId,
                    IdempotencyKey = LedgerIdempotencyKey(request, adjustIn ? "ADJUST_IN" : "ADJUST_OUT", adjustKey, request.SlotId, request.SlotId),
                    ActorCode = LedgerActor(request),
                    OccurredAtUtc = ToUtc(request.OccurredAt)
                });
                _uow.Commit();
                return StockMovementResult.Ok("Đã điều chỉnh MES ledger và SlotLot trong cùng transaction.").WithTransaction(adjustTxn);
            }
            catch (Exception ex)
            {
                try { _uow.Rollback(); } catch { }
                return StockMovementResult.Fail("Lỗi điều chỉnh tồn đồng bộ: " + ex.Message);
            }
        }

        public StockMovementResult RestorePicked(int slotId, StockSlotLot lot)
        {
            AuthorizationGuard.DemandActionAndScope("SHIPPING.DISPATCH", "CANCEL", "SITE", _balance.SiteCode, _balance.SiteCode);
            if (slotId <= 0) return StockMovementResult.Fail("SlotId không hợp lệ khi hoàn hàng chờ giao.");
            if (lot == null || lot.Quantity <= 0 || string.IsNullOrWhiteSpace(lot.LotNo) || string.IsNullOrWhiteSpace(lot.ItemCode))
                return StockMovementResult.Fail("Chi tiết LOT hoàn trả không hợp lệ.");

            try
            {
                _slots.AddLot(slotId, new StockSlotLot
                {
                    LotNo = lot.LotNo,
                    ItemCode = lot.ItemCode,
                    Quantity = lot.Quantity,
                    TemCode = lot.TemCode,
                    RawQr = lot.RawQr,
                    ImportDate = lot.ImportDate,
                    PhieuStatus = lot.PhieuStatus
                });
                // Hàng chờ giao chưa bị trừ STOCKTP; huỷ pick chỉ hoàn SlotLot, không cộng STOCKTP.
                return StockMovementResult.Ok("Đã hoàn chi tiết pick về Slot.");
            }
            catch (Exception ex)
            {
                return StockMovementResult.Fail("Lỗi hoàn chi tiết pick: " + ex.Message);
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
                if (!TryClaimRequest(request, !request.SlotLotWrittenByCaller))
                    return StockMovementResult.AlreadyProcessed("Yêu cầu nhập kho hoặc QR này đã được xử lý trước đó.");

                bool isQuarantineReceive = string.Equals(
                    request.MovementType,
                    StockMovementRequest.Types.ReworkNgReceive,
                    StringComparison.OrdinalIgnoreCase);

                bool isNormalReceive = string.Equals(
                    request.MovementType,
                    StockMovementRequest.Types.Receive,
                    StringComparison.OrdinalIgnoreCase);

                // Mọi lệnh nhập đều cần (SiteCode, PartNo, MaterialLotId): không còn nhập "chỉ có LotNo".
                if (string.IsNullOrWhiteSpace(request.LotNo))
                    return StockMovementResult.Fail("LotNo không được rỗng; MES ledger yêu cầu MaterialLotId.");
                StockLotKey receiveKey = _balance.ResolveLot(request.ItemCode, request.LotNo, true);

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
                            Status = status,
                            MaterialLotId = receiveKey.MaterialLotId
                        });
                }

                if (!string.IsNullOrWhiteSpace(request.LotNo) && !request.SlotLotWrittenByCaller)
                {
                    _slots.AddLot(request.TargetSlotId.Value, new StockSlotLot
                    {
                        LotNo = request.LotNo,
                        ItemCode = request.ItemCode,
                        Quantity = request.Quantity,
                        RawQr = string.IsNullOrWhiteSpace(request.QrData) ? null : request.QrData,
                        TemCode = string.IsNullOrWhiteSpace(request.TemCode) ? null : request.TemCode,
                        ImportDate = isNormalReceive
                            ? request.OccurredAt ?? DateTime.Now
                            : request.ProductionDate ?? request.OccurredAt ?? DateTime.Now,
                        NgaySX = request.ReceivingNgaySX,
                        SoPhieuTong = request.ReceivingSoPhieuTong,
                        MaPhieu = request.ReceivingMaPhieu,
                        ParentSoPhieu = request.ReceivingParentSoPhieu,
                        // NG cách ly: dòng SlotLot riêng (PhieuStatus = 3), KHÔNG cộng STOCKTP.
                        PhieuStatus = isQuarantineReceive ? (byte)3 : (byte)0
                    });
                }
                else
                {
                    _slots.AddQuantity(request.TargetSlotId.Value, request.Quantity, request.ItemCode);
                }

                // Nhập thành phẩm từ sản xuất = PROD_RECEIPT; hàng quay lại từ rework/huỷ pick = RETURN.
                // NG cách ly vào cùng ledger nhưng ở StockState QUARANTINE (không tính tồn AVAILABLE).
                string receiveMovement = isNormalReceive ? "PROD_RECEIPT" : "RETURN";
                long receiveTxn = _balance.Post(new StockLedgerEntry
                {
                    MovementType = receiveMovement,
                    Lot = receiveKey,
                    Quantity = request.Quantity,
                    ToSlotId = request.TargetSlotId,
                    ToStockState = isQuarantineReceive ? "QUARANTINE" : "AVAILABLE",
                    ReferenceType = LedgerReferenceType(request, receiveMovement),
                    ReferenceNo = request.ReferenceId,
                    IdempotencyKey = LedgerIdempotencyKey(request, receiveMovement, receiveKey, null, request.TargetSlotId),
                    ActorCode = LedgerActor(request),
                    OccurredAtUtc = ToUtc(request.OccurredAt)
                });

                return StockMovementResult.Ok("Đã nhập tồn kho.").WithTransaction(receiveTxn);
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

                    // MES ledger phát sinh ISSUE theo Slot: caller (hàng chờ giao) phải mang SlotId nơi hàng nằm.
                    if (!request.SlotId.HasValue || request.SlotId.Value <= 0)
                        return StockMovementResult.Fail("Xuất kho theo MES ledger cần SlotId (hoặc SlotLotId); không còn trừ tồn chỉ theo LOT.");
                    if (string.IsNullOrWhiteSpace(request.ItemCode))
                        return StockMovementResult.Fail("ItemCode (PartNo) không được rỗng khi xuất kho.");

                    if (!TryClaimRequest(request, false))
                        return StockMovementResult.AlreadyProcessed("Mã tham chiếu xuất kho đã được xử lý trước đó.");

                    StockLotKey issueKeyNoLot = _balance.ResolveLot(request.ItemCode, request.LotNo, false);
                    if (issueKeyNoLot == null)
                        return StockMovementResult.Fail(string.Format("LOT [{0}] chưa có trong MES.FVN_MaterialLot.", request.LotNo));
                    if (_balance.GetAvailableQuantity(issueKeyNoLot, request.SlotId.Value) < request.Quantity)
                        return StockMovementResult.Fail(
                            string.Format("MES ledger LOT [{0}] tại Slot {1} không đủ {2}.", request.LotNo, request.SlotId.Value, request.Quantity));

                    long issueTxnNoLot = _balance.Post(BuildIssueEntry(request, issueKeyNoLot, request.SlotId.Value, false));
                    return StockMovementResult.Ok("Đã xuất tồn trên MES ledger.").WithTransaction(issueTxnNoLot);
                }

                if (request.SlotLotId.Value <= 0)
                    return StockMovementResult.Fail("SlotLotId không hợp lệ.");

                byte sourceStatus = _slots.GetPhieuStatus(request.SlotLotId.Value);
                // SCRAP là ngoại lệ DUY NHẤT được lấy hàng ra khỏi SlotLot NG cách ly (PhieuStatus = 3).
                bool isScrap = string.Equals(request.MovementType, "SCRAP", StringComparison.OrdinalIgnoreCase);
                if (sourceStatus != 0 && !(isScrap && sourceStatus == 3))
                    return StockMovementResult.Fail($"Không thể xuất/pick SlotLot {request.SlotLotId.Value}: trạng thái {sourceStatus} không phải Active; hàng NG cách ly không được tính là tồn khả dụng.");

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

                if (!TryClaimRequest(request, false))
                    return StockMovementResult.AlreadyProcessed("Mã tham chiếu xuất/pick kho đã được xử lý trước đó.");

                long? removeTxn = null;
                if (adjustAvailable)
                {
                    int? sourceSlot = _slots.GetSlotId(request.SlotLotId.Value);
                    if (!sourceSlot.HasValue || sourceSlot.Value <= 0)
                        return StockMovementResult.Fail("Không xác định được Slot của SlotLot nguồn.");
                    string ledgerItem = string.IsNullOrWhiteSpace(request.ItemCode) ? sourceItemCode : request.ItemCode;
                    StockLotKey issueKey = _balance.ResolveLot(ledgerItem, sourceLotNo, false);
                    if (issueKey == null)
                        return StockMovementResult.Fail(string.Format("LOT [{0}] chưa có trong MES.FVN_MaterialLot.", sourceLotNo));
                    // Hàng NG cách ly nằm trong ledger ở StockState QUARANTINE; chỉ SCRAP được lấy ra.
                    bool fromQuarantine = sourceStatus == 3;
                    removeTxn = _balance.Post(BuildIssueEntry(request, issueKey, sourceSlot.Value, fromQuarantine));
                }

                _slots.DecreaseLotQuantity(request.SlotLotId.Value, request.Quantity);
                var removed = StockMovementResult.Ok("Đã xuất tồn kho.");
                return removeTxn.HasValue ? removed.WithTransaction(removeTxn.Value) : removed;
            }
            catch (Exception ex)
            {
                return StockMovementResult.Fail("Lỗi xuất tồn kho: " + ex.Message);
            }
        }

        private StockLedgerEntry BuildIssueEntry(StockMovementRequest request, StockLotKey key, int fromSlotId, bool fromQuarantine)
        {
            bool scrap = string.Equals(request.MovementType, "SCRAP", StringComparison.OrdinalIgnoreCase);
            string movement = scrap ? "SCRAP" : "ISSUE";
            return new StockLedgerEntry
            {
                MovementType = movement,
                Lot = key,
                Quantity = request.Quantity,
                FromSlotId = fromSlotId,
                FromStockState = fromQuarantine ? "QUARANTINE" : "AVAILABLE",
                ReferenceType = LedgerReferenceType(request, movement),
                ReferenceNo = request.ReferenceId,
                IdempotencyKey = LedgerIdempotencyKey(request, movement, key, fromSlotId, null),
                ActorCode = LedgerActor(request),
                OccurredAtUtc = ToUtc(request.OccurredAt)
            };
        }

        private static string LedgerReferenceType(StockMovementRequest request, string fallback)
        {
            string value = string.IsNullOrWhiteSpace(request.ReferenceType) ? "PCTP_" + fallback : request.ReferenceType.Trim();
            return value.Length > 30 ? value.Substring(0, 30) : value;
        }

        private static string LedgerActor(StockMovementRequest request)
        {
            return string.IsNullOrWhiteSpace(request.PerformedBy) ? Environment.UserName : request.PerformedBy.Trim();
        }

        private static DateTime? ToUtc(DateTime? local)
        {
            return local.HasValue ? local.Value.ToUniversalTime() : (DateTime?)null;
        }

        /// <summary>
        /// Khoá idempotency của ledger. Có ReferenceId nghiệp vụ → khoá ổn định (retry an toàn, không ghi đôi).
        /// Không có ReferenceId → Guid mới: KHÔNG chống trùng được, caller nên luôn truyền ReferenceId.
        /// </summary>
        private static string LedgerIdempotencyKey(
            StockMovementRequest request, string movement, StockLotKey key, int? fromSlot, int? toSlot)
        {
            string reference = string.IsNullOrWhiteSpace(request.ReferenceId)
                ? Guid.NewGuid().ToString("N")
                : request.ReferenceId.Trim();
            return string.Join("|",
                "PCTP", key.SiteCode, string.IsNullOrWhiteSpace(request.ReferenceType) ? movement : request.ReferenceType.Trim(),
                reference, movement, key.PartNo, key.MaterialLotId,
                fromSlot.HasValue ? fromSlot.Value.ToString() : "-",
                toSlot.HasValue ? toSlot.Value.ToString() : "-");
        }

        private bool TryClaimRequest(StockMovementRequest request, bool includeQr)
        {
            if (request == null) return false;

            bool hasReference = !string.IsNullOrWhiteSpace(request.ReferenceType)
                || !string.IsNullOrWhiteSpace(request.ReferenceId);
            string qr = includeQr ? request.QrData : null;
            // TemCode là mã phiếu nhóm (nhiều tem cùng 1 phiếu) thì không được coi là khoá duy nhất.
            string tem = includeQr && !request.TemCodeIsGroupKey ? request.TemCode : null;
            if (!hasReference && string.IsNullOrWhiteSpace(qr) && string.IsNullOrWhiteSpace(tem))
                return true;

            return _slots.TryRegisterMovement(
                request.ReferenceType,
                request.ReferenceId,
                qr,
                tem,
                includeQr && !request.SlotLotWrittenByCaller);
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
