using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Events
{
    public class ChonLotThuCongEventArgs : EventArgs
    {
        public int Stt { get; }
        public string MaHang { get; }
        public int SoLuong { get; }

        public ChonLotThuCongEventArgs(int stt, string maHang, int soLuong)
        {
            Stt = stt;
            MaHang = maHang;
            SoLuong = soLuong;
        }
    }

   
}
