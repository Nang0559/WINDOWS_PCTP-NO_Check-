using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Shared.Common;
using PCTP.Shared.Models;
using PCTP.VIEWSTOCK.Models;
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    /// <summary>Cập nhật tồn kho khi CNK (Confirm Nhận Kho) — nghiệp vụ nặng nhất, giữ riêng.</summary>
    public interface IPhieuKhoRepository
    {
        //DataTable LoadHangThieu(bool isMayBanQR, string tenBan);
        int CapNhapKho(string gioGiaoFcc, string nhaMay, PhieuTableSet tables, out DataTable errors);
        int CapNhapKhoHTN(string nhaMay, PhieuTableSet tables, out DataTable errors);
        int CapNhapKhoSP(string gioGiaoFcc, string nhaMay, out DataTable errors);
        bool CapNhapKhoYMVN(int stt, string lotSl, string maHang, string ngayGiao,
            string gioGiao, string nhaMay, out DS_ERR_CNK error);
        /// <summary>
        /// Đánh dấu đã giao ĐÚNG dòng đơn xác định bởi PO + mã hàng + ngày + giờ:phút
        /// (cùng PO/mã hàng/ngày vẫn có thể có nhiều đợt giao khác giờ).
        /// Trả về số dòng bảng đơn được cập nhật (0 = đã giao trước đó hoặc không tìm thấy dòng).
        /// </summary>
        int DanhDauDaGiao(OrderLineKey key, CustomerConfig cfg);

        int CapNhapKho(string gioGiaoFcc, string nhaMay, string tmpTable, string docQRTable, out DataTable errors);
        int CapNhapKhoHTN(string nhaMay, string tmpTable, string docQRTable, out DataTable errors);
    }

}
