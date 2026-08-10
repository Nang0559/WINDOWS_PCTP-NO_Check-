using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Models
{
    public class ChoGiaoItem
    {
        public int Id { get; set; }
        public string LotThung { get; set; }
        public string LotGoc { get; set; }
        public string MaHang { get; set; }
        public int SoLuong { get; set; }
        public int? SlotIdNguon { get; set; }
        public string TrangThai { get; set; }
    }

    public class ThungQuetTraInfo
    {
        public int Id { get; set; }
        public string LotThung { get; set; }
        public string LotGoc { get; set; }
        public string MaHang { get; set; }
        public int SlThung { get; set; }
        public bool DaXuLy { get; set; }
    }

    public class NhomLotTraInfo
    {
        public string LotGoc { get; set; }
        public string MaHang { get; set; }
        public int TongSl { get; set; }
    }
}
