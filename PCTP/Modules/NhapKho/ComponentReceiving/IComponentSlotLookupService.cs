using System.Data;
namespace PCTP.Modules.NhapKho.ComponentReceiving
{
    public interface IComponentSlotLookupService
    {
        DataTable GetActiveSlots(string siteCode);
    }
}