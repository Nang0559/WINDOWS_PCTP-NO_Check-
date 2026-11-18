using System.Data;
namespace PCTP.Modules.MesPlanning
{
    public interface IComponentDemandService
    {
        DataTable GetPlanLines(string site);
        long GenerateDemand(long planLineId,string user);
        DataTable GetDemandLines(long planLineId);
        DataTable GetPartLots(string site,string partNo);
        DataTable GetWipSlots(string site);
        DataTable PostIssue(string site,string issueNo,long demandLineId,long lotId,int fromSlot,int toSlot,decimal qty,string user);
    }
}