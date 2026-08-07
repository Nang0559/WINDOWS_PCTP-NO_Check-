using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.CanVas
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
