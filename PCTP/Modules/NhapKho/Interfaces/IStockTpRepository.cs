using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
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
    }
}
