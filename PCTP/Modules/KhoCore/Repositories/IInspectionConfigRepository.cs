using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoCore.Repositories
{
    public interface IInspectionConfigRepository
    {
        List<InspectionConfig> GetAll();
        InspectionConfig GetByItemCode(string itemCode);
        int Insert(InspectionConfig config);
        void Update(InspectionConfig config);
        void Delete(int configId);
    }
}
