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

        void DanhDauPhieuGiaoGocChoGiaoBu(string dinhDanhPhieuGiao, string soPhieuKhachTra, string nguoiThucHien);
       
     
    }
}
