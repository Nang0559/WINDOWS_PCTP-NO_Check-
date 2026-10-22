
using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.KhoVatLy.Kho.Models;
using System;
using System.Collections.Generic;
using System.Data;


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
        /// <summary>
        /// ⚠ NGUY HIỂM với Slot nhiều LOT (đặc biệt Slot ảo A0): xoá toàn bộ SlotLot của Slot rồi ghi lại.
        /// KHÔNG dùng cho nhập/xuất/dời hàng — dùng AppendLot / TakeLotFifo / MoveLot.
        /// </summary>
        void SaveLots(
            int slotId,
            List<LotInfo> lots);

        List<LotInfo> GetLots(int slotId);

        /// <summary>
        /// Thêm 1 dòng SlotLot (INSERT-ONLY) rồi tính lại header Slot. Không xoá / ghi đè
        /// các LOT đang có trong Slot → dùng cho mọi thao tác "nhập thêm", đặc biệt Slot ảo A0.
        /// </summary>
        void AppendLot(
            int slotId,
            string itemCode,
            string lotNo,
            int quantity,
            string temCode,
            string qrData,
            DateTime? importDate,
            byte phieuStatus = 0);

        /// <summary>
        /// Lấy hàng FIFO theo LOT + ItemCode, chỉ UPDATE/DELETE đúng các dòng SlotLot bị lấy,
        /// không đụng các LOT khác trong Slot. Trả về các phần đã lấy.
        /// </summary>
        List<LotInfo> TakeLotFifo(int slotId, string lotNo, string itemCode, int quantity);

        bool ExistsLot(
            int slotId,
            string lotNo);
        List<SlotLotViewInfo> GetAllActiveSlotLots();

        /// <summary>Slot có tồn tại không — kiểm tra trước khi nhập tồn để không vi phạm FK.</summary>
        bool SlotExists(int slotId);

        /// <summary>Toàn bộ Slot (kho / rack / số slot) cho danh sách chọn Slot đích.</summary>
        List<Slot> GetAllSlots();

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
