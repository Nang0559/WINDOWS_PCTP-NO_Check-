using PCTP.Modules.KhoCore.Models;
using System.Collections.Generic;


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
