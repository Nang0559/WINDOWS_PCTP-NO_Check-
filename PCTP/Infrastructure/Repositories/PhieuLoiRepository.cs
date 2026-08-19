using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Domain.Entities;
using PCTP.Models;
using PCTP.VIEWSTOCK.RpIn;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Repository
{
    // PCTP/VIEWSTOCK/Repository/PhieuLoiRepository.cs
    public class PhieuLoiRepository : IPhieuLoiRepository
    {
        private readonly SQLPROVIDER _sql;
        public PhieuLoiRepository(SQLPROVIDER sql) => _sql = sql;

        public int InsertPhieuLoiKhachTra(PhieuLoiKhachTra h)
        {
            int headerId = Convert.ToInt32(_sql.ExecuteScalar(_sql.B7R2_FCCdbb, @"
            INSERT INTO PhieuLoiKhachTra
                (Nguon, SoPhieuKhach, NgayPhatHanh, SlipNo, Ca, NguoiTao, NgayTao)
            OUTPUT INSERTED.Id
            VALUES (@Nguon,@SoPhieuKhach,@NgayPhatHanh,@SlipNo,@Ca,@NguoiTao,GETDATE())",
                new[] {
                new SqlParameter("@Nguon", (int)h.Nguon),
                new SqlParameter("@SoPhieuKhach", (object)h.SoPhieuKhach ?? ""),
                new SqlParameter("@NgayPhatHanh", h.NgayPhatHanh),
                new SqlParameter("@SlipNo", (object)h.SlipNo ?? ""),
                new SqlParameter("@Ca", (object)h.Ca ?? ""),
                new SqlParameter("@NguoiTao", (object)h.NguoiTao ?? "")
                }));

            int stt = 1;
            foreach (var ct in h.ChiTiet)
            {
                _sql.ExecuteNonQuery(_sql.B7R2_FCCdbb, @"
                INSERT INTO PhieuLoiKhachTraCT
                    (PhieuLoiKhachTraId, Stt, Model, MaHang, TenHang, SoLo,
                     SoLuong, NoiDungLoi, CoPhieuLoi, GhiChu)
                VALUES (@Hid,@Stt,@Model,@MaHang,@TenHang,@SoLo,@SoLuong,@ND,@Co,@Ghi)",
                    new[] {
                    new SqlParameter("@Hid", headerId),
                    new SqlParameter("@Stt", stt++),
                    new SqlParameter("@Model", (object)ct.Model ?? ""),
                    new SqlParameter("@MaHang", (object)ct.MaHang ?? ""),
                    new SqlParameter("@TenHang", (object)ct.TenHang ?? ""),
                    new SqlParameter("@SoLo", (object)ct.SoLo ?? ""),
                    new SqlParameter("@SoLuong", ct.SoLuong),
                    new SqlParameter("@ND", (object)ct.NoiDungLoi ?? ""),
                    new SqlParameter("@Co", ct.CoPhieuLoi),
                    new SqlParameter("@Ghi", (object)ct.GhiChu ?? "")
                    });
            }
            return headerId;
        }

        public int InsertPhieuXuLyBatThuong(PhieuXuLyBatThuong p)
        {
            string soPhieu = $"BT-{DateTime.Now:yyMMdd}-" +
                _sql.ExecuteReader(_sql.B7R2_FCCdbb,
                    "SELECT RIGHT('0000' + CAST(ISNULL(MAX(CAST(RIGHT(SoPhieu,4) AS INT)),0)+1 AS VARCHAR),4) " +
                    $"FROM PhieuXuLyBatThuong WHERE SoPhieu LIKE 'BT-{DateTime.Now:yyMMdd}-%'");

            int id = Convert.ToInt32(_sql.ExecuteScalar(_sql.B7R2_FCCdbb, @"
            INSERT INTO PhieuXuLyBatThuong
                (SoPhieu, PhieuLoiKhachTraCTId, Model, MaSanPham, SoLo, SoLoLoi,
                 SoLuongLoi, PhanLoaiXuLy, NoiDungBatThuong, CapDoQuanTrong,
                 CapDoPhienBan, NguoiThucHien, BoPhanPhatHanh, TrangThai, NgayTao)
            OUTPUT INSERTED.Id
            VALUES (@SoPhieu,@CTId,@Model,@MaSP,@SoLo,@SoLoLoi,@SL,@PhanLoai,
                    @ND,@CapDo,@CapDoPB,@NguoiTH,@BoPhan,0,GETDATE())",
                new[] {
                new SqlParameter("@SoPhieu", soPhieu),
                new SqlParameter("@CTId", p.PhieuLoiKhachTraCTId),
                new SqlParameter("@Model", (object)p.Model ?? ""),
                new SqlParameter("@MaSP", (object)p.MaSanPham ?? ""),
                new SqlParameter("@SoLo", (object)p.SoLo ?? ""),
                new SqlParameter("@SoLoLoi", (object)p.SoLoLoi ?? ""),
                new SqlParameter("@SL", p.SoLuongLoi),
                new SqlParameter("@PhanLoai", (object)p.PhanLoaiXuLy ?? ""),
                new SqlParameter("@ND", (object)p.NoiDungBatThuong ?? ""),
                new SqlParameter("@CapDo", (object)p.CapDoQuanTrong ?? ""),
                new SqlParameter("@CapDoPB", (object)p.CapDoPhienBan ?? ""),
                new SqlParameter("@NguoiTH", (object)p.NguoiThucHien ?? ""),
                new SqlParameter("@BoPhan", (object)p.BoPhanPhatHanh ?? "")
                }));

            // Gắn ngược lại CT — 1 dòng CT chỉ được sinh 1 phiếu bất thường
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdbb,
                "UPDATE PhieuLoiKhachTraCT SET PhieuXuLyBatThuongId = @Id WHERE Id = @CTId",
                new SqlParameter("@Id", id), new SqlParameter("@CTId", p.PhieuLoiKhachTraCTId));

            return id;
        }

        public void CapNhatQCDuyet(QCDuyetInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (string.IsNullOrWhiteSpace(input.XacNhanCuoiKetQua))
                throw new ArgumentException("Thiếu kết luận xác nhận lần cuối (OK/NG).");

            // TrangThai chuyển sang QC_DA_DUYET bất kể kết luận OK hay NG —
            // vì đây là mốc "QC đã duyệt", không phải "hàng đạt chất lượng".
            // FormTraHangNGNew sẽ tự lọc theo XacNhanCuoiKetQua khi cho phép trả về SX.
            const string sql = @"
        UPDATE PhieuXuLyBatThuong SET
            PhuongPhapKiemTra          = @PhuongPhapKiemTra,
            KetQuaKiemTra               = @KetQuaKiemTra,
            SoLuongKiemTra              = @SoLuongKiemTra,

            PhuongPhapSua               = @PhuongPhapSua,
            KetQuaSua                   = @KetQuaSua,
            SoLuongSua                  = @SoLuongSua,

            XacNhanCuoiKetQua           = @XacNhanCuoiKetQua,
            NguoiDanhGia                = @NguoiDanhGia,
            NguoiThucHienQC             = @NguoiThucHienQC,
            GhiChuQC                    = @GhiChuQC,

            NgayBoPhanPhatSinh          = @NgayBoPhanPhatSinh,
            HoTenBoPhanPhatSinh         = @HoTenBoPhanPhatSinh,
            NgayQCTiepNhan              = @NgayQCTiepNhan,
            HoTenQCTiepNhan             = @HoTenQCTiepNhan,
            NgayBoPhanPhatHanhXacNhan   = @NgayBoPhanPhatHanhXacNhan,
            HoTenBoPhanPhatHanhXacNhan  = @HoTenBoPhanPhatHanhXacNhan,
            NgayQCDuyet                 = @NgayQCDuyet,
            HoTenQCDuyet                = @HoTenQCDuyet,

            TrangThai                   = @TrangThai
        WHERE Id = @Id AND TrangThai = 5 ";

            _sql.LoadData1(_sql.B7R2_FCCdbb, sql,
                new SqlParameter("@Id", input.Id),
                new SqlParameter("@TrangThai", 1),
                new SqlParameter("@PhuongPhapKiemTra", (object)input.PhuongPhapKiemTra ?? DBNull.Value),
                new SqlParameter("@KetQuaKiemTra", (object)input.KetQuaKiemTra ?? DBNull.Value),
                new SqlParameter("@SoLuongKiemTra", (object)input.SoLuongKiemTra ?? DBNull.Value),
                new SqlParameter("@PhuongPhapSua", (object)input.PhuongPhapSua ?? DBNull.Value),
                new SqlParameter("@KetQuaSua", (object)input.KetQuaSua ?? DBNull.Value),
                new SqlParameter("@SoLuongSua", (object)input.SoLuongSua ?? DBNull.Value),
                new SqlParameter("@XacNhanCuoiKetQua", input.XacNhanCuoiKetQua),
                new SqlParameter("@NguoiDanhGia", (object)input.NguoiDanhGia ?? DBNull.Value),
                new SqlParameter("@NguoiThucHienQC", (object)input.NguoiThucHienQC ?? DBNull.Value),
                new SqlParameter("@GhiChuQC", (object)input.GhiChuQC ?? DBNull.Value),
                new SqlParameter("@NgayBoPhanPhatSinh", (object)input.NgayBoPhanPhatSinh ?? DBNull.Value),
                new SqlParameter("@HoTenBoPhanPhatSinh", (object)input.HoTenBoPhanPhatSinh ?? DBNull.Value),
                new SqlParameter("@NgayQCTiepNhan", (object)input.NgayQCTiepNhan ?? DBNull.Value),
                new SqlParameter("@HoTenQCTiepNhan", (object)input.HoTenQCTiepNhan ?? DBNull.Value),
                new SqlParameter("@NgayBoPhanPhatHanhXacNhan", (object)input.NgayBoPhanPhatHanhXacNhan ?? DBNull.Value),
                new SqlParameter("@HoTenBoPhanPhatHanhXacNhan", (object)input.HoTenBoPhanPhatHanhXacNhan ?? DBNull.Value),
                new SqlParameter("@NgayQCDuyet", (object)input.NgayQCDuyet ?? DBNull.Value),
                new SqlParameter("@HoTenQCDuyet", (object)input.HoTenQCDuyet ?? DBNull.Value));
        }

        public void DanhDauDaTraVeSX(int id, int slotId, string lot)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdbb, @"
            UPDATE PhieuXuLyBatThuong
            SET TrangThai = 2, SlotIdDaTra = @SlotId, LotDaTra = @Lot, NgayTraVeSX = GETDATE()
            WHERE Id = @Id",
                new[] { new SqlParameter("@Id", id), new SqlParameter("@SlotId", slotId),
                     new SqlParameter("@Lot", lot ?? "") });
        }
        public PhieuXuLyBatThuong GetPhieuXuLyBatThuongTheoLot(string lot)
        {
            // Lấy tập ứng viên theo MaSanPham (nếu bạn có thể suy ra từ STOCKTP.Part)
            // rồi lọc chính xác bằng AreLotKeysEquivalent ở tầng C# — vì so khớp LOT
            // cũ/mới không thể biểu diễn gọn bằng 1 điều kiện SQL đơn giản mà không
            // dùng LotCodeHelper.BuildLotMatchSql.
            string sql = $@"
        SELECT TOP 50 * FROM PhieuXuLyBatThuong
        WHERE {LotCodeHelper.BuildLotMatchSql("SoLoLoi", "@lot")}
           OR {LotCodeHelper.BuildLotMatchSql("SoLo", "@lot")}
        ORDER BY NgayTao DESC";

            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdbb, sql,
                new SqlParameter("@lot", lot));

            if (dt == null || dt.Rows.Count == 0) return null;

            // Ưu tiên bản ghi mới nhất còn hiệu lực (chưa Huỷ)
            var row = dt.Rows.Cast<DataRow>()
                .FirstOrDefault(r => SafeInt(r["TrangThai"]) != (int)TrangThaiXuLyBatThuong.Huy)
                ?? dt.Rows[0];

            return MapPhieuXuLyBatThuong(row);
        }

        public void CapNhatDaTraVeSX(SqlConnection conn, SqlTransaction tran,
            int phieuId, int slotId, string lot)
        {
            _sql.ExecuteNonQuery(conn, tran, @"
        UPDATE PhieuXuLyBatThuong SET
            TrangThai   = @TrangThai,
            SlotIdDaTra = @SlotId,
            LotDaTra    = @Lot,
            NgayTraVeSX = GETDATE()
        WHERE Id = @Id",
                new SqlParameter("@TrangThai", (int)TrangThaiXuLyBatThuong.DaTraVeSX),
                new SqlParameter("@SlotId", slotId),
                new SqlParameter("@Lot", lot),
                new SqlParameter("@Id", phieuId));
        }
        public PhieuXuLyBatThuong GetPhieuXuLyBatThuong(int id)
        {
            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdbb,
                "SELECT * FROM PhieuXuLyBatThuong WHERE Id = @Id",
                 new SqlParameter("@Id", id) );

            if (dt.Rows.Count == 0) return null;
            return MapPhieuXuLyBatThuong(dt.Rows[0]);
        }
        public List<PhieuXuLyBatThuong> GetDanhSachChoQC()
        {
            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdbb,
                "SELECT * FROM PhieuXuLyBatThuong WHERE TrangThai = 0 ORDER BY NgayTao");
            return dt.Rows.Cast<DataRow>().Select(MapPhieuXuLyBatThuong).ToList();
        }
        public List<PhieuXuLyBatThuong> GetDanhSachDaDuyetChuaTra()
        {
            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdbb,
                "SELECT * FROM PhieuXuLyBatThuong WHERE TrangThai = 1 ORDER BY NgayQCDuyet");
            return dt.Rows.Cast<DataRow>().Select(MapPhieuXuLyBatThuong).ToList();
        }
        private static PhieuXuLyBatThuong MapPhieuXuLyBatThuong(DataRow r) => new PhieuXuLyBatThuong
        {
            Id = Convert.ToInt32(r["Id"]),
            SoPhieu = r["SoPhieu"]?.ToString(),
            PhieuLoiKhachTraCTId = r["PhieuLoiKhachTraCTId"] == DBNull.Value ? 0 : Convert.ToInt32(r["PhieuLoiKhachTraCTId"]),
            Model = r["Model"]?.ToString(),
            MaSanPham = r["MaSanPham"]?.ToString(),
            SoLo = r["SoLo"]?.ToString(),
            SoLoLoi = r["SoLoLoi"]?.ToString(),
            SoLuongLoi = r["SoLuongLoi"] == DBNull.Value ? 0 : Convert.ToInt32(r["SoLuongLoi"]),
            PhanLoaiXuLy = r["PhanLoaiXuLy"]?.ToString(),
            NoiDungBatThuong = r["NoiDungBatThuong"]?.ToString(),
            CapDoQuanTrong = r["CapDoQuanTrong"]?.ToString(),
            CapDoPhienBan = r["CapDoPhienBan"]?.ToString(),
            NguoiThucHien = r["NguoiThucHien"]?.ToString(),
            BoPhanPhatHanh = r["BoPhanPhatHanh"]?.ToString(),
            TrangThai = r["TrangThai"] == DBNull.Value ? TrangThaiXuLyBatThuong.ChoQC : (TrangThaiXuLyBatThuong)Convert.ToInt32(r["TrangThai"]),
            KetQuaXuLy = r["KetQuaXuLy"]?.ToString(),
            PhuongPhapXuLy = r["PhuongPhapXuLy"]?.ToString(),
            PhuongPhapSua = r["PhuongPhapSua"]?.ToString(),
            QCTiepNhan = r["QCTiepNhan"]?.ToString(),
            NguoiQCDuyet = r["NguoiQCDuyet"]?.ToString(),
            NgayQCDuyet = r["NgayQCDuyet"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NgayQCDuyet"]),
            NgayTao = r["NgayTao"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["NgayTao"]),
            SlotIdDaTra = r["SlotIdDaTra"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SlotIdDaTra"]),
            LotDaTra = r["LotDaTra"]?.ToString(),
            NgayTraVeSX = r["NgayTraVeSX"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NgayTraVeSX"]),

            // ── Bổ sung các trường mới cho luồng nội bộ & định hướng QC ──
            Nguon = r["Nguon"] == DBNull.Value ? NguonPhieuBatThuong.KhachTra : (NguonPhieuBatThuong)Convert.ToInt32(r["Nguon"]),
            SlotIdNguon = r["SlotIdNguon"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SlotIdNguon"]),
            LotNguon = r["LotNguon"]?.ToString(),
            LoaiLoi = r["LoaiLoi"]?.ToString(),
            PhuongPhapDinhHuong = r["PhuongPhapDinhHuong"]?.ToString(),
            NguoiQCDinhHuong = r["NguoiQCDinhHuong"]?.ToString(),
            NgayQCDinhHuong = r["NgayQCDinhHuong"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NgayQCDinhHuong"]),
            NgaySXBaoXong = r["NgaySXBaoXong"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NgaySXBaoXong"]),
            NguoiSXBaoXong = r["NguoiSXBaoXong"]?.ToString(),
            GhiChuSanXuat = r["GhiChuSanXuat"]?.ToString()
        };
        public PhieuLoiKhachTra GetPhieuLoiKhachTra(int id) { throw new NotImplementedException(); }
        public List<PhieuLoiKhachTra> GetDanhSachChuaXuLy() { throw new NotImplementedException(); }
        // PhieuLoiRepository.cs — bổ sung implementation
        public int DemChuaNhapLieu()
        {
            // Không có bảng nào lưu "chứng từ khách gửi nhưng chưa nhập" vì bản chất
            // hành động nhập liệu (InsertPhieuLoiKhachTra) TẠO RA record — nên đây là
            // đếm số phiếu ĐÃ nhập nhưng còn dòng CT chưa gán PhieuXuLyBatThuongId,
            // tức "còn việc phải làm ở bước 1→2".
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdbb,
                "SELECT COUNT(DISTINCT h.Id) FROM PhieuLoiKhachTra h " +
                "JOIN PhieuLoiKhachTraCT ct ON ct.PhieuLoiKhachTraId = h.Id " +
                "WHERE ct.PhieuXuLyBatThuongId IS NULL");
            return int.TryParse(raw, out int v) ? v : 0;
        }

        public int DemChoBanHanhPhieuBatThuong()
        {
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdbb,
                "SELECT COUNT(*) FROM PhieuLoiKhachTraCT WHERE PhieuXuLyBatThuongId IS NULL");
            return int.TryParse(raw, out int v) ? v : 0;
        }

        public int DemChoQC()
        {
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdbb,
                "SELECT COUNT(*) FROM PhieuXuLyBatThuong WHERE TrangThai = 0");
            return int.TryParse(raw, out int v) ? v : 0;
        }

        public int DemSanSangTra()
        {
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdbb,
                "SELECT COUNT(*) FROM PhieuXuLyBatThuong WHERE TrangThai = 1");
            return int.TryParse(raw, out int v) ? v : 0;
        }

        public DataTable GetGridBuoc1_ChungTuMoi()
        {
            return _sql.LoadData1(_sql.B7R2_FCCdbb, @"
        SELECT h.Id AS HeaderId, h.Nguon, h.SoPhieuKhach, h.NgayPhatHanh, h.SlipNo,
               ct.Id AS CTId, ct.Stt, ct.Model, ct.MaHang, ct.TenHang, ct.SoLo,
               ct.SoLuong, ct.NoiDungLoi,
               N'CHUA_NHAP' AS TrangThaiHienThi
        FROM PhieuLoiKhachTra h
        JOIN PhieuLoiKhachTraCT ct ON ct.PhieuLoiKhachTraId = h.Id
        WHERE ct.PhieuXuLyBatThuongId IS NULL
        ORDER BY h.NgayPhatHanh DESC, ct.Stt");
        }

        public DataTable GetGridBuoc2_ChoSinhPhieuBatThuong()
        {
            // Cùng dữ liệu bước 1 — khác ý nghĩa hiển thị: đây là danh sách CHỌN để gộp
            // nhóm và bấm nút "Sinh phiếu xử lý bất thường"
            return GetGridBuoc1_ChungTuMoi();
        }

        public DataTable GetGridBuoc3_ChoQC()
        {
            return _sql.LoadData1(_sql.B7R2_FCCdbb, @"
        SELECT Id, SoPhieu, Model, MaSanPham, SoLo, SoLoLoi, SoLuongLoi,
               PhanLoaiXuLy, NoiDungBatThuong, CapDoQuanTrong,
               NguoiThucHien, BoPhanPhatHanh, NgayTao,
               N'CHO_QC' AS TrangThaiHienThi
        FROM PhieuXuLyBatThuong
        WHERE TrangThai = 0
        ORDER BY NgayTao");
        }

        public DataTable GetGridBuoc4_SanSangTra()
        {
            return _sql.LoadData1(_sql.B7R2_FCCdbb, @"
        SELECT Id, SoPhieu, Model, MaSanPham, SoLo, SoLoLoi, SoLuongLoi,
               PhuongPhapXuLy, PhuongPhapSua, KetQuaXuLy,
               NguoiQCDuyet, NgayQCDuyet,
               N'QC_DA_DUYET' AS TrangThaiHienThi
        FROM PhieuXuLyBatThuong
        WHERE TrangThai = 1
        ORDER BY NgayQCDuyet");
        }

        public void CapNhatQCDinhHuong(int id, string loaiLoi, string phuongPhapDinhHuong, string nguoiQC)
        {
            string query = @"
        UPDATE PhieuXuLyBatThuong SET
            LoaiLoi = @LoaiLoi,
            PhuongPhapDinhHuong = @PP,
            NguoiQCDinhHuong = @NguoiQC,
            NgayQCDinhHuong = GETDATE(),
            TrangThai = @TrangThaiMoi
        WHERE Id = @Id AND TrangThai = @TrangThaiCu";

            var parameters = new[]
            {
                new SqlParameter("@LoaiLoi", (object)loaiLoi ?? DBNull.Value),
                new SqlParameter("@PP", (object)phuongPhapDinhHuong ?? DBNull.Value),
                new SqlParameter("@NguoiQC", (object)nguoiQC ?? DBNull.Value),
                new SqlParameter("@TrangThaiMoi", (int)TrangThaiXuLyBatThuong.QCDaDinhHuong),
                new SqlParameter("@TrangThaiCu", (int)TrangThaiXuLyBatThuong.ChoQC),
                new SqlParameter("@Id", id)
            };

            // Gọi đúng hàm ExecuteNonQuery (nhận params SqlParameter[]) thay vì ExecuteQuery
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdbb, query, parameters);
        }

        public void DanhDauSanXuatBaoXong(int id, string ghiChu, string nguoiThucHien)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdbb, @"
        UPDATE PhieuXuLyBatThuong SET
            GhiChuSanXuat = @Ghi,
            NguoiSXBaoXong = @NguoiTH,
            NgaySXBaoXong = GETDATE(),
            TrangThai = @TrangThaiMoi
        WHERE Id = @Id AND TrangThai = @TrangThaiCu",
                new SqlParameter("@Ghi", (object)ghiChu ?? DBNull.Value),
                new SqlParameter("@NguoiTH", (object)nguoiThucHien ?? DBNull.Value),
                new SqlParameter("@TrangThaiMoi", (int)TrangThaiXuLyBatThuong.ChoQCXacNhanCuoi),
                new SqlParameter("@TrangThaiCu", (int)TrangThaiXuLyBatThuong.QCDaDinhHuong),
                new SqlParameter("@Id", id));
        }
        public int InsertPhieuXuLyBatThuongNoiBo(PhieuXuLyBatThuong p)
        {
            // Tạo số phiếu tương tự như InsertPhieuXuLyBatThuong
            string soPhieu = $"BT-NB-{DateTime.Now:yyMMdd}-" +
                _sql.ExecuteReader(_sql.B7R2_FCCdbb,
                    "SELECT RIGHT('0000' + CAST(ISNULL(MAX(CAST(RIGHT(SoPhieu,4) AS INT)),0)+1 AS VARCHAR),4) " +
                    $"FROM PhieuXuLyBatThuong WHERE SoPhieu LIKE 'BT-NB-{DateTime.Now:yyMMdd}-%'");

            return Convert.ToInt32(_sql.ExecuteScalar(_sql.B7R2_FCCdbb, @"
        INSERT INTO PhieuXuLyBatThuong
            (SoPhieu, Nguon, SlotIdNguon, LotNguon, Model, MaSanPham, SoLo, 
             TrangThai, NgayTao)
        OUTPUT INSERTED.Id
        VALUES (@SoPhieu, 2, @SlotId, @Lot, @Model, @MaSP, @SoLo, 0, GETDATE())",
                new[] {
            new SqlParameter("@SoPhieu", soPhieu),
            new SqlParameter("@SlotId", (object)p.SlotIdNguon ?? DBNull.Value),
            new SqlParameter("@Lot", (object)p.LotNguon ?? ""),
            new SqlParameter("@Model", (object)p.Model ?? ""),
            new SqlParameter("@MaSP", (object)p.MaSanPham ?? ""),
            new SqlParameter("@SoLo", (object)p.SoLo ?? "")
                }));
        }
        public List<PhieuXuLyBatThuong> GetDanhSachChoQCDinhHuong() =>
    GetDanhSachTheoTrangThai(TrangThaiXuLyBatThuong.ChoQC);

        public List<PhieuXuLyBatThuong> GetDanhSachDangSanXuat() =>
            GetDanhSachTheoTrangThai(TrangThaiXuLyBatThuong.QCDaDinhHuong);

        public List<PhieuXuLyBatThuong> GetDanhSachChoQCXacNhanCuoi() =>
            GetDanhSachTheoTrangThai(TrangThaiXuLyBatThuong.ChoQCXacNhanCuoi);

        private List<PhieuXuLyBatThuong> GetDanhSachTheoTrangThai(TrangThaiXuLyBatThuong tt)
        {
            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdbb,
                "SELECT * FROM PhieuXuLyBatThuong WHERE TrangThai = @tt ORDER BY NgayTao DESC",
                new SqlParameter("@tt", (int)tt));
            return dt.Rows.Cast<DataRow>().Select(MapPhieuXuLyBatThuong).ToList();
        }

        public int DemChoQCDinhHuong() => DemTheoTrangThai(TrangThaiXuLyBatThuong.ChoQC);
        public int DemDangSanXuat() => DemTheoTrangThai(TrangThaiXuLyBatThuong.QCDaDinhHuong);
        public int DemChoXacNhanCuoi() => DemTheoTrangThai(TrangThaiXuLyBatThuong.ChoQCXacNhanCuoi);

        private int DemTheoTrangThai(TrangThaiXuLyBatThuong tt)
        {
            // Dùng ExecuteScalar của SQLPROVIDER để lấy giá trị COUNT(*) trực tiếp
            object val = _sql.ExecuteScalar(_sql.B7R2_FCCdbb,
                "SELECT COUNT(*) FROM PhieuXuLyBatThuong WHERE TrangThai = @tt",
                new[] { new SqlParameter("@tt", (int)tt) });

            return val != null && int.TryParse(val.ToString(), out int result) ? result : 0;
        }
        public DataTable GetGridDinhHuong() => GetGridTheoTrangThai((int)TrangThaiXuLyBatThuong.ChoQC);
        public DataTable GetGridDangSanXuat() => GetGridTheoTrangThai((int)TrangThaiXuLyBatThuong.QCDaDinhHuong);
        public DataTable GetGridXacNhanCuoi() => GetGridTheoTrangThai((int)TrangThaiXuLyBatThuong.ChoQCXacNhanCuoi);

        private DataTable GetGridTheoTrangThai(int tt)
        {
            return _sql.LoadData1(_sql.B7R2_FCCdbb,
                "SELECT * FROM PhieuXuLyBatThuong WHERE TrangThai = @tt ORDER BY NgayTao DESC",
                new SqlParameter("@tt", tt));
        }
        private static int SafeInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            return int.TryParse(value.ToString(), out int result) ? result : 0;
        }
    }
}
