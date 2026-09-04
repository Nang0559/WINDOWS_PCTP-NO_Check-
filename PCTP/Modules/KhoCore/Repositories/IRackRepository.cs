using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Repositories
{
    public interface IRackRepository
    {
        int Create(int warehouseId, string rackName, int rowCount, int columnCount);
        
        Rack GetById(int rackId);

        List<Rack> GetRacksByWarehouse(int warehouseId);

        int Insert(int warehouseId, Rack rack);

        void Update(Rack rack);

        void Delete(int rackId);

        bool Exists(
            int warehouseId,
            string rackName);

        int GetRackId(
            int warehouseId,
            string rackName);

        void UpdateLayout(
            int rackId,
            int rowCount,
            int columnCount);
        /// <summary>Tạo 1 Slot trống trong Rack — dùng khi đăng ký Rack mới với N Slot.</summary>
        void InsertSlot(int rackId, int slotNumber, int capacity);
    }
}
