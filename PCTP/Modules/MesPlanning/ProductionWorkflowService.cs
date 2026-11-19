using System;
using System.Data;
using PCTP.Shared.UiMd;
using PCTP.Infrastructure.Security;

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
            actor = AuthorizationGuard.ActorCode;
            DemandEntityScope("PLAN", planId, toStatus == 20 ? "APPROVE" : (toStatus == 900 ? "CANCEL" : "CONFIRM"));
            return Transition("PLAN", "MES_PRODUCTION_PLAN", planId, null, fromStatus, toStatus, actor, reason);
        }

        public DataTable TransitionOrder(long orderId, int fromStatus, int toStatus, string actor, string reason = null)
        {
            actor = AuthorizationGuard.ActorCode;
            DemandEntityScope("ORDER", orderId, toStatus == 900 ? "CANCEL" :
                (toStatus == 10 || toStatus == 40 || toStatus == 50 ? "APPROVE" : "CONFIRM"));
            return Transition("ORDER", "MES_PRODUCTION_ORDER", orderId, null, fromStatus, toStatus, actor, reason);
        }

        public DataTable TransitionOperation(long productionLotId, long routingStepId, int fromStatus,
            int toStatus, string actor, string reason = null)
        {
            actor = AuthorizationGuard.ActorCode;
            DemandEntityScope("OPERATION", productionLotId, toStatus == 900 ? "CANCEL" :
                (toStatus == 50 ? "APPROVE" : "CONFIRM"));
            if (routingStepId <= 0) throw new ArgumentException("RoutingStepId không hợp lệ.");
            return Transition("OPERATION", "MES_PRODUCTION_OPERATION", productionLotId,
                routingStepId, fromStatus, toStatus, actor, reason);
        }

        public DataTable GetTimeline(string entityType, long entityId)
        {
            if (entityId <= 0) throw new ArgumentException("EntityId không hợp lệ.");
            ValidateEntityType(entityType);
            DemandEntityScope(entityType, entityId, "VIEW");
            return _repository.GetTimeline(entityType, entityId);
        }

        private void DemandEntityScope(string entityType, long entityId, string actionCode)
        {
            if (entityId <= 0) throw new ArgumentException("EntityId không hợp lệ.");
            var sites = _repository.GetEntitySites(entityType, entityId);
            if (sites == null || sites.Rows.Count == 0)
                throw new UnauthorizedAccessException("Không xác định được SiteCode của đối tượng workflow.");
            foreach (DataRow row in sites.Rows)
            {
                string site = Convert.ToString(row["SiteCode"]);
                if (string.IsNullOrWhiteSpace(site))
                    throw new UnauthorizedAccessException("Đối tượng workflow chưa có SiteCode.");
                AuthorizationGuard.DemandActionAndScope(
                    string.Equals(entityType, "PLAN", StringComparison.OrdinalIgnoreCase)
                        ? "MES.PLAN_UPLOAD" : "MES.PRODUCTION_EXECUTION",
                    actionCode, "SITE", site, site);
            }
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