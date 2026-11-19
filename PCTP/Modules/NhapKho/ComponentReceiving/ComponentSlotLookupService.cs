using System;
using System.Data;
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
            return _repository.GetActiveSlots(siteCode.Trim().ToUpperInvariant());
        }
    }
}