using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Common
{
    // Thêm vào InspectionConfig hoặc 1 bảng cấu hình riêng — tùy mã hàng có bắt buộc FIFO hay không
    public class ItemFifoConfig
    {
        public string ItemCode { get; set; }
        public bool EnforceFifo { get; set; }   // true = bắt buộc, false = không kiểm tra
    }
}
