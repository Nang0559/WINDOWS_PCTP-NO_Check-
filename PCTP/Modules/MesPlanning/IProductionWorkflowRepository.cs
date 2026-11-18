using System.Data;
namespace PCTP.Modules.MesPlanning
{
    public interface IProductionWorkflowRepository
    {
        DataTable Transition(string entityType, long entityId, long? relatedId, int fromStatus,
            int toStatus, string actor, string reason, string correlationId);
        DataTable GetTimeline(string entityType, long entityId);
    }
}