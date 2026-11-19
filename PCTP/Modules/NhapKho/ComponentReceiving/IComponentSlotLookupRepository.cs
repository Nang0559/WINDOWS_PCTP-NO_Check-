using System.Data;
namespace PCTP.Modules.NhapKho.ComponentReceiving
{
    public interface IComponentSlotLookupRepository
    {
        DataTable GetActiveSlots(string siteCode);
    }
}