using System;


namespace PCTP.Modules.KhoCore.Models
{
    public class InspectionConfig
    {
        public int ConfigId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public int DefaultQty { get; set; } = 1;
        public bool CheckItemCode { get; set; } = true;
        public bool CheckLotNo { get; set; } = true;
        public bool CheckNSX { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public string Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
