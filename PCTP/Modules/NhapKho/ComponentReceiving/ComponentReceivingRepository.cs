using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Modules.NhapKho.ComponentReceiving
{
    /// <summary>Persistence adapter for the MES-owned component receipt procedure.</summary>
    public sealed class ComponentReceivingRepository : IComponentReceivingRepository
    {
        private readonly string _connectionString;

        public ComponentReceivingRepository(string connectionString = null)
        {
            _connectionString = connectionString
                ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public ComponentReceiptResult Post(ComponentReceiptRequest request)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand("MES.usp_FVN_ComponentReceipt_Post", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 60;
                command.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = request.SiteCode;
                command.Parameters.Add("@ReceiptNo", SqlDbType.NVarChar, 80).Value = request.ReceiptNo;
                command.Parameters.Add("@QrPayload", SqlDbType.NVarChar, 500).Value = request.QrPayload;
                var quantity = command.Parameters.Add("@Quantity", SqlDbType.Decimal);
                quantity.Precision = 19;
                quantity.Scale = 6;
                quantity.Value = request.Quantity;
                command.Parameters.Add("@TargetSlotId", SqlDbType.Int).Value = request.TargetSlotId;
                command.Parameters.Add("@ReceivedBy", SqlDbType.NVarChar, 100).Value = request.ReceivedBy;
                command.Parameters.Add("@IdempotencyKey", SqlDbType.NVarChar, 180).Value = request.IdempotencyKey;

                connection.Open();
                using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("Thủ tục nhận linh kiện không trả về kết quả.");
                    return new ComponentReceiptResult
                    {
                        ComponentReceiptId = Convert.ToInt64(reader["ComponentReceiptId"]),
                        ComponentReceiptLineId = Convert.ToInt64(reader["ComponentReceiptLineId"]),
                        InventoryTransactionId = Convert.ToInt64(reader["InventoryTransactionId"]),
                        ComponentLotId = Convert.ToInt64(reader["ComponentLotId"]),
                        MaterialLotId = Convert.ToInt64(reader["MaterialLotId"]),
                        ReceivedQty = Convert.ToDecimal(reader["ReceivedQty"])
                    };
                }
            }
        }
    }
}