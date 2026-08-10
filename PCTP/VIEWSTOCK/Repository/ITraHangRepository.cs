using PCTP.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Repository
{
    public interface ITraHangRepository
    {
        // ── Luồng 1 & 1b: trả hàng về sản xuất ──────────────────────────────
        void InsertTraHang(SqlConnection conn, SqlTransaction tran,
            string lot, int slTra, string lyDoNg, string nguon);

        // ── Luồng 1b: staging "chờ giao" ─────────────────────────────────────
        List<ChoGiaoItem> GetChoGiaoTheoDanhSach(IEnumerable<int> ids);
        List<ChoGiaoItem> GetChoGiaoDangCho();
        void CapNhatTrangThaiChoGiao(SqlConnection conn, SqlTransaction tran,
            IEnumerable<int> ids, string trangThaiMoi);
        void InsertChoGiao(int slotIdNguon, string lotThung, string lotGoc,
            string maHang, int soLuong, string phieuGiaoId);

        // ── Luồng 2: quét thùng khách trả ────────────────────────────────────
        bool ExistsThungDaQuet(int idp, string lotThung);
        void InsertThungQuetTra(int idp, string lotThung, string lotGoc,
            string maHang, int slThung);
        List<NhomLotTraInfo> GetNhomLotChuaXuLy(int idp);
        void DanhDauDaXuLy(SqlConnection conn, SqlTransaction tran, int idp);
        void DanhDauPhieuDaNhapKho(SqlConnection conn, SqlTransaction tran, int idp);

        // ── STOCKTP helpers dùng riêng cho trả hàng ─────────────────────────
        void TruSlConLai(SqlConnection conn, SqlTransaction tran, string lot, int soLuong);
        void NhapLaiHangKhachTra(SqlConnection conn, SqlTransaction tran, string lot, int soLuong);
        void InsertNhanTraTheoIDP(SqlConnection conn, SqlTransaction tran,
            string lot, int slNhanTra, int idp);

        // ITraHangRepository.cs — thêm khai báo
        List<LotInfo> GetSlotLotsInTransaction(SqlConnection conn, SqlTransaction tran, int slotId);
        void SaveSlotLotsInTransaction(SqlConnection conn, SqlTransaction tran, int slotId, List<LotInfo> lots);

        // ITraHangRepository.cs — thêm
        void CloseChoGiaoTheoLot(SqlConnection conn, SqlTransaction tran, IEnumerable<string> lotsDaXuat);
        // ITraHangRepository.cs — thêm
        List<string> LocLotDangChoCNK(IEnumerable<string> lots);
        List<string> LocLotDaCNK(IEnumerable<string> lots);

        int GetSlXuatHienTai(string lot);
    }
}
