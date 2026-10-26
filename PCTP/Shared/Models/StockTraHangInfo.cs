using System;


namespace PCTP.Shared.Models
{
    public class StockTraHangInfo
    {
        public string Lot { get; set; }
        public DateTime? NgayTra { get; set; }
        public int SlTra { get; set; }
        public int SlNhanLai { get; set; }
        public string LyDoNg { get; set; }
        public int SlConLai => SlTra - SlNhanLai;
    }
}
