using System.Data;
namespace PCTP.Modules.MesPlanning
{
    public interface IProductionWorkflowService
    {
        DataTable TransitionPlan(long planId, int fromStatus, int toStatus, string actor, string reason = null);
        DataTable TransitionOrder(long orderId, int fromStatus, int toStatus, string actor, string reason = null);
        DataTable TransitionOperation(long productionLotId, long routingStepId, int fromStatus,
            int toStatus, string actor, string reason = null);
        DataTable GetTimeline(string entityType, long entityId);
        int GetCurrentStatus(string entityType, long entityId, long? relatedId = null);
    }
}