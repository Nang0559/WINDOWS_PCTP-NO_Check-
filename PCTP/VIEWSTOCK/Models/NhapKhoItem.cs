using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class NhapKhoItem
    {
        public string Lot { get; set; }
        public string Find { get; set; }        // 20 ký tự LOT (khớp PhieuNhapInfo.Find)
        public string Part { get; set; }        // MaSP
        public string Name { get; set; }        // TenSP
        public string Model { get; set; }
        public string SP { get; set; } = "";

        public int SlNhap { get; set; }         // = SlSeNhap tại thời điểm confirm
        public int SlSanXuat { get; set; }
        public int SlDaNhap { get; set; }
        public int TonKhoTP { get; set; }       // ← THÊM: để hiển thị/đối chiếu tồn kho

        public string SoPhieu { get; set; }     // tạm = LotNo, chờ xác nhận nguồn thật
        public string LineCodes { get; set; } = "";
        public string DeptCode { get; set; } = "";
        public string LoaiNhap { get; set; } = "N";   // "N" hoặc "NG"

        public int CaSX { get; set; }           // ← ĐỔI: int thay vì string "Ca" + parse
        public DateTime? NgaySX { get; set; }
        public bool KetThucLot { get; set; }    // ← THÊM

        // ── Factory: build trực tiếp từ dòng phiếu trên grid ────────────────
        public static NhapKhoItem FromPhieu(PhieuNhapInfo phieu, string loaiNhap = "N")
        {
            return new NhapKhoItem
            {
                Lot = phieu.LotNo,
                Find = phieu.Find,
                Part = phieu.MaSP,
                Name = phieu.TenSP,
                Model = phieu.Model,
                SlNhap = phieu.SlSeNhap,
                SlSanXuat = phieu.SlSanXuat,
                SlDaNhap = phieu.SlDaNhap,
                TonKhoTP = phieu.TonKhoTP,
                SoPhieu = phieu.LotNo,          // TODO: thay bằng field số phiếu thật nếu có
                LoaiNhap = loaiNhap,
                CaSX = phieu.CaSX,
                NgaySX = phieu.NgaySX,
                KetThucLot = phieu.KetThucLot
            };
        }

        public StockItem ToStockItem() => new StockItem
        {
            Lot = Lot,
            Find = Find,
            Part = Part,
            Name = Name,
            Model = Model,
            SP = SP,
            SlNhap = SlNhap,
            SlConLai = SlNhap,
            SlXuat = 0,
            SlConLaiTmp = 0,
            SoPhieu = SoPhieu,
            StatusNhap = LoaiNhap == "NG" ? "NG" : "TRONG_KHO",
            LineCodes = LineCodes,
            DeptCode = DeptCode,
            NgayNhap = DateTime.Now,
            CaSX = (short)CaSX,
            NgaySX = NgaySX
        };
    }
}
