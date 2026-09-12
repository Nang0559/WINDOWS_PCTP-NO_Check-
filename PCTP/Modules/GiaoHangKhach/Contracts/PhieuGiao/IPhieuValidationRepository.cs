using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    /// <summary>Đếm/kiểm tra trùng lặp mã hàng-số lượng trong phiếu đang bắn.</summary>
    public interface IPhieuValidationRepository
    {
        int CountDocQRCode(string docQRTable);
        bool CheckCoMaNG(string tenBan);
        Dictionary<string, int> GetQcDongGoiBatch(List<string> maHangList);
        bool KiemTraMaTrongPhieu(string maHang, string tenBan);
        DataTable GetDanhSachTrungMaSl(string maHang, int sl, PhieuTableSet tables);
        int CountTrungMaSl(string maHang, int sl, PhieuTableSet tables);
        DataTable GetDonHangChuaLot(PhieuTableSet tables);

        DataTable GetDanhSachTrungMaSl(string maHang, int sl, string tenBan, string docQRTable);
        int CountTrungMaSl(string maHang, int sl, string tenBan, string docQRTable);
        DataTable GetDonHangChuaLot(string tenBan, string docQRTable);
        // IPhieuValidationRepository — thêm
        List<FifoViolation> CheckFifoViolations(string tenBangTmp);

        /// <summary>
        /// Tính hàng thiếu trực tiếp trên DataTable đơn hàng in-memory (KHÔNG qua TMP) —
        /// dùng cho luồng bảng riêng (YMVN/HTN) nơi donHang có thể vừa lấy thẳng từ
        /// Purchase_Order, chưa kịp sync vào TMP. Định nghĩa: theo từng MAHANG, tổng
        /// SOLUONG cần giao (chỉ tính dòng CHƯA có LOT/STATUS khác 'OK') > SLCONLAI
        /// hiện có trong STOCKTP.
        /// </summary>
        DataTable TinhHangThieuTuDonHang(DataTable donHang);

        //void SyncIfsSnapshot(DataTable ifsData, string ifsTable, string ngayGiao);

        DataTable SoSanhLechIFS(DataTable donHangBangRieng, DataTable ifsTable);

    }
}
