using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.KhoVatLy.Application.Helpers;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Helpers;
using PCTP.Infrastructure.Security;
using System;
using System.Collections.Generic;
using System.Data;

using System.Linq;


namespace PCTP.Modules.KhoVatLy.Application.Services
{
    public sealed class SlotService : ISlotService
    {
        private readonly ISlotRepository _repository;

        public SlotService(
            ISlotRepository repository)
        {
            _repository = repository
                ?? throw new ArgumentNullException(nameof(repository));
        }

        private string WarehouseForSlot(int slotId)
        {
            string warehouse = _repository.GetWarehouseNameBySlotId(slotId);
            if (string.IsNullOrWhiteSpace(warehouse))
                throw new UnauthorizedAccessException("Slot không tồn tại trong SiteCode hiện tại hoặc không xác định được kho.");
            return warehouse;
        }

        private void DemandWarehouse(string functionCode, string actionCode, string warehouseName)
        {
            AuthorizationGuard.DemandActionAndScope(functionCode, actionCode, "WAREHOUSE", warehouseName, null);
        }

        private List<SlotLotViewInfo> FilterScopedLots(IEnumerable<SlotLotViewInfo> rows)
        {
            var session = AuthorizationGuard.Demand("WAREHOUSE.STOCK_VIEW", "VIEW");
            return (rows ?? Enumerable.Empty<SlotLotViewInfo>())
                .Where(x => x != null && session.HasDataScope("WAREHOUSE.STOCK_VIEW", "WAREHOUSE", x.WarehouseName, null)).ToList();
        }

        private List<Slot> FilterScopedSlots(IEnumerable<Slot> rows)
        {
            var session = AuthorizationGuard.Demand("WAREHOUSE.STOCK_VIEW", "VIEW");
            return (rows ?? Enumerable.Empty<Slot>())
                .Where(x => x != null && session.HasDataScope("WAREHOUSE.STOCK_VIEW", "WAREHOUSE", x.whname, null)).ToList();
        }

        public List<SlotChuaLotInfo> FindSlotsContainingLot(string lotNo)
        {
            if (string.IsNullOrWhiteSpace(lotNo)) return new List<SlotChuaLotInfo>();
            var session = AuthorizationGuard.Demand("WAREHOUSE.STOCK_VIEW", "VIEW");
            return (_repository.GetSlotsChuaLot(lotNo) ?? new List<SlotChuaLotInfo>())
                .Where(x => x != null && session.HasDataScope("WAREHOUSE.STOCK_VIEW", "WAREHOUSE", x.WarehouseName, null)).ToList();
        }
        public int GetSlotId(
            string warehouseName,
            string rackName,
            int slotNumber)
        {
            DemandWarehouse("WAREHOUSE.STOCK_VIEW", "VIEW", warehouseName);
            if (string.IsNullOrWhiteSpace(warehouseName))
                throw new ArgumentException("Tên kho không được rỗng.");

            if (string.IsNullOrWhiteSpace(rackName))
                throw new ArgumentException(
                    "Tên Rack không được rỗng.");

            if (slotNumber <= 0)
                throw new ArgumentException(
                    "SlotNumber không hợp lệ.");

            return _repository.GetSlotId(
                warehouseName,
                rackName,
                slotNumber);
        }

        public int GetSlotIdFromString(string slotText)
        {
            if (!SlotParser.TryParse(slotText, out string wh, out string rack, out int slotNo, out _)) return -1;
            DemandWarehouse("WAREHOUSE.STOCK_VIEW", "VIEW", wh);
            return _repository.GetSlotId(wh, rack, slotNo);
        }
        public SlotInfo GetSlotInfoFromString(string slotText)
        {
            if (!SlotParser.TryParse(slotText, out string wh, out string rack, out int slotNo, out int capacity))
                throw new ArgumentException($"Chuỗi Slot không hợp lệ: '{slotText}'");
            DemandWarehouse("WAREHOUSE.STOCK_VIEW", "VIEW", wh);

            int slotId = _repository.GetSlotId(wh, rack, slotNo);

            return new SlotInfo
            {
                SlotId = slotId,
                WarehouseName = wh,
                RackName = rack,
                SlotNumber = slotNo,
                Capacity = capacity > 0 ? capacity : _repository.GetCapacity(slotId)
            };
        }

        public int GetCapacity(int slotId)
        {
            if (slotId <= 0) return 0;
            DemandWarehouse("WAREHOUSE.STOCK_VIEW", "VIEW", WarehouseForSlot(slotId));
            return _repository.GetCapacity(slotId);
        }
        public int GetQuantityWithLock(int slotId)
        {
            DemandWarehouse("WAREHOUSE.STOCK_VIEW", "VIEW", WarehouseForSlot(slotId));
            return _repository.GetQuantityWithLock(slotId);
        }
        public int GetQuantity(int slotId)
        {
            DemandWarehouse("WAREHOUSE.STOCK_VIEW", "VIEW", WarehouseForSlot(slotId));
            return _repository.GetQuantity(slotId);
        }
        public bool UpdateSlotInfo(
            string selectedSlot,
            string itemCode,
            DateTime importDate,
            int quantity)
        {
            int slotId =
                GetSlotIdFromString(selectedSlot);

            if (slotId <= 0)
                return false;

            return UpdateSlotInfo(
                slotId,
                itemCode,
                importDate,
                quantity);
        }

        public bool UpdateSlotInfo(
            int slotId,
            string itemCode,
            DateTime importDate,
            int quantity)
        {
            if (slotId <= 0)
                return false;

            if (quantity < 0)
                throw new ArgumentException(
                    "Quantity không được âm.");

            int capacity =
                GetCapacity(slotId);

            if (capacity > 0 &&
                quantity > capacity)
            {
                throw new InvalidOperationException(
                    $"Số lượng {quantity} vượt sức chứa Slot {capacity}.");
            }

            _repository.UpdateHeader(
                   slotId,
                   itemCode,
                   quantity > 0
                       ? importDate
                       : (DateTime?)null,
                   quantity);

            return true;
        }
        public void UpdateSlotHeaderFromLots(
         int slotId,
         List<LotInfo> lots)
        {
            if (slotId <= 0)
                throw new ArgumentException(
                    "SlotId không hợp lệ.",
                    nameof(slotId));

            if (lots == null)
                lots = new List<LotInfo>();

            int quantity =
                LotNoHelper.GetTotalQuantity(lots);

            string itemCode =
           lots.FirstOrDefault()?.ItemCode;

            DateTime? importDate =
                lots
                    .Where(x => x.ImportDate.HasValue)
                    .Select(x => x.ImportDate)
                    .Max();

            _repository.UpdateHeader(
                slotId,
                itemCode,
                quantity > 0 ? importDate : null,
                quantity);
        }
        // Kho/Services/SlotService.cs — implement
        public SlotLotInfo GetLotsBySlotLotId(int slotLotId)
        {
            var row = _repository.GetSlotLotById(slotLotId);
            if (row != null) DemandWarehouse("WAREHOUSE.STOCK_VIEW", "VIEW", WarehouseForSlot(row.SlotId));
            return row;
        }

        // SlotService.DecreaseSlotLotQuantity — sửa lại, dùng UpdateHeader đã có
        public void DecreaseSlotLotQuantity(int slotLotId, int qty)
        {
            var current = _repository.GetSlotLotById(slotLotId);
            if (current == null)
                throw new InvalidOperationException($"Không tìm thấy SlotLot Id={slotLotId}.");
            if (qty <= 0)
                throw new ArgumentException("Số lượng trừ phải lớn hơn 0.", nameof(qty));
            if (current.Quantity < qty)
                throw new InvalidOperationException(
                    $"SlotLot {slotLotId} chỉ còn {current.Quantity}, không đủ để trừ {qty}.");

            int conLai = current.Quantity - qty;

            if (conLai == 0)
                _repository.DeleteSlotLot(slotLotId);
            else
                _repository.UpdateSlotLotQuantity(slotLotId, conLai);

            // ── RULE: tự tính header từ danh sách LOT còn lại, rồi ghi bằng UpdateHeader ──
            var lotsConLai = _repository.GetLots(current.SlotVatLyId);

            int tongQuantity = lotsConLai.Sum(x => x.Quantity);
            var lotGanNhat = lotsConLai.OrderByDescending(x => x.ImportDate).FirstOrDefault();

            _repository.UpdateHeader(
                current.SlotVatLyId,
                lotGanNhat?.ItemCode,
                lotGanNhat?.ImportDate,
                tongQuantity);
        }
        public void LockSlotForUpdate(int slotId)
        {
            if (slotId <= 0)
                throw new ArgumentException("SlotId không hợp lệ.", nameof(slotId));

            _repository.LockSlotForUpdate(slotId);
        }
        public void AppendLot(
            int slotId,
            string itemCode,
            string lotNo,
            int quantity,
            string temCode,
            string qrData,
            DateTime? importDate,
            byte phieuStatus = 0)
        {
            if (slotId <= 0)
                throw new ArgumentException("SlotId không hợp lệ.", nameof(slotId));

            _repository.AppendLot(
                slotId, itemCode, lotNo, quantity, temCode, qrData,
                importDate ?? DateTime.Now, phieuStatus);

            _repository.RefreshSlotHeader(slotId);
        }

        public List<LotInfo> TakeLotFifo(int slotId, string lotNo, string itemCode, int quantity)
        {
            if (slotId <= 0)
                throw new ArgumentException("SlotId không hợp lệ.", nameof(slotId));

            return _repository.TakeLotFifo(slotId, lotNo, itemCode, quantity);
        }

        public void SaveLots(
         int slotId,
         List<LotInfo> lots)
        {
            if (slotId <= 0)
                throw new ArgumentException(
                    "SlotId không hợp lệ.",
                    nameof(slotId));

            _repository.SaveLots(
                slotId,
                lots ?? new List<LotInfo>());
        }
        public void AddQuantity(
        int slotId,
        int quantity,
        string itemCode,
        DateTime? importDate)
        {
            if (slotId <= 0)
                throw new ArgumentException(
                    "SlotId không hợp lệ.",
                    nameof(slotId));

            if (quantity <= 0)
                throw new ArgumentException(
                    "Số lượng phải lớn hơn 0.",
                    nameof(quantity));

            int capacity = GetCapacity(slotId);

            // Nếu capacity > 0 thì cần kiểm tra trước khi cộng
            if (capacity > 0)
            {
                int currentQuantity = _repository.GetQuantity(slotId);

                if (currentQuantity + quantity > capacity)
                {
                    throw new InvalidOperationException(
                        $"Số lượng {currentQuantity + quantity} vượt sức chứa Slot {capacity}.");
                }
            }

            _repository.AddQuantity(
                slotId,
                quantity,
                itemCode,
                importDate);
        }
        public List<LotInfo> GetLots(int slotId)
        {
            if (slotId <= 0) return new List<LotInfo>();
            DemandWarehouse("WAREHOUSE.STOCK_VIEW", "VIEW", WarehouseForSlot(slotId));
            return _repository.GetLots(slotId);
        }

        public bool ExistsLot(
            int slotId,
            string lotNo)
        {
            if (slotId > 0) DemandWarehouse("WAREHOUSE.STOCK_VIEW", "VIEW", WarehouseForSlot(slotId));
            if (slotId <= 0 ||
                string.IsNullOrWhiteSpace(lotNo))
                return false;

            return _repository.ExistsLot(
                slotId,
                lotNo);
        }
        public List<SlotLotViewInfo> GetAllActiveSlotLots()
            => FilterScopedLots(_repository.GetAllActiveSlotLots());

        public bool SlotExists(int slotId)
        {
            if (slotId <= 0) return false;
            DemandWarehouse("WAREHOUSE.STOCK_VIEW", "VIEW", WarehouseForSlot(slotId));
            return _repository.SlotExists(slotId);
        }

        public List<Slot> GetAllSlots()
            => FilterScopedSlots(_repository.GetAllSlots());
        public void ClearSlot(int slotId)
        {
            if (slotId <= 0)
                return;

            _repository.Clear(slotId);
        }

       

        public void ClearSlotTemporarily(Slot slot)
        {
            if (slot == null)
                return;

            slot.ItemCode = "";
            slot.Quantity = 0;
            slot.ImportDate = null;
            slot.IsOccupied = false;

            slot.Lots?.Clear();
        }

        public void BackupSlot(
            Slot slot,
            out Slot backup)
        {
            if (slot == null)
                throw new ArgumentNullException(nameof(slot));

            backup = CloneSlot(slot);
        }

        public void RestoreSlot(
            Slot slot,
            Slot backup)
        {
            if (slot == null)
                throw new ArgumentNullException(nameof(slot));

            if (backup == null)
                throw new ArgumentNullException(nameof(backup));

            slot.ItemCode = backup.ItemCode;
            slot.Quantity = backup.Quantity;
            slot.ImportDate = backup.ImportDate;
            slot.IsOccupied = backup.IsOccupied;

            slot.Lots = backup.Lots
                .Select(CloneLot)
                .ToList();
        }


        // SlotService — thêm
        public List<string> GetEmptySlots(string itemCode, int soLuongNhap)
        {
            AuthorizationGuard.Demand("WAREHOUSE.STOCK_VIEW", "VIEW");
            if (string.IsNullOrWhiteSpace(itemCode) || soLuongNhap <= 0)
                return new List<string>();

            var candidates = _repository.GetEmptySlots(itemCode, soLuongNhap);
            var result = new List<string>(candidates.Count);

            foreach (var slotText in candidates)
            {
                // Không parse được thì giữ nguyên, tránh làm mất slot vì lỗi định dạng
                if (!SlotParser.TryParse(slotText, out string wh, out string rack, out int slotNo, out int capacity))
                {
                    result.Add(slotText);
                    continue;
                }

                int slotId = _repository.GetSlotId(wh, rack, slotNo);
                if (slotId <= 0) continue;

                if (capacity <= 0)
                    capacity = _repository.GetCapacity(slotId);

                // capacity <= 0: slot không giới hạn sức chứa → luôn giữ
                if (capacity > 0 && _repository.GetQuantity(slotId) >= capacity)
                    continue;   // slot đã đủ capacity → loại khỏi danh sách

                result.Add(slotText);
            }

            return result;
        }

        public string GetOrCreateVirtualSlotText(string warehouseName, string rackName, int capacity)
        {
            DemandWarehouse("WAREHOUSE.STOCK_ADJUST", "ADMIN", warehouseName);
            if (string.IsNullOrWhiteSpace(warehouseName))
                throw new ArgumentException("Tên kho không được rỗng.");
            if (string.IsNullOrWhiteSpace(rackName))
                throw new ArgumentException("Tên Rack không được rỗng.");
            if (capacity <= 0)
                throw new ArgumentException("Capacity phải lớn hơn 0.");

            return _repository.GetOrCreateNamedSlot(warehouseName, rackName, capacity);
        }
        private static Slot CloneSlot(Slot source)
        {
            if (source == null)
                return null;

            var clone = new Slot
            {
                SlotId = source.SlotId,
                whname = source.whname,
                RackName = source.RackName,
                Rackid = source.Rackid,
                SlotNumber = source.SlotNumber,
                RowIndex = source.RowIndex,
                ColumnIndex = source.ColumnIndex,
                IsOccupied = source.IsOccupied,
                ItemCode = source.ItemCode,
                Quantity = source.Quantity,
                Capacity = source.Capacity,
                ImportDate = source.ImportDate,
                Lots = new List<LotInfo>()
            };

            if (source.Lots != null)
            {
                foreach (LotInfo lot in source.Lots)
                {
                    clone.Lots.Add(CloneLot(lot));
                }
            }

            return clone;
        }
        private static LotInfo CloneLot(LotInfo source)
        {
            return source?.Clone();
        }

        public void MoveLot(int fromSlotId, int toSlotId, string lotNo)
        {
            DemandWarehouse("WAREHOUSE.MOVE", "CONFIRM", WarehouseForSlot(fromSlotId));
            DemandWarehouse("WAREHOUSE.MOVE", "CONFIRM", WarehouseForSlot(toSlotId));
            if (fromSlotId <= 0)
                throw new ArgumentException("SlotId nguồn không hợp lệ.", nameof(fromSlotId));
            if (toSlotId <= 0)
                throw new ArgumentException("SlotId đích không hợp lệ.", nameof(toSlotId));
            if (string.IsNullOrWhiteSpace(lotNo))
                throw new ArgumentException("LotNo không được rỗng.", nameof(lotNo));

            _repository.MoveLot(fromSlotId, toSlotId, lotNo);
        }
        public DataTable GetOccupiedSlotsForLookup()=> _repository.GetOccupiedSlotsForLookup();
    }
}
