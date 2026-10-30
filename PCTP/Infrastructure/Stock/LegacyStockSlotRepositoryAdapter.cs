using System;
using System.Collections.Generic;
using System.Linq;
using PCTP.Common;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Shared.Helpers;
using PCTP.VIEWSTOCK.Fuction;

namespace PCTP.Infrastructure.Stock
{
    /// <summary>
    /// Transitional adapter from the KhoCore stock-slot contract to the legacy
    /// KhoVatLy ISlotService storage API.
    ///
    /// This adapter intentionally lives outside KhoCore so KhoCore never
    /// depends on KhoVatLy. Business modules consume the KhoCore contract;
    /// the composition/legacy boundary owns this dependency.
    /// </summary>
    public class LegacyStockSlotRepositoryAdapter : IStockSlotRepository
    {
        private readonly ISlotService _legacy;
        private readonly IStockSlotRepository _movementGuard;

        public LegacyStockSlotRepositoryAdapter(ISlotService legacy)
            : this(legacy, null)
        {
        }

        /// <summary>
        /// <paramref name="movementGuard"/> must be bound to the SAME UnitOfWork as <paramref name="legacy"/>
        /// (e.g. BulkStockSlotRepository(db, uow)). Only TryRegisterMovement is delegated to it, so slot
        /// reads/writes keep the legacy behaviour while the idempotency claim (dbo.PCTPStockMovementGuard)
        /// is atomic with them. Without a guard the adapter still fails closed.
        /// </summary>
        public LegacyStockSlotRepositoryAdapter(ISlotService legacy, IStockSlotRepository movementGuard)
        {
            _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
            _movementGuard = movementGuard;
        }

        public int GetLotQuantity(int slotLotId)
        {
            var lot = _legacy.GetLotsBySlotLotId(slotLotId);
            return lot == null ? 0 : lot.Quantity;
        }

        public byte GetPhieuStatus(int slotLotId)
        {
            var lot = _legacy.GetLotsBySlotLotId(slotLotId);
            return lot == null ? (byte)255 : (byte)lot.PhieuStatus;
        }

        public StockSlotLot GetLotDetails(int slotLotId)
        {
            var lot = _legacy.GetLotsBySlotLotId(slotLotId);
            if (lot == null) return null;
            return new StockSlotLot
            {
                LotNo = lot.LotNo,
                ItemCode = lot.ItemCode,
                Quantity = lot.Quantity,
                TemCode = lot.TemCode,
                RawQr = lot.RawQr,
                ImportDate = lot.ImportDate,
                PhieuStatus = (byte)lot.PhieuStatus
            };
        }

        public string GetLotNo(int slotLotId)
        {
            var lot = _legacy.GetLotsBySlotLotId(slotLotId);
            return lot == null ? null : lot.LotNo;
        }

        public string GetItemCode(int slotLotId)
        {
            var lot = _legacy.GetLotsBySlotLotId(slotLotId);
            return lot == null ? null : lot.ItemCode;
        }

        public int? GetSlotId(int slotLotId)
        {
            var lot = _legacy.GetLotsBySlotLotId(slotLotId);
            return lot == null ? (int?)null : lot.SlotVatLyId;
        }

        public void LockSlotForUpdate(int slotId)
        {
            if (slotId <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotId));

            // ISlotService.LockSlotForUpdate delegates to SlotRepository, which
            // shares the same UnitOfWork supplied by the module composition root.
            _legacy.LockSlotForUpdate(slotId);
        }

        public void DecreaseLotQuantity(int slotLotId, int quantity)
        {
            _legacy.DecreaseSlotLotQuantity(slotLotId, quantity);
        }

        public void AddQuantity(int slotId, int quantity, string itemCode)
        {
            _legacy.AddQuantity(slotId, quantity, itemCode, DateTime.Now);
        }

        public bool TryRegisterMovement(string referenceType, string referenceId, string qrData, string temCode, bool checkExistingQr)
        {
            if (string.IsNullOrWhiteSpace(referenceType)
                && string.IsNullOrWhiteSpace(referenceId)
                && string.IsNullOrWhiteSpace(qrData)
                && string.IsNullOrWhiteSpace(temCode))
                return true;

            if (_movementGuard != null)
                return _movementGuard.TryRegisterMovement(referenceType, referenceId, qrData, temCode, checkExistingQr);

            // The legacy ISlotService API does not expose the caller's SQL transaction.
            // Never emulate an idempotency claim in process memory: it would not be atomic
            // with stock writes or shared across application instances.
            throw new NotSupportedException(
                "Stock movement idempotency requires BulkStockSlotRepository and the PCTPStockMovementGuard SQL migration in the same UnitOfWork transaction. LegacyStockSlotRepositoryAdapter fails closed.");
        }

        /// <summary>
        /// INSERT-ONLY. Trước đây method này đọc TOÀN BỘ lot của Slot rồi SaveLots (xoá hết + ghi lại)
        /// → với Slot ảo A0 chứa rất nhiều LOT sẽ làm mất QrData/MaPhieu/NgaySX của các tem đã nhập,
        /// rất chậm, và ghi NULL vào SlotLot.ItemCode. Giờ chỉ thêm đúng 1 dòng mới.
        /// </summary>
        public void AddLot(int slotId, StockSlotLot lot)
        {
            if (lot == null)
                throw new ArgumentNullException(nameof(lot));
            if (string.IsNullOrWhiteSpace(lot.LotNo))
                throw new ArgumentException("LotNo không được rỗng.", nameof(lot));
            if (string.IsNullOrWhiteSpace(lot.ItemCode))
                throw new ArgumentException("ItemCode không được rỗng.", nameof(lot));
            if (lot.Quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(lot.Quantity));

            _legacy.AppendLot(
                slotId,
                lot.ItemCode,
                lot.LotNo,
                lot.Quantity,
                lot.TemCode,
                lot.RawQr,
                lot.ImportDate,
                lot.PhieuStatus);
        }

        /// <summary>
        /// FIFO, cập nhật TỪNG DÒNG qua ISlotService.TakeLotFifo. Trước đây method này GetLots → SaveLots
        /// (xoá + ghi lại cả Slot → mất QrData/MaPhieu của mọi LOT còn lại) và so ItemCode với giá trị
        /// GetLots không bao giờ điền nên luôn báo "không đủ".
        /// </summary>
        public StockSlotTakeResult TakeLot(int slotId, string lotNo, string itemCode, int quantity)
        {
            if (slotId <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotId));
            if (string.IsNullOrWhiteSpace(lotNo))
                throw new ArgumentException("LotNo không được rỗng.", nameof(lotNo));
            if (string.IsNullOrWhiteSpace(itemCode))
                throw new ArgumentException("ItemCode không được rỗng.", nameof(itemCode));
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            var taken = _legacy.TakeLotFifo(slotId, lotNo, itemCode, quantity) ?? new List<LotInfo>();

            return new StockSlotTakeResult
            {
                Quantity = quantity,
                ExportLots = taken
                    .Select(x => new StockSlotLot
                    {
                        LotNo = x.LotNo,
                        ItemCode = x.ItemCode,
                        Quantity = x.Quantity,
                        TemCode = x.TemCode,
                        RawQr = x.RawQr,
                        ImportDate = x.ImportDate
                    })
                    .ToList()
            };
        }
    }
}
