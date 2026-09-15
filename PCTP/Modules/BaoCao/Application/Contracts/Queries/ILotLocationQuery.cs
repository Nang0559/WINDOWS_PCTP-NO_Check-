using PCTP.Modules.BaoCao.Application.Contracts.Models;
using System.Collections.Generic;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    public interface ILotLocationQuery
    {
        IReadOnlyList<LotLocationRow> GetByLot(string lotNo);
    }
}
