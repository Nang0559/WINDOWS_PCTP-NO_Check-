using PCTP.VIEWSTOCK.Models;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.NhapKho.Interfaces
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

        List<(string Lot, int SlConLai)>
            GetDanhSachLotConTon();

        Dictionary<string, int>
            GetSlConLaiBatch(IEnumerable<string> lots);

        int GetSlDaNhap(
            string lot);

        DataTable GetTonKhoHienTai();
        DataTable GetTonKhoTheoLot(List<string> lots);
    }
}
