using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    public interface ITraNoiBoService : IXuLyHangLoiService
    {
        /// <summary>Tạo phiếu trả nội bộ — nhận đủ header + ChiTiet nhiều dòng,
        /// cùng pattern với IKhachTraHangService.TiepNhanPhieuKhachTra.</summary>
        int TaoPhieuTraNoiBo(PhieuTraHang phieu);
        /// <summary>
        /// Giao lại hàng ĐÃ REWORK OK (đã nhập lại kho — Status = DaNhapLaiKho)
        /// cho bộ phận đã phát hiện lỗi ban đầu (PhieuTraHang.BoPhanPhatHienLoi).
        /// KHÁC "giao bù cho khách" (đó là nghiệp vụ riêng của IKhachTraHangService/IGiaoBuNGService).
        /// </summary>
        void GiaoLaiBoPhanPhatHien(int phieuTraHangId, string boPhanNhan, int soLuongGiaoLai, string nguoiThucHien);
    }
}
