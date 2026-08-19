using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.NhapKho.Repository
{
    public interface IStockTpCaseRepository
    {
        bool ExistsCaseHistory(string caseNo);

        void InsertCaseHistory(string caseNo);
    }
}
