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
    public interface IGiaoBuNGRepository
    {
        List<PhieuGiaoGocInfo> TimPhieuGocTheoLot(string lot);
        List<PhieuGiaoGocInfo> TimPhieuGocTheoMaHangNgay(string maHang, DateTime tu, DateTime den);

        // ── Tra cứu LOT rework đã nhập kho (qua luồng NhapTpReceivingService) ──
        StockItem TraCuuLotDaNhapKho(string lot);
        List<SlotChuaLotInfo> GetSlotsChuaLot(string lot); // tái dùng đúng SQL đã có ở TraHangRepository

        void InsertLuuPhieuGiaoBu(SqlConnection conn, SqlTransaction tran,
            PhieuGiaoGocInfo phieuGoc, string lotFccGop, int tongSlFcc, string nguoiThucHien);

        void InsertLuuDocQRCodeGiaoBu(SqlConnection conn, SqlTransaction tran,
            string lotFcc, string maHangFcc, int slTemFcc, string nhaMay, string phieuGocKey);

        // ── CHỈ xuất kho — trừ SLCONLAI, cộng SLXUAT. KHÔNG đụng SLSX ──
        void XuatKhoGiaoBu(SqlConnection conn, SqlTransaction tran, string lot, int soLuong);

        /// <summary>
        /// Tem TỔNG (100003) có SoPhieu định danh duy nhất — chống quét lại tem tổng
        /// đã dùng giao bù trước đó (khác với tem thùng, vốn đã bị chặn trùng ở UI
        /// theo LotFcc trong phiên hiện tại, nhưng không chặn được across-phiên).
        /// </summary>
        bool ExistsGiaoBuTem(string lotFcc, string soPhieu);
    }
}
