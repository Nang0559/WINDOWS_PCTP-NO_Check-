using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Application.Contracts.Repositories;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Infrastructure.Repositories
{
    /// <summary>
    /// SQL persistence adapter for the current-stock read model.
    /// </summary>
    public sealed class CurrentStockRepository : SqlRepositoryBase, ICurrentStockRepository
    {
        public CurrentStockRepository()
            : this(new SQLPROVIDER())
        {
        }

        public CurrentStockRepository(SQLPROVIDER sql)
            : base(
                new PhieuSqlExecutor(sql ?? throw new ArgumentNullException("sql")),
                new UnitOfWork(sql ?? throw new ArgumentNullException("sql")))
        {
        }

        public Task<IReadOnlyList<CurrentStockRow>> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var table = ExecuteStoredProcedure("sp_GetCurrentStockStatus");

            var rows = new List<CurrentStockRow>(table.Rows.Count);
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
                    Status = ReadString(row, "Status", "TrangThai"),
                    ItemName = ReadString(row, "ItemName", "TenSP", "NAME"),
                    Model = ReadString(row, "Model", "MODEL"),
                    ProductionDate = ReadDateTime(row, "NgaySX", "NGAYSX"),
                    ShiftNo = ReadNullableInt(row, "CaSX", "CASX"),
                    QtyProduced = ReadNullableDecimal(row, "SlSanXuat", "SLSX"),
                    QtyReceived = ReadNullableDecimal(row, "SlDaNhap", "SLNHAP"),
                    QtyIssued = ReadNullableDecimal(row, "SlDaXuat", "SLXUAT"),
                    QtyOnHandLot = ReadNullableDecimal(row, "SlTonLot", "SLCONLAI"),
                    ReceivedDate = ReadDateTime(row, "NgayNhap", "NGAYNHAP"),
                    IssuedDate = ReadDateTime(row, "NgayXuat", "NGAYXUAT"),
                    SlotImportDate = ReadDateTime(row, "SlotImportDate", "ImportDate"),
                    DocumentNo = ReadString(row, "SlotMaPhieu", "MaPhieu")
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

        // Các cột mới là tuỳ chọn: SP cũ chưa deploy thì để trống chứ không lỗi.
        private static decimal? ReadNullableDecimal(DataRow row, params string[] names)
        {
            foreach (var name in names)
            {
                if (row.Table.Columns.Contains(name) && row[name] != DBNull.Value)
                    return Convert.ToDecimal(row[name]);
            }
            return null;
        }

        private static int? ReadNullableInt(DataRow row, params string[] names)
        {
            var value = ReadNullableDecimal(row, names);
            return value.HasValue ? (int?)Convert.ToInt32(value.Value) : null;
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
