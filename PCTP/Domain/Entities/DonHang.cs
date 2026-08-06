using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Entities
{
    /// <summary>
    /// Entity đơn hàng — không phụ thuộc bất kỳ UI/SQL/DevExpress nào
    /// </summary>
    public class DonHang
    {
        public int STT { get; set; }
        public string MaHang { get; set; } = "";
        public string TenHang { get; set; } = "";
        public string Cua { get; set; } = "";
        public string Truyen { get; set; } = "";
        public string DonVi { get; set; } = "";
        public int SoLuong { get; set; }
        public int SoHop { get; set; }
        public string GioGiao { get; set; } = "";
        public string NgayGiao { get; set; } = "";
        public string Lot { get; set; } = "";
        public string NhaMay { get; set; } = "";
        public int AddNM { get; set; }
        public string Status { get; set; } = "NG";
        public string StatusDoc { get; set; } = "NG";
        public string TtPhieu { get; set; } = "";
        public string Note { get; set; } = "";
        public string PoNo { get; set; } = "";
        public string PoItem { get; set; } = "";

        // Computed — không lưu DB
        public bool DaCoLot => !string.IsNullOrWhiteSpace(Lot);
        public bool DaGiaoOK => Status == "OK";
    }
}
