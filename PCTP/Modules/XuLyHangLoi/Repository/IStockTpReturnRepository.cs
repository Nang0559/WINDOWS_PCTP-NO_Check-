using PCTP.Shared.Helpers;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    public interface IStockTpReturnRepository
    {
        // ============================================================
        // 1. TÌM HÀNG NG CÓ THỂ ĐƯA ĐI REWORK
        // ============================================================

        List<LotInfo> GetLotsCanRework(
            string maHang,
            string lotNo);

        List<LotInfo> GetLotsCanReworkByPhieuXuLy(
            int phieuXuLyId);


        // ============================================================
        // 2. KIỂM TRA TỒN
        // ============================================================

        int GetTonLot(
            string lotNo);

        int GetTonLotWithLock(
            string lotNo);


        // ============================================================
        // 3. XUẤT KHO ĐI REWORK
        //
        // Một transaction:
        //   Slot
        //   STOCKTP
        //   lịch sử xuất
        // ============================================================

        ScanResult XuatKhoRework(
            int phieuXuLyId,
            int slotId,
            string lotNo,
            int soLuong,
            string nguoiXuat);


        // ============================================================
        // 4. NHẬP LẠI HÀNG NG SAU REWORK
        //
        // Dùng chung:
        //   Khách trả
        //   Trả nội bộ
        // ============================================================

        ScanResult NhapLaiHangNG(
            int phieuXuLyId,
            string lotNo,
            int soLuong,
            int? slotIdDich,
            string nguoiNhap);


        // ============================================================
        // 5. HOÀN TRẢ KHO KHI HỦY QT CHUNG
        //
        // Nếu hàng đã xuất đi rework nhưng QTChung bị hủy.
        // ============================================================

        ScanResult HoanTraKhoKhiHuy(
            int phieuXuLyId,
            string nguoiThucHien);
    }
}

