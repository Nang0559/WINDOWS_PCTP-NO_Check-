using System;
using System.Data;
using PCTP.Infrastructure.Security;
namespace PCTP.Modules.NhapKho.ComponentReceiving
{
    public sealed class ComponentSlotLookupService : IComponentSlotLookupService
    {
        private readonly IComponentSlotLookupRepository _repository;
        public ComponentSlotLookupService(IComponentSlotLookupRepository repository)
        { _repository = repository ?? throw new ArgumentNullException(nameof(repository)); }
        public DataTable GetActiveSlots(string siteCode)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) return new DataTable();
            var site = siteCode.Trim().ToUpperInvariant();
            AuthorizationGuard.DemandScope("WAREHOUSE.COMPONENT_RECEIPT", "SITE", site, site);
            return _repository.GetActiveSlots(site);
        }
    }
}