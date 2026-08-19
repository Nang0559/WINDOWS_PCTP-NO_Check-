using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Application.Interfaces
{
    public interface IRackService
    {
        List<Rack> GetByWarehouse(int warehouseId);

        Rack GetById(int rackId);

        int Create(
            int warehouseId,
            string rackName,
            int rowCount,
            int columnCount);

        void UpdateLayout(
            int rackId,
            int rowCount,
            int columnCount);

        void Delete(int rackId);
    }
}
