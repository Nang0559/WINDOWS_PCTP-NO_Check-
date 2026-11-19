using System;
using PCTP.Infrastructure.Security;
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
            AuthorizationGuard.Demand("MES.PRODUCTION_EXECUTION", "VIEW");
            if (orderId <= 0) throw new ArgumentException("ProductionOrderId không hợp lệ.");
            return _repository.GetOrderMaterials(orderId);
        }

        public DataTable CreateOrder(long planLineId, string orderNo, string actor)
        {
            AuthorizationGuard.Demand("MES.PRODUCTION_EXECUTION", "CREATE");
            if (planLineId <= 0 || string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(actor))
                throw new ArgumentException("PlanLine, OrderNo và người tạo là bắt buộc.");
            return _repository.CreateOrder(planLineId, orderNo.Trim(), actor.Trim());
        }

        public DataTable CreateLot(long orderId, DateTime producedAtUtc, decimal goodQty,
            decimal ngQty, bool finished, string actor)
        {
            AuthorizationGuard.Demand("MES.PRODUCTION_EXECUTION", "CREATE");
            if (orderId <= 0 || goodQty <= 0 || ngQty < 0 || string.IsNullOrWhiteSpace(actor))
                throw new ArgumentException("Thông tin tạo LOT sản xuất không hợp lệ.");
            return _repository.CreateLot(orderId, producedAtUtc, goodQty, ngQty, finished, actor.Trim());
        }

        public DataTable CompleteOperation(long productionLotId, string operationNo, decimal inputQty,
            decimal goodQty, decimal ngQty, int? inputSlotId, int? outputSlotId, string actor)
        {
            AuthorizationGuard.Demand("MES.PRODUCTION_EXECUTION", "CONFIRM");
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
            AuthorizationGuard.Demand("MES.PRODUCTION_EXECUTION", "CONFIRM");
            if (orderMaterialId <= 0 || productionLotId <= 0 || materialLotId <= 0 ||
                fromSlotId <= 0 || quantity <= 0 || string.IsNullOrWhiteSpace(actor))
                throw new ArgumentException("Thông tin tiêu hao LOT linh kiện không hợp lệ.");
            return _repository.ConsumeMaterial(orderMaterialId, productionLotId, materialLotId,
                fromSlotId, quantity, actor.Trim(), Guid.NewGuid().ToString("N"));
        }
        public DataTable PrepareLabels(long productionLotId, string operationNo, int targetQty, string actor)
        {
            AuthorizationGuard.Demand("MES.PRODUCTION_EXECUTION", "CREATE");
            if (productionLotId <= 0 || string.IsNullOrWhiteSpace(operationNo) ||
                targetQty <= 0 || string.IsNullOrWhiteSpace(actor))
                throw new ArgumentException("LOT, công đoạn, số lượng tem và người thao tác là bắt buộc.");
            return _repository.PrepareLabels(productionLotId, operationNo.Trim(), targetQty, actor.Trim());
        }

        public DataTable PrintLabels(long productionLotId, string operationNo, string actor, bool isReprint)
        {
            AuthorizationGuard.Demand("MES.PRODUCTION_EXECUTION", "PRINT");
            if (productionLotId <= 0 || string.IsNullOrWhiteSpace(operationNo) || string.IsNullOrWhiteSpace(actor))
                throw new ArgumentException("LOT, công đoạn và người in là bắt buộc.");
            return _repository.PrintLabels(productionLotId, operationNo.Trim(), actor.Trim(), isReprint);
        }

        public DataTable ApplyLabel(string labelCode, string operationNo, string actor)
        {
            AuthorizationGuard.Demand("MES.PRODUCTION_EXECUTION", "CONFIRM");
            if (string.IsNullOrWhiteSpace(labelCode) || string.IsNullOrWhiteSpace(operationNo) ||
                string.IsNullOrWhiteSpace(actor))
                throw new ArgumentException("Mã tem, công đoạn và người thao tác là bắt buộc.");
            var token = labelCode.Trim();
            if (token.StartsWith("FVN2|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = token.Split('|');
                if (parts.Length != 4 || !parts[1].StartsWith("L", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("QR tem MES không đúng định dạng FVN2|LabelCode|LOT|OperationNo.");
                token = parts[1];
                if (!string.Equals(parts[3], operationNo.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("QR tem thuộc công đoạn khác với công đoạn đang thao tác.");
            }
            return _repository.ApplyLabel(token, operationNo.Trim(), actor.Trim());
        }

        public DataTable VoidLabel(string labelCode, string actor, string reason)
        {
            AuthorizationGuard.Demand("MES.PRODUCTION_EXECUTION", "CANCEL");
            if (string.IsNullOrWhiteSpace(labelCode) || string.IsNullOrWhiteSpace(actor) ||
                string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Mã tem, người thao tác và lý do hủy là bắt buộc.");
            var token = labelCode.Trim();
            if (token.StartsWith("FVN2|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = token.Split('|');
                if (parts.Length != 4 || !parts[1].StartsWith("L", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("QR tem MES không đúng định dạng FVN2|LabelCode|LOT|OperationNo.");
                token = parts[1];
            }
            return _repository.VoidLabel(token, actor.Trim(), reason.Trim());
        }

    }
}
