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

        /// <summary>
        /// Insert 1 dòng trực tiếp vào bảng TMP theo đúng schema chuẩn (STT, CUA, TRUYEN,
        /// MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GEAR, GIOGIAO, STATUS, PO_NO, TTPHIEU).
        /// Dùng cho các nguồn đơn hàng ghi từng dòng một (VD: <c>TableOrderRepo</c> khi đồng
        /// bộ đơn YMVN/HTN) thay vì đi qua <see cref="LuuVaLoad(PhieuTableSet, string, DataTable, string, string, string, int)"/>
        /// (vốn dành cho nguồn nạp nguyên khối kèm gọi SP xử lý QR) — tránh mỗi nơi tự viết lại
        /// SQL INSERT trên bảng TMP.
        /// </summary>
        void InsertTmpRow(
            string tmpTable,
            string stt, string cua, string truyen, string maHang, string tenHang,
            string lot, string dv, int slXuat, string ngayGiao, string gear,
            string gioXuat, string poNo = "", string cusPoNo = "");

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
