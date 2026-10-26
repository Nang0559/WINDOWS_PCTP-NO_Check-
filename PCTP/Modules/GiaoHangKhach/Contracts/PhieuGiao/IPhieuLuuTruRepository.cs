using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    /// <summary>Phiếu đã lưu (lịch sử) — LUUPHIEUGIAOHANG.</summary>
    public interface IPhieuLuuTruRepository
    {
        DataTable LoadLuuPhieuCaNgay(string nhaMay, string ngayGiao);
        DataTable LoadLuuPhieu(string nhaMay, string ngayGiao, string gioGiaoFcc);
        int LuuPhieuSP(string nhaMay, string ngayGiao, string gioGiaoFcc, string loaiPhieu);
        void CapNhapTTPHIEU(string nhaMay, string ngayGiao, string gioGiaoFcc, int stt, string ghiChu);
        Dictionary<string, int> LoadTonKhoBatch(List<string> maHangList);
    }
}
