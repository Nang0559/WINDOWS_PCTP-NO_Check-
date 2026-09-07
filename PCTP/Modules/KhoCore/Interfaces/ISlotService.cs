using CrystalDecisions.Shared;
using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Application.Interfaces
{
    public interface ISlotService
    {
        // ============================================================
        // SLOT - TRA CỨU
        // ============================================================
        List<SlotChuaLotInfo> FindSlotsContainingLot(string lotNo);
        int GetSlotId(
            string warehouseName,
            string rackName,
            int slotNumber);

        int GetSlotIdFromString(string slotText);

        int GetCapacity(int slotId);

        SlotInfo GetSlotInfoFromString(string slotText);

        int GetQuantity(int slotId);

        int GetQuantityWithLock(int slotId);
        // ============================================================
        // SLOT - CẬP NHẬT HEADER
        // ============================================================

        bool UpdateSlotInfo(
            string selectedSlot,
            string itemCode,
            DateTime importDate,
            int quantity);

        bool UpdateSlotInfo(
            int slotId,
            string itemCode,
            DateTime importDate,
            int quantity);
        void UpdateSlotHeaderFromLots(int slotId, List<LotInfo> lots);

        // ============================================================
        // SLOT LOT
        // ============================================================
        // sử dụng cho rewwork 
        SlotLotInfo GetLotsBySlotLotId(int slotLotId);
        void DecreaseSlotLotQuantity(int slotLotId, int qty);
        // sử dụng chung
        void LockSlotForUpdate(int slotId);
        void SaveLots(
            int slotId,
            List<LotInfo> lots);

        List<LotInfo> GetLots(int slotId);

        bool ExistsLot(
            int slotId,
            string lotNo);
        List<SlotLotViewInfo> GetAllActiveSlotLots();

        // ============================================================
        // NHẬP / CỘNG TỒN
        // ============================================================

        void AddQuantity(
            int slotId,
            int quantity,
            string itemCode,
            DateTime? importDate);


        // ============================================================
        // SLOT
        // ============================================================

        void ClearSlot(int slotId);


        // ============================================================
        // SLOT TẠM TRÊN UI / MEMORY
        // ============================================================

        void ClearSlotTemporarily(Slot slot);

        void BackupSlot(
            Slot slot,
            out Slot backup);

        void RestoreSlot(
            Slot slot,
            Slot backup);


        // ============================================================
        // TÌM SLOT TRỐNG
        // ============================================================

        List<string> GetEmptySlots(
            string itemCode,
            int soLuongNhap);


        // ============================================================
        // SLOT ẢO
        // ============================================================

        string GetOrCreateVirtualSlotText(
            string warehouseName,
            string rackName,
            int capacity);

        /// <summary>
        /// Dời 1 LOT giữa 2 Slot — KHÔNG trừ STOCKTP, KHÔNG qua ChoGiao. Dùng khi:
        /// (a) sắp xếp lại kho nội bộ, (b) dời phần dư sau khi PickToChoGiao xuất 1 phần LOT.
        /// </summary>
        void MoveLot(int fromSlotId, int toSlotId, string lotNo);

        DataTable GetOccupiedSlotsForLookup();
    }
}
