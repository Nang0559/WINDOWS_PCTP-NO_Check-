using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.NhapKho.Repository
{
    public interface IStockTpRepository
    {
        bool ExistsStockTp(string lot);

        StockItem GetByLot(string lot);

        int GetSlConLai(string lot);

        void InsertStockTp(
            NhapKhoItem item,
            int status);

        void UpdateStockTp(
            string lot,
            int slSeNhap,
            int status);

        void XuatKhoThat(
            string lot,
            int soLuong);

        List<(string Lot, int SlConLai)>
            GetDanhSachLotConTon();

        Dictionary<string, int>
            GetSlConLaiBatch(IEnumerable<string> lots);

        int GetSlDaNhap(
            string lot);

        /// <summary>
        /// Ghi đè trực tiếp SLCONLAI của 1 LOT — CHỈ dùng cho màn hình điều chỉnh tồn kho thủ công
        /// khi phát hiện lệch dữ liệu (ví dụ CapNhapKho báo lỗi, hoặc đối soát A0 vs STOCKTP).
        /// KHÔNG dùng trong luồng nhập/xuất bình thường — các luồng đó phải cộng/trừ qua
        /// UpdateStockTp / XuatKhoThat để giữ đúng lịch sử SLNHAP/SLXUAT.
        /// </summary>
        void DieuChinhSlConLai(string lot, int slConLaiMoi);

        // IStockTpRepository — thêm
        DataTable GetTonKhoHienTai();
        DataTable GetTonKhoTheoLot(List<string> lots);
       
    }
}
