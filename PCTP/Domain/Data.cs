using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.FuctionPrint
{
    public class Record
    {
        public int STT { get; set; }

        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;

        public DateTime DocDate { get; set; }

        public string ItemLotCode { get; set; } = string.Empty;

        public int ShiftCode { get; set; }

        public int QCDG { get; set; }

        public int Quantity9 { get; set; }

        /// <summary>
        /// Số lượng thực tế muốn ghép.
        /// </summary>
        public int SLG { get; set; }

        /// <summary>
        /// true = Lot chưa ghép / còn sử dụng.
        /// false = Lot đã được ghép.
        /// </summary>
        public bool State { get; set; }

        public string QRCODE { get; set; } = string.Empty;
    }

    public class DetailGL
    {
        public int? STT { get; set; }

        public int Quantity9 { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string ItemLotCode { get; set; } = string.Empty;

        public int ShiftCode { get; set; }

        public string Model { get; set; } = string.Empty;

        public string MO { get; set; } = string.Empty;

        public DateTime DocDate { get; set; }

        public string QRCODE { get; set; } = string.Empty;
    }
}
