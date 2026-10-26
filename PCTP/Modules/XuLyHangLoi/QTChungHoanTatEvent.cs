using PCTP.Domain.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi
{
    public class QTChungHoanTatEvent : DomainEvent
    {
        public int PhieuXuLyBatThuongId { get; }
        public int PhieuKhachTraId { get; }
        public string Nguon { get; }          // "KhachTra" | "TraNoiBo"
        public bool KetQuaOK { get; }         // từ QCXacNhanCuoi
    }
}
