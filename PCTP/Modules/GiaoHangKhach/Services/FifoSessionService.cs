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

        public void InitializeFromTables(FifoSessionState state, string tmpTable, string docQrTable)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            _db.ValidateTableName(tmpTable);
            _db.ValidateTableName(docQrTable);

            // TMP có cột STATUS ('OK' = đã Cập nhật kho, SLCONLAI đã trừ) thì mới nhận biết được dòng
            // đã có LOT nhưng kho CHƯA trừ.
            DataTable statusProbe = _db.LoadData($"SELECT COL_LENGTH(N'dbo.[{tmpTable}]', N'STATUS') AS L");
            bool hasStatus = statusProbe.Rows.Count > 0 && statusProbe.Rows[0]["L"] != DBNull.Value;

            // Dòng chưa có LOT (còn phải giao).
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

            // Tem đã đọc nhưng chưa Hoàn thành (chưa DG).
            DataTable scannedRows = _db.LoadData($@"
SELECT STT, MAHANGFCC, LOTFCC, SLTEMFCC
FROM [{docQrTable}]
WHERE ISNULL(KETQUA, '') <> 'DG'");

            // Dòng đã có LOT nhưng chưa Cập nhật kho: kho vẫn còn tính lượng này.
            DataTable allocatedRows = hasStatus
                ? _db.LoadData($@"
SELECT t.STT, t.MAHANG, t.LOT, t.SOLUONG
FROM [{tmpTable}] t
WHERE t.LOT IS NOT NULL AND t.LOT <> ''
  AND UPPER(LTRIM(RTRIM(ISNULL(t.[STATUS], '')))) <> 'OK'
  AND t.MAHANG IN
  (
      SELECT q.MAHANGFCC
      FROM [{docQrTable}] q
      WHERE ISNULL(q.KETQUA, '') <> 'DG'
      GROUP BY q.MAHANGFCC
  )")
                : new DataTable();

            var scannedMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in scannedRows.Rows)
            {
                string part = row["MAHANGFCC"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(part)) continue;
                int qty = row["SLTEMFCC"] == DBNull.Value ? 0 : Convert.ToInt32(row["SLTEMFCC"]);
                scannedMap[part] = (scannedMap.TryGetValue(part, out int current) ? current : 0) + qty;
            }

            var allocatedMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in allocatedRows.Rows)
            {
                string part = row["MAHANG"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(part)) continue;
                int qty = row["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(row["SOLUONG"]);
                allocatedMap[part] = (allocatedMap.TryGetValue(part, out int current) ? current : 0) + qty;
            }

            var orderLines = new List<FifoOrderLine>();
            foreach (DataRow row in orders.Rows)
            {
                string part = row["MAHANG"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(part)) continue;

                int orderQty = row["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(row["SOLUONG"]);
                int scannedQty = scannedMap.TryGetValue(part, out int scannedValue) ? scannedValue : 0;
                int allocatedQty = allocatedMap.TryGetValue(part, out int allocatedValue) ? allocatedValue : 0;

                // Còn phải đọc thêm?
                if (Math.Max(orderQty - scannedQty, 0) <= 0) continue;

                // Hàng đợi FIFO phải tính trên TOÀN BỘ nhu cầu chưa trừ kho (chưa có LOT + đã có LOT chưa trừ kho),
                // rồi mới trừ phần đã dùng. Nếu chỉ tính phần còn lại thì LOT cũ (vd tồn 262) vẫn được cấp đủ 262
                // dù tem đã đọc trước đó đã lấy mất 200 của nó.
                orderLines.Add(new FifoOrderLine { ItemCode = part, NeedQty = orderQty + allocatedQty });
            }

            state.Reset();
            InitializeParts(state, orderLines);

            // Trừ lượng LOT mà các dòng đã có LOT (chưa Cập nhật kho) đã dùng. STT âm để không trùng STT tem QR.
            foreach (DataRow row in allocatedRows.Rows)
            {
                int stt = row["STT"] == DBNull.Value ? 0 : Convert.ToInt32(row["STT"]);
                int qty = row["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(row["SOLUONG"]);
                state.ConsumeExisting(-Math.Abs(stt), row["MAHANG"]?.ToString(), row["LOT"]?.ToString(), qty);
            }

            // Trừ lượng LOT của các tem đã đọc (chưa DG) trước đó.
            foreach (DataRow row in scannedRows.Rows)
            {
                int stt = row["STT"] == DBNull.Value ? 0 : Convert.ToInt32(row["STT"]);
                int qty = row["SLTEMFCC"] == DBNull.Value ? 0 : Convert.ToInt32(row["SLTEMFCC"]);
                state.ConsumeExisting(stt, row["MAHANGFCC"]?.ToString(), row["LOTFCC"]?.ToString(), qty);
            }
        }

        public IReadOnlyList<FifoRamEntry> GetRamSnapshot(FifoSessionState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return state.GetRamSnapshot();
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