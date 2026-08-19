using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    /// <summary>
    /// Đối xứng với IIFSRepository — nhưng cho nguồn "bảng riêng" (Purchase_Order_YMVN,
    /// Purchase_Order_HTN, và giao đặc biệt qua CustomerConfig.OrderTableGiaoDacBiet).
    /// Tách khỏi IPhieuRepository để OrderTableLoadStrategy chỉ phụ thuộc đúng những gì
    /// nó cần — không kéo theo toàn bộ god-interface (CNK, DocQR, LuuTru, GiaoDB...).
    /// </summary>
    public interface ITableOrderRepository
    {
        /// <summary>Load đơn hàng gốc từ bảng riêng + tự merge LOT đã lưu (LUUPHIEUGIAOHANG) nội bộ.</summary>
        DataTable LoadPhieuTuBangRieng(string ngayGiao, string gioFilter, bool isLoaiSP,
            string dockCodeSP, CustomerConfig cfg, string tenBangOverride = null);

        DataTable LoadPhieuDangDocYMVN(string tmpTable, bool isLoaiSP = false);

        DataTable LoadHangThieuYMVN(string ngayXuatMDY, bool isLoaiSP);

        IReadOnlyList<string> GetDanhSachGioYMVN(string ngayXuatMDY);
        IReadOnlyList<string> GetGioGiaoYMVN(string ngayGiao);

        void UploadMilkrunSP(DataTable donHang, string ngayGiao);

        void InsertTmpYMVN(string stt, string cua, string truyen, string maHang, string tenHang,
            string lot, string dv, int slXuat, string ngayGiao, string gear, string gioXuat,
            string tmpTable, string poNo = "", string cusPoNo = "");

        /// <summary>Đối chiếu bảng riêng với IFS thật — phát hiện lệch/thiếu PO giữa 2 nguồn.</summary>
        DataTable SoSanhDonHangVoiIFS(DataTable donHangBangRieng, string ngayGiao, CustomerConfig cfg);
        Dictionary<string, int> GetQcDongGoiBatch(
        List<string> maHangList);
    }
}
