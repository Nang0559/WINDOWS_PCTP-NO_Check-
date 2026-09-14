using PCTP.Modules.NhapKho.Repository;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.NhapKho.Services
{
    public sealed class StockTpLookupService : IStockTpLookupService
    {
        private readonly IStockTpRepository _repository;

        public StockTpLookupService(IStockTpRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public StockItem GetByLot(string lotNo)
        {
            if (string.IsNullOrWhiteSpace(lotNo))
                return null;
            return _repository.GetByLot(lotNo);
        }

        public DataTable GetTonKhoHienTai() => _repository.GetTonKhoHienTai();

        public DataTable GetTonKhoTheoLot(List<string> lots) => _repository.GetTonKhoTheoLot(lots);
    }
}
