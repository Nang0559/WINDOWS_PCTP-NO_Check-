using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    /// <summary>Giao DB (đơn hàng đặc biệt nhập tay/upload trước đây).</summary>
    public interface IPhieuGiaoDBRepository
    {
        DataTable GetDanhSachMaHang();
        DataTable LoadTmpPhieuGiaoDB(string tenBan);
        void LuuGiaoDB(DataTable donHang, string gioFccMoTa, int addNm,
            string tmpTable, string ifsTable, string nhaMayOverride = "");
    }
}
