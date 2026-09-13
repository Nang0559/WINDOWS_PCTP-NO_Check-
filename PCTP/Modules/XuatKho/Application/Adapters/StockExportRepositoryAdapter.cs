using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.XuatKho.Interfaces;
using System;

namespace PCTP.Modules.XuatKho.Application.Adapters
{
    /// <summary>
    /// Transitional adapter from the legacy XuatKho STOCKTP repository to the
    /// KhoCore stock-balance port. Keep this adapter on the legacy/module side;
    /// KhoCore must not depend on XuatKho.
    /// </summary>
    public sealed class StockExportRepositoryAdapter : IStockBalanceRepository
    {
        private readonly IStockExportRepository _legacy;

        public StockExportRepositoryAdapter(IStockExportRepository legacy)
        {
            _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        }

        public int GetAvailableQuantity(string lotNo)
        {
            return _legacy.GetSlConLai(lotNo);
        }

        public void DecreaseAvailableQuantity(string lotNo, int quantity)
        {
            _legacy.DecreaseStockTp(lotNo, quantity);
        }

        public void AdjustAvailableQuantity(string lotNo, int delta)
        {
            _legacy.AdjustSlConLai(lotNo, delta);
        }

        public bool TryDecreaseAvailableQuantity(string lotNo, int quantity)
        {
            return _legacy.TryDecreaseSlConLai(lotNo, quantity);
        }
    }
}
