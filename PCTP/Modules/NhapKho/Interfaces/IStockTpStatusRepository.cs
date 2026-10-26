using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.NhapKho.Repository
{
    public interface IStockTpStatusRepository
    {
        void MoLaiLot(
            string lot,
            string find = null);

        bool DongBoSLSXVaMoLaiNeuThayDoi(
            string lot,
            string find,
            int slsxMoiTuMES);
    }
}
