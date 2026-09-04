using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Application.Interfaces
{
    public interface IWarehouseService
    {
        List<Warehouse> GetAll();

        //void Save(Warehouse warehouse);

        string GetProductName(string itemCode);
        InspectionConfig GetInspectionConfig(string itemCode);
        bool Exists(string warehouseName);
        List<string> GetRackNames(string warehouseName);
        DataTable GetActiveItemList();

        /// <summary>
        /// Đăng ký kho: tạo Warehouse nếu chưa có, tạo Rack mới, tạo đủ SlotCount = RowCount*ColumnCount
        /// slot trống trong Rack đó với capacity chỉ định. Toàn bộ chạy trong 1 transaction.
        /// </summary>
        void RegisterWarehouseAndRack(
            string warehouseName, string rackName,
            int rowCount, int columnCount, int slotCapacity);


    }
}
