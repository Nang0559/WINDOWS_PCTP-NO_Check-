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
        /// <summary>In YMVN theo các giờ đang tích (null/rỗng = toàn bộ lưới).</summary>
        DataTable BuildReportDataYMVN(DataTable donHang, IList<string> checkedGios);
        DataTable BuildReportDataTuBangRieng(DataTable donHang, DataTable addressTable, string ngayXuat);
        DataTable InGhepLot(IEnumerable<GhepLotItem> selectedRows, string machineName);
    }
}
