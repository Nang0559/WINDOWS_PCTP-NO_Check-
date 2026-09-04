using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.UiMd
{
    public class WorkflowTransition
    {
        public int Id { get; set; }
        public string ProcessCode { get; set; }
        public int FromStatus { get; set; }
        public int ToStatus { get; set; }
        public string ActionName { get; set; }
        public string Description { get; set; } // Chứa ghi chú hoặc điều kiện LINQ Dynamic (ví dụ: "SoLuongNG > 0")
        public bool IsActive { get; set; }
    }
}
