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
        bool KiemTraMaTrongPhieu(string maHang, string tenBan);
        DataTable GetDanhSachTrungMaSl(string maHang, int sl, PhieuTableSet tables);
        int CountTrungMaSl(string maHang, int sl, PhieuTableSet tables);
        DataTable GetDonHangChuaLot(PhieuTableSet tables);

        DataTable GetDanhSachTrungMaSl(string maHang, int sl, string tenBan, string docQRTable);
        int CountTrungMaSl(string maHang, int sl, string tenBan, string docQRTable);
        DataTable GetDonHangChuaLot(string tenBan, string docQRTable);
   
    }
}
