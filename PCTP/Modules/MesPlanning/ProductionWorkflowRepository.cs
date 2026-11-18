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

        public DataTable Transition(string entityType, long entityId, long? relatedId, int fromStatus,
            int toStatus, string actor, string reason, string correlationId)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ProductionWorkflow_Transition", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 90;
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