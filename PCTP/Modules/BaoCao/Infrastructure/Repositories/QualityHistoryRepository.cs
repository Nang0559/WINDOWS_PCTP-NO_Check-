using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Application.Contracts.Repositories;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
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

        public Task<IReadOnlyList<string>> GetItemCodesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = @"
SELECT DISTINCT ItemCode
FROM InspectionLog
WHERE ItemCode IS NOT NULL AND LTRIM(RTRIM(ItemCode)) <> ''
ORDER BY ItemCode";
            var table = LoadData(sql);
            var values = new List<string>(table.Rows.Count);
            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (row[0] != DBNull.Value)
                {
                    string value = Convert.ToString(row[0]);
                    if (!string.IsNullOrWhiteSpace(value))
                        values.Add(value.Trim());
                }
            }
            return Task.FromResult<IReadOnlyList<string>>(values);
        }

        public Task<IReadOnlyList<QualityHistoryRow>> SearchAsync(
            DateTime from,
            DateTime to,
            string itemCode,
            string result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // "Đến ngày" thường là 23:59:59.9999999. SQL DateTime làm tròn giá trị này sang 00:00:00 của NGÀY SAU
            // nên BETWEEN lấy nhầm phiên đầu ngày hôm sau. Với cuối ngày thì dùng cận trên loại trừ (ngày sau, <).
            bool endOfDay = to.TimeOfDay >= new TimeSpan(23, 59, 59);
            DateTime upper = endOfDay ? to.Date.AddDays(1) : to;
            var where = "WHERE CheckedAt >= @From AND CheckedAt " + (endOfDay ? "<" : "<=") + " @To";
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@From", SqlDbType.DateTime) { Value = from },
                new SqlParameter("@To", SqlDbType.DateTime) { Value = upper }
            };

            if (!string.IsNullOrWhiteSpace(itemCode))
            {
                // The shared lookup sends an exact item code in item mode and free text
                // in LOT/QR mode. InspectionLog currently stores LOT values, not a QR
                // payload column, so LOT/QR mode searches the session LOT and box LOT.
                if (itemCode.StartsWith("LOTQR:", StringComparison.OrdinalIgnoreCase))
                {
                    string keyword = itemCode.Substring(6).Trim();

                    // LOT ghép "LOT1-SL1,LOT2-SL2" được tách thành từng LOT; mỗi LOT tra riêng bằng khóa
                    // 20 ký tự (hoặc 13 ký tự với chuẩn cũ) rồi OR lại. LOT in đầy đủ dài hơn 20 ký tự
                    // cũng quy về khóa nên vẫn khớp LOT tổng / LOT thùng đã lưu.
                    List<string> keys = BuildLotSearchKeys(keyword);
                    var lotPredicates = new List<string>();
                    for (int i = 0; i < keys.Count; i++)
                    {
                        string name = "@LotSearch" + i;
                        lotPredicates.Add(@"(
                        LotNoTong LIKE " + name + @"
                        OR EXISTS (
                            SELECT 1 FROM InspectionLog detail
                            WHERE detail.InspectionCode = InspectionLog.InspectionCode
                              AND detail.BoxLotNo LIKE " + name + @"
                        )
                        OR InspectionCode LIKE " + name + @"
                    )");
                        parameters.Add(new SqlParameter(name, SqlDbType.NVarChar, 200)
                        { Value = "%" + keys[i] + "%" });
                    }
                    if (lotPredicates.Count > 0)
                        where += " AND (" + string.Join(" OR ", lotPredicates) + ")";
                }
                else
                {
                    // Match the shared lookup behavior: allow a complete code or
                    // a partial code typed into the item tab. Trim stored values too,
                    // because older inspection rows may contain padded ItemCode data.
                    where += " AND LTRIM(RTRIM(ISNULL(ItemCode, N''))) LIKE @ItemCode";
                    parameters.Add(new SqlParameter("@ItemCode", SqlDbType.NVarChar, 202)
                    { Value = "%" + itemCode.Trim() + "%" });
                }
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

        /// <summary>
        /// Tách chuỗi nhập (LOT đơn, LOT in đầy đủ, LOT ghép hoặc mã phiên) thành các khóa tra cứu.
        /// Chuỗi không phải LOT (vd mã phiên) được giữ nguyên.
        /// </summary>
        internal static List<string> BuildLotSearchKeys(string keyword)
        {
            var keys = new List<string>();
            if (string.IsNullOrWhiteSpace(keyword)) return keys;

            IEnumerable<string> lots;
            try
            {
                List<KeyValuePair<string, decimal>> parsed = keyword.IndexOf(',') >= 0
                    ? LotCodeHelper.ParseCompositeLot(keyword)
                    : new List<KeyValuePair<string, decimal>>();
                lots = parsed.Count > 0 ? parsed.Select(x => x.Key) : new[] { keyword.Trim() };
            }
            catch (FormatException)
            {
                lots = new[] { keyword.Trim() };
            }

            foreach (string lot in lots)
            {
                // Chỉ cắt về 20 ký tự khi LOT dài hơn khóa (LOT in đầy đủ). Chuỗi ngắn hơn giữ nguyên
                // như người dùng gõ để vẫn tra được theo một phần LOT / mã phiên.
                string key = LotCodeHelper.ClassifyLotKey(lot) == LotKeyKind.Full20
                    ? LotCodeHelper.GetFullKey(lot)
                    : (lot ?? string.Empty).Trim();
                if (key.Length > 0 && !keys.Contains(key, StringComparer.OrdinalIgnoreCase))
                    keys.Add(key);
            }
            return keys;
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
