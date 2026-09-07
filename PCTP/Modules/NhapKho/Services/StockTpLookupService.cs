using PCTP.Modules.NhapKho.Repository;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.NhapKho.Services
{
    public sealed class StockTpLookupService : IStockTpLookupService
    {
        private readonly IStockTpRepository _repository;

        public StockTpLookupService(IStockTpRepository repository)
        {
            _repository = repository
                ?? throw new ArgumentNullException(nameof(repository));
        }

        public StockItem GetByLot(string lotNo)
        {
            if (string.IsNullOrWhiteSpace(lotNo))
                return null;

            return _repository.GetByLot(lotNo);
        }
    }
}
