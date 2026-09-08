using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoCore.Services
{
    public interface IStockHistoryService
    {
        void SaveHistory(
            string actionType,
            string itemCode,
            LotInfo lot,
            int? fromSlotId,
            int? toSlotId,
            string performedBy);
    }

    
}
