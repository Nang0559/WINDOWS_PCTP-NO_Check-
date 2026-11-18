using System;
using System.Data;
using PCTP.Shared.UiMd;

namespace PCTP.Modules.MesPlanning
{
    /// <summary>
    /// Application boundary for the shared workflow engine and MES production workflow persistence.
    /// Workflow decides whether a transition is allowed; SQL rechecks it and enforces domain guards
    /// in the same transaction as state and audit updates.
    /// </summary>
    public sealed class ProductionWorkflowService : IProductionWorkflowService
    {
        private readonly IProductionWorkflowRepository _repository;
        private readonly IWorkflowTransitionService _transitions;

        public ProductionWorkflowService(IProductionWorkflowRepository repository,
            IWorkflowTransitionService transitions)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        }

        public DataTable TransitionPlan(long planId, int fromStatus, int toStatus, string actor, string reason = null)
        {
            return Transition("PLAN", "MES_PRODUCTION_PLAN", planId, null, fromStatus, toStatus, actor, reason);
        }

        public DataTable TransitionOrder(long orderId, int fromStatus, int toStatus, string actor, string reason = null)
        {
            return Transition("ORDER", "MES_PRODUCTION_ORDER", orderId, null, fromStatus, toStatus, actor, reason);
        }

        public DataTable TransitionOperation(long productionLotId, long routingStepId, int fromStatus,
            int toStatus, string actor, string reason = null)
        {
            if (routingStepId <= 0) throw new ArgumentException("RoutingStepId không hợp lệ.");
            return Transition("OPERATION", "MES_PRODUCTION_OPERATION", productionLotId,
                routingStepId, fromStatus, toStatus, actor, reason);
        }

        public DataTable GetTimeline(string entityType, long entityId)
        {
            if (entityId <= 0) throw new ArgumentException("EntityId không hợp lệ.");
            ValidateEntityType(entityType);
            return _repository.GetTimeline(entityType, entityId);
        }

        private DataTable Transition(string entityType, string processCode, long entityId,
            long? relatedId, int fromStatus, int toStatus, string actor, string reason)
        {
            if (entityId <= 0) throw new ArgumentException("EntityId không hợp lệ.");
            if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("ActorCode là bắt buộc.");
            if (fromStatus == toStatus) throw new ArgumentException("Không tạo transition khi trạng thái không thay đổi.");
            _transitions.EnsureCanTransition(processCode, fromStatus, toStatus);
            return _repository.Transition(entityType, entityId, relatedId, fromStatus, toStatus,
                actor.Trim(), string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                Guid.NewGuid().ToString("D"));
        }

        private static void ValidateEntityType(string entityType)
        {
            if (!string.Equals(entityType, "PLAN", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entityType, "ORDER", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entityType, "OPERATION", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("EntityType chỉ nhận PLAN, ORDER hoặc OPERATION.");
        }
    }
}