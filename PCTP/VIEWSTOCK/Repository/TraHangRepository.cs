using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Models;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Repository
{
    public class TraHangRepository : ITraHangRepository
    {
        private readonly SQLPROVIDER _sql;
        public TraHangRepository(SQLPROVIDER sql) => _sql = sql;

        private static string Esc(string s) => (s ?? "").Replace("'", "''");

        // ════════════════════════════════════════════════════════════════
        // STOCKTPTRAHANG — ghi nhận 1 lần trả hàng NG (chưa được nhận lại)
        // ════════════════════════════════════════════════════════════════
        public void InsertTraHang(SqlConnection conn, SqlTransaction tran,
            string lot, int slTra, string lyDoNg, string nguon)
        {
            _sql.ExecuteNonQuery(conn, tran, @"
                INSERT INTO STOCKTPTRAHANG
                    (LOT, NGAYTRA, SLTRA, SLNHANLAI,SLCONLAI, LY_DO_NG, STATUS)
                VALUES
                    (@lot, GETDATE(), @sl, 0,@sl, @lyDo, 0)",
                new SqlParameter("@lot", lot),
                new SqlParameter("@sl", slTra),
                new SqlParameter("@lyDo", $"[{nguon}] " + (lyDoNg ?? "")));
        }

        public void TruSlConLai(SqlConnection conn, SqlTransaction tran, string lot, int soLuong)
        {
            string match = LotCodeHelper.BuildLotMatchSql("LOT", "@lot");
            _sql.ExecuteNonQuery(conn, tran,
                $"UPDATE STOCKTP SET SLCONLAI = ISNULL(SLCONLAI,0) - @sl WHERE {match}",
                new SqlParameter("@sl", soLuong), new SqlParameter("@lot", lot));
        }

        // Khách trả hàng: cộng lại tồn, trừ SLXUAT (vì hàng coi như "chưa xuất" nữa)
        public void NhapLaiHangKhachTra(SqlConnection conn, SqlTransaction tran, string lot, int soLuong)
        {
            string match = LotCodeHelper.BuildLotMatchSql("LOT", "@lot");
            _sql.ExecuteNonQuery(conn, tran, $@"
        UPDATE STOCKTP SET
            SLCONLAI = ISNULL(SLCONLAI,0) + @sl,
            SLXUAT   = ISNULL(SLXUAT,0)   - @sl
        WHERE {match}",
                new SqlParameter("@sl", soLuong), new SqlParameter("@lot", lot));
        }

        public void InsertNhanTraTheoIDP(SqlConnection conn, SqlTransaction tran,
     string lot, int slNhanTra, int idp)
        {
            string match = LotCodeHelper.BuildLotMatchSql("LOT", "@lot");
            _sql.ExecuteNonQuery(conn, tran, $@"
        INSERT INTO STOCKTPNHANTRA
            (LOT, PART_NO, PART_NAME, NGAY_NHAN_TRA, SL_NHAN_TRA, LY_DO_NG)
        SELECT TOP 1 @lot, PART, NAME, GETDATE(), @sl, N'Khách trả — Phiếu ' + CAST(@idp AS NVARCHAR(20))
        FROM STOCKTP WHERE {match}",
                new SqlParameter("@lot", lot),
                new SqlParameter("@sl", slNhanTra),
                new SqlParameter("@idp", idp));
        }


        // ════════════════════════════════════════════════════════════════
        // TMPCHOGIAO — staging chờ giao
        // ════════════════════════════════════════════════════════════════
        public void InsertChoGiao(int slotIdNguon, string lotThung, string lotGoc,
            string maHang, int soLuong, string phieuGiaoId)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, @"
                INSERT INTO TMPCHOGIAO
                    (LotThung, LotGoc, MaHang, SoLuong, SlotIdNguon, PhieuGiaoId, TrangThai)
                VALUES
                    (@lt, @lg, @mh, @sl, @slot, @pg, 'CHO_GIAO')",
                new SqlParameter("@lt", lotThung),
                new SqlParameter("@lg", lotGoc),
                new SqlParameter("@mh", maHang),
                new SqlParameter("@sl", soLuong),
                new SqlParameter("@slot", (object)slotIdNguon ?? DBNull.Value),
                new SqlParameter("@pg", (object)phieuGiaoId ?? DBNull.Value));
        }

        public List<ChoGiaoItem> GetChoGiaoTheoDanhSach(IEnumerable<int> ids)
        {
            var idList = ids?.ToList() ?? new List<int>();
            var result = new List<ChoGiaoItem>();
            if (idList.Count == 0) return result;

            string inClause = string.Join(",", idList);
            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                $"SELECT * FROM TMPCHOGIAO WHERE Id IN ({inClause})");

            foreach (DataRow r in dt.Rows)
                result.Add(new ChoGiaoItem
                {
                    Id = Convert.ToInt32(r["Id"]),
                    LotThung = r["LotThung"].ToString(),
                    LotGoc = r["LotGoc"].ToString(),
                    MaHang = r["MaHang"].ToString(),
                    SoLuong = Convert.ToInt32(r["SoLuong"]),
                    SlotIdNguon = r["SlotIdNguon"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SlotIdNguon"]),
                    TrangThai = r["TrangThai"].ToString()
                });
            return result;
        }

        public void CapNhatTrangThaiChoGiao(SqlConnection conn, SqlTransaction tran,
            IEnumerable<int> ids, string trangThaiMoi)
        {
            var idList = ids?.ToList() ?? new List<int>();
            if (idList.Count == 0) return;

            string inClause = string.Join(",", idList);
            _sql.ExecuteNonQuery(conn, tran,
                $"UPDATE TMPCHOGIAO SET TrangThai = @tt WHERE Id IN ({inClause})",
                new SqlParameter("@tt", trangThaiMoi));
        }

        // ════════════════════════════════════════════════════════════════
        // TMPQUETTHUNGTRA — quét thùng khách trả
        // ════════════════════════════════════════════════════════════════
        public bool ExistsThungDaQuet(int idp, string lotThung)
        {
            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                "SELECT COUNT(*) FROM TMPQUETTHUNGTRA WHERE IDP=@idp AND LotThung=@lt",
                new[] { new SqlParameter("@idp", idp), new SqlParameter("@lt", lotThung) });
            return int.TryParse(kq?.ToString(), out int v) && v > 0;
        }

        public void InsertThungQuetTra(int idp, string lotThung, string lotGoc,
            string maHang, int slThung)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, @"
                INSERT INTO TMPQUETTHUNGTRA (IDP, LotThung, LotGoc, MaHang, SlThung, DaXuLy)
                VALUES (@idp, @lt, @lg, @mh, @sl, 0)",
                new SqlParameter("@idp", idp),
                new SqlParameter("@lt", lotThung),
                new SqlParameter("@lg", lotGoc),
                new SqlParameter("@mh", maHang),
                new SqlParameter("@sl", slThung));
        }

        public List<NhomLotTraInfo> GetNhomLotChuaXuLy(int idp)
        {
            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, @"
                SELECT LotGoc, MaHang, SUM(SlThung) AS TongSL
                FROM TMPQUETTHUNGTRA
                WHERE IDP = @idp AND DaXuLy = 0
                GROUP BY LotGoc, MaHang",
                new List<SqlParameter> { new SqlParameter("@idp", idp) });

            return dt.Rows.Cast<DataRow>().Select(r => new NhomLotTraInfo
            {
                LotGoc = r["LotGoc"].ToString(),
                MaHang = r["MaHang"].ToString(),
                TongSl = Convert.ToInt32(r["TongSL"])
            }).ToList();
        }

        public void DanhDauDaXuLy(SqlConnection conn, SqlTransaction tran, int idp)
        {
            _sql.ExecuteNonQuery(conn, tran,
                "UPDATE TMPQUETTHUNGTRA SET DaXuLy = 1 WHERE IDP = @idp AND DaXuLy = 0",
                new SqlParameter("@idp", idp));
        }

        public void DanhDauPhieuDaNhapKho(SqlConnection conn, SqlTransaction tran, int idp)
        {
            _sql.ExecuteNonQuery(conn, tran,
                "UPDATE TMPPHIEUNHANDB SET DA_NHAP_KHO = 1 WHERE IDP = @idp",
                new SqlParameter("@idp", idp));
        }
        // TraHangRepository.cs — thêm implementation

        /// <summary>
        /// Ghi lại toàn bộ SlotLot của 1 Slot (xoá cũ, insert lại danh sách mới) và cập nhật
        /// bảng Slot tổng hợp (Quantity/ItemCode/ImportDate/IsOccupied) — TẤT CẢ trong cùng
        /// conn/tran được truyền vào từ ngoài.
        ///
        /// Đây là bản sao có chủ đích của SlotHelper.SaveSlotLots + SlotHelper.UpdateSlotQuantity,
        /// KHÔNG được gọi lại 2 hàm đó vì chúng tự mở SqlConnection/SqlTransaction riêng — nếu gọi
        /// sẽ phá vỡ tính atomic của giao dịch trả hàng NG (Slot có thể bị trừ dù STOCKTP rollback).
        /// Mọi thay đổi nghiệp vụ ở SaveSlotLots gốc (ví dụ thêm cột mới) cần đồng bộ lại ở đây.
        /// </summary>
        public void SaveSlotLotsInTransaction(SqlConnection conn, SqlTransaction tran,
            int slotId, List<LotInfo> lots)
        {
            lots = lots ?? new List<LotInfo>();

            // ── 1. Xoá toàn bộ Lot cũ của Slot ───────────────────────────────────
            using (var cmdDelete = new SqlCommand(
                "DELETE FROM SlotLot WHERE SlotId = @SlotId", conn, tran))
            {
                cmdDelete.Parameters.AddWithValue("@SlotId", slotId);
                cmdDelete.ExecuteNonQuery();
            }

            // ── 2. Insert lại toàn bộ Lot còn lại ────────────────────────────────
            foreach (var lot in lots)
            {
                using (var cmdInsert = new SqlCommand(@"
            INSERT INTO SlotLot
            (
                SlotId,
                ItemCode,
                LotNo,
                Quantity,
                TemCode,
                QrData,
                ImportDate,
                MaPhieu
            )
            VALUES
            (
                @SlotId,
                @ItemCode,
                @LotNo,
                @Quantity,
                @TemCode,
                @QrData,
                @ImportDate,
                @MaPhieu
            )", conn, tran))
                {
                    cmdInsert.Parameters.AddWithValue("@SlotId", slotId);
                    cmdInsert.Parameters.AddWithValue("@LotNo",
                        (object)lot.LotNo ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@Quantity", lot.Quantity);
                    cmdInsert.Parameters.AddWithValue("@TemCode",
                        (object)lot.TemCode ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@QrData",
                        (object)lot.QRInfo?.RawQr ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@MaPhieu",
                        (object)lot.QRInfo?.MaPhieu ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@ItemCode",
                        (object)lot.QRInfo?.ItemCode ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@ImportDate",
                        (object)lot.QRInfo?.ImportDate ?? DateTime.Now);

                    cmdInsert.ExecuteNonQuery();
                }
            }

            // ── 3. Cập nhật lại Slot tổng hợp — y hệt SlotHelper.UpdateSlotQuantity ─
            using (var cmdUpdate = new SqlCommand(@"
        UPDATE s
        SET
            Quantity =
            (
                SELECT ISNULL(SUM(sl.Quantity),0)
                FROM SlotLot sl
                WHERE sl.SlotId = s.SlotId
            ),

            ItemCode =
            (
                SELECT TOP (1) sl.ItemCode
                FROM SlotLot sl
                WHERE sl.SlotId = s.SlotId
                ORDER BY sl.ImportDate DESC, sl.CreatedDate DESC
            ),

            ImportDate =
            (
                SELECT TOP (1) sl.ImportDate
                FROM SlotLot sl
                WHERE sl.SlotId = s.SlotId
                ORDER BY sl.ImportDate DESC, sl.CreatedDate DESC
            ),

            IsOccupied =
            CASE
                WHEN EXISTS
                (
                    SELECT 1
                    FROM SlotLot sl
                    WHERE sl.SlotId = s.SlotId
                )
                THEN 1
                ELSE 0
            END

        FROM Slot s
        WHERE s.SlotId = @SlotId;", conn, tran))
            {
                cmdUpdate.Parameters.AddWithValue("@SlotId", slotId);
                cmdUpdate.ExecuteNonQuery();
            }
        }

        // TraHangRepository.cs — thêm implementation

        /// <summary>
        /// Đọc SlotLot theo đúng conn/tran hiện tại (không tự mở connection mới như
        /// SlotHelper.GetSlotLots) — dùng khi cần đọc-sửa-ghi trong cùng 1 giao dịch,
        /// ví dụ ExportSpecificLot bên dưới.
        /// </summary>
        public List<LotInfo> GetSlotLotsInTransaction(SqlConnection conn, SqlTransaction tran, int slotId)
        {
            var lots = new List<LotInfo>();

            using (var cmd = new SqlCommand(@"
        SELECT ItemCode, LotNo, Quantity, TemCode, QrData, MaPhieu, ImportDate
        FROM SlotLot
        WHERE SlotId = @SlotId
        ORDER BY LotNo", conn, tran))
            {
                cmd.Parameters.AddWithValue("@SlotId", slotId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string lotNo = reader["LotNo"] == DBNull.Value ? "" : reader["LotNo"].ToString();
                        int quantity = Convert.ToInt32(reader["Quantity"]);
                        string temCode = reader["TemCode"] == DBNull.Value ? "" : reader["TemCode"].ToString();
                        string qrData = reader["QrData"] == DBNull.Value ? "" : reader["QrData"].ToString();

                        QRCodeInfo qrInfo = null;
                        if (!string.IsNullOrWhiteSpace(qrData))
                        {
                            try { qrInfo = QRCodeParser.ParseQRCode(qrData); }
                            catch (FormatException) { qrInfo = null; }
                        }

                        if (qrInfo == null)
                        {
                            qrInfo = new QRCodeInfo
                            {
                                LotNo = lotNo,
                                ItemCode = reader["ItemCode"] == DBNull.Value ? "" : reader["ItemCode"].ToString(),
                                Quantity = quantity,
                                MaPhieu = reader["MaPhieu"] == DBNull.Value ? "" : reader["MaPhieu"].ToString(),
                                ImportDate = reader["ImportDate"] == DBNull.Value
                                    ? (DateTime?)null : Convert.ToDateTime(reader["ImportDate"]),
                                RawQr = qrData
                            };
                        }
                        else
                        {
                            qrInfo.Quantity = quantity;
                        }

                        lots.Add(new LotInfo
                        {
                            LotNo = lotNo,
                            Quantity = quantity,
                            TemCode = temCode,
                            RawQr = qrData,
                            QRInfo = qrInfo
                        });
                    }
                }
            }

            return lots;
        }

        // TraHangRepository.cs — thêm
        /// <summary>
        /// Khi CapNhapKho (HVN_PGH) đã trừ STOCKTP.SLXUAT thành công cho 1 danh sách LOT
        /// (tức phiếu giao đã ghép LOT + xác nhận CNK), đóng luôn các dòng TMPCHOGIAO
        /// tương ứng (nếu có) sang DA_GIAO — tránh việc TMPCHOGIAO bị "mồ côi" mãi ở
        /// trạng thái CHO_GIAO trong khi hàng đã thực sự rời kho theo giấy tờ.
        ///
        /// KHÔNG trừ STOCKTP ở đây — SP Usp_Qrcode_Update_Stock2405 đã trừ rồi.
        /// </summary>
        public void CloseChoGiaoTheoLot(SqlConnection conn, SqlTransaction tran,
            IEnumerable<string> lotsDaXuat)
        {
            var lots = lotsDaXuat?.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().ToList();
            if (lots == null || lots.Count == 0) return;

            string inClause = string.Join(",", lots.Select(l => $"'{Esc(l)}'"));
            _sql.ExecuteNonQuery(conn, tran, $@"
        UPDATE TMPCHOGIAO
        SET TrangThai = 'DA_GIAO'
        WHERE LotGoc IN ({inClause})
          AND TrangThai = 'CHO_GIAO'");
        }
        // TraHangRepository.cs — thêm
        /// <summary>Trả về các LOT (trong danh sách truyền vào) đang tồn tại trong bất kỳ
        /// TMPPHIEUGIAOHANG* nào với STATUS != 'OK' — nghĩa là đang chờ CNK bên HVN_PGH.</summary>
        public List<string> LocLotDangChoCNK(IEnumerable<string> lots)
        {
            var lotList = lots?.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().ToList();
            if (lotList == null || lotList.Count == 0) return new List<string>();

            string inClause = string.Join(",", lotList.Select(l => $"'{Esc(l)}'"));
            // Kiểm tra qua bảng TMPPHIEUGIAOHANG chính — nếu bạn có nhiều bảng TMP theo customer
            // (TmpTableSP, TmpTable_100002...), cân nhắc UNION thêm ở đây.
            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, $@"
        SELECT DISTINCT LOT FROM TMPPHIEUGIAOHANG
        WHERE LOT IN ({inClause}) AND ISNULL(STATUS,'') <> 'OK'");

            return dt.Rows.Cast<DataRow>().Select(r => r["LOT"].ToString()).ToList();
        }
        // TraHangRepository.cs
        public List<string> LocLotDaCNK(IEnumerable<string> lots)
        {
            var lotList = lots?.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().ToList();
            if (lotList == null || lotList.Count == 0) return new List<string>();

            string inClause = string.Join(",", lotList.Select(l => $"'{Esc(l)}'"));
            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, $@"
                SELECT DISTINCT LOT FROM TMPPHIEUGIAOHANG
                WHERE LOT IN ({inClause}) AND STATUS = 'OK'");

            return dt.Rows.Cast<DataRow>().Select(r => r["LOT"].ToString()).ToList();
        }

        public int GetSlXuatHienTai(string lot)
        {
            string match = LotCodeHelper.BuildLotMatchSql("LOT", "@lot");
            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                $"SELECT ISNULL(SLXUAT,0) FROM STOCKTP WHERE {match}",
                new[] { new SqlParameter("@lot", lot) });
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public List<ChoGiaoItem> GetChoGiaoDangCho()
        {
            var result = new List<ChoGiaoItem>();
            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                "SELECT * FROM TMPCHOGIAO WHERE TrangThai = 'CHO_GIAO' ORDER BY LotGoc, MaHang");

            foreach (DataRow r in dt.Rows)
                result.Add(new ChoGiaoItem
                {
                    Id = Convert.ToInt32(r["Id"]),
                    LotThung = r["LotThung"].ToString(),
                    LotGoc = r["LotGoc"].ToString(),
                    MaHang = r["MaHang"].ToString(),
                    SoLuong = Convert.ToInt32(r["SoLuong"]),
                    SlotIdNguon = r["SlotIdNguon"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SlotIdNguon"]),
                    TrangThai = r["TrangThai"].ToString()
                });
            return result;
        }
        // TraHangRepository.cs — thêm

        /// <summary>
        /// Lịch sử giao hàng của 1 LOT — LOT trong LUUPHIEUGIAOHANG có thể là chuỗi ghép
        /// "LOTA-100,LOTB-50" nên phải LIKE theo 3 dạng thay vì so khớp tuyệt đối.
        /// lotKey PHẢI là khoá chuẩn 20 ký tự (LotNoHelper.GetStockTpKey), không phải
        /// khoá hiển thị NormalizeLot.
        /// </summary>
        public List<LichSuGiaoHangInfo> GetLichSuGiaoHangTheoLot(string lotKey)
        {
            var result = new List<LichSuGiaoHangInfo>();
            if (string.IsNullOrWhiteSpace(lotKey)) return result;
            lotKey = lotKey.Trim();

            string lotValueExpr = "LTRIM(RTRIM(LEFT(part.value, CHARINDEX('-', part.value + '-') - 1)))";
            string match = LotCodeHelper.BuildLotMatchSql(lotValueExpr, "@lot");

            string sql = $@"
        SELECT DISTINCT g.STT, g.LOT, g.MAHANG, g.TENHANG, g.SOLUONG, g.NGAYGIAO, g.GIOGIAOFCC, g.NHAMAY, g.CUA, g.TRUYEN
        FROM LUUPHIEUGIAOHANG g
        CROSS APPLY STRING_SPLIT(g.LOT, ',') part
        WHERE {match}
        ORDER BY g.NGAYGIAO DESC";

            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb, sql, new SqlParameter("@lot", lotKey));
            foreach (DataRow r in dt.Rows)
            {
                result.Add(new LichSuGiaoHangInfo
                {
                    Stt = r["STT"] == DBNull.Value ? 0 : Convert.ToInt32(r["STT"]),
                    Lot = r["LOT"]?.ToString(),
                    MaHang = r["MAHANG"]?.ToString(),
                    TenHang = r["TENHANG"]?.ToString(),
                    SoLuong = r["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(r["SOLUONG"]),
                    NgayGiao = r["NGAYGIAO"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NGAYGIAO"]),
                    GioGiao = r["GIOGIAOFCC"]?.ToString(),
                    NhaMay = r["NHAMAY"]?.ToString(),
                    Cua = r["CUA"]?.ToString(),
                    Truyen = r["TRUYEN"]?.ToString()
                });
            }
            return result;
        }

        /// <summary>LOTFCC/LOTHVN trong LUUDOCQRCODE đã là khoá đơn (StockTpKey) — so khớp trực tiếp.</summary>
        public List<LichSuQrCodeInfo> GetLichSuQrCodeTheoLot(string lotKey)
        {
            var result = new List<LichSuQrCodeInfo>();
            if (string.IsNullOrWhiteSpace(lotKey)) return result;
            lotKey = lotKey.Trim();

            string fccExpr = "LTRIM(RTRIM(LEFT(part_fcc.value, CHARINDEX('-', part_fcc.value + '-') - 1)))";
            string hvnExpr = "LTRIM(RTRIM(LEFT(part_hvn.value, CHARINDEX('-', part_hvn.value + '-') - 1)))";
            string matchFcc = LotCodeHelper.BuildLotMatchSql(fccExpr, "@lot");
            string matchHvn = LotCodeHelper.BuildLotMatchSql(hvnExpr, "@lot");

            string sql = $@"
        SELECT DISTINCT qr.STT, qr.LOTFCC, qr.MAHANGFCC, qr.SLTEMFCC, qr.LOTHVN, qr.MAHANGHVN, qr.SLTEMHVN,
               qr.KETQUA, qr.NGAYXUAT, qr.GIOXUAT, qr.NHAMAY, qr.GIOGIAO
        FROM LUUDOCQRCODE qr
        OUTER APPLY STRING_SPLIT(qr.LOTFCC, ',') part_fcc
        OUTER APPLY STRING_SPLIT(qr.LOTHVN, ',') part_hvn
        WHERE ({matchFcc}) OR ({matchHvn})
        ORDER BY qr.NGAYXUAT DESC";

            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql,
                new List<SqlParameter> { new SqlParameter("@lot", lotKey) });
            foreach (DataRow r in dt.Rows)
            {
                result.Add(new LichSuQrCodeInfo
                {
                    Stt = r["STT"] == DBNull.Value ? 0 : Convert.ToInt32(r["STT"]),
                    LotFcc = r["LOTFCC"]?.ToString(),
                    MaHangFcc = r["MAHANGFCC"]?.ToString(),
                    SlTemFcc = r["SLTEMFCC"] == DBNull.Value ? 0 : Convert.ToInt32(r["SLTEMFCC"]),
                    LotHvn = r["LOTHVN"]?.ToString(),
                    MaHangHvn = r["MAHANGHVN"]?.ToString(),
                    SlTemHvn = r["SLTEMHVN"] == DBNull.Value ? 0 : Convert.ToInt32(r["SLTEMHVN"]),
                    KetQua = r["KETQUA"]?.ToString(),
                    NgayXuat = r["NGAYXUAT"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NGAYXUAT"]),
                    GioXuat = r["GIOXUAT"]?.ToString(),
                    NhaMay = r["NHAMAY"]?.ToString(),
                    GioGiao = r["GIOGIAO"]?.ToString()
                });
            }
            return result;
        }

        /// <summary>
        /// Tra LOT theo Mã hàng + khoảng ngày giao — dùng khi người dùng KHÔNG có LotNo
        /// trong tay (chỉ biết mã hàng, ngày giao khách báo trả). LOT ở đây tách từ chuỗi
        /// ghép "LOTA-100,LOTB-50" thành từng LOT riêng bằng STRING_SPLIT (SQL Server 2016+;
        /// nếu DB cũ hơn cần thay bằng hàm split thủ công/CLR — xem ghi chú dưới).
        /// </summary>
        public List<LotUngVienInfo> TimLotTheoMaHangNgay(string maHang, DateTime tuNgay, DateTime denNgay)
        {
            var result = new List<LotUngVienInfo>();
            if (string.IsNullOrWhiteSpace(maHang)) return result;

            string joinMatch = LotCodeHelper.BuildLotMatchSql("s.LOT", "giao.LOT");

            string sql = $@"
    SELECT 
            s.LOT,
            s.PART AS MAHANG,
            s.NAME AS TENHANG,
            s.SLSX AS SoLuongSanXuat,
            s.SLNHAP AS SoLuongNhap,
            s.SLCONLAI AS SoLuongConLai,
            s.NGAYNHAP AS NgayNhap,
            ISNULL(giao.TongSl, 0) AS TongSlDaGiao,
            ISNULL(giao.SoPhieu, 0) AS SoPhieuGiao
        FROM STOCKTP s
        INNER JOIN (
            SELECT 
                LTRIM(RTRIM(LEFT(part.value, CHARINDEX('-', part.value + '-') - 1))) AS LOT,
                g.MAHANG,
                SUM(TRY_CAST(SUBSTRING(part.value, CHARINDEX('-', part.value + '-') + 1, 50) AS INT)) AS TongSl,
                COUNT(DISTINCT g.STT) AS SoPhieu
            FROM LUUPHIEUGIAOHANG g
            CROSS APPLY STRING_SPLIT(g.LOT, ',') part
            WHERE g.MAHANG = @maHang
              AND CAST(g.NGAYGIAO AS DATE) BETWEEN @tuNgay AND @denNgay
              AND part.value <> ''
            GROUP BY LTRIM(RTRIM(LEFT(part.value, CHARINDEX('-', part.value + '-') - 1))), g.MAHANG
        ) giao ON {joinMatch} AND s.PART = giao.MAHANG
        WHERE s.PART = @maHang
        ORDER BY s.LOT DESC;";

            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb, sql,
                    new SqlParameter("@maHang", maHang),
                    new SqlParameter("@tuNgay", tuNgay.Date),
                    new SqlParameter("@denNgay", denNgay.Date));
            if (dt == null) return result;
            foreach (DataRow r in dt.Rows)
            {
                result.Add(new LotUngVienInfo
                {
                    Lot = r["LOT"]?.ToString(),
                    MaHang = r["MAHANG"]?.ToString(),
                    TenHang = r["TENHANG"]?.ToString(),
                    SoLuongSanXuat = r["SoLuongSanXuat"] == DBNull.Value ? 0 : Convert.ToInt32(r["SoLuongSanXuat"]),
                    SoLuongNhap = r["SoLuongNhap"] == DBNull.Value ? 0 : Convert.ToInt32(r["SoLuongNhap"]),
                    SoLuongConLai = r["SoLuongConLai"] == DBNull.Value ? 0 : Convert.ToInt32(r["SoLuongConLai"]),
                    NgayNhap = r["NgayNhap"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NgayNhap"]),
                    TongSlDaGiaoTheoLot = r["TongSlDaGiao"] == DBNull.Value ? 0 : Convert.ToInt32(r["TongSlDaGiao"]),
                    SoPhieuGiao = r["SoPhieuGiao"] == DBNull.Value ? 0 : Convert.ToInt32(r["SoPhieuGiao"])
                });
            }
            return result;
        }
        public List<ChoGiaoItem> GetChoGiaoTheoLot(string lotGoc)
        {
            var result = new List<ChoGiaoItem>();
            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb,
                "SELECT * FROM TMPCHOGIAO WHERE LotGoc=@lot AND TrangThai='CHO_GIAO'",
                 new SqlParameter("@lot", lotGoc) );
            if (dt == null) return result;
            foreach (DataRow r in dt.Rows)
                result.Add(new ChoGiaoItem
                {
                    Id = Convert.ToInt32(r["Id"]),
                    LotThung = r["LotThung"].ToString(),
                    LotGoc = r["LotGoc"].ToString(),
                    MaHang = r["MaHang"].ToString(),
                    SoLuong = Convert.ToInt32(r["SoLuong"]),
                    SlotIdNguon = r["SlotIdNguon"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SlotIdNguon"]),
                    TrangThai = r["TrangThai"].ToString()
                });
            return result;
        }
        public List<SlotChuaLotInfo> GetSlotsChuaLot(string lot)
        {
            var result = new List<SlotChuaLotInfo>();
            if (string.IsNullOrWhiteSpace(lot)) return result;

            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdbb, @"
                SELECT sl.SlotId, sl.Quantity, sl.TemCode, sl.ImportDate,
                       s.SlotNumber, r.RackName, w.Name AS WarehouseName
                FROM SlotLot sl
                JOIN Slot s      ON s.SlotId      = sl.SlotId
                JOIN Rack r      ON r.RackId      = s.RackId
                JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
                WHERE sl.LotNo = @lot
                ORDER BY sl.Quantity DESC",
                 new SqlParameter("@lot", lot) );
            if (dt == null) return result;
            foreach (DataRow r in dt.Rows)
                result.Add(new SlotChuaLotInfo
                {
                    SlotId = Convert.ToInt32(r["SlotId"]),
                    Quantity = Convert.ToInt32(r["Quantity"]),
                    TemCode = r["TemCode"]?.ToString(),
                    ImportDate = r["ImportDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ImportDate"]),
                    WarehouseName = r["WarehouseName"]?.ToString(),
                    RackName = r["RackName"]?.ToString(),
                    SlotNumber = Convert.ToInt32(r["SlotNumber"])
                });
            return result;
        }
    }
}
