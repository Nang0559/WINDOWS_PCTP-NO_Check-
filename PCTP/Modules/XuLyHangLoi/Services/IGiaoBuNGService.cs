using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    public interface IGiaoBuNGService
    {
        List<HangChoGiao> GetHangSanSangGiaoBu(int phieuKhachTraId);
        ScanResult GiaoBuTheoQR(int phieuKhachTraId, string rawQr, string nguoiGiao);
        ScanResult XacNhanHoanTatGiaoBu(int phieuKhachTraId, string nguoiGiao);
    }
}
