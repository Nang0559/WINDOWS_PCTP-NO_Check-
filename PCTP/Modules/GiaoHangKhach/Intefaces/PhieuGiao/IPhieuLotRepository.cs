using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    /// <summary>LOT — lấy LOT tự động, cập nhật, lấy lại (undo) khi bắn QR.</summary>
    public interface IPhieuLotRepository
    {
        string GetLotNo(string maHang, int stt, int dem, int slGiao, PhieuTableSet tables);
        void CapNhapLotTmpPhieu(int stt, string lot, string tenBan);
        void LayLaiLotNo(int stt, PhieuTableSet tables);
        DataTable LoadGhepLot();
        DataTable GetDanhSachLotTuKho(string maHang);

        string GetLotNo(string maHang, int stt, int dem, int slGiao,
            string docQRTable = "DOCQRCODE", string tmpTable = "TMPPHIEUGIAOHANG");
        void LayLaiLotNo(int stt, string tenBan, string docQRTable);
    }

}
