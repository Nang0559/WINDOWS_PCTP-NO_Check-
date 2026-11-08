using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Modules.BaoCao.Application.Contracts;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Application.Contracts.Repositories;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Infrastructure.Repositories
{
    /// <summary>
    /// SQL persistence adapter for stock history read queries.
    /// All SQL/ADO.NET access stays below the repository boundary.
    /// </summary>
    public sealed class StockHistoryRepository : SqlRepositoryBase, IStockHistoryRepository
    {
        public StockHistoryRepository()
            : this(new SQLPROVIDER())
        {
        }

        public StockHistoryRepository(SQLPROVIDER sql)
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
            FROM StockHistory
            WHERE ItemCode IS NOT NULL
            ORDER BY ItemCode";

            var table = LoadData(sql);
            var result = new List<string>(table.Rows.Count);

            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (row[0] == DBNull.Value)
                    continue;

                var value = Convert.ToString(row[0]);
                if (!string.IsNullOrWhiteSpace(value))
                    result.Add(value);
            }

            return Task.FromResult<IReadOnlyList<string>>(result);
        }

        public Task<IReadOnlyList<ItemHistoryRow>> SearchAsync(
            HistorySearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            if (criteria == null)
                throw new ArgumentNullException("criteria");

            cancellationToken.ThrowIfCancellationRequested();

            // "Đến ngày" chọn từ DateEdit là 00:00 -> SP (so sánh Date <= @ToDate) sẽ bỏ sót cả ngày cuối.
            // Quy về cuối ngày (23:59:59.997, độ chính xác của DATETIME).
            DateTime? toInclusive = criteria.To;
            if (toInclusive.HasValue && toInclusive.Value.TimeOfDay == TimeSpan.Zero)
                toInclusive = toInclusive.Value.Date.AddDays(1).AddMilliseconds(-3);

            var parameters = new[]
            {
                new SqlParameter("@FromDate", criteria.From.HasValue ? (object)criteria.From.Value : DBNull.Value),
                new SqlParameter("@ToDate", toInclusive.HasValue ? (object)toInclusive.Value : DBNull.Value),
                new SqlParameter("@ItemCode", string.IsNullOrWhiteSpace(criteria.PartNo) ? (object)DBNull.Value : criteria.PartNo)
            };

            // sp_GetStockHistory (không có định nghĩa đầy đủ trong Git) không trả QrData / MaPhieu / FromSlotId / ToSlotId
            // nên QR bị rơi về TemCode và vị trí/số phiếu trống. Đọc thẳng StockHistory (+ bổ sung từ SlotLot);
            // nếu truy vấn trực tiếp lỗi (schema khác) thì quay về SP như cũ.
            DataTable table;
            try { table = LoadData(HistorySql, CloneParameters(parameters)); }
            catch (SqlException)
            {
                table = ExecuteStoredProcedure("sp_GetStockHistory", CloneParameters(parameters));
            }
            var slotNames = LoadSlotNames();
            var rows = table.AsEnumerable().Select(r => Map(r, slotNames)).ToList();
            EnrichDefectDocuments(rows);

            // Không phụ thuộc cách SP lọc ngày: lọc lại khoảng ngày ở đây (dòng không có ngày thì giữ lại).
            if (criteria.From.HasValue)
                rows = rows.Where(x => !x.EventTime.HasValue || x.EventTime.Value >= criteria.From.Value).ToList();
            if (toInclusive.HasValue)
                rows = rows.Where(x => !x.EventTime.HasValue || x.EventTime.Value <= toInclusive.Value).ToList();

            // Stored procedure contract remains unchanged for deployed DBs. Apply
            // optional LOT/QR/document criteria to its read-only result set.
            if (!string.IsNullOrWhiteSpace(criteria.LotNo))
                rows = rows.Where(x => MatchesLot(x.LotNo, criteria.LotNo)).ToList();

            if (criteria.LotNos != null && criteria.LotNos.Count > 0)
            {
                // Một lần gọi SP cho toàn bộ LOT của LOT ghép (thay vì N lần gọi / N LOT).
                var wanted = criteria.LotNos.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
                rows = rows.Where(r => wanted.Any(w => MatchesLot(r.LotNo, w))).ToList();
            }

            if (!string.IsNullOrWhiteSpace(criteria.QrCode))
            {
                string keyword = criteria.QrCode.Trim();
                rows = rows.Where(x =>
                    Contains(x.QrCode, keyword) ||
                    Contains(x.LotNo, keyword) ||
                    Contains(x.DocumentNo, keyword)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(criteria.DocumentNo))
                rows = rows.Where(x => Contains(x.DocumentNo, criteria.DocumentNo.Trim())).ToList();

            return Task.FromResult<IReadOnlyList<ItemHistoryRow>>(rows);
        }

        private static readonly Regex DefectDocumentRef =
            new Regex(@"^XLBT#(\d+)$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Dòng kho do xử lý hàng lỗi có MaPhieu dạng "XLBT#<id>" (id nội bộ, khó đọc).
        /// Bổ sung số phiếu thật để hiển thị và hỗ trợ tra cứu theo số phiếu.
        /// Nếu bảng xử lý hàng lỗi chưa tồn tại hoặc không đọc được, giữ nguyên số phiếu gốc.
        /// </summary>
        private void EnrichDefectDocuments(List<ItemHistoryRow> rows)
        {
            try
            {
                var ids = new HashSet<int>();
                foreach (ItemHistoryRow row in rows)
                {
                    Match match;
                    int id;
                    if (TryParseDefectRef(row.DocumentNo, out match) &&
                        int.TryParse(match.Groups[1].Value, out id))
                        ids.Add(id);
                }

                if (ids.Count == 0)
                    return;

                var names = new Dictionary<int, string>();
                string sql = "SELECT Id, SoPhieu FROM dbo.FVN_PhieuXuLyBatThuong WHERE Id IN (" +
                    string.Join(",", ids) + ")";
                foreach (DataRow data in LoadData(sql).Rows)
                {
                    if (data["SoPhieu"] != DBNull.Value)
                        names[Convert.ToInt32(data["Id"])] = Convert.ToString(data["SoPhieu"]);
                }

                foreach (ItemHistoryRow row in rows)
                {
                    Match match;
                    int id;
                    string soPhieu;
                    if (TryParseDefectRef(row.DocumentNo, out match) &&
                        int.TryParse(match.Groups[1].Value, out id) &&
                        names.TryGetValue(id, out soPhieu) &&
                        !string.IsNullOrWhiteSpace(soPhieu))
                    {
                        row.DocumentNo = row.DocumentNo.Trim() + " (" + soPhieu.Trim() + ")";
                    }
                }
            }
            catch (Exception)
            {
                // Bổ sung số phiếu chỉ là thông tin hỗ trợ; không làm hỏng báo cáo lịch sử kho.
            }
        }

        private static bool TryParseDefectRef(string documentNo, out Match match)
        {
            match = DefectDocumentRef.Match((documentNo ?? string.Empty).Trim());
            return match.Success;
        }

        private static SqlParameter[] CloneParameters(SqlParameter[] source)
        {
            return source.Select(p => new SqlParameter(p.ParameterName, p.Value)).ToArray();
        }

        // QR: ưu tiên QrData của chính dòng lịch sử, thiếu thì lấy QrData của SlotLot khớp (Slot + Item + LOT + Tem,
        // gần thời điểm giao dịch nhất). SlotLot cũng bổ sung SlotId để ra tên vị trí khi lịch sử không lưu From/ToSlotId.
        // Không bao giờ dùng TemCode làm QR.
        private const string HistorySql = @"
            SELECT SH.Id,
                   SH.ActionType,
                   SH.ItemCode,
                   SH.TemCode,
                   SH.LotNo,
                   SH.Quantity,
                   SH.[Date],
                   SH.PerformedBy,
                   SH.MaPhieu,
                   SH.FromSlotId,
                   SH.ToSlotId,
                   COALESCE(SH.SlotId, SL.SlotId) AS SlotId,
                   COALESCE(NULLIF(SH.QrData, N''), SL.QrData) AS QrData
            FROM dbo.StockHistory AS SH
            OUTER APPLY (
                SELECT TOP 1 sl.SlotId, sl.QrData
                FROM dbo.SlotLot AS sl
                WHERE (NULLIF(SH.QrData, N'') IS NULL
                       OR COALESCE(SH.ToSlotId, SH.FromSlotId, SH.SlotId) IS NULL)
                  AND sl.LotNo = SH.LotNo
                  AND sl.ItemCode = SH.ItemCode
                  AND (SH.TemCode IS NULL OR sl.TemCode = SH.TemCode)
                  AND (COALESCE(SH.ToSlotId, SH.FromSlotId, SH.SlotId) IS NULL
                       OR sl.SlotId = COALESCE(SH.ToSlotId, SH.FromSlotId, SH.SlotId))
                ORDER BY ABS(DATEDIFF(SECOND, sl.CreatedDate, SH.[Date]))
            ) AS SL
            WHERE (@FromDate IS NULL OR SH.[Date] >= @FromDate)
              AND (@ToDate IS NULL OR SH.[Date] <= @ToDate)
              AND (@ItemCode IS NULL OR SH.ItemCode = @ItemCode)
            ORDER BY SH.[Date] DESC, SH.Id DESC";

        /// <summary>SlotId -> "Kho-Rack-SlotN" (cùng định dạng với sp_GetCurrentStockStatus). Lỗi/thiếu bảng thì trả rỗng, báo cáo vẫn chạy.</summary>
        private Dictionary<int, string> LoadSlotNames()
        {
            var map = new Dictionary<int, string>();
            try
            {
                const string sql = @"
                SELECT s.SlotId,
                       ISNULL(w.Name, N'?') + N'-' + ISNULL(r.RackName, N'?') + N'-Slot' + CAST(s.SlotNumber AS NVARCHAR(20)) AS SlotName
                FROM dbo.Slot AS s
                LEFT JOIN dbo.Rack AS r ON r.RackId = s.RackId
                LEFT JOIN dbo.Warehouse AS w ON w.WarehouseId = r.WarehouseId";
                foreach (DataRow row in LoadData(sql).Rows)
                    map[Convert.ToInt32(row["SlotId"])] = Convert.ToString(row["SlotName"]);
            }
            catch (Exception) { }
            return map;
        }

        private static string ResolveLocation(string raw, Dictionary<int, string> slotNames)
        {
            int id;
            if (raw != null && int.TryParse(raw.Trim(), out id))
            {
                string name;
                return slotNames.TryGetValue(id, out name) ? name : "Slot #" + id;
            }
            return raw;
        }

        private static ItemHistoryRow Map(DataRow row, Dictionary<int, string> slotNames)
        {
            var item = new ItemHistoryRow
            {
                ItemCode = ReadString(row, "ItemCode", "PartNo"),
                QrCode = ReadString(row, "QrData", "QRData", "QRCode", "QR"),
                TemCode = ReadString(row, "TemCode"),
                LotNo = ReadString(row, "LotNo", "LOTNo", "LOT"),
                ActionType = ReadString(row, "ActionType", "Action", "EventType"),
                Quantity = ReadDecimal(row, "Quantity", "Qty", "SoLuong", "SL"),
                EventTime = ReadDateTime(row, "Date", "EventTime", "CreatedAt", "Ngay"),
                FromLocation = ResolveLocation(ReadString(row, "FromLocation", "FromSlot", "FromSlotId"), slotNames),
                ToLocation = ResolveLocation(ReadString(row, "ToLocation", "ToSlot", "ToSlotId"), slotNames),
                DocumentNo = ReadString(row, "MaPhieu", "DocumentNo", "PhieuNo"),
                UserName = ReadString(row, "PerformedBy", "UserName", "CreatedBy")
            };

            // Lịch sử không lưu From/ToSlotId: dùng SlotId (từ StockHistory hoặc SlotLot) theo chiều nhập/xuất.
            if (string.IsNullOrEmpty(item.FromLocation) && string.IsNullOrEmpty(item.ToLocation))
            {
                string slot = ResolveLocation(ReadString(row, "SlotId"), slotNames);
                if (!string.IsNullOrEmpty(slot))
                {
                    if (item.Direction == "Xuất") item.FromLocation = slot;
                    else item.ToLocation = slot;
                }
            }
            return item;
        }

        private static bool Contains(string value, string keyword)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesLot(string rawLot, string requestedLot)
        {
            if (string.IsNullOrWhiteSpace(rawLot) || string.IsNullOrWhiteSpace(requestedLot))
                return false;

            string wanted = requestedLot.Trim();
            // LOT trong lịch sử có thể là LOT đơn, LOT in đầy đủ (kèm counter+qty) hoặc LOT ghép.
            foreach (LotAllocation part in LotCodeHelper.ParseCompositeLotTolerant(rawLot, null))
                if (LotCodeHelper.AreLotKeysEquivalent(part.LotNo, wanted))
                    return true;

            return LotCodeHelper.AreLotKeysEquivalent(rawLot.Trim(), wanted) ||
                Contains(rawLot, wanted);
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
