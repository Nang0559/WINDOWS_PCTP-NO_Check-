using PCTP.Modules.GiaoHangKhach.Intefaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Builds the RAM FIFO snapshot when the QR-reading session starts.
    /// </summary>
    public sealed class FifoSessionService
    {
        private readonly IItemFifoConfigRepository _configRepository;
        private readonly IPhieuLotRepository _lotRepository;

        public FifoSessionService(
            IItemFifoConfigRepository configRepository,
            IPhieuLotRepository lotRepository)
        {
            _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
            _lotRepository = lotRepository ?? throw new ArgumentNullException(nameof(lotRepository));
        }

        public void Initialize(FifoSessionState state, DataTable orderRows)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            state.Reset();
            if (orderRows == null || orderRows.Rows.Count == 0) return;

            var grouped = orderRows.AsEnumerable()
                .Select(row => new
                {
                    ItemCode = row.Table.Columns.Contains("MAHANG")
                        ? row["MAHANG"]?.ToString().Trim() ?? string.Empty
                        : string.Empty,
                    Quantity = row.Table.Columns.Contains("SOLUONG") && row["SOLUONG"] != DBNull.Value
                        ? Convert.ToInt32(row["SOLUONG"])
                        : 0
                })
                .Where(x => !string.IsNullOrEmpty(x.ItemCode) && x.Quantity > 0)
                .GroupBy(x => x.ItemCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    ItemCode = g.Key,
                    NeedQty = g.Sum(x => x.Quantity)
                })
                .ToList();

            foreach (var item in grouped)
            {
                if (!_configRepository.GetEnforceFifo(item.ItemCode))
                    continue;

                DataTable stockTable = _lotRepository.GetDanhSachLotTuKho(item.ItemCode);
                var stock = new List<FifoStockLine>();

                foreach (DataRow row in stockTable.Rows)
                {
                    int available = row.Table.Columns.Contains("SLCONLAI") && row["SLCONLAI"] != DBNull.Value
                        ? Convert.ToInt32(row["SLCONLAI"])
                        : 0;
                    if (available <= 0) continue;

                    int rank = row.Table.Columns.Contains("FIFO_RANK") && row["FIFO_RANK"] != DBNull.Value
                        ? Convert.ToInt32(row["FIFO_RANK"])
                        : int.MaxValue;

                    stock.Add(new FifoStockLine
                    {
                        LotKey = FifoSessionState.NormalizeLot(row["LOT"]?.ToString()),
                        DisplayLot = row["LOT"]?.ToString()?.Trim() ?? string.Empty,
                        AvailableQty = available,
                        FifoRank = rank
                    });
                }

                state.Initialize(item.ItemCode, item.NeedQty, stock);
            }
        }
    }
}
