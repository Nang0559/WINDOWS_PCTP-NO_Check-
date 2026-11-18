using System;
using System.Data;

namespace PCTP.Modules.MesPlanning
{
    public interface IProductionExecutionService
    {
        DataTable GetOrderMaterials(long orderId);
        DataTable CreateOrder(long planLineId, string orderNo, string actor);
        DataTable CreateLot(long orderId, DateTime producedAtUtc, decimal goodQty,
            decimal ngQty, bool finished, string actor);
        DataTable CompleteOperation(long productionLotId, string operationNo, decimal inputQty,
            decimal goodQty, decimal ngQty, int? inputSlotId, int? outputSlotId, string actor);
        DataTable ConsumeMaterial(long orderMaterialId, long productionLotId, long materialLotId,
            int fromSlotId, decimal quantity, string actor);
    }
}
