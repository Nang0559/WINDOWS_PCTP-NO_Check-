using PCTP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public interface IInPhieuService
    {
        DataTable BuildReportDataGiaoDB(DataTable donHang);
        DataTable BuildReportData(string ngayXuat, string gioXuat, string gioXuatH,
            string nhaMay, int addNm, int hinhThucIn, DataTable addressTable);
        DataTable BuildReportDataYMVN(DataTable donHang);
        DataTable BuildReportDataTuBangRieng(DataTable donHang, DataTable addressTable, string ngayXuat);
        DataTable InGhepLot(IEnumerable<GhepLotItem> selectedRows, string machineName);
    }
}
