using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Enums
{
    public enum MachineRole
    {
        ChiXem = 0,       // chỉ được xem, không được bắn QR
        DuocBanQR = 1      // được phép bắn QR (ghi vào TMPPHIEUGIAOHANG/CNK)
    }
}
