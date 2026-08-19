using PCTP.Modules.KhoVatLy.Application.Helpers;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        public List<SlotChuaLotInfo> FindSlotsContainingLot(string lotNo)
        {
            if (string.IsNullOrWhiteSpace(lotNo))
                return new List<SlotChuaLotInfo>();

            return _repository.GetSlotsChuaLot(lotNo);
        }
        public int GetSlotId(
            string warehouseName,
            string rackName,
            int slotNumber)
        {
            if (string.IsNullOrWhiteSpace(warehouseName))
                throw new ArgumentException(
                    "Tên kho không được rỗng.");

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
            if (!SlotParser.TryParse(
                slotText,
                out string warehouse,
                out string rack,
                out int slot,
                out _))
            {
                return -1;
            }

            return GetSlotId(
                warehouse,
                rack,
                slot);
        }
        public SlotInfo GetSlotInfoFromString(string slotText)
        {
            if (string.IsNullOrWhiteSpace(slotText))
                throw new ArgumentException(
                    "Thông tin Slot không được rỗng.",
                    nameof(slotText));

            Match match = Regex.Match(
                slotText,
                @"WH\s*:\s*(?<wh>.*?)\s*-\s*Rack\s*:\s*(?<rack>.*?)\s*-\s*Slot\s*:\s*(?<slot>\d+)(?:\s*-\s*Capacity\s*:\s*(?<capacity>\d+))?",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                throw new FormatException(
                    $"Không thể phân tích thông tin Slot: [{slotText}]");

            string warehouse = match.Groups["wh"].Value.Trim();
            string rack = match.Groups["rack"].Value.Trim();

            if (!int.TryParse(
                    match.Groups["slot"].Value,
                    out int slotNumber))
            {
                throw new FormatException(
                    $"Số Slot không hợp lệ: [{slotText}]");
            }

            int capacity = 0;

            if (match.Groups["capacity"].Success)
            {
                int.TryParse(
                    match.Groups["capacity"].Value,
                    out capacity);
            }

            return new SlotInfo
            {
                WarehouseName = warehouse,
                RackName = rack,
                SlotNumber = slotNumber,
                Capacity = capacity
            };
        }

        public int GetCapacity(int slotId)
        {
            if (slotId <= 0)
                return 0;

            return _repository.GetCapacity(slotId);
        }
        public int GetQuantityWithLock(int slotId)
        {
            return _repository.GetQuantityWithLock(slotId);
        }
        public int GetQuantity(int slotId)
        {
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
        public SlotLotInfo GetLotsBySlotLotId(int slotLotId) => _repository.GetSlotLotById(slotLotId);

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
            if (slotId <= 0)
                return new List<LotInfo>();

            return _repository.GetLots(slotId);
        }

        public bool ExistsLot(
            int slotId,
            string lotNo)
        {
            if (slotId <= 0 ||
                string.IsNullOrWhiteSpace(lotNo))
                return false;

            return _repository.ExistsLot(
                slotId,
                lotNo);
        }

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
            if (string.IsNullOrWhiteSpace(itemCode) || soLuongNhap <= 0)
                return new List<string>();

            return _repository.GetEmptySlots(itemCode, soLuongNhap);
        }

        public string GetOrCreateVirtualSlotText(string warehouseName, string rackName, int capacity)
        {
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

    }
}
