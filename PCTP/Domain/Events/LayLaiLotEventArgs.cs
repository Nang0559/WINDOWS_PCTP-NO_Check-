using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Events
{
    public class LayLaiLotEventArgs : EventArgs
    {
        public int Stt { get; }
        public LayLaiLotEventArgs(int stt)
        {
            Stt = stt;
        }
    }
}
