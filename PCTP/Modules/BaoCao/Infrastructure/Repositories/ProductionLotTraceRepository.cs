using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Infrastructure.Repositories
{
    /// <summary>Thông tin sản xuất của một LOT thành phẩm (đọc từ dbo.STOCKTP, chỉ các cột sản xuất).</summary>
    public sealed class ProductionLotTraceRow
    {
        public string Lot { get; set; }
        public string PartNo { get; set; }
        public string PartName { get; set; }
        public string Model { get; set; }
        public int? Shift { get; set; }
        public DateTime? ProductionDate { get; set; }
        public decimal? ProducedQty { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public decimal? ReceivedQty { get; set; }
        public string LineCode { get; set; }
        public string DeptCode { get; set; }
    }

    public interface IProductionLotTraceQuery
    {
        /// <summary>Tìm bản ghi sản xuất theo LOT (khớp khóa 13/20 ký tự như LotCodeHelper) và mã hàng.</summary>
        Task<IReadOnlyList<ProductionLotTraceRow>> FindByLotAsync(
            string lotNo, string partNo, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Read-only. Chỉ lấy dữ kiện sản xuất (ngày/ca/SL sản xuất, ngày/SL nhập TP, line, bộ phận).
    /// Không đọc SLCONLAI/SLCONLAITMP: tồn hiện tại không phải bằng chứng lịch sử.
    /// </summary>
    public sealed class ProductionLotTraceRepository : SqlRepositoryBase, IProductionLotTraceQuery
    {
        public ProductionLotTraceRepository() : this(new SQLPROVIDER()) { }

        public ProductionLotTraceRepository(SQLPROVIDER sql)
            : base(
                new PhieuSqlExecutor(sql ?? throw new ArgumentNullException("sql")),
                new UnitOfWork(sql ?? throw new ArgumentNullException("sql")))
        {
        }

        public Task<IReadOnlyList<ProductionLotTraceRow>> FindByLotAsync(
            string lotNo, string partNo, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = new List<ProductionLotTraceRow>();
            if (string.IsNullOrWhiteSpace(lotNo))
                return Task.FromResult<IReadOnlyList<ProductionLotTraceRow>>(result);

            string lot = lotNo.Trim();
            string sql = @"
SELECT TOP (50)
    S.LOT, S.PART, S.NAME, S.MODEL, S.CASX, S.NGAYSX, S.SLSX,
    S.NGAYNHAP, S.SLNHAP, S.LineCodes, S.DeptCode
FROM dbo.STOCKTP S
WHERE " + LotCodeHelper.BuildLotMatchSql("S.LOT", "@Lot") + @"
  AND (@Part IS NULL OR LTRIM(RTRIM(S.PART)) = @Part)
ORDER BY S.NGAYSX DESC, S.LOT";

            DataTable table = LoadData(sql,
                new SqlParameter("@Lot", SqlDbType.NVarChar, 100) { Value = lot },
                new SqlParameter("@Part", SqlDbType.NVarChar, 100)
                {
                    Value = string.IsNullOrWhiteSpace(partNo) ? (object)DBNull.Value : partNo.Trim()
                });

            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string rowLot = Str(row, "LOT");
                // Khớp lại ở C# để giữ cùng quy ước với LotCodeHelper (tránh khớp nhầm prefix/suffix).
                if (!LotCodeHelper.AreLotKeysEquivalent(rowLot, lot))
                    continue;

                result.Add(new ProductionLotTraceRow
                {
                    Lot = rowLot,
                    PartNo = Str(row, "PART"),
                    PartName = Str(row, "NAME"),
                    Model = Str(row, "MODEL"),
                    Shift = Int(row, "CASX"),
                    ProductionDate = Date(row, "NGAYSX"),
                    ProducedQty = Dec(row, "SLSX"),
                    ReceivedDate = Date(row, "NGAYNHAP"),
                    ReceivedQty = Dec(row, "SLNHAP"),
                    LineCode = Str(row, "LineCodes"),
                    DeptCode = Str(row, "DeptCode")
                });
            }

            return Task.FromResult<IReadOnlyList<ProductionLotTraceRow>>(result);
        }

        private static string Str(DataRow r, string c) =>
            r.Table.Columns.Contains(c) && r[c] != DBNull.Value ? Convert.ToString(r[c]).Trim() : string.Empty;

        private static int? Int(DataRow r, string c) =>
            r.Table.Columns.Contains(c) && r[c] != DBNull.Value ? (int?)Convert.ToInt32(r[c]) : null;

        private static decimal? Dec(DataRow r, string c) =>
            r.Table.Columns.Contains(c) && r[c] != DBNull.Value ? (decimal?)Convert.ToDecimal(r[c]) : null;

        private static DateTime? Date(DataRow r, string c) =>
            r.Table.Columns.Contains(c) && r[c] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r[c]) : null;
    }
}
