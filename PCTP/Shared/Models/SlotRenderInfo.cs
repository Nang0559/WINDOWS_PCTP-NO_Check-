using PCTP.Modules.KhoCore.Models;


namespace PCTP.Shared.Models
{
    public class SlotRenderInfo
    {
        public Slot Slot;
        public string RackName;
        public string WarehouseName;
        public int Row { get; set; }      // mới
        public int Column { get; set; }   // mới
    }
  

}
