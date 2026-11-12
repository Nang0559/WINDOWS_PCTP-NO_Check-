using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    /// <summary>Giao DB (đơn hàng đặc biệt nhập tay/upload trước đây).</summary>
    public interface IPhieuGiaoDBRepository
    {
        DataTable GetDanhSachMaHang();
        DataTable LoadTmpPhieuGiaoDB(string tenBan, DateTime ngayGiao, int addNm);
        DataTable BuildDonHangTuUpload();
        /// <summary>Sau Cập nhật kho Giao DB: ghi LOT/STATUS='OK' của các dòng đã giao từ bảng TMP về FVN_Purchase_Order_DB_CT để lần nạp lại không dựng lại chúng thành NG. Trả về số dòng ghi.</summary>
        int DongBoDaGiaoVaoDonGiaoDB(string tmpTable);
        /// <summary>Đơn chờ xử lý (STATUS='NG') theo ngày + nhà máy: NGAYGIAO, ADDNM, SODONG.</summary>
        DataTable ThongKeDonChoXuLy();
        void LuuGiaoDB(DataTable donHang, string gioFccMoTa, int addNm,
            string tmpTable, string ifsTable, string nhaMayOverride = "",
            string docQrTable = "DOCQRCODE_DB");
        int TaoPhieuVaChiTietGiaoDB(
            string ten, DateTime ngayLap, int nhaMay, string nhaMayName,
            string note, DataTable chiTiet);
        /// <summary>Header phiếu Giao DB theo ngày lập + nhà máy (0/1 dòng; cột SOPHIEU = số header trùng khóa).</summary>
        DataTable GetHeaderGiaoDB(DateTime ngayLap, int nhaMay);
        /// <summary>Chi tiết phiếu theo ngày + nhà máy; cột DaGiao = dòng đã xử lý (khóa, không sửa/xóa).</summary>
        DataTable GetChiTietGiaoDB(DateTime ngayLap, int nhaMay);
        /// <summary>Tạo mới hoặc cập nhật phiếu (1 ngày + 1 nhà máy = 1 phiếu); chi tiết theo RowState Added/Modified/Deleted.</summary>
        int LuuPhieuGiaoDB(string ten, DateTime ngayLap, int nhaMay, string nhaMayName, string note, DataTable chiTiet);
        /// <summary>Xóa cả phiếu của ngày + nhà máy; chỉ khi không còn dòng đã giao. Trả về số chi tiết đã xóa.</summary>
        int XoaPhieuGiaoDB(DateTime ngayLap, int nhaMay);
        //void UploadChiTietGiaoDB(DataTable chiTiet, bool xoaCuTruoc);
    }
}
