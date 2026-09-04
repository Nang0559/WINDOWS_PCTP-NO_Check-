using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoCore.Interfaces
{
    public interface IInspectionConfigService
    {
        List<InspectionConfig> GetAll();
        InspectionConfig GetByItemCode(string itemCode);
        void Save(InspectionConfig config);     // Insert hoặc Update
        void Delete(int configId);
        bool NeedsInspection(string itemCode); // true nếu IsActive = true
    }
}
