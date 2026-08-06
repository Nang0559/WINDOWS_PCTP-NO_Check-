using PCTP.ClassSQL;
using PCTP.FuctionMain;
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
    public class PhieuNhapRepository : IPhieuNhapRepository
    {
        private readonly SQLPROVIDER _sql;
        public PhieuNhapRepository(SQLPROVIDER sql) => _sql = sql;

        public DataTable GetPhieuNhap()
            => _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                "SELECT * FROM vNhapTP ORDER BY NGAY_SAN_XUAT DESC");

        public bool KiemTraTrungCase(string caseNo)
        {
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT COUNT(*) FROM NHAP_TP_HIS " +
                $"WHERE LOTCASE = '{SqlHelper.Esc(caseNo)}'");
            return raw != "0";
        }

        public void LuuLichSuNhap(NhapKhoItem item)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, @"
            INSERT INTO NHAP_TP_HIS
                (LOTCASE, LOT, MAHANG, SOLUONG, NGAYNHAP, LOAINHAPH)
            VALUES (@CASE, @LOT, @MH, @SL, GETDATE(), @LOAI)",
                new SqlParameter("@CASE", item.CaseNo),
                new SqlParameter("@LOT", item.Lot),
                new SqlParameter("@MH", item.Part),
                new SqlParameter("@SL", item.SlNhap),
                new SqlParameter("@LOAI", item.LoaiNhap));
        }

        
    }
}
