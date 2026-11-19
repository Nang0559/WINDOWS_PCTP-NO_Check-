using System;
using System.Data;

namespace PCTP.Modules.MesPlanning
{
    public interface IProductionExecutionRepository
    {
        string GetPlanLineSiteCode(long planLineId);
        string GetOrderSiteCode(long orderId);
        string GetLotSiteCode(long productionLotId);
        string GetLabelSiteCode(string labelCode);
        DataTable GetOrderMaterials(long orderId);
        DataTable GetRoutingSteps(long productionLotId);
        DataTable CreateOrder(long planLineId, string orderNo, string actor);
        DataTable CreateLot(long orderId, DateTime producedAtUtc, decimal goodQty, decimal ngQty, bool finished, string actor);
        DataTable CompleteOperation(long productionLotId, string operationNo, decimal inputQty,
            decimal goodQty, decimal ngQty, int? inputSlotId, int? outputSlotId,
            string actor, string idempotencyKey);
        DataTable ConsumeMaterial(long orderMaterialId, long productionLotId, long materialLotId,
            int fromSlotId, decimal quantity, string actor, string idempotencyKey);
        DataTable PrepareLabels(long productionLotId, string operationNo, int targetQty, string actor);
        DataTable PrintLabels(long productionLotId, string operationNo, string actor, bool isReprint);
        DataTable ApplyLabel(string labelCode, string operationNo, string actor);
        DataTable VoidLabel(string labelCode, string actor, string reason);
    }
}
