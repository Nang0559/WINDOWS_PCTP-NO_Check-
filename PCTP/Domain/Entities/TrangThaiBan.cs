using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Entities
{
    public class TrangThaiBan
    {
        public bool DangBan { get; set; }  // có DOCQRCODE chưa hoàn thành
        public int AddNM { get; set; }  // 1=VP, 2=HN
        public string NgayGiao { get; set; }
        public string GioGiaoFCC { get; set; }  // giờ đang bắn, vd: "'06'"
        public string MoTaGio { get; set; }
        public string NhaMay { get; set; }
        public bool DataKhongKhop { get; set; } // DOCQRCODE có data nhưng TMPPHIEUGIAOHANG rỗng
    }
}
