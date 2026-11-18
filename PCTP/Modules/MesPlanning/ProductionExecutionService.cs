using System;
using System.Data;

namespace PCTP.Modules.MesPlanning
{
    public sealed class ProductionExecutionService : IProductionExecutionService
    {
        private readonly IProductionExecutionRepository _repository;

        public ProductionExecutionService(IProductionExecutionRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public DataTable GetOrderMaterials(long orderId)
        {
            if (orderId <= 0) throw new ArgumentException("ProductionOrderId không hợp lệ.");
            return _repository.GetOrderMaterials(orderId);
        }

        public DataTable CreateOrder(long planLineId, string orderNo, string actor)
        {
            if (planLineId <= 0 || string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(actor))
                throw new ArgumentException("PlanLine, OrderNo và người tạo là bắt buộc.");
            return _repository.CreateOrder(planLineId, orderNo.Trim(), actor.Trim());
        }

        public DataTable CreateLot(long orderId, DateTime producedAtUtc, decimal goodQty,
            decimal ngQty, bool finished, string actor)
        {
            if (orderId <= 0 || goodQty <= 0 || ngQty < 0 || string.IsNullOrWhiteSpace(actor))
                throw new ArgumentException("Thông tin tạo LOT sản xuất không hợp lệ.");
            return _repository.CreateLot(orderId, producedAtUtc, goodQty, ngQty, finished, actor.Trim());
        }

        public DataTable CompleteOperation(long productionLotId, string operationNo, decimal inputQty,
            decimal goodQty, decimal ngQty, int? inputSlotId, int? outputSlotId, string actor)
        {
            if (productionLotId <= 0 || string.IsNullOrWhiteSpace(operationNo) ||
                inputQty <= 0 || goodQty < 0 || ngQty < 0 || goodQty + ngQty > inputQty ||
                string.IsNullOrWhiteSpace(actor))
                throw new ArgumentException("Thông tin hoàn thành công đoạn không hợp lệ.");
            return _repository.CompleteOperation(productionLotId, operationNo.Trim(), inputQty,
                goodQty, ngQty, inputSlotId, outputSlotId, actor.Trim(), Guid.NewGuid().ToString("N"));
        }

        public DataTable ConsumeMaterial(long orderMaterialId, long productionLotId, long materialLotId,
            int fromSlotId, decimal quantity, string actor)
        {
            if (orderMaterialId <= 0 || productionLotId <= 0 || materialLotId <= 0 ||
                fromSlotId <= 0 || quantity <= 0 || string.IsNullOrWhiteSpace(actor))
                throw new ArgumentException("Thông tin tiêu hao LOT linh kiện không hợp lệ.");
            return _repository.ConsumeMaterial(orderMaterialId, productionLotId, materialLotId,
                fromSlotId, quantity, actor.Trim(), Guid.NewGuid().ToString("N"));
        }
    }
}
