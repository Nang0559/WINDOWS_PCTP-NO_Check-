using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.CanVas
{
    public class SlotLayoutInfo
    {
        public Slot SlotData { get; set; }
        public Rectangle Bounds { get; set; } // Vị trí vùng vẽ của Slot này
    }
}
