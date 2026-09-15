using PCTP.Modules.KhoCore.Models;
using System.Collections.Generic;


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
