using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Modules.MesPlanning
{
    public sealed class ProductionExecutionRepository : IProductionExecutionRepository
    {
        private readonly string _connectionString;

        public ProductionExecutionRepository(string connectionString = null)
        {
            _connectionString = connectionString ??
                ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public string GetPlanLineSiteCode(long planLineId)
        {
            return ScalarSite("SELECT SiteCode FROM MES.FVN_ProductionPlanLine WHERE ProductionPlanLineId=@Id", planLineId);
        }

        public string GetOrderSiteCode(long orderId)
        {
            return ScalarSite("SELECT SiteCode FROM MES.FVN_ProductionOrder WHERE ProductionOrderId=@Id", orderId);
        }

        public string GetLotSiteCode(long productionLotId)
        {
            return ScalarSite("SELECT SiteCode FROM MES.FVN_ProductionLot WHERE ProductionLotId=@Id", productionLotId);
        }

        public string GetLabelSiteCode(string labelCode)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(@"SELECT TOP(1) pl.SiteCode
                FROM MES.FVN_ProductionLabel l
                JOIN MES.FVN_ProductionLot pl ON pl.ProductionLotId=l.ProductionLotId
                WHERE l.LabelCode=@LabelCode", cn))
            {
                cmd.Parameters.Add("@LabelCode", SqlDbType.VarChar, 16).Value = labelCode;
                cn.Open();
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value);
            }
        }

        private string ScalarSite(string sql, long id)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
                cn.Open();
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value);
            }
        }

        public DataTable GetOrderMaterials(long orderId)
        {
            return Execute("MES.usp_FVN_ProductionOrder_MaterialList", cmd =>
                cmd.Parameters.Add("@ProductionOrderId", SqlDbType.BigInt).Value = orderId);
        }

        public DataTable CreateOrder(long planLineId, string orderNo, string actor)
        {
            return Execute("MES.usp_FVN_ProductionOrder_CreateFromPlan", cmd =>
            {
                cmd.Parameters.Add("@ProductionPlanLineId", SqlDbType.BigInt).Value = planLineId;
                cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 80).Value = orderNo;
                cmd.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = actor;
            });
        }

        public DataTable CreateLot(long orderId, DateTime producedAtUtc, decimal goodQty,
            decimal ngQty, bool finished, string actor)
        {
            return Execute("MES.usp_FVN_ProductionLot_Create", cmd =>
            {
                cmd.Parameters.Add("@ProductionOrderId", SqlDbType.BigInt).Value = orderId;
                cmd.Parameters.Add("@ProducedAtUtc", SqlDbType.DateTime2).Value = producedAtUtc;
                AddDecimal(cmd, "@GoodQty", goodQty);
                AddDecimal(cmd, "@NgQty", ngQty);
                cmd.Parameters.Add("@IsFinishedProduct", SqlDbType.Bit).Value = finished;
                cmd.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = actor;
            });
        }

        public DataTable CompleteOperation(long productionLotId, string operationNo, decimal inputQty,
            decimal goodQty, decimal ngQty, int? inputSlotId, int? outputSlotId,
            string actor, string idempotencyKey)
        {
            return Execute("MES.usp_FVN_ProductionOperation_Complete", cmd =>
            {
                cmd.Parameters.Add("@ProductionLotId", SqlDbType.BigInt).Value = productionLotId;
                cmd.Parameters.Add("@OperationNo", SqlDbType.NVarChar, 40).Value = operationNo;
                AddDecimal(cmd, "@InputQty", inputQty);
                AddDecimal(cmd, "@GoodQty", goodQty);
                AddDecimal(cmd, "@NgQty", ngQty);
                cmd.Parameters.Add("@InputSlotId", SqlDbType.Int).Value = (object)inputSlotId ?? DBNull.Value;
                cmd.Parameters.Add("@OutputSlotId", SqlDbType.Int).Value = (object)outputSlotId ?? DBNull.Value;
                cmd.Parameters.Add("@OperatorCode", SqlDbType.NVarChar, 100).Value = actor;
                cmd.Parameters.Add("@Remarks", SqlDbType.NVarChar, 500).Value = DBNull.Value;
                cmd.Parameters.Add("@IdempotencyKey", SqlDbType.NVarChar, 180).Value = idempotencyKey;
            });
        }

        public DataTable ConsumeMaterial(long orderMaterialId, long productionLotId, long materialLotId,
            int fromSlotId, decimal quantity, string actor, string idempotencyKey)
        {
            return Execute("MES.usp_FVN_ProductionMaterial_Consume", cmd =>
            {
                cmd.Parameters.Add("@ProductionOrderMaterialId", SqlDbType.BigInt).Value = orderMaterialId;
                cmd.Parameters.Add("@ProductionLotId", SqlDbType.BigInt).Value = productionLotId;
                cmd.Parameters.Add("@MaterialLotId", SqlDbType.BigInt).Value = materialLotId;
                cmd.Parameters.Add("@FromSlotId", SqlDbType.Int).Value = fromSlotId;
                AddDecimal(cmd, "@Quantity", quantity);
                cmd.Parameters.Add("@OperatorCode", SqlDbType.NVarChar, 100).Value = actor;
                cmd.Parameters.Add("@IdempotencyKey", SqlDbType.NVarChar, 180).Value = idempotencyKey;
                cmd.Parameters.Add("@QrRawValue", SqlDbType.NVarChar, 1000).Value = DBNull.Value;
            });
        }

        public DataTable PrepareLabels(long productionLotId, string operationNo, int targetQty, string actor)
        {
            return Execute("MES.usp_FVN_ProductionLabel_Prepare", cmd =>
            {
                cmd.Parameters.Add("@ProductionLotId", SqlDbType.BigInt).Value = productionLotId;
                cmd.Parameters.Add("@OperationNo", SqlDbType.NVarChar, 40).Value = operationNo;
                cmd.Parameters.Add("@TargetQty", SqlDbType.Int).Value = targetQty;
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
            });
        }

        public DataTable PrintLabels(long productionLotId, string operationNo, string actor, bool isReprint)
        {
            return Execute("MES.usp_FVN_ProductionLabel_Print", cmd =>
            {
                cmd.Parameters.Add("@ProductionLotId", SqlDbType.BigInt).Value = productionLotId;
                cmd.Parameters.Add("@OperationNo", SqlDbType.NVarChar, 40).Value = operationNo;
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                cmd.Parameters.Add("@IsReprint", SqlDbType.Bit).Value = isReprint;
            });
        }

        public DataTable ApplyLabel(string labelCode, string operationNo, string actor)
        {
            return Execute("MES.usp_FVN_ProductionLabel_Apply", cmd =>
            {
                cmd.Parameters.Add("@LabelCode", SqlDbType.VarChar, 16).Value = labelCode;
                cmd.Parameters.Add("@OperationNo", SqlDbType.NVarChar, 40).Value = operationNo;
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
            });
        }

        public DataTable VoidLabel(string labelCode, string actor, string reason)
        {
            return Execute("MES.usp_FVN_ProductionLabel_Void", cmd =>
            {
                cmd.Parameters.Add("@LabelCode", SqlDbType.VarChar, 16).Value = labelCode;
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 300).Value = reason;
            });
        }

        private DataTable Execute(string procedure, Action<SqlCommand> bind)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(procedure, connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 90;
                bind(command);
                var result = new DataTable();
                using (var adapter = new SqlDataAdapter(command))
                    adapter.Fill(result);
                return result;
            }
        }

        private static void AddDecimal(SqlCommand command, string name, decimal value)
        {
            var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
            parameter.Precision = 19;
            parameter.Scale = 6;
            parameter.Value = value;
        }
    }
}
