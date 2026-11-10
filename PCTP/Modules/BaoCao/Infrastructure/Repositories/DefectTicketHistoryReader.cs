using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PCTP.Modules.BaoCao.Infrastructure.Repositories
{
    /// <summary>
    /// Đọc (read-only) dữ liệu của luồng XỬ LÝ HÀNG LỖI (phiếu trả hàng KTR# / phiếu xử lý bất thường XLBT#)
    /// để 3 báo cáo tra cứu hiển thị được các phiếu đã lập:
    ///  - FormBaoCaoQualityHistory : các lần QC của phiếu (QC ban đầu, QC rework, QC xác nhận cuối).
    ///  - FormBaoCaoStockHistory / FormBaoCaoTraceability : sự kiện của phiếu (lập phiếu, truy vết LOT, định hướng,
    ///    giao sản xuất, QC cuối, hủy) chen vào timeline cùng các giao dịch kho thật.
    ///
    /// Vì sao cần: các báo cáo trên chỉ đọc dbo.InspectionLog (QC quét thùng khi nhập kho) và dbo.StockHistory
    /// (giao dịch kho). Luồng hàng lỗi KHÔNG ghi InspectionLog, và các bước lập phiếu / QC / giao sản xuất không
    /// phát sinh giao dịch kho nên không có dòng StockHistory. Không đổi cách ghi dữ liệu (không đụng giao dịch kho);
    /// chỉ bổ sung phía đọc.
    ///
    /// Mỗi nguồn được đọc độc lập; bảng chưa tồn tại / schema khác chỉ làm nguồn đó rỗng, không làm hỏng báo cáo.
    /// </summary>
    public sealed class DefectTicketHistoryReader : SqlRepositoryBase
    {
        public const string QcInitial = "QC1";   // FVN_PhieuXuLyBatThuongQCInitial (QC phân loại ban đầu)
        public const string QcRework = "QCR";    // FVN_PXLB_ReworkQC (QC Rework - luồng Phase 5-9)
        public const string QcFinal = "QC2";     // FVN_TraHangQTChungQC (QC xác nhận cuối)

        // XLBT#<id phiếu xử lý>-<QC1|QCR|QC2>#<id dòng QC>
        private static readonly Regex QcCodePattern = new Regex(
            @"^XLBT#(\d+)-(QC1|QCR|QC2)#(\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public DefectTicketHistoryReader()
            : this(new SQLPROVIDER())
        {
        }

        public DefectTicketHistoryReader(SQLPROVIDER sql)
            : base(
                new PhieuSqlExecutor(sql ?? throw new ArgumentNullException("sql")),
                new UnitOfWork(sql ?? throw new ArgumentNullException("sql")))
        {
        }

        // =====================================================================
        // Helper thuần (không truy cập DB) - có unit test
        // =====================================================================

        public static string BuildQcCode(int ticketId, string kind, int qcRowId)
        {
            return "XLBT#" + ticketId.ToString(CultureInfo.InvariantCulture) + "-" + kind + "#" +
                qcRowId.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryParseQcCode(string code, out int ticketId, out string kind, out int qcRowId)
        {
            ticketId = 0;
            kind = null;
            qcRowId = 0;
            if (string.IsNullOrWhiteSpace(code)) return false;
            Match match = QcCodePattern.Match(code.Trim());
            if (!match.Success) return false;
            if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out ticketId)) return false;
            if (!int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out qcRowId)) return false;
            kind = match.Groups[2].Value.ToUpperInvariant();
            return true;
        }

        /// <summary>Số phiếu hiển thị: "XLBT#12 (PT-001-1)" - cùng định dạng với báo cáo lịch sử kho.</summary>
        public static string BuildDocumentRef(string refType, int id, string soPhieu)
        {
            string text = (string.IsNullOrWhiteSpace(refType) ? "XLBT" : refType.Trim()) + "#" +
                id.ToString(CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(soPhieu) ? text : text + " (" + soPhieu.Trim() + ")";
        }

        public static string DescribeQuantities(int inspected, int ok, int ng, int? rework, int? scrap)
        {
            string text = "Đã kiểm " + inspected.ToString("N0") + " | OK " + ok.ToString("N0") + " | NG " + ng.ToString("N0");
            if (rework.HasValue || scrap.HasValue)
                text += " (Rework " + (rework ?? 0).ToString("N0") + ", Loại bỏ " + (scrap ?? 0).ToString("N0") + ")";
            return text;
        }

        public static string DescribeDirection(int huongXuLy)
        {
            switch (huongXuLy)
            {
                case 1: return "Từ chối giao bù (không phải lỗi thật)";
                case 2: return "Chỉ giao bù";
                case 3: return "Cần rework";
                default: return "Chưa xác định";
            }
        }

        public static string DescribeSource(int nguon)
        {
            return nguon == 2 ? "Khách trả" : nguon == 1 ? "Trả nội bộ" : "Không rõ nguồn";
        }

        // =====================================================================
        // QC của phiếu -> Lịch sử chất lượng
        // =====================================================================

        /// <summary>
        /// Các lần QC của phiếu hàng lỗi trong khoảng ngày, map về cùng model với phiên InspectionLog.
        /// "Số thùng / Đạt / Không đạt" = số dòng LOT được kiểm / đạt / không đạt; số lượng OK/NG nằm ở phần chi tiết.
        /// </summary>
        public IReadOnlyList<QualityHistoryRow> LoadQcSessions(DateTime from, DateTime upper, bool upperExclusive)
        {
            var result = new List<QualityHistoryRow>();
            foreach (string kind in new[] { QcInitial, QcRework, QcFinal })
            {
                string sql = SessionSelect(kind) + " WHERE " + SessionTimeColumn(kind) + " >= @From AND " +
                    SessionTimeColumn(kind) + (upperExclusive ? " < " : " <= ") + "@To";
                DataTable table = TryLoad(sql,
                    new SqlParameter("@From", SqlDbType.DateTime) { Value = from },
                    new SqlParameter("@To", SqlDbType.DateTime) { Value = upper });
                foreach (DataRow row in table.Rows)
                    result.Add(MapSession(kind, row));
            }
            return result;
        }

        /// <summary>Chi tiết (từng LOT) của một lần QC có mã XLBT#...; mã khác trả danh sách rỗng.</summary>
        public IReadOnlyList<QualityHistoryDetailRow> LoadQcDetails(string inspectionCode)
        {
            var details = new List<QualityHistoryDetailRow>();
            int ticketId, qcRowId;
            string kind;
            if (!TryParseQcCode(inspectionCode, out ticketId, out kind, out qcRowId))
                return details;

            DataTable session = TryLoad(SessionSelect(kind) + " WHERE q.Id = @Id",
                new SqlParameter("@Id", SqlDbType.Int) { Value = qcRowId });
            if (session.Rows.Count == 0)
                return details;
            DataRow head = session.Rows[0];
            DateTime? checkedAt = ReadDateTime(head, "CheckedAt");

            if (kind == QcInitial)
            {
                // QC ban đầu có kết quả theo từng LOT bị ảnh hưởng (snapshot truy vết).
                DataTable lots = TryLoad(@"
SELECT a.LotNo, a.SoLuongDaKiemTra, a.SoLuongOK, a.SoLuongNG, a.SoLuongRework, a.SoLuongLoaiBo
FROM dbo.FVN_PhieuXuLyBatThuongAffectedLot a
WHERE a.PhieuXuLyBatThuongId = @P
ORDER BY a.Id",
                    new SqlParameter("@P", SqlDbType.Int) { Value = ticketId });
                foreach (DataRow lot in lots.Rows)
                {
                    int ng = ReadInt(lot, "SoLuongNG");
                    details.Add(new QualityHistoryDetailRow
                    {
                        BoxLotNo = ReadString(lot, "LotNo"),
                        BoxProductionDate = null,
                        IsMatch = ng == 0,
                        MismatchFields = DescribeQuantities(
                            ReadInt(lot, "SoLuongDaKiemTra"), ReadInt(lot, "SoLuongOK"), ng,
                            ReadInt(lot, "SoLuongRework"), ReadInt(lot, "SoLuongLoaiBo")),
                        CheckedAt = checkedAt
                    });
                }
                if (details.Count > 0)
                    return details;
            }

            // QC rework / QC cuối (hoặc QC ban đầu chưa có snapshot LOT): một dòng tổng hợp.
            int sessionNg = ReadInt(head, "QtyNg");
            details.Add(new QualityHistoryDetailRow
            {
                BoxLotNo = ReadString(head, "LotNo"),
                BoxProductionDate = null,
                IsMatch = sessionNg == 0,
                MismatchFields = DescribeQuantities(ReadInt(head, "Checked"), ReadInt(head, "QtyOk"), sessionNg, null, null),
                CheckedAt = checkedAt
            });
            return details;
        }

        private static string SessionTimeColumn(string kind)
        {
            return kind == QcInitial ? "q.ConfirmedAt" : kind == QcRework ? "q.ConfirmedAt" : "q.ThoiGian";
        }

        private const string LotOfTicket = "ISNULL(NULLIF(LTRIM(RTRIM(p.SoLoLoi)), N''), p.SoLo)";

        private static string SessionSelect(string kind)
        {
            if (kind == QcInitial)
            {
                return @"
SELECT q.Id AS QcId, q.PhieuXuLyBatThuongId AS PhieuId, p.SoPhieu, p.MaSanPham AS ItemCode,
       " + LotOfTicket + @" AS LotNo,
       q.SoLuongDaKiemTra AS Checked, q.SoLuongOK AS QtyOk, q.SoLuongNG AS QtyNg,
       q.ConfirmedAt AS CheckedAt,
       (SELECT COUNT(*) FROM dbo.FVN_PhieuXuLyBatThuongAffectedLot a
         WHERE a.PhieuXuLyBatThuongId = q.PhieuXuLyBatThuongId) AS LotCount,
       (SELECT COUNT(*) FROM dbo.FVN_PhieuXuLyBatThuongAffectedLot a
         WHERE a.PhieuXuLyBatThuongId = q.PhieuXuLyBatThuongId AND a.SoLuongNG > 0) AS FailLots,
       STUFF((SELECT N', ' + a2.LotNo FROM dbo.FVN_PhieuXuLyBatThuongAffectedLot a2
               WHERE a2.PhieuXuLyBatThuongId = q.PhieuXuLyBatThuongId
               ORDER BY a2.Id FOR XML PATH(N'')), 1, 2, N'') AS AllLots
FROM dbo.FVN_PhieuXuLyBatThuongQCInitial q
JOIN dbo.FVN_PhieuXuLyBatThuong p ON p.Id = q.PhieuXuLyBatThuongId";
            }

            if (kind == QcRework)
            {
                return @"
SELECT q.Id AS QcId, q.PhieuXuLyBatThuongId AS PhieuId, p.SoPhieu, p.MaSanPham AS ItemCode,
       " + LotOfTicket + @" AS LotNo,
       q.SoLuongRework AS Checked, q.SoLuongOK AS QtyOk, q.SoLuongNG AS QtyNg,
       q.ConfirmedAt AS CheckedAt,
       1 AS LotCount,
       CASE WHEN q.SoLuongNG > 0 THEN 1 ELSE 0 END AS FailLots,
       CAST(NULL AS NVARCHAR(MAX)) AS AllLots
FROM dbo.FVN_PXLB_ReworkQC q
JOIN dbo.FVN_PhieuXuLyBatThuong p ON p.Id = q.PhieuXuLyBatThuongId";
            }

            // QC xác nhận cuối: LOT nhập lại = LOT đã xuất rework (QCXacNhanCuoi bắt buộc khớp), thiếu thì lấy LOT lỗi của phiếu.
            return @"
SELECT q.Id AS QcId, q.PhieuXuLyBatThuongId AS PhieuId, p.SoPhieu, p.MaSanPham AS ItemCode,
       ISNULL((SELECT TOP 1 x.LotXuat FROM dbo.FVN_TraHangQTChungXuat x
                WHERE x.PhieuXuLyBatThuongId = q.PhieuXuLyBatThuongId AND x.LoaiXuat = N'Rework'
                ORDER BY x.Id), " + LotOfTicket + @") AS LotNo,
       q.SoLuongDaRework AS Checked, q.SoLuongOK AS QtyOk, q.SoLuongNG AS QtyNg,
       q.ThoiGian AS CheckedAt,
       1 AS LotCount,
       CASE WHEN q.SoLuongNG > 0 THEN 1 ELSE 0 END AS FailLots,
       CAST(NULL AS NVARCHAR(MAX)) AS AllLots
FROM dbo.FVN_TraHangQTChungQC q
JOIN dbo.FVN_PhieuXuLyBatThuong p ON p.Id = q.PhieuXuLyBatThuongId";
        }

        private static QualityHistoryRow MapSession(string kind, DataRow row)
        {
            int ticketId = ReadInt(row, "PhieuId");
            int lotCount = Math.Max(1, ReadInt(row, "LotCount"));
            int failLots = Math.Min(lotCount, ReadInt(row, "FailLots"));
            int ng = ReadInt(row, "QtyNg");
            return new QualityHistoryRow
            {
                InspectionCode = BuildQcCode(ticketId, kind, ReadInt(row, "QcId")),
                ItemCode = ReadString(row, "ItemCode"),
                LotNo = ReadString(row, "LotNo"),
                ProductionDate = null,
                TotalQuantity = ReadInt(row, "Checked"),
                DocumentNo = BuildDocumentRef("XLBT", ticketId, ReadString(row, "SoPhieu")),
                TotalBox = lotCount,
                PassCount = lotCount - failLots,
                FailCount = failLots,
                FinalResult = ng == 0 ? "PASS" : "FAIL",
                CheckedAt = ReadDateTime(row, "CheckedAt"),
                RelatedLots = ReadString(row, "AllLots")
            };
        }

        /// <summary>Tên bước QC hiển thị trên timeline của phiên.</summary>
        public static string QcKindLabel(string kind)
        {
            if (string.Equals(kind, QcInitial, StringComparison.OrdinalIgnoreCase)) return "QC phân loại ban đầu (hàng lỗi)";
            if (string.Equals(kind, QcRework, StringComparison.OrdinalIgnoreCase)) return "QC rework (hàng lỗi)";
            if (string.Equals(kind, QcFinal, StringComparison.OrdinalIgnoreCase)) return "QC xác nhận cuối (hàng lỗi)";
            return "QC hàng lỗi";
        }

        // =====================================================================
        // Sự kiện của phiếu -> Lịch sử kho / Truy vết (timeline)
        // =====================================================================

        /// <summary>
        /// Sự kiện của phiếu KHÔNG phải giao dịch kho. Các bước có giao dịch kho thật (xuất rework, nhập lại OK/NG,
        /// scrap, nhập NG khách trả, hoàn kho khi hủy) đã có trong StockHistory nên không lặp lại ở đây.
        /// Vị trí (From/ToLocation) trả về dạng SlotId thô; nơi gọi tự đổi sang tên vị trí.
        /// </summary>
        public IReadOnlyList<ItemHistoryRow> LoadTicketEvents(DateTime? from, DateTime? to)
        {
            var events = new List<ItemHistoryRow>();
            SqlParameter[] range = new[]
            {
                new SqlParameter("@From", SqlDbType.DateTime) { Value = from.HasValue ? (object)from.Value : DBNull.Value },
                new SqlParameter("@To", SqlDbType.DateTime) { Value = to.HasValue ? (object)to.Value : DBNull.Value }
            };

            // Cột: RefType, RefId, SoPhieu, ActionType, EventTime, ItemCode, LotNo, Qty, UserName, Detail, SlotId
            // 1) Lập phiếu trả hàng (khách trả / trả nội bộ) - từng dòng hàng của phiếu
            AddEvents(events, @"
SELECT N'KTR' AS RefType, h.Id AS RefId, h.SoPhieu, N'XLBT_LAP_PHIEU_TRA' AS ActionType, h.CreatedAt AS EventTime,
       ct.MaHang AS ItemCode, ct.LotNo AS LotNo, ct.SoLuong AS Qty, h.CreatedBy AS UserName,
       CONCAT(CASE h.Nguon WHEN 2 THEN N'Khách trả' WHEN 1 THEN N'Trả nội bộ' ELSE N'Không rõ nguồn' END,
              CASE WHEN NULLIF(LTRIM(RTRIM(ct.LyDoNg)), N'') IS NULL THEN N'' ELSE N' | Lý do NG: ' + ct.LyDoNg END,
              CASE WHEN NULLIF(LTRIM(RTRIM(h.LyDo)), N'') IS NULL THEN N'' ELSE N' | Lý do: ' + h.LyDo END) AS Detail,
       ct.SlotIdNguon AS SlotId
FROM dbo.FVN_PhieuTraHang h
JOIN dbo.FVN_PhieuTraHangCT ct ON ct.PhieuTraHangId = h.Id
WHERE (@From IS NULL OR h.CreatedAt >= @From) AND (@To IS NULL OR h.CreatedAt <= @To)",
                range);

            // 2) Lập phiếu xử lý bất thường
            AddEvents(events, @"
SELECT N'XLBT' AS RefType, p.Id AS RefId, p.SoPhieu, N'XLBT_LAP_PHIEU' AS ActionType, p.CreatedAt AS EventTime,
       p.MaSanPham AS ItemCode, " + LotOfTicket + @" AS LotNo, p.SoLuongLoi AS Qty, p.CreatedBy AS UserName,
       CONCAT(CASE p.Nguon WHEN 2 THEN N'Khách trả' WHEN 1 THEN N'Trả nội bộ' ELSE N'Không rõ nguồn' END,
              CASE WHEN NULLIF(LTRIM(RTRIM(p.SoPhieuTraHangGoc)), N'') IS NULL THEN N'' ELSE N' | Phiếu trả gốc: ' + p.SoPhieuTraHangGoc END,
              CASE WHEN NULLIF(LTRIM(RTRIM(p.NoiDungBatThuong)), N'') IS NULL THEN N'' ELSE N' | Nội dung: ' + p.NoiDungBatThuong END) AS Detail,
       p.SlotIdNguon AS SlotId
FROM dbo.FVN_PhieuXuLyBatThuong p
WHERE (@From IS NULL OR p.CreatedAt >= @From) AND (@To IS NULL OR p.CreatedAt <= @To)",
                range);

            // 3) Truy vết LOT ảnh hưởng (mỗi LOT trong snapshot một dòng - giúp tra được phiếu từ LOT bị ảnh hưởng)
            AddEvents(events, @"
SELECT N'XLBT' AS RefType, a.PhieuXuLyBatThuongId AS RefId, p.SoPhieu, N'XLBT_TRUY_VET_LOT' AS ActionType, a.SnapshotAt AS EventTime,
       a.MaSanPham AS ItemCode, a.LotNo AS LotNo, a.SoLuongAnhHuong AS Qty, a.SnapshotBy AS UserName,
       CONCAT(N'Nguồn truy vết: ',
              CASE a.SourceType WHEN 1 THEN N'Kho' WHEN 2 THEN N'Sản xuất / WIP' WHEN 3 THEN N'Khách trả' ELSE CAST(a.SourceType AS NVARCHAR(20)) END,
              CASE WHEN NULLIF(LTRIM(RTRIM(a.SourceReference)), N'') IS NULL THEN N'' ELSE N' | ' + a.SourceReference END) AS Detail,
       a.SlotId AS SlotId
FROM dbo.FVN_PhieuXuLyBatThuongAffectedLot a
JOIN dbo.FVN_PhieuXuLyBatThuong p ON p.Id = a.PhieuXuLyBatThuongId
WHERE (@From IS NULL OR a.SnapshotAt >= @From) AND (@To IS NULL OR a.SnapshotAt <= @To)",
                range);

            // 4) QC định hướng (chỉ lưu hướng xử lý + người/ngày trên phiếu, không có bảng QC riêng)
            AddEvents(events, @"
SELECT N'XLBT' AS RefType, p.Id AS RefId, p.SoPhieu, N'XLBT_DINH_HUONG' AS ActionType, p.NgayDinhHuong AS EventTime,
       p.MaSanPham AS ItemCode, " + LotOfTicket + @" AS LotNo, p.SoLuongLoi AS Qty, p.NguoiDinhHuong AS UserName,
       CONCAT(N'Hướng xử lý: ',
              CASE p.HuongXuLy WHEN 1 THEN N'Từ chối giao bù (không phải lỗi thật)' WHEN 2 THEN N'Chỉ giao bù'
                               WHEN 3 THEN N'Cần rework' ELSE N'Chưa xác định' END) AS Detail,
       CAST(NULL AS INT) AS SlotId
FROM dbo.FVN_PhieuXuLyBatThuong p
WHERE p.NgayDinhHuong IS NOT NULL AND p.HuongXuLy > 0
  AND (@From IS NULL OR p.NgayDinhHuong >= @From) AND (@To IS NULL OR p.NgayDinhHuong <= @To)",
                range);

            // 5) QC phân loại ban đầu (theo từng LOT bị ảnh hưởng)
            AddEvents(events, @"
SELECT N'XLBT' AS RefType, q.PhieuXuLyBatThuongId AS RefId, p.SoPhieu, N'XLBT_QC_BAN_DAU' AS ActionType, q.ConfirmedAt AS EventTime,
       a.MaSanPham AS ItemCode, a.LotNo AS LotNo, a.SoLuongDaKiemTra AS Qty, q.ConfirmedBy AS UserName,
       CONCAT(N'OK ', a.SoLuongOK, N' | NG ', a.SoLuongNG, N' (Rework ', a.SoLuongRework, N', Loại bỏ ', a.SoLuongLoaiBo, N')',
              CASE WHEN NULLIF(LTRIM(RTRIM(q.KetLuan)), N'') IS NULL THEN N'' ELSE N' | Kết luận: ' + q.KetLuan END) AS Detail,
       CAST(NULL AS INT) AS SlotId
FROM dbo.FVN_PhieuXuLyBatThuongQCInitial q
JOIN dbo.FVN_PhieuXuLyBatThuong p ON p.Id = q.PhieuXuLyBatThuongId
JOIN dbo.FVN_PhieuXuLyBatThuongAffectedLot a ON a.PhieuXuLyBatThuongId = q.PhieuXuLyBatThuongId
WHERE q.ConfirmedAt IS NOT NULL
  AND (@From IS NULL OR q.ConfirmedAt >= @From) AND (@To IS NULL OR q.ConfirmedAt <= @To)",
                range);

            // 6) Giao hàng rework cho sản xuất (không đổi tồn kho nên không có StockHistory)
            AddEvents(events, @"
SELECT N'XLBT' AS RefType, g.PhieuXuLyBatThuongId AS RefId, p.SoPhieu, N'XLBT_GIAO_SX' AS ActionType, g.ThoiGian AS EventTime,
       ISNULL(NULLIF(LTRIM(RTRIM(g.MaHang)), N''), p.MaSanPham) AS ItemCode, g.LotGiao AS LotNo, g.SoLuongGiao AS Qty, g.NguoiNhan AS UserName,
       CONCAT(N'Bộ phận nhận: ', ISNULL(g.BoPhanNhan, N'-'), N' | Người nhận: ', ISNULL(g.NguoiNhan, N'-')) AS Detail,
       CAST(NULL AS INT) AS SlotId
FROM dbo.FVN_TraHangQTChungGiao g
JOIN dbo.FVN_PhieuXuLyBatThuong p ON p.Id = g.PhieuXuLyBatThuongId
WHERE (@From IS NULL OR g.ThoiGian >= @From) AND (@To IS NULL OR g.ThoiGian <= @To)",
                range);

            // 7) QC xác nhận cuối
            AddEvents(events, @"
SELECT N'XLBT' AS RefType, q.PhieuXuLyBatThuongId AS RefId, p.SoPhieu, N'XLBT_QC_CUOI' AS ActionType, q.ThoiGian AS EventTime,
       p.MaSanPham AS ItemCode,
       ISNULL((SELECT TOP 1 x.LotXuat FROM dbo.FVN_TraHangQTChungXuat x
                WHERE x.PhieuXuLyBatThuongId = q.PhieuXuLyBatThuongId AND x.LoaiXuat = N'Rework'
                ORDER BY x.Id), " + LotOfTicket + @") AS LotNo,
       q.SoLuongDaRework AS Qty, q.NguoiQC AS UserName,
       CONCAT(N'OK ', q.SoLuongOK, N' | NG ', q.SoLuongNG,
              CASE WHEN NULLIF(LTRIM(RTRIM(q.KetLuan)), N'') IS NULL THEN N'' ELSE N' | Kết luận: ' + q.KetLuan END) AS Detail,
       CAST(NULL AS INT) AS SlotId
FROM dbo.FVN_TraHangQTChungQC q
JOIN dbo.FVN_PhieuXuLyBatThuong p ON p.Id = q.PhieuXuLyBatThuongId
WHERE (@From IS NULL OR q.ThoiGian >= @From) AND (@To IS NULL OR q.ThoiGian <= @To)",
                range);

            // 7b) Hoàn tất phiếu xử lý (kể cả hoàn tất ngay sau Initial QC): ghi rõ có/không có giao dịch kho
            //     để người xem biết phiếu kết thúc mà không Rework là do QC, không phải do sót bước.
            AddEvents(events, @"
SELECT N'XLBT' AS RefType, p.Id AS RefId, p.SoPhieu, N'XLBT_HOAN_TAT' AS ActionType, p.UpdatedAt AS EventTime,
       p.MaSanPham AS ItemCode, " + LotOfTicket + @" AS LotNo, p.SoLuongLoi AS Qty, p.UpdatedBy AS UserName,
       CONCAT(N'Hoàn tất xử lý',
              CASE WHEN qi.Id IS NULL THEN N'' ELSE CONCAT(N' | Initial QC: OK ', qi.SoLuongOK, N', NG ', qi.SoLuongNG, N' (Rework ', qi.SoLuongRework, N', Loại bỏ ', qi.SoLuongLoaiBoBanDau, N')') END,
              CASE WHEN EXISTS(SELECT 1 FROM dbo.FVN_TraHangQTChungXuat x WHERE x.PhieuXuLyBatThuongId = p.Id)
                   THEN N' | Có xuất kho Rework/Scrap' ELSE N' | Không phát sinh giao dịch kho' END) AS Detail,
       CAST(NULL AS INT) AS SlotId
FROM dbo.FVN_PhieuXuLyBatThuong p
LEFT JOIN dbo.FVN_PhieuXuLyBatThuongQCInitial qi ON qi.PhieuXuLyBatThuongId = p.Id
WHERE p.Status = 100 AND p.UpdatedAt IS NOT NULL
  AND (@From IS NULL OR p.UpdatedAt >= @From) AND (@To IS NULL OR p.UpdatedAt <= @To)",
                range);

            // 8) Hủy phiếu xử lý
            AddEvents(events, @"
SELECT N'XLBT' AS RefType, p.Id AS RefId, p.SoPhieu, N'XLBT_HUY' AS ActionType, p.NgayHuy AS EventTime,
       p.MaSanPham AS ItemCode, " + LotOfTicket + @" AS LotNo, p.SoLuongLoi AS Qty, p.NguoiHuy AS UserName,
       CONCAT(N'Lý do hủy: ', ISNULL(p.LyDoHuy, N'-')) AS Detail,
       CAST(NULL AS INT) AS SlotId
FROM dbo.FVN_PhieuXuLyBatThuong p
WHERE p.NgayHuy IS NOT NULL
  AND (@From IS NULL OR p.NgayHuy >= @From) AND (@To IS NULL OR p.NgayHuy <= @To)",
                range);

            return events;
        }

        private void AddEvents(List<ItemHistoryRow> target, string sql, SqlParameter[] range)
        {
            DataTable table = TryLoad(sql, CloneParameters(range));
            foreach (DataRow row in table.Rows)
            {
                string refType = ReadString(row, "RefType");
                int refId = ReadInt(row, "RefId");
                int? slotId = row.Table.Columns.Contains("SlotId") && row["SlotId"] != DBNull.Value
                    ? (int?)Convert.ToInt32(row["SlotId"])
                    : null;
                target.Add(new ItemHistoryRow
                {
                    ItemCode = ReadString(row, "ItemCode"),
                    TemCode = (refType ?? "XLBT") + "#" + refId.ToString(CultureInfo.InvariantCulture),
                    LotNo = ReadString(row, "LotNo"),
                    ActionType = ReadString(row, "ActionType"),
                    Quantity = ReadInt(row, "Qty"),
                    EventTime = ReadDateTime(row, "EventTime"),
                    FromLocation = slotId.HasValue ? slotId.Value.ToString(CultureInfo.InvariantCulture) : null,
                    DocumentNo = BuildDocumentRef(refType, refId, ReadString(row, "SoPhieu")),
                    UserName = ReadString(row, "UserName"),
                    Note = ReadString(row, "Detail")
                });
            }
        }

        // =====================================================================
        // Tiện ích đọc
        // =====================================================================

        private static SqlParameter[] CloneParameters(SqlParameter[] source)
        {
            var copy = new SqlParameter[source.Length];
            for (int i = 0; i < source.Length; i++)
                copy[i] = new SqlParameter(source[i].ParameterName, source[i].SqlDbType) { Value = source[i].Value };
            return copy;
        }

        /// <summary>Đọc một nguồn; bảng/cột chưa có hoặc lỗi truy vấn chỉ làm nguồn đó rỗng (báo cáo vẫn chạy).</summary>
        private DataTable TryLoad(string sql, params SqlParameter[] parameters)
        {
            try
            {
                DataTable table = LoadData(sql, parameters);
                return table ?? new DataTable();
            }
            catch (Exception)
            {
                return new DataTable();
            }
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

        private static DateTime? ReadDateTime(DataRow row, string name)
        {
            return row.Table.Columns.Contains(name) && row[name] != DBNull.Value
                ? Convert.ToDateTime(row[name])
                : (DateTime?)null;
        }
    }
}
