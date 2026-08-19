using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    public sealed class TraHangQTChungRepository : SqlRepositoryBase, ITraHangQTChungRepository
    {
        public TraHangQTChungRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        #region Xuất

        public int InsertXuat(TraHangQTChungXuat e)
        {
            object id = ExecuteScalar(
                "INSERT INTO FVN_TraHangQTChung_Xuat " +
                "(PhieuXuLyId, SlotId, LotNo, MaHang, SoLuong, TonTruoc, TonSau, ThoiGian, NguoiXuat, LyDo, Note) " +
                "OUTPUT INSERTED.Id " +
                "VALUES (@pxl, @slot, @lot, @ma, @sl, @tt, @ts, GETDATE(), @nx, @ld, @note)",
                new SqlParameter("@pxl", e.PhieuXuLyId), new SqlParameter("@slot", e.SlotId),
                new SqlParameter("@lot", (object)e.LotNo ?? DBNull.Value),
                new SqlParameter("@ma", (object)e.MaHang ?? DBNull.Value),
                new SqlParameter("@sl", e.SoLuong), new SqlParameter("@tt", e.TonTruoc),
                new SqlParameter("@ts", e.TonSau),
                new SqlParameter("@nx", (object)e.NguoiXuat ?? DBNull.Value),
                new SqlParameter("@ld", (object)e.LyDo ?? DBNull.Value),
                new SqlParameter("@note", (object)e.Note ?? DBNull.Value));
            return Convert.ToInt32(id);
        }

        public List<TraHangQTChungXuat> GetXuat(int phieuXuLyId)
        {
            DataTable dt = LoadData("SELECT * FROM FVN_TraHangQTChung_Xuat WHERE PhieuXuLyId = @id ORDER BY ThoiGian",
                new SqlParameter("@id", phieuXuLyId));
            return dt.Rows.Cast<DataRow>().Select(MapXuat).ToList();
        }

        public TraHangQTChungXuat GetXuatById(int id)
        {
            DataTable dt = LoadData("SELECT * FROM FVN_TraHangQTChung_Xuat WHERE Id = @id", new SqlParameter("@id", id));
            return dt.Rows.Count > 0 ? MapXuat(dt.Rows[0]) : null;
        }

        #endregion

        #region Giao

        public int InsertGiao(TraHangQTChungGiao e)
        {
            object id = ExecuteScalar(
                "INSERT INTO FVN_TraHangQTChung_Giao " +
                "(PhieuXuLyId, LotNo, MaHang, SoLuong, ThoiGian, NguoiGiao, NguoiNhan, BoPhanNhan, SoPhieuGiaoNhan, Note) " +
                "OUTPUT INSERTED.Id " +
                "VALUES (@pxl, @lot, @ma, @sl, GETDATE(), @ng, @nn, @bp, @spgn, @note)",
                new SqlParameter("@pxl", e.PhieuXuLyId),
                new SqlParameter("@lot", (object)e.LotNo ?? DBNull.Value),
                new SqlParameter("@ma", (object)e.MaHang ?? DBNull.Value),
                new SqlParameter("@sl", e.SoLuong),
                new SqlParameter("@ng", (object)e.NguoiGiao ?? DBNull.Value),
                new SqlParameter("@nn", (object)e.NguoiNhan ?? DBNull.Value),
                new SqlParameter("@bp", (object)e.BoPhanNhan ?? DBNull.Value),
                new SqlParameter("@spgn", (object)e.SoPhieuGiaoNhan ?? DBNull.Value),
                new SqlParameter("@note", (object)e.Note ?? DBNull.Value));
            return Convert.ToInt32(id);
        }

        public List<TraHangQTChungGiao> GetGiao(int phieuXuLyId)
        {
            DataTable dt = LoadData("SELECT * FROM FVN_TraHangQTChung_Giao WHERE PhieuXuLyId = @id ORDER BY ThoiGian",
                new SqlParameter("@id", phieuXuLyId));
            return dt.Rows.Cast<DataRow>().Select(MapGiao).ToList();
        }

        public TraHangQTChungGiao GetGiaoById(int id)
        {
            DataTable dt = LoadData("SELECT * FROM FVN_TraHangQTChung_Giao WHERE Id = @id", new SqlParameter("@id", id));
            return dt.Rows.Count > 0 ? MapGiao(dt.Rows[0]) : null;
        }

        #endregion

        #region QC

        public int InsertQC(TraHangQTChungQC e)
        {
            object id = ExecuteScalar(
                "INSERT INTO FVN_TraHangQTChung_QC " +
                "(PhieuXuLyId, SoLuongDaRework, SoLuongOK, SoLuongNG, ThoiGian, NguoiQC, KetLuan, Note) " +
                "OUTPUT INSERTED.Id " +
                "VALUES (@pxl, @drw, @ok, @ng, GETDATE(), @qc, @kl, @note)",
                new SqlParameter("@pxl", e.PhieuXuLyId), new SqlParameter("@drw", e.SoLuongDaRework),
                new SqlParameter("@ok", e.SoLuongOK), new SqlParameter("@ng", e.SoLuongNG),
                new SqlParameter("@qc", (object)e.NguoiQC ?? DBNull.Value),
                new SqlParameter("@kl", (object)e.KetLuan ?? DBNull.Value),
                new SqlParameter("@note", (object)e.Note ?? DBNull.Value));
            return Convert.ToInt32(id);
        }

        public TraHangQTChungQC GetQC(int phieuXuLyId)
        {
            DataTable dt = LoadData(
                "SELECT TOP 1 * FROM FVN_TraHangQTChung_QC WHERE PhieuXuLyId = @id ORDER BY ThoiGian DESC",
                new SqlParameter("@id", phieuXuLyId));
            return dt.Rows.Count > 0 ? MapQC(dt.Rows[0]) : null;
        }

        #endregion

        #region NhapNG

        public int InsertNhapNG(TraHangQTChungNhapNG e)
        {
            object id = ExecuteScalar(
                "INSERT INTO FVN_TraHangQTChung_NhapNG " +
                "(PhieuXuLyId, LotNo, MaHang, SoLuongNG, SlotIdNhap, ThoiGian, NguoiNhap, LyDo, Note) " +
                "OUTPUT INSERTED.Id " +
                "VALUES (@pxl, @lot, @ma, @sl, @slot, GETDATE(), @nn, @ld, @note)",
                new SqlParameter("@pxl", e.PhieuXuLyId),
                new SqlParameter("@lot", (object)e.LotNo ?? DBNull.Value),
                new SqlParameter("@ma", (object)e.MaHang ?? DBNull.Value),
                new SqlParameter("@sl", e.SoLuongNG),
                new SqlParameter("@slot", (object)e.SlotIdNhap ?? DBNull.Value),
                new SqlParameter("@nn", (object)e.NguoiNhap ?? DBNull.Value),
                new SqlParameter("@ld", (object)e.LyDo ?? DBNull.Value),
                new SqlParameter("@note", (object)e.Note ?? DBNull.Value));
            return Convert.ToInt32(id);
        }

        public List<TraHangQTChungNhapNG> GetNhapNG(int phieuXuLyId)
        {
            DataTable dt = LoadData("SELECT * FROM FVN_TraHangQTChung_NhapNG WHERE PhieuXuLyId = @id ORDER BY ThoiGian",
                new SqlParameter("@id", phieuXuLyId));
            return dt.Rows.Cast<DataRow>().Select(MapNhapNG).ToList();
        }

        public TraHangQTChungNhapNG GetNhapNGById(int id)
        {
            DataTable dt = LoadData("SELECT * FROM FVN_TraHangQTChung_NhapNG WHERE Id = @id", new SqlParameter("@id", id));
            return dt.Rows.Count > 0 ? MapNhapNG(dt.Rows[0]) : null;
        }

        #endregion

        #region Timeline & kiểm tra tiến độ

        public List<QTChungTimelineItem> GetTimeline(int phieuXuLyId)
        {
            // UNION ALL 4 bảng — mỗi bảng map thành 1 "Buoc" khác nhau.
            string sql = @"
            SELECT 'XUAT' AS Buoc, Id AS RefId, ThoiGian, LotNo, MaHang, SoLuong,
                   NguoiXuat AS NguoiThucHien,
                   CONCAT(N'Xuất kho rework: ', SoLuong, N' từ Slot ', SlotId) AS NoiDung, Note
            FROM FVN_TraHangQTChung_Xuat WHERE PhieuXuLyId = @id
            UNION ALL
            SELECT 'GIAO', Id, ThoiGian, LotNo, MaHang, SoLuong, NguoiGiao,
                   CONCAT(N'Giao sản xuất: ', SoLuong, N' cho ', ISNULL(BoPhanNhan,'')), Note
            FROM FVN_TraHangQTChung_Giao WHERE PhieuXuLyId = @id
            UNION ALL
            SELECT 'QC', Id, ThoiGian, NULL, NULL, SoLuongOK + SoLuongNG, NguoiQC,
                   CONCAT(N'QC xác nhận: OK=', SoLuongOK, N' NG=', SoLuongNG), Note
            FROM FVN_TraHangQTChung_QC WHERE PhieuXuLyId = @id
            UNION ALL
            SELECT 'NHAP_NG', Id, ThoiGian, LotNo, MaHang, SoLuongNG, NguoiNhap,
                   CONCAT(N'Nhập lại NG: ', SoLuongNG), Note
            FROM FVN_TraHangQTChung_NhapNG WHERE PhieuXuLyId = @id
            ORDER BY ThoiGian";
            DataTable dt = LoadData(sql, new SqlParameter("@id", phieuXuLyId));
            return dt.Rows.Cast<DataRow>().Select(r => new QTChungTimelineItem
            {
                PhieuXuLyId = phieuXuLyId,
                Buoc = r["Buoc"] as string,
                RefId = r["RefId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["RefId"]),
                ThoiGian = Convert.ToDateTime(r["ThoiGian"]),
                LotNo = r["LotNo"] as string,
                MaHang = r["MaHang"] as string,
                SoLuong = r["SoLuong"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SoLuong"]),
                NguoiThucHien = r["NguoiThucHien"] as string,
                NoiDung = r["NoiDung"] as string,
                Note = r["Note"] as string
            }).ToList();
        }

        public bool DaXuatKho(int phieuXuLyId) => Exists("FVN_TraHangQTChung_Xuat", phieuXuLyId);
        public bool DaGiaoSanXuat(int phieuXuLyId) => Exists("FVN_TraHangQTChung_Giao", phieuXuLyId);
        public bool DaQCXacNhan(int phieuXuLyId) => Exists("FVN_TraHangQTChung_QC", phieuXuLyId);
        public bool DaNhapNG(int phieuXuLyId) => Exists("FVN_TraHangQTChung_NhapNG", phieuXuLyId);

        private bool Exists(string table, int phieuXuLyId)
        {
            object kq = ExecuteScalar($"SELECT COUNT(*) FROM {table} WHERE PhieuXuLyId = @id",
                new SqlParameter("@id", phieuXuLyId));
            return Convert.ToInt32(kq ?? 0) > 0;
        }

        #endregion

        #region Tổng hợp

        public int GetTongSoLuongDaXuat(int phieuXuLyId) => SumScalar("FVN_TraHangQTChung_Xuat", "SoLuong", phieuXuLyId);
        public int GetTongSoLuongDaGiao(int phieuXuLyId) => SumScalar("FVN_TraHangQTChung_Giao", "SoLuong", phieuXuLyId);
        public int GetTongSoLuongOK(int phieuXuLyId) => SumScalar("FVN_TraHangQTChung_QC", "SoLuongOK", phieuXuLyId);
        public int GetTongSoLuongNG(int phieuXuLyId) => SumScalar("FVN_TraHangQTChung_QC", "SoLuongNG", phieuXuLyId);
        public int GetTongSoLuongDaNhapNG(int phieuXuLyId) => SumScalar("FVN_TraHangQTChung_NhapNG", "SoLuongNG", phieuXuLyId);

        private int SumScalar(string table, string column, int phieuXuLyId)
        {
            object kq = ExecuteScalar($"SELECT ISNULL(SUM({column}),0) FROM {table} WHERE PhieuXuLyId = @id",
                new SqlParameter("@id", phieuXuLyId));
            return Convert.ToInt32(kq ?? 0);
        }

        #endregion

        private static TraHangQTChungXuat MapXuat(DataRow r) => new TraHangQTChungXuat
        {
            Id = Convert.ToInt32(r["Id"]),
            PhieuXuLyId = Convert.ToInt32(r["PhieuXuLyId"]),
            SlotId = Convert.ToInt32(r["SlotId"]),
            LotNo = r["LotNo"] as string,
            MaHang = r["MaHang"] as string,
            SoLuong = Convert.ToInt32(r["SoLuong"]),
            TonTruoc = Convert.ToInt32(r["TonTruoc"]),
            TonSau = Convert.ToInt32(r["TonSau"]),
            ThoiGian = Convert.ToDateTime(r["ThoiGian"]),
            NguoiXuat = r["NguoiXuat"] as string,
            LyDo = r["LyDo"] as string,
            Note = r["Note"] as string
        };

        private static TraHangQTChungGiao MapGiao(DataRow r) => new TraHangQTChungGiao
        {
            Id = Convert.ToInt32(r["Id"]),
            PhieuXuLyId = Convert.ToInt32(r["PhieuXuLyId"]),
            LotNo = r["LotNo"] as string,
            MaHang = r["MaHang"] as string,
            SoLuong = Convert.ToInt32(r["SoLuong"]),
            ThoiGian = Convert.ToDateTime(r["ThoiGian"]),
            NguoiGiao = r["NguoiGiao"] as string,
            NguoiNhan = r["NguoiNhan"] as string,
            BoPhanNhan = r["BoPhanNhan"] as string,
            SoPhieuGiaoNhan = r["SoPhieuGiaoNhan"] as string,
            Note = r["Note"] as string
        };

        private static TraHangQTChungQC MapQC(DataRow r) => new TraHangQTChungQC
        {
            Id = Convert.ToInt32(r["Id"]),
            PhieuXuLyId = Convert.ToInt32(r["PhieuXuLyId"]),
            SoLuongDaRework = Convert.ToInt32(r["SoLuongDaRework"]),
            SoLuongOK = Convert.ToInt32(r["SoLuongOK"]),
            SoLuongNG = Convert.ToInt32(r["SoLuongNG"]),
            ThoiGian = Convert.ToDateTime(r["ThoiGian"]),
            NguoiQC = r["NguoiQC"] as string,
            KetLuan = r["KetLuan"] as string,
            Note = r["Note"] as string
        };

        private static TraHangQTChungNhapNG MapNhapNG(DataRow r) => new TraHangQTChungNhapNG
        {
            Id = Convert.ToInt32(r["Id"]),
            PhieuXuLyId = Convert.ToInt32(r["PhieuXuLyId"]),
            LotNo = r["LotNo"] as string,
            MaHang = r["MaHang"] as string,
            SoLuongNG = Convert.ToInt32(r["SoLuongNG"]),
            SlotIdNhap = r["SlotIdNhap"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SlotIdNhap"]),
            ThoiGian = Convert.ToDateTime(r["ThoiGian"]),
            NguoiNhap = r["NguoiNhap"] as string,
            LyDo = r["LyDo"] as string,
            Note = r["Note"] as string
        };
    }
}
