using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.XuatKho.Interfaces;
using System;

namespace PCTP.Modules.XuLyHangLoi.Application.Adapters
{
    /// <summary>
    /// Transitional adapter for XuLyHangLoi stock-balance operations.
    /// The legacy STOCKTP repository remains the storage implementation during migration.
    /// Keep this adapter at the module boundary; KhoCore must not reference XuLyHangLoi.
    /// </summary>
    public sealed class ReworkStockBalanceAdapter : IStockBalanceRepository
    {
        private readonly IStockExportRepository _legacy;

        public ReworkStockBalanceAdapter(IStockExportRepository legacy)
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
