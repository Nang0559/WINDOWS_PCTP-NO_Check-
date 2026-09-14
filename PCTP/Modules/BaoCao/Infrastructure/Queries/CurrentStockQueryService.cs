using PCTP.ClassSQL;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Infrastructure.Queries
{
    /// <summary>
    /// Transitional read adapter for the existing current-stock stored procedure.
    /// </summary>
    public sealed class CurrentStockQueryService : ICurrentStockQuery
    {
        private readonly SQLPROVIDER _sql;

        public CurrentStockQueryService()
        {
            _sql = new SQLPROVIDER();
        }

        public Task<IReadOnlyList<CurrentStockRow>> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var table = _sql.LoadData(
                _sql.B7R2_FCCdbb,
                "sp_GetCurrentStockStatus",
                new SqlParameter[0]);

            var rows = new List<CurrentStockRow>();
            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(new CurrentStockRow
                {
                    ItemCode = ReadString(row, "ItemCode", "PartNo"),
                    LotNo = ReadString(row, "LotNo", "LOTNo", "LOT"),
                    QrCode = ReadString(row, "QrData", "QRData", "QRCode", "QR"),
                    Quantity = ReadDecimal(row, "Quantity", "Qty", "SoLuong", "SL"),
                    LocationCode = ReadString(row, "LocationCode", "SlotCode", "SlotId", "Location"),
                    Status = ReadString(row, "Status", "TrangThai")
                });
            }

            return Task.FromResult<IReadOnlyList<CurrentStockRow>>(rows);
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
    }
}
