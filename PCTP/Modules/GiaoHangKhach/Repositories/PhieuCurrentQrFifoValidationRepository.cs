using PCTP.FuctionMain;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    /// <summary>
    /// CNK-time FIFO validator. Unlike the legacy validation entry point,
    /// this validator considers only delivery rows that still own a current QR
    /// record through DOCQR.STTBAN.
    /// </summary>
    internal sealed class PhieuCurrentQrFifoValidationRepository : SqlRepositoryBase
    {
        public PhieuCurrentQrFifoValidationRepository(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow)
        {
        }

        public List<FifoViolation> Check(string tmpTable, string docQRTable)
        {
            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

            const int keyLen = PCTP.Common.LotCodeHelper.LEN_LEGACY_KEY;
            var result = new List<FifoViolation>();

            // CNK FIFO scope is defined by the QR linkage, not by GridView focus
            // and not by MAHANG/SOLUONG. A row with no current QR must not consume
            // FIFO allocation and must not generate a FIFO violation.
            DataTable selectedRows = LoadData($@"
SELECT
    tmp.STT,
    tmp.MAHANG AS MaHang,
    tmp.LOT AS LotDaChon,
    ISNULL(tmp.SOLUONG, 0) AS SoLuong
FROM [{tmpTable}] tmp
INNER JOIN FVN_ItemFifoConfig cfg
    ON cfg.ItemCode = tmp.MAHANG
   AND ISNULL(cfg.EnforceFifo, 0) = 1
WHERE ISNULL(tmp.STATUS, '') NOT IN ('NG', 'OK')
  AND ISNULL(tmp.LOT, '') <> ''
  AND EXISTS
  (
      SELECT 1
      FROM [{docQRTable}] qr
      WHERE ISNULL(qr.STTBAN, 0) = tmp.STT
  )
ORDER BY tmp.MAHANG, tmp.STT; ");

            foreach (var partGroup in selectedRows.AsEnumerable()
                .GroupBy(r => r["MaHang"]?.ToString()?.Trim() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase))
            {
                string maHang = partGroup.Key;
                if (string.IsNullOrEmpty(maHang))
                    continue;

                DataTable fifoRows = LoadData($@"
SELECT
    LEFT(LOT, {keyLen}) AS LOTKEY,
    MIN(LOT) AS LOTDISPLAY,
    SUM(ISNULL(SLCONLAI, 0)) AS SLCONLAI
FROM STOCKTP
WHERE PART = @ma
  AND ISNULL(SLCONLAI, 0) > 0
  AND LEN(ISNULL(LOT, '')) >= {keyLen}
GROUP BY PART, LEFT(LOT, {keyLen})
ORDER BY
    LEFT(LEFT(LOT, {keyLen}), 6),
    CASE SUBSTRING(LEFT(LOT, {keyLen}), 12, 1)
        WHEN '0' THEN 0
        WHEN '1' THEN 1
        WHEN '2' THEN 2
        WHEN '3' THEN 3
        ELSE 9
    END,
    LEFT(LOT, {keyLen});",
                    new SqlParameter("@ma", maHang));

                var fifo = fifoRows.AsEnumerable()
                    .Select(r => new FifoStockLine
                    {
                        Key = r["LOTKEY"]?.ToString()?.Trim() ?? string.Empty,
                        Display = r["LOTDISPLAY"]?.ToString()?.Trim() ?? string.Empty,
                        Stock = r["SLCONLAI"] == DBNull.Value
                            ? 0
                            : Convert.ToInt32(r["SLCONLAI"])
                    })
                    .Where(x => !string.IsNullOrEmpty(x.Key) && x.Stock > 0)
                    .ToList();

                if (fifo.Count == 0)
                {
                    foreach (DataRow row in partGroup)
                    {
                        result.Add(new FifoViolation
                        {
                            Stt = SafeInt(row["STT"]),
                            MaHang = maHang,
                            LotDaChon = row["LotDaChon"]?.ToString()?.Trim() ?? string.Empty,
                            LotDungRaPhaiChon = string.Empty,
                            SlotIdDungRaPhaiChon = 0,
                            SoLuong = SafeInt(row["SoLuong"])
                        });
                    }
                    continue;
                }

                var selectionsByRow = partGroup
                    .Select(row => new RowSelection
                    {
                        Stt = SafeInt(row["STT"]),
                        LotText = row["LotDaChon"]?.ToString()?.Trim() ?? string.Empty,
                        SoLuong = SafeInt(row["SoLuong"]),
                        Selections = ParseLotSelections(
                            row["LotDaChon"]?.ToString(),
                            SafeInt(row["SoLuong"]))
                    })
                    .Where(x => x.Selections.Count > 0)
                    .OrderBy(x => x.Stt)
                    .ToList();

                int totalSelectedQty = selectionsByRow
                    .Sum(x => x.Selections.Sum(s => s.Quantity));

                var allowedByLot = BuildAllowedAllocation(fifo, totalSelectedQty);

                foreach (RowSelection row in selectionsByRow)
                {
                    var trial = new Dictionary<string, int>(
                        allowedByLot,
                        StringComparer.OrdinalIgnoreCase);

                    string requiredLot = string.Empty;
                    bool rowValid = true;

                    foreach (LotSelection selection in row.Selections)
                    {
                        int remaining;
                        if (!trial.TryGetValue(selection.LotKey, out remaining)
                            || remaining < selection.Quantity)
                        {
                            rowValid = false;
                            requiredLot = FindRequiredLot(fifo, trial);
                            break;
                        }

                        trial[selection.LotKey] = remaining - selection.Quantity;
                    }

                    if (!rowValid)
                    {
                        result.Add(new FifoViolation
                        {
                            Stt = row.Stt,
                            MaHang = maHang,
                            LotDaChon = row.LotText,
                            LotDungRaPhaiChon = requiredLot,
                            SlotIdDungRaPhaiChon = 0,
                            SoLuong = row.SoLuong
                        });
                        continue;
                    }

                    allowedByLot = trial;
                }
            }

            return result
                .GroupBy(x => new { x.Stt, x.MaHang, x.LotDaChon })
                .Select(g => g.First())
                .ToList();
        }

        private static Dictionary<string, int> BuildAllowedAllocation(
            List<FifoStockLine> fifo,
            int requiredQty)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int remaining = Math.Max(requiredQty, 0);

            foreach (FifoStockLine row in fifo)
            {
                if (remaining <= 0)
                    break;

                int allowed = Math.Min(remaining, row.Stock);
                if (allowed <= 0)
                    continue;

                result[row.Key] = allowed;
                remaining -= allowed;
            }

            return result;
        }

        private static string FindRequiredLot(
            List<FifoStockLine> fifo,
            Dictionary<string, int> remainingAllowed)
        {
            foreach (FifoStockLine row in fifo)
            {
                int remaining;
                if (remainingAllowed.TryGetValue(row.Key, out remaining)
                    && remaining > 0)
                    return row.Display;
            }

            return fifo.Count == 0 ? string.Empty : fifo[0].Display;
        }

        private static int SafeInt(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0;

            int.TryParse(value.ToString(), out int result);
            return result;
        }

        private static List<LotSelection> ParseLotSelections(
            string value,
            int defaultQuantity)
        {
            var result = new List<LotSelection>();
            if (string.IsNullOrWhiteSpace(value))
                return result;

            foreach (string token in value.Split(
                new[] { ',' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                string part = token.Trim();
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                int separator = part.LastIndexOf('-');
                if (separator <= 0 || separator >= part.Length - 1)
                {
                    if (defaultQuantity <= 0)
                        continue;

                    string key = part.Length <= 13
                        ? part
                        : part.Substring(0, 13);

                    result.Add(new LotSelection
                    {
                        LotKey = key,
                        Lot = part,
                        Quantity = defaultQuantity
                    });
                    continue;
                }

                string lotPart = part.Substring(0, separator).Trim();
                string qtyPart = part.Substring(separator + 1).Trim();

                int quantity;
                if (!int.TryParse(qtyPart, out quantity) || quantity <= 0)
                {
                    if (defaultQuantity <= 0)
                        continue;

                    string key = part.Length <= 13
                        ? part
                        : part.Substring(0, 13);

                    result.Add(new LotSelection
                    {
                        LotKey = key,
                        Lot = part,
                        Quantity = defaultQuantity
                    });
                    continue;
                }

                string lotKey = lotPart.Length <= 13
                    ? lotPart
                    : lotPart.Substring(0, 13);

                result.Add(new LotSelection
                {
                    LotKey = lotKey,
                    Lot = lotPart,
                    Quantity = quantity
                });
            }

            return result;
        }

        private sealed class LotSelection
        {
            public string LotKey { get; set; }
            public string Lot { get; set; }
            public int Quantity { get; set; }
        }

        private sealed class RowSelection
        {
            public int Stt { get; set; }
            public string LotText { get; set; }
            public int SoLuong { get; set; }
            public List<LotSelection> Selections { get; set; }
        }

        private sealed class FifoStockLine
        {
            public string Key { get; set; }
            public string Display { get; set; }
            public int Stock { get; set; }
        }
    }
}
