using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Events
{
    public class LotStatusResetEvent : DomainEvent
    {
        public string Lot { get; }
        public string Find { get; }   // khớp với PhieuNhapInfo.Find nếu có

        public LotStatusResetEvent(string lot, string find)
        {
            Lot = lot;
            Find = find;
        }
    }
}
