using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.NhapKho.Interfaces
{
    public interface IWarehouseDashboardService
    {
        int GetTongTonStockTp();
        int GetTongTonRackThat();
        int GetTongTonKhoTam();
        int DemLechDoiChieu();
        DataTable GetGridLechDoiChieu();
    }
}
