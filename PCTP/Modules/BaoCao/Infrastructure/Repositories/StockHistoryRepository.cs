using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Modules.BaoCao.Application.Contracts;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Application.Contracts.Repositories;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Infrastructure.Repositories
{
    /// <summary>
    /// SQL persistence adapter for stock history read queries.
    /// All SQL/ADO.NET access stays below the repository boundary.
    /// </summary>
    public sealed class StockHistoryRepository : SqlRepositoryBase, IStockHistoryRepository
    {
        public StockHistoryRepository()
            : this(new SQLPROVIDER())
        {
        }

        public StockHistoryRepository(SQLPROVIDER sql)
            : base(
                new PhieuSqlExecutor(sql ?? throw new ArgumentNullException("sql")),
                new UnitOfWork(sql ?? throw new ArgumentNullException("sql")))
        {
        }

        public Task<IReadOnlyList<string>> GetItemCodesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
SELECT DISTINCT ItemCode
FROM StockHistory
WHERE ItemCode IS NOT NULL
ORDER BY ItemCode";

            var table = LoadData(sql);
            var result = new List<string>(table.Rows.Count);

            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (row[0] == DBNull.Value)
                    continue;

                var value = Convert.ToString(row[0]);
                if (!string.IsNullOrWhiteSpace(value))
                    result.Add(value);
            }

            return Task.FromResult<IReadOnlyList<string>>(result);
        }

        public Task<IReadOnlyList<ItemHistoryRow>> SearchAsync(
            HistorySearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            if (criteria == null)
                throw new ArgumentNullException("criteria");

            cancellationToken.ThrowIfCancellationRequested();

            var parameters = new[]
            {
                new SqlParameter("@FromDate", criteria.From.HasValue ? (object)criteria.From.Value : DBNull.Value),
                new SqlParameter("@ToDate", criteria.To.HasValue ? (object)criteria.To.Value : DBNull.Value),
                new SqlParameter("@ItemCode", string.IsNullOrWhiteSpace(criteria.PartNo) ? (object)DBNull.Value : criteria.PartNo)
            };

            var table = LoadData("sp_GetStockHistory", parameters);
            var rows = table.AsEnumerable().Select(Map).ToList();
            return Task.FromResult<IReadOnlyList<ItemHistoryRow>>(rows);
        }

        private static ItemHistoryRow Map(DataRow row)
        {
            return new ItemHistoryRow
            {
                ItemCode = ReadString(row, "ItemCode", "PartNo"),
                QrCode = ReadString(row, "QrData", "QRData", "QRCode", "QR"),
                LotNo = ReadString(row, "LotNo", "LOTNo", "LOT"),
                ActionType = ReadString(row, "ActionType", "Action", "EventType"),
                Quantity = ReadDecimal(row, "Quantity", "Qty", "SoLuong", "SL"),
                EventTime = ReadDateTime(row, "Date", "EventTime", "CreatedAt", "Ngay"),
                FromLocation = ReadString(row, "FromSlotId", "FromLocation", "FromSlot"),
                ToLocation = ReadString(row, "ToSlotId", "ToLocation", "ToSlot"),
                DocumentNo = ReadString(row, "MaPhieu", "DocumentNo", "PhieuNo"),
                UserName = ReadString(row, "PerformedBy", "UserName", "CreatedBy")
            };
        }

        private static string ReadString(DataRow row, params string[] names)
        {
            foreach (var name in names)
            {
                if (row.Table.Columns.Contains(name) && row[name] != DBNull.Value)
                    return Convert.ToString(row[name]);
            }
            return null;
        }

        private static decimal ReadDecimal(DataRow row, params string[] names)
        {
            foreach (var name in names)
            {
                if (row.Table.Columns.Contains(name) && row[name] != DBNull.Value)
                    return Convert.ToDecimal(row[name]);
            }
            return 0m;
        }

        private static DateTime? ReadDateTime(DataRow row, params string[] names)
        {
            foreach (var name in names)
            {
                if (row.Table.Columns.Contains(name) && row[name] != DBNull.Value)
                    return Convert.ToDateTime(row[name]);
            }
            return null;
        }
    }
}
