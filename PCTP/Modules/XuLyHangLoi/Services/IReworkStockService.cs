using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Helpers;
using System.Collections.Generic;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    /// <summary>
    /// Điều phối nghiệp vụ xuất kho đi rework / nhập lại sau rework / hoàn trả khi huỷ.
    ///
    /// Stock mutation không còn thuộc contract này: implementation phải route
    /// Slot/SlotLot/STOCKTP mutation qua KhoCore.IStockMovementService.
    /// Interface chỉ mô tả nghiệp vụ Rework; persistence details không được leak ra caller.
    /// </summary>
    public interface IReworkStockService
    {
        List<LotInfo> GetLotsCanRework(string maHang, string lotNo);
        List<LotInfo> GetLotsCanReworkByPhieuXuLy(int phieuXuLyId);

        ScanResult XuatKhoRework(
            int phieuXuLyId,
            int slotLotId,
            string lotNo,
            int soLuong,
            string nguoiXuat);

        ScanResult NhapLaiHangNG(
            int phieuXuLyId,
            string lotNo,
            int soLuongNG,
            int? slotIdOK,
            int? slotIdNG,
            string nguoiNhap);

        ScanResult NhapLaiHangOK(
            int phieuXuLyId,
            string lotNo,
            int soLuongOK,
            int slotIdOK,
            string nguoiNhap);

        ScanResult HoanTraKhoKhiHuy(
            int phieuXuLyId,
            string nguoiThucHien);
    }
}
