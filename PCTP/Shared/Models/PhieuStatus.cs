using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Models
{
    public enum PhieuStatus : byte
    {
        Active = 0,     // đang tồn tại trong kho, dùng được
        Split = 1,      // đã bị tách (1 phần xuất, 1 phần dư) -> không dùng nữa, chỉ để trace lịch sử
        ExportedOut = 2 // đã xuất hẳn ra khỏi hệ thống kho nội bộ (giao hàng / tiêu thụ)
    }
}
