using PCTP.ClassSQL;
using PCTP.Common;
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
    /// SQL persistence adapter for inspection/quality history.
    /// </summary>
    public sealed class QualityHistoryRepository : SqlRepositoryBase, IQualityHistoryRepository
    {
        public QualityHistoryRepository()
            : this(new SQLPROVIDER())
        {
        }

        public QualityHistoryRepository(SQLPROVIDER sql)
            : base(
                new PhieuSqlExecutor(sql ?? throw new ArgumentNullException("sql")),
                new UnitOfWork(sql ?? throw new ArgumentNullException("sql")))
        {
        }

        public Task<IReadOnlyList<QualityHistoryRow>> SearchAsync(
            DateTime from,
            DateTime to,
            string itemCode,
            string result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var where = "WHERE CheckedAt BETWEEN @From AND @To";
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@From", SqlDbType.DateTime) { Value = from },
                new SqlParameter("@To", SqlDbType.DateTime) { Value = to }
            };

            if (!string.IsNullOrWhiteSpace(itemCode))
            {
                where += " AND ItemCode = @ItemCode";
                parameters.Add(new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100) { Value = itemCode });
            }

            if (!string.IsNullOrWhiteSpace(result) && !string.Equals(result, "Tất cả", StringComparison.OrdinalIgnoreCase))
            {
                where += " AND FinalResult = @Result";
                parameters.Add(new SqlParameter("@Result", SqlDbType.NVarChar, 10) { Value = result });
            }

            var sql = @"
SELECT
    InspectionCode,
    MAX(ItemCode) AS ItemCode,
    MAX(LotNoTong) AS LotNoTong,
    MAX(NSXTong) AS NSXTong,
    MAX(SoLuongTong) AS SoLuongTong,
    MAX(MaPhieu) AS MaPhieu,
    COUNT(*) AS TotalBox,
    SUM(CASE WHEN IsMatch = 1 THEN 1 ELSE 0 END) AS PassCount,
    SUM(CASE WHEN IsMatch = 0 THEN 1 ELSE 0 END) AS FailCount,
    MAX(FinalResult) AS FinalResult,
    MIN(CheckedAt) AS CheckedAt
FROM InspectionLog
" + where + @"
GROUP BY InspectionCode
ORDER BY MIN(CheckedAt) DESC";

            var table = LoadData(sql, parameters.ToArray());
            var rows = table.AsEnumerable().Select(MapMaster).ToList();
            return Task.FromResult<IReadOnlyList<QualityHistoryRow>>(rows);
        }

        public Task<IReadOnlyList<QualityHistoryDetailRow>> GetDetailsAsync(
            string inspectionCode,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inspectionCode))
                return Task.FromResult<IReadOnlyList<QualityHistoryDetailRow>>(new List<QualityHistoryDetailRow>());

            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
SELECT BoxLotNo, BoxNSX, IsMatch, MismatchFields, CheckedAt
FROM InspectionLog
WHERE InspectionCode = @Code
ORDER BY LogId";

            var table = LoadData(
                sql,
                new SqlParameter("@Code", SqlDbType.NVarChar, 50) { Value = inspectionCode });

            var rows = table.AsEnumerable().Select(MapDetail).ToList();
            return Task.FromResult<IReadOnlyList<QualityHistoryDetailRow>>(rows);
        }

        private static QualityHistoryRow MapMaster(DataRow row)
        {
            return new QualityHistoryRow
            {
                InspectionCode = ReadString(row, "InspectionCode"),
                ItemCode = ReadString(row, "ItemCode"),
                LotNo = ReadString(row, "LotNoTong"),
                ProductionDate = ReadString(row, "NSXTong"),
                TotalQuantity = ReadInt(row, "SoLuongTong"),
                DocumentNo = ReadString(row, "MaPhieu"),
                TotalBox = ReadInt(row, "TotalBox"),
                PassCount = ReadInt(row, "PassCount"),
                FailCount = ReadInt(row, "FailCount"),
                FinalResult = ReadString(row, "FinalResult"),
                CheckedAt = ReadDateTime(row, "CheckedAt")
            };
        }

        private static QualityHistoryDetailRow MapDetail(DataRow row)
        {
            return new QualityHistoryDetailRow
            {
                BoxLotNo = ReadString(row, "BoxLotNo"),
                BoxProductionDate = ReadString(row, "BoxNSX"),
                IsMatch = ReadBool(row, "IsMatch"),
                MismatchFields = ReadString(row, "MismatchFields"),
                CheckedAt = ReadDateTime(row, "CheckedAt")
            };
        }

        private static string ReadString(DataRow row, string name)
        {
            return row.Table.Columns.Contains(name) && row[name] != DBNull.Value
                ? Convert.ToString(row[name])
                : null;
        }

        private static int ReadInt(DataRow row, string name)
        {
            return row.Table.Columns.Contains(name) && row[name] != DBNull.Value
                ? Convert.ToInt32(row[name])
                : 0;
        }

        private static bool ReadBool(DataRow row, string name)
        {
            return row.Table.Columns.Contains(name) && row[name] != DBNull.Value && Convert.ToBoolean(row[name]);
        }

        private static DateTime? ReadDateTime(DataRow row, string name)
        {
            return row.Table.Columns.Contains(name) && row[name] != DBNull.Value
                ? Convert.ToDateTime(row[name])
                : (DateTime?)null;
        }
    }
}
