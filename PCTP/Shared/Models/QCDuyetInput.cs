using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Models
{
    public class QCDuyetInput
    {
        public int Id { get; set; }

        public string PhuongPhapKiemTra { get; set; }
        public string KetQuaKiemTra { get; set; }
        public int? SoLuongKiemTra { get; set; }

        public string PhuongPhapSua { get; set; }
        public string KetQuaSua { get; set; }
        public int? SoLuongSua { get; set; }

        public string XacNhanCuoiKetQua { get; set; }
        public string NguoiDanhGia { get; set; }
        public string NguoiThucHienQC { get; set; }
        public string GhiChuQC { get; set; }

        public DateTime? NgayBoPhanPhatSinh { get; set; }
        public string HoTenBoPhanPhatSinh { get; set; }

        public DateTime? NgayQCTiepNhan { get; set; }
        public string HoTenQCTiepNhan { get; set; }

        public DateTime? NgayBoPhanPhatHanhXacNhan { get; set; }
        public string HoTenBoPhanPhatHanhXacNhan { get; set; }

        public DateTime? NgayQCDuyet { get; set; }
        public string HoTenQCDuyet { get; set; }
    }
}
