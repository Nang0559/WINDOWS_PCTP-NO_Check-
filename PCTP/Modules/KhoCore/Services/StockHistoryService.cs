using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoCore.Services
{
    public sealed class StockHistoryService : IStockHistoryService
    {
        private readonly IStockHistoryRepository _repository;

        public StockHistoryService(IStockHistoryRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void SaveHistory(
            string actionType,
            string itemCode,
            LotInfo lot,
            int? fromSlotId,
            int? toSlotId,
            string performedBy)
            => _repository.SaveHistory(actionType, itemCode, lot, fromSlotId, toSlotId, performedBy);
    }
}
