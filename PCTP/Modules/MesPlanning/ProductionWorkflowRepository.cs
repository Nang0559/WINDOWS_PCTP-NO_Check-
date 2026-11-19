using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Modules.MesPlanning
{
    public sealed class ProductionWorkflowRepository : IProductionWorkflowRepository
    {
        private readonly string _connectionString;
        public ProductionWorkflowRepository(string connectionString = null)
        {
            _connectionString = connectionString ??
                ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public int GetCurrentStatus(string entityType, long entityId, long? relatedId)
        {
            string sql;
            if (string.Equals(entityType, "PLAN", StringComparison.OrdinalIgnoreCase))
                sql = "SELECT WorkflowStatusCode FROM MES.FVN_ProductionPlan WHERE ProductionPlanId=@EntityId";
            else if (string.Equals(entityType, "ORDER", StringComparison.OrdinalIgnoreCase))
                sql = "SELECT WorkflowStatusCode FROM MES.FVN_ProductionOrder WHERE ProductionOrderId=@EntityId";
            else if (string.Equals(entityType, "OPERATION", StringComparison.OrdinalIgnoreCase))
                sql = "SELECT StatusCode FROM MES.FVN_ProductionOperationWorkflow WHERE ProductionLotId=@EntityId AND RoutingStepId=@RelatedId";
            else throw new ArgumentException("EntityType chỉ nhận PLAN, ORDER hoặc OPERATION.");

            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add("@EntityId", SqlDbType.BigInt).Value = entityId;
                cmd.Parameters.Add("@RelatedId", SqlDbType.BigInt).Value = (object)relatedId ?? DBNull.Value;
                cn.Open();
                var value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("Không tìm thấy trạng thái workflow của đối tượng.");
                return Convert.ToInt32(value);
            }
        }

        public DataTable GetEntitySites(string entityType, long entityId)
        {
            string sql;
            if (string.Equals(entityType, "PLAN", StringComparison.OrdinalIgnoreCase))
                sql = "SELECT DISTINCT SiteCode FROM MES.FVN_ProductionPlanLine WHERE ProductionPlanId=@EntityId";
            else if (string.Equals(entityType, "ORDER", StringComparison.OrdinalIgnoreCase))
                sql = "SELECT SiteCode FROM MES.FVN_ProductionOrder WHERE ProductionOrderId=@EntityId";
            else if (string.Equals(entityType, "OPERATION", StringComparison.OrdinalIgnoreCase))
                sql = "SELECT SiteCode FROM MES.FVN_ProductionLot WHERE ProductionLotId=@EntityId";
            else throw new ArgumentException("EntityType chỉ nhận PLAN, ORDER hoặc OPERATION.");

            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add("@EntityId", SqlDbType.BigInt).Value = entityId;
                var result = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(result);
                return result;
            }
        }

        public DataTable Transition(string entityType, long entityId, long? relatedId, int fromStatus,
            int toStatus, string actor, string reason, string correlationId)
        {
            bool firmPlan = string.Equals(entityType, "PLAN", StringComparison.OrdinalIgnoreCase) && toStatus == 20;
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(firmPlan
                ? "MES.usp_FVN_ProductionPlan_Firm"
                : "MES.usp_FVN_ProductionWorkflow_Transition", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 120;
                if (firmPlan)
                {
                    cmd.Parameters.Add("@ProductionPlanId", SqlDbType.BigInt).Value = entityId;
                    cmd.Parameters.Add("@FromStatus", SqlDbType.Int).Value = fromStatus;
                    cmd.Parameters.Add("@ToStatus", SqlDbType.Int).Value = toStatus;
                    cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                    cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 500).Value = (object)reason ?? DBNull.Value;
                    Guid firmCorrelation;
                    cmd.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value =
                        Guid.TryParse(correlationId, out firmCorrelation) ? (object)firmCorrelation : DBNull.Value;
                }
                else
                {
                    cmd.Parameters.Add("@EntityType", SqlDbType.VarChar, 20).Value = entityType;
                    cmd.Parameters.Add("@EntityId", SqlDbType.BigInt).Value = entityId;
                    cmd.Parameters.Add("@RelatedId", SqlDbType.BigInt).Value = (object)relatedId ?? DBNull.Value;
                    cmd.Parameters.Add("@FromStatus", SqlDbType.Int).Value = fromStatus;
                    cmd.Parameters.Add("@ToStatus", SqlDbType.Int).Value = toStatus;
                    cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                    cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 500).Value = (object)reason ?? DBNull.Value;
                    Guid correlation;
                    cmd.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value =
                        Guid.TryParse(correlationId, out correlation) ? (object)correlation : DBNull.Value;
                }
                var result = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(result);
                return result;
            }
        }

        public DataTable GetTimeline(string entityType, long entityId)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ProductionWorkflow_Timeline", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@EntityType", SqlDbType.VarChar, 20).Value = entityType;
                cmd.Parameters.Add("@EntityId", SqlDbType.BigInt).Value = entityId;
                var result = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(result);
                return result;
            }
        }
    }
}