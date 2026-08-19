using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Repositories
{
  
    public interface IStockHistoryRepository
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
