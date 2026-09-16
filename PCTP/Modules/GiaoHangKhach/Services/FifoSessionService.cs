using PCTP.Modules.GiaoHangKhach.Intefaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public sealed class FifoSessionService
    {
        private readonly IItemFifoConfigRepository _configRepository;
        private readonly IPhieuLotRepository _lotRepository;
        private readonly PhieuSqlExecutor _db;

        public FifoSessionService(
            IItemFifoConfigRepository configRepository,
            IPhieuLotRepository lotRepository,
            PhieuSqlExecutor db)
        {
            _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
            _lotRepository = lotRepository ?? throw new ArgumentNullException(nameof(lotRepository));
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public void Initialize(FifoSessionState state, DataTable orderRows)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            state.Reset();
            if (orderRows == null || orderRows.Rows.Count == 0) return;

            var grouped = orderRows.AsEnumerable()
                .Select(row => new
                {
                    ItemCode = GetString(row, "MAHANG", "MAHANGFCC", "MAHANGHVN"),
                    Quantity = GetInt(row, "SOLUONG", "SLGIAO", "SLXUAT", "SL")
                })
                .Where(x => !string.IsNullOrEmpty(x.ItemCode) && x.Quantity > 0)
                .GroupBy(x => x.ItemCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { ItemCode = g.Key, NeedQty = g.Sum(x => x.Quantity) })
                .ToList();

            InitializeParts(state, grouped.Select(x => new FifoOrderLine { ItemCode = x.ItemCode, NeedQty = x.NeedQty }));
        }

        /// <summary>
        /// Reads the actual temporary delivery order and subtracts QR quantities
        /// already scanned in this session. This is used lazily from LoadAll()
        /// after the order synchronization has completed.
        /// </summary>
        public void InitializeFromTables(FifoSessionState state, string tmpTable, string docQrTable)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            _db.ValidateTableName(tmpTable);
            _db.ValidateTableName(docQrTable);

            DataTable orders = _db.LoadData($@"
SELECT t.MAHANG, SUM(ISNULL(t.SOLUONG, 0)) AS SOLUONG
FROM [{tmpTable}] t
WHERE (t.LOT = '' OR t.LOT IS NULL)
  AND t.MAHANG IN
  (
      SELECT q.MAHANGFCC
      FROM [{docQrTable}] q
      WHERE ISNULL(q.KETQUA, '') <> 'DG'
      GROUP BY q.MAHANGFCC
  )
GROUP BY t.MAHANG");

            DataTable scanned = _db.LoadData($@"
SELECT MAHANGFCC, SUM(ISNULL(SLTEMFCC, 0)) AS SLDAQUET
FROM [{docQrTable}]
WHERE ISNULL(KETQUA, '') <> 'DG'
GROUP BY MAHANGFCC");

            var scannedMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in scanned.Rows)
            {
                string part = row["MAHANGFCC"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(part)) continue;
                scannedMap[part] = row["SLDAQUET"] == DBNull.Value ? 0 : Convert.ToInt32(row["SLDAQUET"]);
            }

            var orderLines = new List<FifoOrderLine>();
            foreach (DataRow row in orders.Rows)
            {
                string part = row["MAHANG"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(part)) continue;

                int orderQty = row["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(row["SOLUONG"]);
                int scannedQty = scannedMap.TryGetValue(part, out int scannedValue) ? scannedValue : 0;
                int remaining = Math.Max(orderQty - scannedQty, 0);
                if (remaining > 0)
                    orderLines.Add(new FifoOrderLine { ItemCode = part, NeedQty = remaining });
            }

            state.Reset();
            InitializeParts(state, orderLines);
        }

        private void InitializeParts(FifoSessionState state, IEnumerable<FifoOrderLine> orderLines)
        {
            foreach (var item in orderLines
                .Where(x => x != null && !string.IsNullOrEmpty(x.ItemCode) && x.NeedQty > 0)
                .GroupBy(x => x.ItemCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { ItemCode = g.Key, NeedQty = g.Sum(x => x.NeedQty) }))
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

        private static string GetString(DataRow row, params string[] columns)
        {
            foreach (string column in columns)
            {
                if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) continue;
                string value = row[column]?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(value)) return value;
            }
            return string.Empty;
        }

        private static int GetInt(DataRow row, params string[] columns)
        {
            foreach (string column in columns)
            {
                if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) continue;
                int value;
                if (int.TryParse(row[column].ToString(), out value)) return value;
            }
            return 0;
        }

        private sealed class FifoOrderLine
        {
            public string ItemCode { get; set; }
            public int NeedQty { get; set; }
        }
    }
}
