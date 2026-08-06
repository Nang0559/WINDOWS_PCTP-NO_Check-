using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Fuction
{
    public static class PrintHelper
    {
        /// <summary>
        /// Dựng PXuatINModel từ 1 PrintLotResult đã gộp sẵn (LotNoHelper.CreatePrintData).
        /// Đây là nơi DUY NHẤT quyết định cách map PrintLotResult -> PXuatINModel cho luồng in
        /// phiếu xuất/nhập lại kho — mọi chỉ số in cần đổi (Ca, format Ngay/Gio...) chỉ sửa ở đây.
        /// </summary>
        public static PXuatINModel CreatePrintModel(
            PrintLotResult printData,
            string loaiPhieu,
            string productName,
            int slotNumber,
            int soLuongXuat,
            int soLuongTon,
            string nguoiThucHien = "")
        {
            return new PXuatINModel
            {
                LoaiPhieu = loaiPhieu,

                Ca = "",

                SoThuTuXe = slotNumber.ToString(),

                TenSanPham = productName,

                MaSanPham = printData.ItemCode,

                LotNo = printData.LotNo,

                SoLuong = printData.Quantity,

                CheckTem = printData.TemCode,

                NguoiThucHien = nguoiThucHien, // ✅ FIX: trước đây hardcode "" — mất tên người thực hiện

                QrData = printData.QrData,

                Ngay = DateTime.Now.ToString("dd/MM"),

                Gio = DateTime.Now.ToString("HH:mm"),

                SoLuongXuat = soLuongXuat,

                NguoiXuat = nguoiThucHien, // ✅ FIX: trước đây hardcode ""

                SoLuongTon = soLuongTon
            };
        }
    }
}
