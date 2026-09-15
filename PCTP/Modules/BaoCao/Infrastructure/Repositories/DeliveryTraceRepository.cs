using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Application.Contracts.Repositories;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Infrastructure.Repositories
{
    /// <summary>
    /// Read-only delivery trace adapter.
    ///
    /// Source of truth:
    ///   dbo.LUUPHIEUGIAOHANG
    ///   dbo.LUUDOCQRCODE
    ///
    /// STT is intentionally NOT used as the QR business join key.
    /// </summary>
    public sealed class DeliveryTraceRepository : SqlRepositoryBase, IDeliveryTraceRepository
    {
        private const string HondaVp = "HON DA - VIET NAM(NHA MAY VP)";
        private const string HondaHn = "HON DA - VIET NAM(NHA MAY HN)";
        private const string Yamaha = "YAMAHA - VIET NAM";

        public DeliveryTraceRepository()
            : this(new SQLPROVIDER())
        {
        }

        public DeliveryTraceRepository(SQLPROVIDER sql)
            : base(
                new PhieuSqlExecutor(sql ?? throw new ArgumentNullException("sql")),
                new UnitOfWork(sql ?? throw new ArgumentNullException("sql")))
        {
        }

        public Task<IReadOnlyList<string>> GetItemCodesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
SELECT DISTINCT P.MAHANG
FROM dbo.LUUPHIEUGIAOHANG P
WHERE NULLIF(LTRIM(RTRIM(P.MAHANG)), '') IS NOT NULL
ORDER BY P.MAHANG";

            return Task.FromResult<IReadOnlyList<string>>(
                LoadStringList(sql, cancellationToken));
        }

        public Task<IReadOnlyList<string>> GetCustomersAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<string> result = new List<string>
            {
                "HON DA - VIET NAM",
                Yamaha
            };

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<DeliveryTraceRow>> SearchQrAsync(
            string qrCode,
            string customerLabelData,
            string partNo,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var where = new List<string> { "1 = 1" };
            var parameters = new List<SqlParameter>();

            AddDateFilter(where, parameters, from, to);
            AddLikeFilter(where, parameters, "P.MAHANG", partNo, "@PartNo");

            if (!string.IsNullOrWhiteSpace(qrCode))
            {
                where.Add("ISNULL(D.MAFCC, '') LIKE @QrCode");
                parameters.Add(new SqlParameter("@QrCode", "%" + qrCode.Trim() + "%"));
            }

            if (!string.IsNullOrWhiteSpace(customerLabelData))
            {
                where.Add(@"(
                    ISNULL(D.LOTHVN, '') LIKE @CustomerLabel
                    OR ISNULL(D.MAHANGHVN, '') LIKE @CustomerLabel
                    OR ISNULL(D.MAFCC, '') LIKE @CustomerLabel
                )");
                parameters.Add(new SqlParameter(
                    "@CustomerLabel",
                    "%" + customerLabelData.Trim() + "%"));
            }

            return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                MapRows(Load(where, parameters), cancellationToken));
        }

        public Task<IReadOnlyList<DeliveryTraceRow>> SearchLotAsync(
            string lotNo,
            string partNo,
            string customerName,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var where = new List<string> { "1 = 1" };
            var parameters = new List<SqlParameter>();

            AddDateFilter(where, parameters, from, to);
            AddLikeFilter(where, parameters, "P.MAHANG", partNo, "@PartNo");
            AddLikeFilter(where, parameters, "P.LOT", lotNo, "@LotNo");
            AddCustomerFilter(where, parameters, customerName);

            DataTable table = Load(where, parameters);
            var result = new List<DeliveryTraceRow>(table.Rows.Count);

            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DeliveryTraceRow mapped = Map(row);

                if (!string.IsNullOrWhiteSpace(lotNo) &&
                    !ContainsLot(mapped.LotNoRaw, lotNo))
                {
                    continue;
                }

                result.Add(mapped);
            }

            return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(result);
        }

        public Task<IReadOnlyList<DeliveryTraceRow>> SearchCustomerAsync(
            string customerName,
            string partNo,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var where = new List<string> { "1 = 1" };
            var parameters = new List<SqlParameter>();

            AddDateFilter(where, parameters, from, to);
            AddLikeFilter(where, parameters, "P.MAHANG", partNo, "@PartNo");
            AddCustomerFilter(where, parameters, customerName);

            return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                MapRows(Load(where, parameters), cancellationToken));
        }

        public Task<IReadOnlyList<DeliveryTraceRow>> FindByQrAsync(
            string qrCode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(qrCode))
                return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                    new List<DeliveryTraceRow>());

            var where = new List<string>
            {
                "ISNULL(D.MAFCC, '') LIKE @QrCode"
            };
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@QrCode", "%" + qrCode.Trim() + "%")
            };

            return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                MapRows(Load(where, parameters), cancellationToken));
        }

        private DataTable Load(
            List<string> where,
            List<SqlParameter> parameters)
        {
            const string sqlPrefix = @"
SELECT
    P.STT,
    P.CUA,
    P.TRUYEN,
    P.MAHANG,
    P.TENHANG,
    P.LOT,
    P.DV,
    P.SOLUONG,
    P.NGAYGIAO,
    P.GIOGIAO,
    P.STATUS,
    P.TTPHIEU,
    P.NHAMAY,
    P.GIOGIAOFCC,
    P.GearYMVN,
    P.HOP,
    P.STATUSDOC,
    P.Note,
    ISNULL(P.PO_NO, '') AS PO_NO,
    ISNULL(P.PO_ITEM, '') AS PO_ITEM,
    D.LOTFCC,
    D.MAHANGFCC,
    D.SLTEMFCC,
    D.LOTHVN,
    D.MAHANGHVN,
    D.SLTEMHVN,
    D.KETQUA,
    D.NGAYXUAT,
    D.GIOXUAT,
    D.GIOGIAO AS DOC_GIOGIAO,
    D.MAFCC,
    D.STT AS DOC_STT,
    D.STATUS AS DOC_STATUS
FROM dbo.LUUPHIEUGIAOHANG P
LEFT JOIN dbo.LUUDOCQRCODE D
    ON ISNULL(LTRIM(RTRIM(D.NHAMAY)), '') = ISNULL(LTRIM(RTRIM(P.NHAMAY)), '')
   AND ISNULL(LTRIM(RTRIM(D.MAHANGFCC)), '') = ISNULL(LTRIM(RTRIM(P.MAHANG)), '')
   AND CONVERT(date, D.NGAYXUAT) = CONVERT(date, P.NGAYGIAO)
   AND (
        (
            ISNULL(LTRIM(RTRIM(P.NHAMAY)), '') = 'YAMAHA - VIET NAM'
            AND COALESCE(
                NULLIF(
                    SUBSTRING(
                        LTRIM(RTRIM(D.GIOGIAO)),
                        PATINDEX('%[^0]%', LTRIM(RTRIM(D.GIOGIAO)) + 'x'),
                        50),
                    ''),
                '0') =
                COALESCE(
                    NULLIF(
                        SUBSTRING(
                            LTRIM(RTRIM(P.CUA)),
                            PATINDEX('%[^0]%', LTRIM(RTRIM(P.CUA)) + 'x'),
                            50),
                        ''),
                    '0')
        )
        OR
        (
            ISNULL(LTRIM(RTRIM(P.NHAMAY)), '') <> 'YAMAHA - VIET NAM'
            AND ISNULL(LTRIM(RTRIM(D.GIOXUAT)), '') =
                ISNULL(LTRIM(RTRIM(P.GIOGIAOFCC)), '')
        )
   )
WHERE ";

            string sql = sqlPrefix +
                         string.Join(" AND ", where) +
                         @"
ORDER BY P.NGAYGIAO DESC, P.STT DESC, D.STT DESC";

            return LoadData(sql, parameters.ToArray());
        }

        private List<string> LoadStringList(
            string sql,
            CancellationToken cancellationToken)
        {
            DataTable table = LoadData(sql);
            var result = new List<string>(table.Rows.Count);

            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (row[0] == DBNull.Value)
                    continue;

                string value = Convert.ToString(row[0]).Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    result.Add(value);
            }

            return result;
        }

        private static List<DeliveryTraceRow> MapRows(
            DataTable table,
            CancellationToken cancellationToken)
        {
            var result = new List<DeliveryTraceRow>(table.Rows.Count);
            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Add(Map(row));
            }

            return result;
        }

        private static DeliveryTraceRow Map(DataRow row)
        {
            string nhaMay = DbValueHelper.GetString(row, "NHAMAY");
            DateTime? ngayGiao = DbValueHelper.GetNullableDateTime(row, "NGAYGIAO");
            string gioGiao = DbValueHelper.GetString(row, "GIOGIAO");
            string poNo = DbValueHelper.GetString(row, "PO_NO");
            int stt = DbValueHelper.GetInt(row, "STT");
            string lotFcc = DbValueHelper.GetString(row, "LOTFCC");

            return new DeliveryTraceRow
            {
                DeliveryKey = BuildDeliveryKey(nhaMay, ngayGiao, gioGiao, poNo, stt),
                DocumentNo = poNo,
                DeliveryDate = ngayGiao,
                CustomerCode = GetCustomerCode(nhaMay),
                CustomerName = GetCustomerName(nhaMay),
                PartNo = DbValueHelper.GetString(row, "MAHANG"),
                PartName = DbValueHelper.GetString(row, "TENHANG"),
                QRCode = DbValueHelper.GetString(row, "MAFCC"),
                CustomerLabelData = DbValueHelper.GetString(row, "LOTHVN"),
                LotNoRaw = string.IsNullOrWhiteSpace(lotFcc)
                    ? DbValueHelper.GetString(row, "LOT")
                    : lotFcc,
                Quantity = DbValueHelper.ToDecimal(row["SOLUONG"]),
                Unit = DbValueHelper.GetString(row, "DV"),
                Factory = nhaMay,
                Status = DbValueHelper.GetString(row, "STATUS")
            };
        }

        private static string GetCustomerCode(string nhaMay)
        {
            if (string.Equals(nhaMay, Yamaha, StringComparison.OrdinalIgnoreCase))
                return "100002";

            if (string.Equals(nhaMay, HondaVp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nhaMay, HondaHn, StringComparison.OrdinalIgnoreCase))
                return "100001";

            return null;
        }

        private static string GetCustomerName(string nhaMay)
        {
            if (string.Equals(nhaMay, Yamaha, StringComparison.OrdinalIgnoreCase))
                return Yamaha;

            if (string.Equals(nhaMay, HondaVp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nhaMay, HondaHn, StringComparison.OrdinalIgnoreCase))
                return "HON DA - VIET NAM";

            return nhaMay;
        }

        private static void AddCustomerFilter(
            List<string> where,
            List<SqlParameter> parameters,
            string customerName)
        {
            if (string.IsNullOrWhiteSpace(customerName))
                return;

            string customer = customerName.Trim();

            if (string.Equals(customer, "100001", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(customer, "HON DA - VIET NAM", StringComparison.OrdinalIgnoreCase))
            {
                where.Add("P.NHAMAY IN (@HondaVp, @HondaHn)");
                parameters.Add(new SqlParameter("@HondaVp", HondaVp));
                parameters.Add(new SqlParameter("@HondaHn", HondaHn));
                return;
            }

            if (string.Equals(customer, "100002", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(customer, Yamaha, StringComparison.OrdinalIgnoreCase))
            {
                where.Add("P.NHAMAY = @Yamaha");
                parameters.Add(new SqlParameter("@Yamaha", Yamaha));
                return;
            }

            where.Add("1 = 0");
        }

        private static void AddDateFilter(
            List<string> where,
            List<SqlParameter> parameters,
            DateTime? from,
            DateTime? to)
        {
            if (from.HasValue)
            {
                where.Add("P.NGAYGIAO >= @FromDate");
                parameters.Add(new SqlParameter("@FromDate", from.Value.Date));
            }

            if (to.HasValue)
            {
                where.Add("P.NGAYGIAO < @ToDateExclusive");
                parameters.Add(new SqlParameter("@ToDateExclusive", to.Value.Date.AddDays(1)));
            }
        }

        private static void AddLikeFilter(
            List<string> where,
            List<SqlParameter> parameters,
            string column,
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            where.Add("ISNULL(" + column + ", '') LIKE " + parameterName);
            parameters.Add(new SqlParameter(parameterName, "%" + value.Trim() + "%"));
        }

        private static bool ContainsLot(string rawLot, string requestedLot)
        {
            if (string.IsNullOrWhiteSpace(rawLot) || string.IsNullOrWhiteSpace(requestedLot))
                return false;

            try
            {
                foreach (var item in LotCodeHelper.ParseCompositeLot(rawLot))
                {
                    if (LotCodeHelper.AreLotKeysEquivalent(item.Key, requestedLot.Trim()))
                        return true;
                }
            }
            catch (FormatException)
            {
            }

            return string.Equals(rawLot.Trim(), requestedLot.Trim(), StringComparison.OrdinalIgnoreCase) ||
                   rawLot.IndexOf(requestedLot.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildDeliveryKey(
            string nhaMay,
            DateTime? ngayGiao,
            string gioGiao,
            string poNo,
            int stt)
        {
            return string.Join(
                "|",
                NormalizeKeyPart(nhaMay),
                ngayGiao.HasValue ? ngayGiao.Value.ToString("yyyyMMdd") : string.Empty,
                NormalizeKeyPart(gioGiao),
                NormalizeKeyPart(poNo),
                stt.ToString());
        }

        private static string NormalizeKeyPart(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
