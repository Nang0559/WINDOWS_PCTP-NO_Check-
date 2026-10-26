

namespace PCTP.Modules.KhoCore.Models
{
    public class BoxScanResult
    {
        public string TemCode { get; set; }
        public string ItemCode { get; set; }
        public string NSX { get; set; }
        public bool IsMatch { get; set; }
        public string MismatchFields { get; set; }
    }
}
