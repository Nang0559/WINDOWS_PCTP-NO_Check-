using PCTP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class LotInfo
    {
        // phiếu nhập 
        public string MaPhieuKho { get; set; }      // ← MỚI, map cột SlotLot.MaPhieu
        public string ParentSoPhieuKho { get; set; } // ← MỚI, map cột SlotLot.ParentSoPhieu
        public PhieuStatus PhieuStatus { get; set; } = PhieuStatus.Active; // ← MỚI
        // phiếu giao
        public string LotNo { get; set; }

        public int Quantity { get; set; }

        public string TemCode { get; set; }

        public string RawQr { get; set; }

        public QRCodeInfo QRInfo { get; set; }
    }

}
