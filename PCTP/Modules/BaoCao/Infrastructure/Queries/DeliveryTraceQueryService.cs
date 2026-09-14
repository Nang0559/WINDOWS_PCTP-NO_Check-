using PCTP.ClassSQL;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Shared.Common;
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
    /// Read-only adapter over LUUPHIEUGIAOHANG.
    /// DeliveryKey = NHAMAY + NGAYGIAO + GIOGIAO + PO_NO + STT.
    /// DOCQRCODE is not joined because it has no confirmed persisted DeliveryKey relationship.
    /// </summary>
    public sealed class DeliveryTraceQueryService : IQrTraceQuery, ILotTraceQuery, ICustomerDeliveryQuery
    {
        private readonly SQLPROVIDER _sql;

        public DeliveryTraceQueryService()
        {
            _sql = new SQLPROVIDER();
        }

        public Task<IReadOnlyList<DeliveryTraceRow>> SearchAsync(
            string qrCode,
            string customerLabelData,
            string partNo,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HashSet<string> columns = LoadColumns();
            bool hasQr = columns.Contains("QRCode") || columns.Contains("QR") || columns.Contains("QRData");
            bool hasCustomerLabel = columns.Contains("CustomerLabelData")
                                    || columns.Contains("CustomerLabel")
                                    || columns.Contains("CustomerQR")
                                    || columns.Contains("QRKhachHang");

            if ((!string.IsNullOrWhiteSpace(qrCode) && !hasQr)
                || (!string.IsNullOrWhiteSpace(customerLabelData) && !hasCustomerLabel))
            {
                return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(new List<DeliveryTraceRow>());
            }

            var where = new List<string> { "1 = 1" };
            var parameters = new List<SqlParameter>();
            AddDateFilter(where, parameters, from, to);
            AddLikeFilter(where, parameters, "MAHANG", partNo, "@PartNo");
            AddFirstExistingLikeFilter(columns, new[] { "QRCode", "QR", "QRData" }, qrCode, where, parameters, "@QrCode");
            AddFirstExistingLikeFilter(columns, new[] { "CustomerLabelData", "CustomerLabel", "CustomerQR", "QRKhachHang" }, customerLabelData, where, parameters, "@CustomerLabel");

            DataTable table = Load(where, parameters, columns);
            var result = new List<DeliveryTraceRow>(table.Rows.Count);
            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Add(Map(row));
            }

            return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(result);
        }

        public Task<IReadOnlyList<DeliveryLotTraceRow>> GetLotsAsync(
            string deliveryKey,
            string qrCode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(deliveryKey))
                return Task.FromResult<IReadOnlyList<DeliveryLotTraceRow>>(new List<DeliveryLotTraceRow>());

            HashSet<string> columns = LoadColumns();
            bool hasQr = columns.Contains("QRCode") || columns.Contains("QR") || columns.Contains("QRData");
            if (!string.IsNullOrWhiteSpace(qrCode) && !hasQr)
                return Task.FromResult<IReadOnlyList<DeliveryLotTraceRow>>(new List<DeliveryLotTraceRow>());

            var where = new List<string> { "1 = 1" };
            var parameters = new List<SqlParameter>();
            AddFirstExistingLikeFilter(columns, new[] { "QRCode", "QR", "QRData" }, qrCode, where, parameters, "@QrCode");

            DataTable table = Load(where, parameters, columns);
            var result = new List<DeliveryLotTraceRow>();

            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DeliveryTraceRow delivery = Map(row);
                if (!string.Equals(delivery.DeliveryKey, deliveryKey.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    foreach (var lot in LotCodeHelper.ParseCompositeLot(delivery.LotNoRaw))
                    {
                        result.Add(new DeliveryLotTraceRow
                        {
                            DeliveryKey = delivery.DeliveryKey,
                            QRCode = delivery.QRCode,
                            LotNo = lot.Key,
                            Quantity = lot.Value
                        });
                    }
                }
                catch (FormatException)
                {
                    // A legacy row may contain a plain LOT without "-quantity".
                    // Keep the trace instead of dropping the historical delivery.
                    if (!string.IsNullOrWhiteSpace(delivery.LotNoRaw))
                    {
                        result.Add(new DeliveryLotTraceRow
                        {
                            DeliveryKey = delivery.DeliveryKey,
                            QRCode = delivery.QRCode,
                            LotNo = delivery.LotNoRaw.Trim(),
                            Quantity = delivery.Quantity ?? 0m
                        });
                    }
                }
            }

            return Task.FromResult<IReadOnlyList<DeliveryLotTraceRow>>(result);
        }

        async Task<IReadOnlyList<DeliveryTraceRow>> ILotTraceQuery.SearchAsync(
            string lotNo,
            string partNo,
            string customerName,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HashSet<string> columns = LoadColumns();
            bool hasCustomerName = columns.Contains("CustomerName");
            if (!string.IsNullOrWhiteSpace(customerName) && !hasCustomerName)
                return new List<DeliveryTraceRow>();

            var where = new List<string> { "1 = 1" };
            var parameters = new List<SqlParameter>();
            AddDateFilter(where, parameters, from, to);
            AddLikeFilter(where, parameters, "MAHANG", partNo, "@PartNo");
            AddLikeFilter(where, parameters, "LOT", lotNo, "@LotNo");
            AddLikeFilter(where, parameters, "CustomerName", customerName, "@CustomerName");

            DataTable table = Load(where, parameters, columns);
            var result = new List<DeliveryTraceRow>();
            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DeliveryTraceRow mapped = Map(row);
                if (!string.IsNullOrWhiteSpace(lotNo) && !ContainsLot(mapped.LotNoRaw, lotNo))
                    continue;
                result.Add(mapped);
            }

            return result;
        }

        async Task<IReadOnlyList<DeliveryTraceRow>> ICustomerDeliveryQuery.SearchAsync(
            string customerName,
            string partNo,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HashSet<string> columns = LoadColumns();
            if (!string.IsNullOrWhiteSpace(customerName) && !columns.Contains("CustomerName"))
                return new List<DeliveryTraceRow>();

            var where = new List<string> { "1 = 1" };
            var parameters = new List<SqlParameter>();
            AddDateFilter(where, parameters, from, to);
            AddLikeFilter(where, parameters, "MAHANG", partNo, "@PartNo");
            AddLikeFilter(where, parameters, "CustomerName", customerName, "@CustomerName");

            DataTable table = Load(where, parameters, columns);
            var result = new List<DeliveryTraceRow>(table.Rows.Count);
            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Add(Map(row));
            }

            return result;
        }

        private DataTable Load(List<string> where, List<SqlParameter> parameters, HashSet<string> columns)
        {
            string optionalSelect = BuildOptionalSelect(columns);
            string sql = @"
SELECT
    STT,
    CUA,
    TRUYEN,
    MAHANG,
    TENHANG,
    LOT,
    DV,
    SOLUONG,
    NGAYGIAO,
    GIOGIAO,
    STATUS,
    TTPHIEU,
    NHAMAY,
    HOP,
    STATUSDOC,
    Note,
    ISNULL(PO_NO, '') AS PO_NO,
    ISNULL(PO_ITEM, '') AS PO_ITEM" + optionalSelect + @"
FROM dbo.LUUPHIEUGIAOHANG
WHERE " + string.Join(" AND ", where) + @"
ORDER BY NGAYGIAO DESC, STT DESC";

            return _sql.LoadData(_sql.B7R2_FCCdbb, sql, parameters.ToArray());
        }

        private HashSet<string> LoadColumns()
        {
            const string sql = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'LUUPHIEUGIAOHANG'";

            DataTable table = _sql.LoadData(_sql.B7R2_FCCdbb, sql);
            return new HashSet<string>(
                table.AsEnumerable()
                    .Select(r => Convert.ToString(r["COLUMN_NAME"]))
                    .Where(s => !string.IsNullOrWhiteSpace(s)),
                StringComparer.OrdinalIgnoreCase);
        }

        private static string BuildOptionalSelect(HashSet<string> columns)
        {
            string[] candidates =
            {
                "QRCode", "QR", "QRData",
                "CustomerLabelData", "CustomerLabel", "CustomerQR", "QRKhachHang",
                "CustomerCode", "CustomerName"
            };

            var selected = candidates
                .Where(columns.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(c => ", [" + c + "] AS [" + c + "]")
                .ToList();

            return selected.Count == 0 ? string.Empty : string.Concat(selected);
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
                CustomerLabelData = GetOptional(row, "CustomerLabelData", "CustomerLabel", "CustomerQR", "QRKhachHang"),
                LotNoRaw = DbValueHelper.GetString(row, "LOT"),
                Quantity = DbValueHelper.ToDecimal(row["SOLUONG"]),
                Unit = DbValueHelper.GetString(row, "DV"),
                Factory = nhaMay,
                Status = DbValueHelper.GetString(row, "STATUS")
            };
        }

        private static string BuildDeliveryKey(string nhaMay, DateTime? ngayGiao, string gioGiao, string poNo, int stt)
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
            return (value ?? "").Trim().ToUpperInvariant();
        }

        private static void AddDateFilter(List<string> where, List<SqlParameter> parameters, DateTime? from, DateTime? to)
        {
            if (from.HasValue)
            {
                where.Add("NGAYGIAO >= @FromDate");
                parameters.Add(new SqlParameter("@FromDate", from.Value.Date));
            }

            if (to.HasValue)
            {
                where.Add("NGAYGIAO < @ToDateExclusive");
                parameters.Add(new SqlParameter("@ToDateExclusive", to.Value.Date.AddDays(1)));
            }
        }

        private static void AddLikeFilter(List<string> where, List<SqlParameter> parameters, string column, string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            where.Add("ISNULL([" + column + "], '') LIKE " + parameterName);
            parameters.Add(new SqlParameter(parameterName, "%" + value.Trim() + "%"));
        }

        private static void AddFirstExistingLikeFilter(HashSet<string> columns, string[] candidates, string value, List<string> where, List<SqlParameter> parameters, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            string column = candidates.FirstOrDefault(columns.Contains);
            if (string.IsNullOrWhiteSpace(column))
                return;

            AddLikeFilter(where, parameters, column, value, parameterName);
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
                // Preserve visibility of legacy malformed LOT values.
            }

            return string.Equals(rawLot.Trim(), requestedLot.Trim(), StringComparison.OrdinalIgnoreCase)
                   || rawLot.IndexOf(requestedLot.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
