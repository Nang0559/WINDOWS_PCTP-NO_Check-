using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    
        /// <summary>
        /// Điều phối xuất kho đi rework / nhập lại sau rework / hoàn trả khi huỷ.
        /// KHÔNG tự viết SQL đụng Slot/STOCKTP — tái dùng ISlotService (module Kho)
        /// và IStockExportRepository.AdjustSlConLai (module XuatKho, chỉ đụng SLCONLAI).
        /// Lịch sử ghi qua ITraHangQTChungRepository (đã có sẵn InsertXuat/InsertNhapNG).
        /// </summary>
        public interface IReworkStockService
        {
            List<LotInfo> GetLotsCanRework(string maHang, string lotNo);
            List<LotInfo> GetLotsCanReworkByPhieuXuLy(int phieuXuLyId);

            ScanResult XuatKhoRework(int phieuXuLyId, int slotLotId, string lotNo, int soLuong, string nguoiXuat);
            ScanResult NhapLaiHangNG(int phieuXuLyId, string lotNo, int soLuong, int? slotIdDich, string nguoiNhap);
            ScanResult HoanTraKhoKhiHuy(int phieuXuLyId, string nguoiThucHien);
        }

        
    
}
