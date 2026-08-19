using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Repository
{
    public interface IWarehouseRepository
    {
        string GetProductNameByCode(string itemCode);

        void SaveWarehouse(Warehouse warehouse);

        List<Warehouse> GetAllWarehouses();
        InspectionConfig GetInspectionConfig(string itemCode);
    }
}
