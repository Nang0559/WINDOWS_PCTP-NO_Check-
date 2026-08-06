using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Events
{
    public class TTPHIEUEventArgs : EventArgs
    {
        public int Stt { get; }
        public string GhiChu { get; }
        public TTPHIEUEventArgs(int stt, string ghiChu)
        {
            Stt = stt;
            GhiChu = ghiChu;
        }
    }
}
