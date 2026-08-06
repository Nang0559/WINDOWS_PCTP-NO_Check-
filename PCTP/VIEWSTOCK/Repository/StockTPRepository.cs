using PCTP.ClassSQL;
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
    public class StockTpRepository : IStockTpRepository
    {
        private readonly SQLPROVIDER _sqlProvider;

        public StockTpRepository(SQLPROVIDER sqlProvider) => _sqlProvider = sqlProvider;

        

        // ══════════════ PHIẾU SẢN XUẤT (vNhapTP) ══════════════
        public PhieuNhapInfo GetPhieuByFind(string find)
        {
            const string sql = @"SELECT STT, FIND, LOT_NO, MODEL, TEN_SAN_PHAM, MA_SAN_PHAM,
                                     CA_SAN_XUAT, NGAY_SAN_XUAT, SL_DA_SAN_XUAT,
                                     SL_DA_NHAP, SL_DA_TRA, LY_DO_TRA, TON_KHO_TP, KET_THUC_LOT
                              FROM vNhapTP WHERE FIND = @find";

            using var conn = OpenConn();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@find", find);

            using var rd = cmd.ExecuteReader();
            return rd.Read() ? MapPhieu(rd) : null;
        }

        public List<PhieuNhapInfo> GetPhieuTong()
        {
            const string sql = @"SELECT STT, FIND, LOT_NO, MODEL, TEN_SAN_PHAM, MA_SAN_PHAM,
                                     CA_SAN_XUAT, NGAY_SAN_XUAT, SL_DA_SAN_XUAT,
                                     SL_DA_NHAP, SL_DA_TRA, LY_DO_TRA, TON_KHO_TP, KET_THUC_LOT
                              FROM vNhapTP ORDER BY NGAY_SAN_XUAT DESC";

            var list = new List<PhieuNhapInfo>();
            using var conn = OpenConn();
            using var cmd = new SqlCommand(sql, conn);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) list.Add(MapPhieu(rd));
            return list;
        }

        private static PhieuNhapInfo MapPhieu(SqlDataReader rd) => new PhieuNhapInfo
        {
            Stt = rd["STT"] as int? ?? 0,
            Find = rd["FIND"] as string,
            LotNo = rd["LOT_NO"] as string,
            Model = rd["MODEL"] as string,
            TenSP = rd["TEN_SAN_PHAM"] as string,
            MaSP = rd["MA_SAN_PHAM"] as string,
            CaSX = rd["CA_SAN_XUAT"] as int? ?? 0,
            NgaySX = rd["NGAY_SAN_XUAT"] as DateTime? ?? DateTime.MinValue,
            SlSanXuat = rd["SL_DA_SAN_XUAT"] as int? ?? 0,
            SlDaNhap = rd["SL_DA_NHAP"] as int? ?? 0,
            SlDaTra = rd["SL_DA_TRA"] as int? ?? 0,
            LyDoTra = rd["LY_DO_TRA"] as string,
            TonKhoTP = rd["TON_KHO_TP"] as int? ?? 0,
            KetThucLot = (rd["KET_THUC_LOT"] as int? ?? 0) == 1
        };

        // ══════════════ STOCKTP ══════════════
        public bool ExistsStockTp(string lot)
        {
            const string sql = "SELECT COUNT(*) FROM STOCKTP WHERE LOT = @lot";
            using var conn = OpenConn();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lot", lot);
            return (int)cmd.ExecuteScalar() > 0;
        }

        public StockItem GetByLot(string lot)
        {
            const string sql = "SELECT * FROM STOCKTP WHERE LOT = @lot";
            using var conn = OpenConn();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lot", lot);
            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return null;

            return new StockItem
            {
                Lot = rd["LOT"] as string,
                Part = rd["Part"] as string,
                Name = rd["NAME"] as string,
                Model = rd["MODEL"] as string,
                SlNhap = rd["SLNHAP"] as int?,
                SlConLai = rd["SLCONLAI"] as int?,
                SlXuat = rd["SLXUAT"] as int?,
                Satus = rd["Satus"] as short?,
                CaSX = rd["CASX"] as short?,
                NgaySX = rd["NGAYSX"] as DateTime?,
                NgayNhap = rd["NGAYNHAP"] as DateTime?
            };
        }

        public int GetSlConLai(string lot)
        {
            const string sql = "SELECT ISNULL(SLCONLAI, 0) FROM STOCKTP WHERE LOT = @lot";
            using var conn = OpenConn();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lot", lot);
            var result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0 : (int)result;
        }

        public void InsertStockTp(NhapKhoItem item, int status)
        {
            const string sql = @"INSERT INTO STOCKTP
            (LOT, MODEL, Part, NAME, CASX, NGAYSX, SLSX, NGAYNHAP, SLNHAP, NGAYXUAT, SLXUAT, SLCONLAI, Satus)
            VALUES (@lot, @model, @part, @name, @casx, @ngaysx, @slsx, @ngaynhap, @slnhap, @ngaynhap, 0, @slnhap, @status)";

            using var conn = OpenConn();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lot", item.Lot);
            cmd.Parameters.AddWithValue("@model", (object)item.Model ?? "");
            cmd.Parameters.AddWithValue("@part", (object)item.Part ?? "");
            cmd.Parameters.AddWithValue("@name", (object)item.Name ?? "");
            cmd.Parameters.AddWithValue("@casx", item.CaSX);
            cmd.Parameters.AddWithValue("@ngaysx", (object)item.NgaySX ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@slsx", item.SlSanXuat);
            cmd.Parameters.AddWithValue("@ngaynhap", DateTime.Now);
            cmd.Parameters.AddWithValue("@slnhap", item.SlNhap);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.ExecuteNonQuery();
        }

        public void UpdateStockTp(string lot, int slSeNhap, int status)
        {
            const string sql = @"UPDATE STOCKTP SET
            SLNHAP = ISNULL(SLNHAP,0) + @sl,
            SLCONLAI = ISNULL(SLCONLAI,0) + @sl,
            NGAYNHAP = CAST(GETDATE() AS smalldatetime),
            Satus = @status
            WHERE LOT = @lot";

            using var conn = OpenConn();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@sl", slSeNhap);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@lot", lot);
            cmd.ExecuteNonQuery();
        }

        public void GanRackSlot(string lot, string rackCode, string slotCode)
        {
            const string sql = "UPDATE STOCKTP SET RackCode = @rack, SlotCode = @slot WHERE LOT = @lot";
            using var conn = OpenConn();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@rack", rackCode);
            cmd.Parameters.AddWithValue("@slot", slotCode);
            cmd.Parameters.AddWithValue("@lot", lot);
            cmd.ExecuteNonQuery();
        }

        // ══════════════ CASE DEDUP (NHAP_TP_HIS) ══════════════
        public bool ExistsCaseHistory(string caseNo)
        {
            const string sql = "SELECT COUNT(*) FROM NHAP_TP_HIS WHERE LOTCASE = @caseNo";
            using var conn = OpenConn();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@caseNo", caseNo);
            return (int)cmd.ExecuteScalar() > 0;
        }

        public void InsertCaseHistory(string caseNo)
        {
            const string sql = "INSERT INTO NHAP_TP_HIS (LOTCASE) VALUES (@caseNo)";
            using var conn = OpenConn();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@caseNo", caseNo);
            cmd.ExecuteNonQuery();
        }

        // ══════════════ NG (STOCKTPTRAHANG / STOCKTPNHANTRA) ══════════════
        public List<StockTraHangInfo> GetTraHangConLai(string lot)
        {
            const string sql = @"SELECT LOT, NGAYTRA, SLTRA, SLNHANLAI, LY_DO_NG
                              FROM STOCKTPTRAHANG WHERE STATUS = 0 AND LOT = @lot";

            var list = new List<StockTraHangInfo>();
            using var conn = OpenConn();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lot", lot);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new StockTraHangInfo
                {
                    Lot = rd["LOT"] as string,
                    NgayTra = rd["NGAYTRA"] as DateTime?,
                    SlTra = rd["SLTRA"] as int? ?? 0,
                    SlNhanLai = rd["SLNHANLAI"] as int? ?? 0,
                    LyDoNg = rd["LY_DO_NG"] as string
                });
            }
            return list;
        }

        public void InsertNhanTra(string lot, string part, string name, int slNhanLai, string lyDoNg)
        {
            const string sql = @"INSERT INTO STOCKTPNHANTRA
            (LOT, PART_NO, PART_NAME, NGAY_NHAN_TRA, SL_NHAN_TRA, LY_DO_NG)
            VALUES (@lot, @part, @name, @ngay, @sl, @lyDo)";

            using var conn = OpenConn();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@lot", lot);
            cmd.Parameters.AddWithValue("@part", part ?? "");
            cmd.Parameters.AddWithValue("@name", name ?? "");
            cmd.Parameters.AddWithValue("@ngay", DateTime.Now);
            cmd.Parameters.AddWithValue("@sl", slNhanLai);
            cmd.Parameters.AddWithValue("@lyDo", lyDoNg ?? "");
            cmd.ExecuteNonQuery();
        }

        public void UpdateTraHangSauNhanLai(string lot, string lyDoNg, int slNhanLai, int status)
        {
            const string sql = @"UPDATE STOCKTPTRAHANG SET
            SLNHANLAI = SLNHANLAI + @sl,
            SLCONLAI = SLCONLAI - @sl,
            STATUS = @status
            WHERE LOT = @lot AND LY_DO_NG = @lyDo";

            using var conn = OpenConn();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@sl", slNhanLai);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@lot", lot);
            cmd.Parameters.AddWithValue("@lyDo", lyDoNg ?? "");
            cmd.ExecuteNonQuery();
        }
    }
}
