using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Enums
{
    public static class NguonXuLyBatThuongExtensions
    {
        public static string ToProcessCode(this NguonXuLyBatThuong nguon)
        {
            switch (nguon)
            {
                case NguonXuLyBatThuong.KhachTra:
                    return "KHACH_TRA";
                case NguonXuLyBatThuong.TraNoiBo:
                    return "TRA_NOI_BO";
                default:
                    throw new ArgumentOutOfRangeException(nameof(nguon), nguon, null);
            }
        }
    }
}
