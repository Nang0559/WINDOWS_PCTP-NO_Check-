using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class PhieuNhapInfo
    {
        public int Stt { get; set; }
        public string Find { get; set; }   // FIND = LOT_NO 20 ký tự
        public string LotNo { get; set; }   // LOT_NO
        public string Model { get; set; }
        public string TenSP { get; set; }   // TEN_SAN_PHAM
        public string MaSP { get; set; }   // MA_SAN_PHAM
        public int CaSX { get; set; }   // CA_SAN_XUAT
        public DateTime NgaySX { get; set; }   // NGAY_SAN_XUAT
        public int SlSanXuat { get; set; }   // SL_DA_SAN_XUAT
        public DateTime NgayNhap { get; set; }
        public int SlDaNhap { get; set; }   // SL_DA_NHAP
        public int SlDaTra { get; set; }
        public string LyDoTra { get; set; }
        public int TonKhoTP { get; set; }   // TON_KHO_TP
        public int SlSeNhap { get; set; }   // SL_SE_NHAP (user nhập)
        public bool KetThucLot { get; set; }  // KET_THUC_LOT
    }
}
