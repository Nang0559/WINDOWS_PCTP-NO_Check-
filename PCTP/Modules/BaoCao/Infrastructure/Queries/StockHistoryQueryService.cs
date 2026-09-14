using PCTP.ClassSQL;
using PCTP.Modules.BaoCao.Application.Contracts;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Infrastructure.Queries
{
    /// <summary>
    /// Transitional read adapter for the existing StockHistory stored procedure.
    /// SQL is isolated here; the UI/application layer never knows the procedure name.
    /// </summary>
    public sealed class StockHistoryQueryService : IStockHistoryQuery
    {
        private readonly SQLPROVIDER _sql;

        public StockHistoryQueryService()
        {
            _sql = new SQLPROVIDER();
        }

        public Task<IReadOnlyList<string>> GetItemCodesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var connection = new SqlConnection(_sql.B7R2_FCCdbb))
            using (var command = new SqlCommand(
                "SELECT DISTINCT ItemCode FROM StockHistory WHERE ItemCode IS NOT NULL ORDER BY ItemCode",
                connection))
            {
                connection.Open();

                var result = new List<string>();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!reader.IsDBNull(0))
                            result.Add(Convert.ToString(reader.GetValue(0)));
                    }
                }

                return Task.FromResult<IReadOnlyList<string>>(result);
            }
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

            var table = _sql.LoadData(_sql.B7R2_FCCdbb, "sp_GetStockHistory", parameters);
            var rows = table.AsEnumerable()
                .Select(Map)
                .ToList();

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
