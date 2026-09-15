using PCTP.Modules.BaoCao.Application.Contracts.Models;
using System.Collections.Generic;

namespace PCTP.Modules.BaoCao.Application.Contracts.Repositories
{
    /// <summary>
    /// Read-only access to the physical LOT location projection.
    /// </summary>
    public interface ILotLocationRepository
    {
        IReadOnlyList<LotLocationRow> GetByLot(string lotNo);
    }
}
