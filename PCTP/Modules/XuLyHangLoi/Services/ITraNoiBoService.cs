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
        int TaoPhieuTraNoiBo(string maHang, string lotNo, int soLuong, string noiDung, string nguoiTao);
    }
}
