using PCTP.ClassSQL;
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
    /// SQL Server persistence adapter for delivery traceability.
    /// All database concerns stay in Infrastructure: SQL, schema discovery,
    /// parameters and DataTable-to-projection mapping.
    /// </summary>
    public sealed class DeliveryTraceRepository : SqlRepositoryBase, IDeliveryTraceRepository
    {
        private readonly object _schemaSync = new object();
        private HashSet<string> _columns;

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

            const string sql = @"SELECT DISTINCT MAHANG
FROM dbo.LUUPHIEUGIAOHANG
WHERE NULLIF(LTRIM(RTRIM(MAHANG)), '') IS NOT NULL
ORDER BY MAHANG";

            return Task.FromResult<IReadOnlyList<string>>(
                LoadStringList(sql, cancellationToken));
        }

        public Task<IReadOnlyList<string>> GetCustomersAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HashSet<string> columns = GetColumns();
            string customerColumn = new[] { "CustomerName", "CustomerCode" }
                .FirstOrDefault(columns.Contains);

            if (string.IsNullOrWhiteSpace(customerColumn))
                return Task.FromResult<IReadOnlyList<string>>(new List<string>());

            string sql = @"SELECT DISTINCT [" + customerColumn + @"]
FROM dbo.LUUPHIEUGIAOHANG
WHERE NULLIF(LTRIM(RTRIM([" + customerColumn + @"])), '') IS NOT NULL
ORDER BY [" + customerColumn + "]";

            return Task.FromResult<IReadOnlyList<string>>(
                LoadStringList(sql, cancellationToken));
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

            HashSet<string> columns = GetColumns();
            bool hasQr = HasAny(columns, "QRCode", "QR", "QRData");
            bool hasCustomerLabel = HasAny(
                columns,
                "CustomerLabelData",
                "CustomerLabel",
                "CustomerQR",
                "QRKhachHang");

            if ((!string.IsNullOrWhiteSpace(qrCode) && !hasQr) ||
                (!string.IsNullOrWhiteSpace(customerLabelData) && !hasCustomerLabel))
            {
                return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                    new List<DeliveryTraceRow>());
            }

            var where = new List<string> { "1 = 1" };
            var parameters = new List<SqlParameter>();

            AddDateFilter(where, parameters, from, to);
            AddLikeFilter(where, parameters, "MAHANG", partNo, "@PartNo");
            AddFirstExistingLikeFilter(
                columns,
                new[] { "QRCode", "QR", "QRData" },
                qrCode,
                where,
                parameters,
                "@QrCode");
            AddFirstExistingLikeFilter(
                columns,
                new[] { "CustomerLabelData", "CustomerLabel", "CustomerQR", "QRKhachHang" },
                customerLabelData,
                where,
                parameters,
                "@CustomerLabel");

            DataTable table = Load(where, parameters, columns);
            return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                MapRows(table, cancellationToken));
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

            HashSet<string> columns = GetColumns();
            if (!string.IsNullOrWhiteSpace(customerName) &&
                !HasAny(columns, "CustomerCode", "CustomerName"))
            {
                return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                    new List<DeliveryTraceRow>());
            }

            var where = new List<string> { "1 = 1" };
            var parameters = new List<SqlParameter>();

            AddDateFilter(where, parameters, from, to);
            AddLikeFilter(where, parameters, "MAHANG", partNo, "@PartNo");
            AddLikeFilter(where, parameters, "LOT", lotNo, "@LotNo");
            AddCustomerFilter(columns, customerName, where, parameters, "@Customer");

            DataTable table = Load(where, parameters, columns);
            var rows = new List<DeliveryTraceRow>();

            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DeliveryTraceRow mapped = Map(row);
                if (!string.IsNullOrWhiteSpace(lotNo) &&
                    !ContainsLot(mapped.LotNoRaw, lotNo))
                {
                    continue;
                }

                rows.Add(mapped);
            }

            return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(rows);
        }

        public Task<IReadOnlyList<DeliveryTraceRow>> SearchCustomerAsync(
            string customerName,
            string partNo,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HashSet<string> columns = GetColumns();
            if (!string.IsNullOrWhiteSpace(customerName) &&
                !HasAny(columns, "CustomerCode", "CustomerName"))
            {
                return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                    new List<DeliveryTraceRow>());
            }

            var where = new List<string> { "1 = 1" };
            var parameters = new List<SqlParameter>();

            AddDateFilter(where, parameters, from, to);
            AddLikeFilter(where, parameters, "MAHANG", partNo, "@PartNo");
            AddCustomerFilter(columns, customerName, where, parameters, "@Customer");

            DataTable table = Load(where, parameters, columns);
            return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                MapRows(table, cancellationToken));
        }

        public Task<IReadOnlyList<DeliveryTraceRow>> FindByQrAsync(
            string qrCode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(qrCode))
                return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                    new List<DeliveryTraceRow>());

            HashSet<string> columns = GetColumns();
            if (!HasAny(columns, "QRCode", "QR", "QRData"))
                return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                    new List<DeliveryTraceRow>());

            var where = new List<string> { "1 = 1" };
            var parameters = new List<SqlParameter>();

            AddFirstExistingLikeFilter(
                columns,
                new[] { "QRCode", "QR", "QRData" },
                qrCode,
                where,
                parameters,
                "@QrCode");

            DataTable table = Load(where, parameters, columns);
            return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                MapRows(table, cancellationToken));
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

        private DataTable Load(
            List<string> where,
            List<SqlParameter> parameters,
            HashSet<string> columns)
        {
            string optionalSelect = BuildOptionalSelect(columns);

            string sql = @"SELECT STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GIOGIAO,
       STATUS, TTPHIEU, NHAMAY, HOP, STATUSDOC, Note,
       ISNULL(PO_NO, '') AS PO_NO, ISNULL(PO_ITEM, '') AS PO_ITEM" + optionalSelect + @"
FROM dbo.LUUPHIEUGIAOHANG
WHERE " + string.Join(" AND ", where) + @"
ORDER BY NGAYGIAO DESC, STT DESC";

            return LoadData(sql, parameters.ToArray());
        }

        private HashSet<string> GetColumns()
        {
            if (_columns != null)
                return _columns;

            lock (_schemaSync)
            {
                if (_columns != null)
                    return _columns;

                const string sql = @"SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'LUUPHIEUGIAOHANG'";

                DataTable table = LoadData(sql);
                _columns = new HashSet<string>(
                    table.AsEnumerable()
                        .Select(r => Convert.ToString(r["COLUMN_NAME"]))
                        .Where(s => !string.IsNullOrWhiteSpace(s)),
                    StringComparer.OrdinalIgnoreCase);

                return _columns;
            }
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

            return new DeliveryTraceRow
            {
                DeliveryKey = BuildDeliveryKey(nhaMay, ngayGiao, gioGiao, poNo, stt),
                DocumentNo = poNo,
                DeliveryDate = ngayGiao,
                CustomerCode = DbValueHelper.GetString(row, "CustomerCode"),
                CustomerName = DbValueHelper.GetString(row, "CustomerName"),
                PartNo = DbValueHelper.GetString(row, "MAHANG"),
                PartName = DbValueHelper.GetString(row, "TENHANG"),
                QRCode = GetOptional(row, "QRCode", "QR", "QRData"),
                CustomerLabelData = GetOptional(
                    row,
                    "CustomerLabelData",
                    "CustomerLabel",
                    "CustomerQR",
                    "QRKhachHang"),
                LotNoRaw = DbValueHelper.GetString(row, "LOT"),
                Quantity = DbValueHelper.ToDecimal(row["SOLUONG"]),
                Unit = DbValueHelper.GetString(row, "DV"),
                Factory = nhaMay,
                Status = DbValueHelper.GetString(row, "STATUS")
            };
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
                ngayGiao.HasValue ? ngayGiao.Value.ToString("yyyyMMdd") : "",
                NormalizeKeyPart(gioGiao),
                NormalizeKeyPart(poNo),
                stt.ToString());
        }

        private static string NormalizeKeyPart(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static void AddDateFilter(
            List<string> where,
            List<SqlParameter> parameters,
            DateTime? from,
            DateTime? to)
        {
            if (from.HasValue)
            {
                where.Add("NGAYGIAO >= @FromDate");
                parameters.Add(new SqlParameter("@FromDate", from.Value.Date));
            }

            if (to.HasValue)
            {
                where.Add("NGAYGIAO < @ToDateExclusive");
                parameters.Add(
                    new SqlParameter("@ToDateExclusive", to.Value.Date.AddDays(1)));
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

            where.Add("ISNULL([" + column + "], '') LIKE " + parameterName);
            parameters.Add(
                new SqlParameter(parameterName, "%" + value.Trim() + "%"));
        }

        private static void AddCustomerFilter(
            HashSet<string> columns,
            string value,
            List<string> where,
            List<SqlParameter> parameters,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            string[] customerColumns = new[] { "CustomerCode", "CustomerName" }
                .Where(columns.Contains)
                .ToArray();

            if (customerColumns.Length == 0)
                return;

            where.Add(
                "(" +
                string.Join(
                    " OR ",
                    customerColumns.Select(
                        c => "ISNULL([" + c + "], '') LIKE " + parameterName)) +
                ")");

            parameters.Add(
                new SqlParameter(parameterName, "%" + value.Trim() + "%"));
        }

        private static void AddFirstExistingLikeFilter(
            HashSet<string> columns,
            string[] candidates,
            string value,
            List<string> where,
            List<SqlParameter> parameters,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            string column = candidates.FirstOrDefault(columns.Contains);
            if (!string.IsNullOrWhiteSpace(column))
                AddLikeFilter(where, parameters, column, value, parameterName);
        }

        private static bool HasAny(HashSet<string> columns, params string[] names)
        {
            return names.Any(columns.Contains);
        }

        private static string BuildOptionalSelect(HashSet<string> columns)
        {
            string[] candidates =
            {
                "QRCode",
                "QR",
                "QRData",
                "CustomerLabelData",
                "CustomerLabel",
                "CustomerQR",
                "QRKhachHang",
                "CustomerCode",
                "CustomerName"
            };

            return string.Concat(
                candidates
                    .Where(columns.Contains)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(c => ", [" + c + "] AS [" + c + "]"));
        }

        private static string GetOptional(DataRow row, params string[] names)
        {
            foreach (string name in names)
            {
                if (row.Table.Columns.Contains(name) && row[name] != DBNull.Value)
                    return Convert.ToString(row[name]).Trim();
            }

            return null;
        }

        private static bool ContainsLot(string rawLot, string requestedLot)
        {
            if (string.IsNullOrWhiteSpace(rawLot) ||
                string.IsNullOrWhiteSpace(requestedLot))
            {
                return false;
            }

            try
            {
                foreach (var item in LotCodeHelper.ParseCompositeLot(rawLot))
                {
                    if (LotCodeHelper.AreLotKeysEquivalent(
                        item.Key,
                        requestedLot.Trim()))
                    {
                        return true;
                    }
                }
            }
            catch (FormatException)
            {
            }

            return string.Equals(
                       rawLot.Trim(),
                       requestedLot.Trim(),
                       StringComparison.OrdinalIgnoreCase) ||
                   rawLot.IndexOf(
                       requestedLot.Trim(),
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
