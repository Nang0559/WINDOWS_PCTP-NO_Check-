using PCTP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    /// <summary>
    /// 1 dòng "phiếu" đang tồn tại vật lý trong kho — dùng để đối chiếu
    /// LotNo (STOCKTP) với các SlotLot đang Active.
    /// </summary>
    public class PhieuLocationInfo
    {
        public string LotNo { get; set; }
        public string MaPhieu { get; set; }         // số phiếu kho hiện tại
        public string ParentSoPhieu { get; set; }    // phiếu cha (nếu bị tách ra)
        public string SoPhieuTong { get; set; }      // số phiếu gốc trên tem
        public PhieuStatus Status { get; set; }
        public int Quantity { get; set; }
        public int SlotId { get; set; }
        public string WarehouseName { get; set; }
        public string RackName { get; set; }
        public int SlotNumber { get; set; }
        public DateTime? ImportDate { get; set; }

        public bool CoTheDung => Status == PhieuStatus.Active;
    }
}
