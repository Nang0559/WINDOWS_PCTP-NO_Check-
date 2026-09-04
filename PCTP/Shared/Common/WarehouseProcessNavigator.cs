using PCTP.ClassSQL;
using PCTP.QRCODE_HVN.PGH;
using PCTP.QRCODE_HVN.YMN;
using PCTP.VIEWSTOCK;
using PCTP.VIEWSTOCK.Repository;
using PCTP.VIEWSTOCK.ViewForm;
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Common
{
    /// <summary>
    /// Điểm điều phối DUY NHẤT để mở các form quy trình nghiệp vụ kho.
    /// Main_APP (accordion) và MainStockSV (process bar) đều gọi qua đây —
    /// tránh code trùng lặp "using (var f = new FormXxx()) f.ShowDialog()"
    /// rải rác nhiều nơi, và đảm bảo mọi nơi mở form đều đồng nhất hành vi.
    ///
    /// Navigator KHÔNG chứa logic nghiệp vụ — chỉ new đúng dependency (Tầng 3:
    /// SqlProvider/Repository) và mở đúng Form (Tầng 2: Module).
    /// </summary>
    public static class WarehouseProcessNavigator
    {
        // ── Repository dùng chung — new 1 lần theo lời gọi, không giữ static instance
        // để tránh cache dữ liệu cũ giữa các lần mở form (mỗi lần mở là 1 phiên làm việc mới).
        private static IPhieuLoiRepository CreatePhieuLoiRepo()
            => new PhieuLoiRepository(new SQLPROVIDER());

        public static void OpenBanDoKho(IWin32Window owner)
        {
            var f = new MainStockSV();
            f.Show(); // Show (không ShowDialog) — bản đồ kho dùng như cửa sổ làm việc song song
        }

        public static void OpenNhapKhoTienTrinh(IWin32Window owner, MainStockSV mainStock = null)
        {
            using (var f = new FormNhapKhoTienTrinh(mainStock))
                f.ShowDialog(owner);
        }

        /// <summary>
        /// Mốc 3a — QC Định hướng ban đầu: phiếu vừa được ban hành (TrangThai=ChoQC),
        /// QC nhập phương pháp định hướng xử lý (Nắn/Vặn/Cắt gọt...) trước khi
        /// chuyển bộ phận sản xuất thực hiện.
        /// </summary>
        public static void OpenQCDinhHuong(IWin32Window owner, int? preselectId = null)
        {
            using (var f = new FormXuLyBatThuong(CreatePhieuLoiRepo(), XuLyBatThuongMode.DinhHuong, preselectId))
                f.ShowDialog(owner);
        }

        /// <summary>
        /// Mốc 3b — QC Xác nhận lần cuối: sau khi SX đã sửa/xử lý xong, QC vào
        /// chốt kết luận OK/NG cuối cùng (TrangThai chuyển sang QCDaDuyet).
        /// Đây là điều kiện KHOÁ bắt buộc trước khi FormTraHangNGNew cho trả về SX.
        /// </summary>
        public static void OpenQCXacNhanCuoi(IWin32Window owner, int? preselectId = null)
        {
            using (var f = new FormXuLyBatThuong(CreatePhieuLoiRepo(), XuLyBatThuongMode.XacNhanCuoi, preselectId))
                f.ShowDialog(owner);
        }

        public static void OpenTraHangNG(IWin32Window owner, int? preselectPhieuId = null)
        {
            using (var f = new FormGiaoBuNG(preselectPhieuId))
                f.ShowDialog(owner);
        }

        public static void OpenGiaoBuNG(IWin32Window owner)
        {
            using (var f = new frmGiaoBuNG())
                f.ShowDialog(owner);
        }

        public static void OpenGiaoHangHVN(string customerNo)
        {
            var frm = new HVN_PGH(customerNo);
            frm.Show();
        }

        public static void OpenGiaoHangYMVN(string mpSp)
        {
            YMVN_CHONGIAO.MP_SP = mpSp;
            var frm = new GIAOHANGYMN();
            frm.Show();
        }
    }
}
