using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    public interface IPhieuValidationRepository
    {
        int CountDocQRCode(string docQRTable);
        bool CheckCoMaNG(string tenBan);
        Dictionary<string, int> GetQcDongGoiBatch(List<string> maHangList);
        bool KiemTraMaTrongPhieu(string maHang, string tenBan);
        DataTable GetDanhSachTrungMaSl(string maHang, int sl, PhieuTableSet tables);
        int CountTrungMaSl(string maHang, int sl, PhieuTableSet tables);
        DataTable GetDonHangChuaLot(PhieuTableSet tables);
        DataTable GetDanhSachTrungMaSl(string maHang, int sl, string tenBan, string docQRTable);
        int CountTrungMaSl(string maHang, int sl, string tenBan, string docQRTable);
        DataTable GetDonHangChuaLot(string tenBan, string docQRTable);
        List<FifoViolation> CheckFifoViolations(string tenBangTmp);
        DataTable TinhHangThieuTuDonHang(DataTable donHang);
        DataTable SoSanhLechIFS(DataTable donHangBangRieng, DataTable ifsData);
    }
}
