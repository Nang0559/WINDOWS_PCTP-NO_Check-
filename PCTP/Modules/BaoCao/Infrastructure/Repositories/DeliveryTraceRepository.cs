using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Modules.BaoCao.Application.Contracts.Models;
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
    ///
    /// Actual source tables:
    ///   dbo.LUUPHIEUGIAOHANG  - delivery header/detail evidence
    ///   dbo.LUUDOCQRCODE      - FCC/HVN document-QR mapping
    ///
    /// Customer information is intentionally NOT fabricated here because neither
    /// of the supplied tables contains CustomerCode/CustomerName columns.
    /// </summary>
    public sealed class DeliveryTraceRepository : SqlRepositoryBase, IDeliveryTraceRepository
    {
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

            // LUUPHIEUGIAOHANG and LUUDOCQRCODE do not contain customer code/name.
            // Do not invent a customer column or run INFORMATION_SCHEMA discovery.
            return Task.FromResult<IReadOnlyList<string>>(
                new List<string>());
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

            // The real QR/document table has no QRCode/QRData column.
            // MAFCC is the FCC document/QR identifier; LOTHVN/MAHANGHVN are
            // the customer-side lot/item mapping available in LUUDOCQRCODE.
            if (string.IsNullOrWhiteSpace(qrCode) &&
                string.IsNullOrWhiteSpace(customerLabelData) &&
                string.IsNullOrWhiteSpace(partNo))
            {
                // Still allow date-only traceability search.
            }

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

            DataTable table = Load(where, parameters);
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

            // There is no customer column in the two actual source tables.
            // A customer filter cannot be truthfully applied at this persistence boundary.
            if (!string.IsNullOrWhiteSpace(customerName))
            {
                return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                    new List<DeliveryTraceRow>());
            }

            var where = new List<string> { "1 = 1" };
            var parameters = new List<SqlParameter>();

            AddDateFilter(where, parameters, from, to);
            AddLikeFilter(where, parameters, "P.MAHANG", partNo, "@PartNo");
            AddLikeFilter(where, parameters, "P.LOT", lotNo, "@LotNo");

            DataTable table = Load(where, parameters);
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

            // No customer field exists in either actual table. Returning no rows is
            // safer than silently returning all customers for a requested customer.
            if (!string.IsNullOrWhiteSpace(customerName))
            {
                return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                    new List<DeliveryTraceRow>());
            }

            var where = new List<string> { "1 = 1" };
            var parameters = new List<SqlParameter>();

            AddDateFilter(where, parameters, from, to);
            AddLikeFilter(where, parameters, "P.MAHANG", partNo, "@PartNo");

            DataTable table = Load(where, parameters);
            return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                MapRows(table, cancellationToken));
        }

        public Task<IReadOnlyList<DeliveryTraceRow>> FindByQrAsync(
            string qrCode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(qrCode))
            {
                return Task.FromResult<IReadOnlyList<DeliveryTraceRow>>(
                    new List<DeliveryTraceRow>());
            }

            var where = new List<string>
            {
                "ISNULL(D.MAFCC, '') LIKE @QrCode"
            };
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@QrCode", "%" + qrCode.Trim() + "%")
            };

            DataTable table = Load(where, parameters);
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
            List<SqlParameter> parameters)
        {
            // LUUDOCQRCODE is joined to the delivery row using the fields that
            // exist in BOTH real tables. The date is compared at DATE precision
            // because both NGAYXUAT and NGAYGIAO are smalldatetime values.
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
    ON D.STT = P.STT
   AND ISNULL(LTRIM(RTRIM(D.NHAMAY)), '') = ISNULL(LTRIM(RTRIM(P.NHAMAY)), '')
   AND ISNULL(LTRIM(RTRIM(D.GIOGIAO)), '') = ISNULL(LTRIM(RTRIM(P.GIOGIAO)), '')
   AND CONVERT(date, D.NGAYXUAT) = CONVERT(date, P.NGAYGIAO)
WHERE ";

            string sql = sqlPrefix +
                         string.Join(" AND ", where) +
                         @"
ORDER BY P.NGAYGIAO DESC, P.STT DESC, D.STT DESC";

            return LoadData(sql, parameters.ToArray());
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

            string maFcc = DbValueHelper.GetString(row, "MAFCC");
            string lotHvn = DbValueHelper.GetString(row, "LOTHVN");
            string lotFcc = DbValueHelper.GetString(row, "LOTFCC");

            return new DeliveryTraceRow
            {
                DeliveryKey = BuildDeliveryKey(
                    nhaMay,
                    ngayGiao,
                    gioGiao,
                    poNo,
                    stt),
                DocumentNo = poNo,
                DeliveryDate = ngayGiao,

                // These columns do not exist in the two supplied tables.
                CustomerCode = null,
                CustomerName = null,

                PartNo = DbValueHelper.GetString(row, "MAHANG"),
                PartName = DbValueHelper.GetString(row, "TENHANG"),

                // Actual document QR/FCC identifier available in LUUDOCQRCODE.
                QRCode = maFcc,

                // Customer-side trace evidence available from LUUDOCQRCODE.
                CustomerLabelData = lotHvn,

                // Preserve the original delivery LOT and enrich the projection
                // with FCC/HVN document information through the existing DTO fields.
                LotNoRaw = string.IsNullOrWhiteSpace(lotFcc)
                    ? DbValueHelper.GetString(row, "LOT")
                    : lotFcc,

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
                where.Add("P.NGAYGIAO >= @FromDate");
                parameters.Add(new SqlParameter("@FromDate", from.Value.Date));
            }

            if (to.HasValue)
            {
                where.Add("P.NGAYGIAO < @ToDateExclusive");
                parameters.Add(new SqlParameter(
                    "@ToDateExclusive",
                    to.Value.Date.AddDays(1)));
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
            parameters.Add(new SqlParameter(
                parameterName,
                "%" + value.Trim() + "%"));
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
        private static SlotMovementRow MapSlotMovement(DataRow row) => new SlotMovementRow
        {
            ActionType = DbValueHelper.GetString(row, "ActionType"),
            ItemCode = DbValueHelper.GetString(row, "ItemCode"),
            LotNo = DbValueHelper.GetString(row, "LotNo"),
            Quantity = DbValueHelper.GetInt(row, "Quantity"),
            Date = DbValueHelper.GetNullableDateTime(row, "Date") ?? default,

            FromSlotId = row["FromSlotId"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["FromSlotId"]),
            FromWarehouse = DbValueHelper.GetString(row, "FromWarehouse"),
            FromRack = DbValueHelper.GetString(row, "FromRack"),
            FromSlotNumber = row["FromSlotNumber"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["FromSlotNumber"]),

            ToSlotId = row["ToSlotId"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["ToSlotId"]),
            ToWarehouse = DbValueHelper.GetString(row, "ToWarehouse"),
            ToRack = DbValueHelper.GetString(row, "ToRack"),
            ToSlotNumber = row["ToSlotNumber"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["ToSlotNumber"]),

            PerformedBy = DbValueHelper.GetString(row, "PerformedBy")
        };

        private static HangChoGiaoRow MapHangChoGiao(DataRow row) => new HangChoGiaoRow
        {
            Id = DbValueHelper.GetInt(row, "Id"),
            MaHang = DbValueHelper.GetString(row, "MaHang"),
            LotThung = DbValueHelper.GetString(row, "LotThung"),
            LotGoc = DbValueHelper.GetString(row, "LotGoc"),
            SoLuong = DbValueHelper.GetInt(row, "SoLuong"),
            SlotIdNguon = row["SlotIdNguon"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["SlotIdNguon"]),
            TrangThai = DbValueHelper.GetString(row, "TrangThai"),
            NgayXuatKho = DbValueHelper.GetNullableDateTime(row, "NgayXuatKho") ?? default,
            NguoiXuatKho = DbValueHelper.GetString(row, "NguoiXuatKho"),
            NgayGiao = DbValueHelper.GetNullableDateTime(row, "NgayGiao"),
            NguoiGiao = DbValueHelper.GetString(row, "NguoiGiao")
        };
    }
}
