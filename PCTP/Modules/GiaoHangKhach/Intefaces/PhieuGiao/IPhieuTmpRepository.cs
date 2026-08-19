using PCTP.Domain.Entities;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    /// <summary>Vòng đời bảng TMP đang bắn QR — load/lưu/xoá/trạng thái.</summary>
    public interface IPhieuTmpRepository
    {
        DataTable LoadTuTmpTable(string tmpTable);
        DataTable GetDonHangHienTai(string tenBan);
        DataTable LoadPhieuDocQR(string ngayGiao, string nhaMay, string gioFcc, int addNm, PhieuTableSet tables);
        DataTable LuuVaLoad(PhieuTableSet tables, string tenSP, DataTable donHang,
            string ngayGiao, string nhaMay, string gioFcc, int addNm);
        void XoaTmpPhieu(string tenBan);
        void XoaDocQRCode(string docQRTable);
        TrangThaiBan GetTrangThaiDangBan(PhieuTableSet tables);
        TrangThaiBan GetTrangThaiDangBanYMVN(PhieuTableSet tables);
        void EnsureTablesExist();

        // ── Overload cũ giữ tương thích ngược (wrapper) ──────────────────
        DataTable LoadPhieuDocQR(string ngayGiao, string nhaMay, string gioFcc, int addNm,
            string tmpTable, string ifsTable, string docQRTable);
        DataTable LuuVaLoad(string tenSPBang, string tenSP, DataTable donHang,
            string ngayGiao, string nhaMay, string gioFcc, int addNm,
            string tenBan, string docQRTable, string ifsView = "");
        TrangThaiBan GetTrangThaiDangBan(string tmpTable, string docQRTable);
        TrangThaiBan GetTrangThaiDangBanYMVN(string tmpTable, string docQRTable);
    }
}
