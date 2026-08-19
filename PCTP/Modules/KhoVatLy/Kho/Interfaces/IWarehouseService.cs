using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Application.Interfaces
{
    public interface IWarehouseService
    {
        List<Warehouse> GetAll();

        void Save(Warehouse warehouse);

        string GetProductName(string itemCode);
        InspectionConfig GetInspectionConfig(string itemCode);
    }
}
