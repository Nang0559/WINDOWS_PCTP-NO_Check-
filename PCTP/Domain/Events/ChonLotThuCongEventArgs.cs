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

    public class ChonLotResult
    {
        // LOT ghép cuối cùng: "LOT1-100,LOT2-50"
        public string LotGhep { get; set; }
        public bool Confirmed { get; set; }
    }

    // Một dòng LOT user chọn
    public class LotKhoItem
    {
        public string LotNo { get; set; }
        public int SlConLai { get; set; }
        public int SlChon { get; set; }  // user nhập
    }
}
