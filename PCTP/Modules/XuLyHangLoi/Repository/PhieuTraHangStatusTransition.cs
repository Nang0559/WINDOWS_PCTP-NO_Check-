using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    public static class PhieuTraHangStatusTransition
    {
        private static readonly Dictionary<PhieuTraHangStatus, PhieuTraHangStatus[]> KhachTraMap =
            new Dictionary<PhieuTraHangStatus, PhieuTraHangStatus[]>
            {
                [PhieuTraHangStatus.Moi] = new[] { PhieuTraHangStatus.ChoTaoPhieuBatThuong, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.ChoTaoPhieuBatThuong] = new[] { PhieuTraHangStatus.DaTaoPhieuBatThuong, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.DaTaoPhieuBatThuong] = new[] { PhieuTraHangStatus.DangXuLyQTChung, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.DangXuLyQTChung] = new[] { PhieuTraHangStatus.QCDaXacNhan, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.QCDaXacNhan] = new[] { PhieuTraHangStatus.DaNhapLaiKho, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.DaNhapLaiKho] = new[] { PhieuTraHangStatus.ChoGiaoBu, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.ChoGiaoBu] = new[] { PhieuTraHangStatus.DaGiaoBu, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.DaGiaoBu] = new[] { PhieuTraHangStatus.HoanTat, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.Loi] = new[] { PhieuTraHangStatus.DangXuLyQTChung, PhieuTraHangStatus.ChoTaoPhieuBatThuong },
                [PhieuTraHangStatus.HoanTat] = Array.Empty<PhieuTraHangStatus>()
            };

        // TraNoiBo: KHÔNG đi qua ChoGiaoBu/DaGiaoBu — nội bộ không giao bù cho khách,
        // hàng nhập lại kho xong là kết thúc quy trình.
        private static readonly Dictionary<PhieuTraHangStatus, PhieuTraHangStatus[]> TraNoiBoMap =
            new Dictionary<PhieuTraHangStatus, PhieuTraHangStatus[]>
            {
                [PhieuTraHangStatus.Moi] = new[] { PhieuTraHangStatus.ChoTaoPhieuBatThuong, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.ChoTaoPhieuBatThuong] = new[] { PhieuTraHangStatus.DaTaoPhieuBatThuong, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.DaTaoPhieuBatThuong] = new[] { PhieuTraHangStatus.DangXuLyQTChung, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.DangXuLyQTChung] = new[] { PhieuTraHangStatus.QCDaXacNhan, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.QCDaXacNhan] = new[] { PhieuTraHangStatus.DaNhapLaiKho, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.DaNhapLaiKho] = new[] { PhieuTraHangStatus.HoanTat, PhieuTraHangStatus.Loi },
                [PhieuTraHangStatus.Loi] = new[] { PhieuTraHangStatus.DangXuLyQTChung, PhieuTraHangStatus.ChoTaoPhieuBatThuong },
                [PhieuTraHangStatus.HoanTat] = Array.Empty<PhieuTraHangStatus>()
            };

        public static bool IsValidTransition(NguonXuLyBatThuong nguon, PhieuTraHangStatus from, PhieuTraHangStatus to)
        {
            var map = nguon == NguonXuLyBatThuong.KhachTra ? KhachTraMap : TraNoiBoMap;
            return map.TryGetValue(from, out var allowed) && allowed.Contains(to);
        }
    }
}
