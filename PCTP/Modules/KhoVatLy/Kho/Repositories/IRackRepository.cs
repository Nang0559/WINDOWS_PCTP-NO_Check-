using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Repositories
{
    public interface IRackRepository
    {
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
    }
}
