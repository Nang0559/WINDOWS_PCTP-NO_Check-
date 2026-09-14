using PCTP.Domain.Entities;
using System;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Business orchestration cho scenario Giao DB (Phase 7).
    /// Implementation: <see cref="PhieuGiaoDbService"/>.
    /// </summary>
    public interface IPhieuGiaoDbService
    {
        DataTable GetDanhSachMaHang();

        int TaoPhieuVaChiTiet(
            string ten,
            DateTime ngayLap,
            int nhaMay,
            string nhaMayName,
            string note,
            DataTable chiTiet);

        void LuuGiaoDB(DataTable donHang, GioXuat gioXuat, int addNm);

        DataTable LoadTmpPhieuGiaoDB(DateTime ngayGiao, int addNm);

        void XuLySauUpload();
    }
}