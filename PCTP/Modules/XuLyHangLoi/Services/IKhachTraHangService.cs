using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{


    public interface IKhachTraHangService : IXuLyHangLoiService
    {
        int TiepNhanPhieuKhachTra(PhieuTraHang phieu);

        List<PhieuGiaoUngVienInfo> TimPhieuGiaoUngVien(string maHang, DateTime? ngayGiao, string lotNo);

        void GanPhieuGiaoGoc(int phieuKhachTraItemId, string dinhDanhPhieuGiao);

        /// <summary>
        /// Các lần giao của mã hàng (tuỳ chọn: LOT) của khách này trong [tuNgay, denNgay].
        /// Mỗi phiếu kèm SoLuongLot (SL của đúng LOT trong phiếu) khi có tra theo LOT.
        /// </summary>
        List<PhieuGiaoUngVienInfo> TimPhieuGiaoTheoKhoang(
            string customerNo,
            string maHang,
            string lotNo,
            DateTime tuNgay,
            DateTime denNgay);

        /// <summary>
        /// Số lượng ĐÃ GIAO để đối chiếu với SL khách trả: SL của LOT trong phiếu giao
        /// (nếu có LOT và tách được), ngược lại tổng SL dòng giao. Null nếu không tìm thấy phiếu.
        /// </summary>
        int? LaySoLuongDaGiao(string dinhDanhPhieuGiao, string lotNo);

        void DanhDauPhieuGiaoGocChoGiaoBu(string dinhDanhPhieuGiao, string soPhieuKhachTra, string nguoiThucHien);
       
     
    }
}
