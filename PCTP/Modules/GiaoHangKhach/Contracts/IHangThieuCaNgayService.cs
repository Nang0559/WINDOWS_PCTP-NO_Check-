using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    public interface IHangThieuCaNgayService
    {
        DataTable TinhHangThieuCaNgay(DateTime ngayGiao, string nhaMay, int addNm, CustomerConfig cfg);
    }
}
