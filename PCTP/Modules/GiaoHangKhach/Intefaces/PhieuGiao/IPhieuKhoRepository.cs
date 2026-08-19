using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    /// <summary>Cập nhật tồn kho khi CNK (Confirm Nhận Kho) — nghiệp vụ nặng nhất, giữ riêng.</summary>
    public interface IPhieuKhoRepository
    {
        DataTable LoadHangThieu(bool isMayBanQR, string tenBan);
        int CapNhapKho(string gioGiaoFcc, string nhaMay, PhieuTableSet tables, out DataTable errors);
        int CapNhapKhoHTN(string nhaMay, PhieuTableSet tables, out DataTable errors);
        int CapNhapKhoSP(string gioGiaoFcc, string nhaMay, out DataTable errors);
        bool CapNhapKhoYMVN(int stt, string lotSl, string maHang, string ngayGiao,
            string gioGiao, string nhaMay, out DS_ERR_CNK error);
        void DanhDauDaGiao(string poNo, string maHang, string ngayGiao, CustomerConfig cfg);

        int CapNhapKho(string gioGiaoFcc, string nhaMay, string tmpTable, string docQRTable, out DataTable errors);
        int CapNhapKhoHTN(string nhaMay, string tmpTable, string docQRTable, out DataTable errors);
    }

}
