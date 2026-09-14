using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.VIEWSTOCK.Models;
using System.Collections.Generic;

namespace PCTP.Modules.KhoVatLy.Repositories
{
    public interface IBulkStockSlotRepository
    {
        /// <summary>
        /// Lấy SlotId của Slot ảo A0, tự tạo Warehouse/Rack/Slot nếu chưa có.
        /// PHẢI được gọi trong transaction (Uow.Begin() đã chạy) — dùng
        /// sp_getapplock để chống 2 transaction cùng tạo trùng lần đầu.
        /// </summary>
        int GetOrCreateVirtualSlotId(string warehouseName, string rackName, int capacity);

        /// <summary>
        /// Khoá dòng Slot (UPDLOCK, ROWLOCK) trong transaction hiện tại — mọi
        /// transaction khác cũng gọi hàm này trên cùng SlotId sẽ phải CHỜ cho
        /// tới khi transaction này Commit/Rollback. Đây là điểm serialize duy
        /// nhất cần thiết để tránh lost-update trên SlotLot.
        /// </summary>
        void LockSlotForUpdate(int slotId);

        /// <summary>
        /// Chỉ đọc LOT hiện tại của Slot. Mọi mutation Slot/SlotLot phải đi qua
        /// IStockMovementService; interface này không còn expose bulk write.
        /// </summary>
        List<LotInfo> GetLots(int slotId);
    }
}