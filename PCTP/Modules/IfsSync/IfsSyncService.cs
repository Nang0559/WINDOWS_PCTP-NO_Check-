using Newtonsoft.Json;
using PCTP.ClassSQL;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Oracle.ManagedDataAccess.Client;

namespace PCTP.Modules.IfsSync
{
    public sealed class IfsSyncResult
    {
        public long SyncBatchId { get; internal set; }
        public int PartCatalogRows { get; internal set; }
        public int InventoryPartRows { get; internal set; }
        public int BomHeaderRows { get; internal set; }
        public int BomAlternativeRows { get; internal set; }
        public int BomComponentRows { get; internal set; }
        public string Status { get; internal set; }
    }

    /// <summary>
    /// One-way IFSAPP view -> FVN_PCTP staging -> normalized IFS snapshot.
    /// It intentionally never uses SQLPROVIDER.CreateConnection(), because that
    /// legacy factory is pinned to B7R2_FCC.
    /// </summary>
    public sealed class IfsSyncService
    {
        private readonly IFSPROVIDER _ifs;
        private readonly string _targetConnectionString;

        public IfsSyncService()
            : this(
                ConfigurationManager.ConnectionStrings["IFS_ORACLE"]?.ConnectionString,
                ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString)
        {
        }

        public IfsSyncService(string oracleConnectionString, string targetSqlConnectionString)
        {
            if (string.IsNullOrWhiteSpace(oracleConnectionString))
                throw new InvalidOperationException("Thiếu connection string IFS_ORACLE.");
            if (string.IsNullOrWhiteSpace(targetSqlConnectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");

            if (targetSqlConnectionString.IndexOf("YOUR_SQL_", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("Chưa cấu hình connection string FVN_PCTP trong PCTP.Database.config.");

            var target = new SqlConnectionStringBuilder(targetSqlConnectionString);
            if (!string.Equals(target.InitialCatalog, "FVN_PCTP", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Từ chối đồng bộ: Initial Catalog phải là FVN_PCTP, không được ghi vào B7R2_FCC.");

            _ifs = new IFSPROVIDER(oracleConnectionString);
            _targetConnectionString = targetSqlConnectionString;
        }

        public IfsSyncResult SyncAll()
        {
            using (var connection = new SqlConnection(_targetConnectionString))
            {
                connection.Open();
                long batchId = BeginBatch(connection);

                try
                {
                    int partCatalog = StageEntity(connection, batchId, "PART_CATALOG",
                        "SELECT * FROM IFSAPP.PART_CATALOG ORDER BY PART_NO");
                    int inventoryPart = StageEntity(connection, batchId, "INVENTORY_PART",
                        "SELECT * FROM IFSAPP.INVENTORY_PART ORDER BY CONTRACT, PART_NO");
                    int bomHeader = StageEntity(connection, batchId, "BOM_HEADER",
                        "SELECT * FROM IFSAPP.MANUF_STRUCTURE_HEAD ORDER BY CONTRACT, PART_NO, ENG_CHG_LEVEL, BOM_TYPE_DB");
                    int bomAlternative = StageEntity(connection, batchId, "BOM_ALTERNATIVE",
                        "SELECT * FROM IFSAPP.MANUF_STRUCT_ALTERNATE ORDER BY CONTRACT, PART_NO, ENG_CHG_LEVEL, BOM_TYPE_DB, ALTERNATIVE_NO");
                    int bomComponent = StageEntity(connection, batchId, "BOM_COMPONENT",
                        "SELECT * FROM IFSAPP.MANUF_STRUCTURE ORDER BY CONTRACT, PART_NO, ENG_CHG_LEVEL, BOM_TYPE_DB, ALTERNATIVE_NO, LINE_ITEM_NO");

                    using (var command = new SqlCommand(@"
UPDATE IFS.SyncBatch
SET Status='STAGED',
    PartCatalogRows=@PartCatalogRows,
    InventoryPartRows=@InventoryPartRows,
    BomHeaderRows=@BomHeaderRows,
    BomAlternativeRows=@BomAlternativeRows,
    BomComponentRows=@BomComponentRows
WHERE SyncBatchId=@SyncBatchId AND Status='RUNNING';", connection))
                    {
                        command.Parameters.AddWithValue("@PartCatalogRows", partCatalog);
                        command.Parameters.AddWithValue("@InventoryPartRows", inventoryPart);
                        command.Parameters.AddWithValue("@BomHeaderRows", bomHeader);
                        command.Parameters.AddWithValue("@BomAlternativeRows", bomAlternative);
                        command.Parameters.AddWithValue("@BomComponentRows", bomComponent);
                        command.Parameters.AddWithValue("@SyncBatchId", batchId);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("Không thể chuyển SyncBatch sang STAGED.");
                    }

                    using (var command = new SqlCommand("IFS.usp_ApplySyncBatch", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 1800;
                        command.Parameters.AddWithValue("@SyncBatchId", batchId);
                        command.ExecuteNonQuery();
                    }

                    return new IfsSyncResult
                    {
                        SyncBatchId = batchId,
                        PartCatalogRows = partCatalog,
                        InventoryPartRows = inventoryPart,
                        BomHeaderRows = bomHeader,
                        BomAlternativeRows = bomAlternative,
                        BomComponentRows = bomComponent,
                        Status = "APPLIED"
                    };
                }
                catch (Exception ex)
                {
                    try
                    {
                        using (var command = new SqlCommand(@"
UPDATE IFS.SyncBatch
SET Status='FAILED', CompletedAtUtc=SYSUTCDATETIME(),
    ErrorSummary=@ErrorSummary
WHERE SyncBatchId=@SyncBatchId AND Status<>'APPLIED';", connection))
                        {
                            command.Parameters.Add("@ErrorSummary", SqlDbType.NVarChar, 2000).Value =
                                ex.ToString().Length > 2000 ? ex.ToString().Substring(0, 2000) : ex.ToString();
                            command.Parameters.AddWithValue("@SyncBatchId", batchId);
                            command.ExecuteNonQuery();
                        }
                        using (var error = new SqlCommand(@"
INSERT IFS.SyncError(SyncBatchId, ErrorCode, ErrorMessage, ErrorDetails, LoggedAtUtc)
VALUES(@SyncBatchId, 'SYNC_FAILED', @ErrorMessage, @ErrorDetails, SYSUTCDATETIME());", connection))
                        {
                            error.Parameters.AddWithValue("@SyncBatchId", batchId);
                            error.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, 2000).Value =
                                ex.Message.Length > 2000 ? ex.Message.Substring(0, 2000) : ex.Message;
                            error.Parameters.Add("@ErrorDetails", SqlDbType.NVarChar, -1).Value = ex.ToString();
                            error.ExecuteNonQuery();
                        }
                    }
                    catch
                    {
                        // Preserve the original synchronization exception.
                    }
                    throw;
                }
            }
        }

        private static long BeginBatch(SqlConnection connection)
        {
            using (var command = new SqlCommand(@"
INSERT IFS.SyncBatch
    (SyncMode, Status, StartedAtUtc, InitiatedBy)
OUTPUT INSERTED.SyncBatchId
VALUES ('FULL', 'RUNNING', SYSUTCDATETIME(), @InitiatedBy);", connection))
            {
                command.Parameters.Add("@InitiatedBy", SqlDbType.NVarChar, 128).Value =
                    Environment.UserDomainName + "\\" + Environment.UserName;
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private int StageEntity(SqlConnection connection, long batchId, string entityName, string oracleSql)
        {
            var stage = CreateStageTable();
            int count = 0;
            try
            {
                // Stream Oracle rows in bounded chunks. A large INVENTORY_PART view
                // therefore does not have to fit into one in-memory DataTable.
                _ifs.ExecuteReaderStrict(oracleSql, reader =>
                {
                    while (reader.Read())
                    {
                        AddStageRow(stage, batchId, entityName, reader);
                        count++;
                        if (stage.Rows.Count >= 2000)
                        {
                            BulkStage(connection, stage);
                            stage.Clear();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Đọc/stage IFS entity " + entityName + " thất bại: " + ex.Message, ex);
            }

            if (count == 0)
                throw new InvalidOperationException("IFS view " + entityName +
                    " trả về 0 dòng. Batch bị dừng để tránh hiểu nhầm lỗi quyền/kết nối là dữ liệu rỗng.");

            if (stage.Rows.Count > 0)
                BulkStage(connection, stage);
            return count;
        }

        private static DataTable CreateStageTable()
        {
            var table = new DataTable();
            table.Columns.Add("SyncBatchId", typeof(long));
            table.Columns.Add("EntityName", typeof(string));
            table.Columns.Add("SiteCode", typeof(string));
            table.Columns.Add("PartNo", typeof(string));
            table.Columns.Add("EngChgLevel", typeof(string));
            table.Columns.Add("BomTypeDb", typeof(string));
            table.Columns.Add("AlternativeNo", typeof(string));
            table.Columns.Add("LineItemNo", typeof(decimal));
            table.Columns.Add("SourceRowKey", typeof(string));
            table.Columns.Add("SourceObjVersion", typeof(string));
            table.Columns.Add("PayloadJson", typeof(string));
            table.Columns.Add("SourceHash", typeof(byte[]));
            return table;
        }

        private static void BulkStage(SqlConnection connection, DataTable stage)
        {
            // SqlBulkCopy asks SQL Server for destination-column collation metadata.
            // Use an explicitly database-qualified destination so metadata lookup
            // cannot resolve against a different current database/context.
            string databaseName;
            using (var check = new SqlCommand("SELECT DB_NAME();", connection))
            {
                databaseName = Convert.ToString(check.ExecuteScalar());
            }

            if (!string.Equals(databaseName, "FVN_PCTP", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Từ chối stage IFS: database hiện tại là '" + (databaseName ?? "(NULL)") +
                    "', yêu cầu FVN_PCTP.");

            // Probe the actual destination table instead of OBJECT_ID, which can
            // return NULL for a valid table when metadata visibility is restricted.
            try
            {
                using (var check = new SqlCommand(
                    "SELECT TOP (0) SyncStageRowId FROM [IFS].[SyncStageRow];", connection))
                {
                    check.ExecuteNonQuery();
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 208)
                    throw new InvalidOperationException(
                        "Không tìm thấy bảng [IFS].[SyncStageRow] trong FVN_PCTP. " +
                        "Kiểm tra migration 012. SQL: " + ex.Message, ex);
                if (ex.Number == 229)
                    throw new InvalidOperationException(
                        "Tài khoản SQL của ứng dụng thiếu quyền SELECT trên [IFS].[SyncStageRow]. " +
                        "SQL: " + ex.Message, ex);
                throw;
            }

            using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null))
            {
                bulk.DestinationTableName = "[FVN_PCTP].[IFS].[SyncStageRow]";
                bulk.BatchSize = 2000;
                bulk.BulkCopyTimeout = 1800;
                foreach (DataColumn column in stage.Columns)
                    bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                bulk.WriteToServer(stage);
            }
        }

        private static void AddStageRow(DataTable stage, long batchId, string entityName, OracleDataReader reader)
        {
            var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
                payload[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);

            string json = JsonConvert.SerializeObject(payload, Formatting.None);
            byte[] hash;
            using (SHA256 sha = SHA256.Create())
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));

            string partNo = ReadString(reader, "PART_NO");
            if (string.IsNullOrWhiteSpace(partNo))
                throw new InvalidOperationException(entityName + " có dòng thiếu PART_NO.");

            string siteCode = entityName == "PART_CATALOG" ? null : ReadString(reader, "CONTRACT");
            if (entityName != "PART_CATALOG" && string.IsNullOrWhiteSpace(siteCode))
                throw new InvalidOperationException(entityName + " có dòng thiếu CONTRACT.");

            string engChgLevel = ReadString(reader, "ENG_CHG_LEVEL");
            string bomTypeDb = ReadString(reader, "BOM_TYPE_DB");
            string alternativeNo = ReadString(reader, "ALTERNATIVE_NO");
            object lineItem = DBNull.Value;

            if (entityName.StartsWith("BOM_", StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(engChgLevel) || string.IsNullOrWhiteSpace(bomTypeDb)))
                throw new InvalidOperationException(entityName + " thiếu ENG_CHG_LEVEL hoặc BOM_TYPE_DB.");

            if ((entityName == "BOM_ALTERNATIVE" || entityName == "BOM_COMPONENT") &&
                string.IsNullOrWhiteSpace(alternativeNo))
                throw new InvalidOperationException(entityName + " có dòng thiếu ALTERNATIVE_NO.");

            if (entityName == "BOM_COMPONENT")
            {
                string lineItemText = ReadString(reader, "LINE_ITEM_NO");
                if (string.IsNullOrWhiteSpace(lineItemText))
                    throw new InvalidOperationException("MANUF_STRUCTURE có dòng thiếu LINE_ITEM_NO.");
                lineItem = Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("LINE_ITEM_NO")), CultureInfo.InvariantCulture);
            }

            stage.Rows.Add(
                batchId, entityName, DbValue(siteCode), partNo,
                DbValue(engChgLevel), DbValue(bomTypeDb), DbValue(alternativeNo),
                lineItem, DbValue(ReadString(reader, "OBJKEY")),
                DbValue(ReadString(reader, "OBJVERSION")), json, hash);
        }

        private static string ReadString(OracleDataReader reader, string column)
        {
            int ordinal;
            try { ordinal = reader.GetOrdinal(column); }
            catch (IndexOutOfRangeException) { return null; }
            return reader.IsDBNull(ordinal)
                ? null
                : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static object DbValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value;
        }
    }
}
