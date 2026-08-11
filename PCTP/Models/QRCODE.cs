using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Models
{
    public class QrcodeModels    {
        public string LOTFCC { get; set; }
        public string MAHANGFCC { get; set; }
        public string SLTEMFCC { get; set; }
        public string LOTHVN { get; set; }
        public string MAHANGHVN { get; set; }
        public string SLTEMHVN { get; set; }
        public string STATUS { get; set; }
        public string MAFCC { get; set; }
        public string STT { get; set; }
        public string KETQUA { get; set; }
        public string CUA { get; set; }
        public string TRUYEN { get; set; }
        public string GIO { get; set; }
        public string STTBAN { get; set; }
        public string SUALOTHVN { get; set; }
        public string TGLUU { get; set; }
        public string FindTem { get; set; }
    }
    public class PhieuGiaoGocInfo
    {
        public int Stt { get; set; }
        public string Lot { get; set; }
        public string MaHang { get; set; }
        public string TenHang { get; set; }
        public int SoLuong { get; set; }
        public DateTime? NgayGiao { get; set; }
        public string GioGiao { get; set; }
        public string NhaMay { get; set; }
        public string Cua { get; set; }
        public string Truyen { get; set; }
        public string PoNo { get; set; }
        public string Note { get; set; }
        public string DinhDanhKey { get; set; }
    }

    public class TemFccQuetInfo
    {
        public string LotFcc { get; set; }
        public string MaHangFcc { get; set; }
        public int SlTemFcc { get; set; }
        public string RawQr { get; set; }

        // ── Resolve sau khi tra STOCKTP/Slot — dùng lúc xác nhận giao bù ──
        public int SlotIdNguon { get; set; }
        public int SlConLaiTaiSlot { get; set; }
    }
}
