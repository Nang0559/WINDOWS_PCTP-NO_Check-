using PCTP.Modules.GiaoHangKhach.Mode;
using PCTP.Shared.Enums;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Models
{
    public class OrderLoadContext
    {
        public CustomerConfig Config { get; set; }
        public DateTime NgayGiao { get; set; }
        public string NhaMay { get; set; }
        public int AddNm { get; set; }
        public string GioFcc { get; set; }
        public string GioFccMoTa { get; set; }
        public OrderCategory Category { get; set; }
        public OrderSourceKind Source { get; set; }
        public MachineRole MachineRole { get; set; }
        public bool IsBanQR { get; set; }
        public IList<string> CheckedGios { get; set; }
    }
}
