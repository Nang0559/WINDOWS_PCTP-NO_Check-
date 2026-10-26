using PCTP.Modules.BaoCao.Application.Contracts.Models;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Application.Contracts.Repositories;
using System.Collections.Generic;

namespace PCTP.Modules.BaoCao.Infrastructure.Queries
{
    public sealed class LotLocationQueryService : ILotLocationQuery
    {
        private readonly ILotLocationRepository _repository;

        public LotLocationQueryService(ILotLocationRepository repository)
        {
            _repository = repository;
        }

        public IReadOnlyList<LotLocationRow> GetByLot(string lotNo)
        {
            return _repository.GetByLot(lotNo);
        }
    }
}
