using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.NhapKho.Models
{
    public sealed class StockImportRequest
    {
        public string LotNo { get; set; }

        public string MaHang { get; set; }

        public int SoLuong { get; set; }

        public StockImportPurpose Purpose { get; set; }

        public int? PhieuXuLyId { get; set; }

        public string LyDo { get; set; }

        public string NguoiThucHien { get; set; }
    }
}
