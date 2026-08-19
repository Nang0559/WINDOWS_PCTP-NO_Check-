using PCTP.Shared.Enums;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public interface IMachinePermissionService
    {
        /// <summary>Role của máy đang thao tác hiện tại (so tên máy với TT=1 trong tbl_QR_MAY_DOCQR).</summary>
        MachineRole GetCurrentRole();

        /// <summary>Kiểm tra bắt buộc trước hành động ghi (bắn QR/CNK) — throw nếu không đủ quyền.</summary>
        void EnsureCanBanQR();

        /// <summary>
        /// Tên "bàn/trạm" hiển thị trên UI, tuỳ theo role hiện tại. Với máy bắn QR,
        /// trả về tên bàn theo cấu hình khách hàng (cfg)/loại SP; với máy chỉ xem,
        /// trả về tenBanView kèm nhãn "(chỉ xem)".
        /// </summary>
        string GetTenBanTheoRole(CustomerConfig cfg, bool isSP, string tenBanView);

        /// <summary>Đổi máy bắn QR — có xác nhận, ghi lại thông tin lần đổi gần nhất vào LichSu.</summary>
        void ChuyenMayBan(string tenMayMoi);
    }
}
