
using PCTP.Shared.Models;
using System.Collections.Generic;
using System.Drawing;


namespace PCTP.Modules.KhoVatLy.CanVas
{
    public class RackLayoutInfo
    {
        public RackRenderInfo RackData { get; set; }
        public Rectangle Bounds { get; set; }         // Vị trí vùng vẽ của cả Rack
        public Rectangle HeaderBounds { get; set; }   // Vị trí vùng tiêu đề
        public List<SlotLayoutInfo> Slots { get; set; } = new List<SlotLayoutInfo>();

        public Rectangle SummaryTextBounds { get; set; } = Rectangle.Empty;
    }
}
