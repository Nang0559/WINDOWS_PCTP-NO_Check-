using System;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.NhapKho.Interfaces;
using PCTP.VIEWSTOCK.Models;

namespace PCTP.Modules.NhapKho.Application.Adapters
{
    /// <summary>
    /// Transitional adapter from KhoCore receiving persistence to the legacy
    /// NhapKho STOCKTP repository. Remove after STOCKTP storage is moved under KhoCore.
    /// </summary>
    public sealed class StockReceivingRepositoryAdapter : IStockReceivingRepository
    {
        private readonly IStockTpRepository _legacy;

        public StockReceivingRepositoryAdapter(IStockTpRepository legacy)
        {
            _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        }

        public bool Exists(string lotNo)
        {
            return _legacy.ExistsStockTp(lotNo);
        }

        public int GetReceivedQuantity(string lotNo)
        {
            return _legacy.GetSlDaNhap(lotNo);
        }

        public void Insert(StockReceivingRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            _legacy.InsertStockTp(new NhapKhoItem
            {
                Lot = record.LotNo,
                Part = record.ItemCode,
                Name = record.ItemName,
                Model = record.Model,
                CaSX = ParseCaSX(record.ProductionCase),   // ← sửa
                NgaySX = record.ProductionDate,
                SlSanXuat = record.ProductionQuantity,
                SlNhap = record.ReceivedQuantity
            }, record.Status);
        }

        private static int ParseCaSX(string productionCase)
            => int.TryParse(productionCase, out int ca) ? ca : 0;

        public void Update(string lotNo, int receivedQuantityDelta, int status)
        {
            _legacy.UpdateStockTp(lotNo, receivedQuantityDelta, status);
        }
    }
}
