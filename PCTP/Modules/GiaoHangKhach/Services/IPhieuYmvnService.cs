using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Business service cho flow YMVN/MilkRun (Phase 7).
    /// Implementation: <see cref="PhieuYmvnService"/>.
    /// </summary>
    public interface IPhieuYmvnService
    {
        void CapNhapKho(
            string ngayGiao,
            string gioXuat,
            string nhaMay,
            DataTable donHang);

        void HoanThanh(bool isLoaiSP = false);

        List<string> GetDanhSachGio(string ngayXuatMDY);

        void UploadMilkrunSP(DataTable donHang, string ngayGiao);

        void SyncPhieuTuBangRiengChoDocQR(
            DataTable donHang,
            string ngayGiao,
            List<string> checkedGios = null);
    }
}