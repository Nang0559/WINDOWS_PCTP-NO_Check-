using Newtonsoft.Json;
using PCTP.ClassSQL;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
                    Environment.UserDomainName + "\" + Environment.UserName;
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private int StageEntity(SqlConnection connection, long batchId, string entityName, string oracleSql)
        {
            // Strict method throws on Oracle errors. Empty results are rejected so an
            // unavailable/unauthorized IFS view cannot masquerade as a successful sync.
            DataTable source = _ifs.ExecuteQueryStrict(oracleSql);
            if (source.Rows.Count == 0)
                throw new InvalidOperationException("IFS view " + entityName +
                    " trả về 0 dòng. Batch bị dừng để tránh hiểu nhầm lỗi quyền/kết nối là dữ liệu rỗng.");

            DataTable stage = BuildStageRows(batchId, entityName, source);
            using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null))
            {
                bulk.DestinationTableName = "IFS.SyncStageRow";
                bulk.BatchSize = 2000;
                bulk.BulkCopyTimeout = 1800;
                foreach (DataColumn column in stage.Columns)
                    bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                bulk.WriteToServer(stage);
            }
            return source.Rows.Count;
        }

        private static DataTable BuildStageRows(long batchId, string entityName, DataTable source)
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

            foreach (DataRow row in source.Rows)
            {
                var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (DataColumn column in source.Columns)
                    payload[column.ColumnName] = row[column] == DBNull.Value ? null : row[column];

                string json = JsonConvert.SerializeObject(payload, Formatting.None);
                byte[] hash;
                using (SHA256 sha = SHA256.Create())
                    hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));

                string partNo = ReadString(row, "PART_NO");
                if (string.IsNullOrWhiteSpace(partNo))
                    throw new InvalidOperationException(entityName + " có dòng thiếu PART_NO.");

                object lineItem = DBNull.Value;
                string siteCode = ReadString(row, "CONTRACT");
                if (entityName != "PART_CATALOG" && string.IsNullOrWhiteSpace(siteCode))
                    throw new InvalidOperationException(entityName + " có dòng thiếu CONTRACT.");

                if (entityName.StartsWith("BOM_", StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(ReadString(row, "ENG_CHG_LEVEL")) ||
                        string.IsNullOrWhiteSpace(ReadString(row, "BOM_TYPE_DB")))
                        throw new InvalidOperationException(entityName + " thiếu ENG_CHG_LEVEL hoặc BOM_TYPE_DB.");
                }
                if (entityName == "BOM_ALTERNATIVE" || entityName == "BOM_COMPONENT")
                {
                    if (string.IsNullOrWhiteSpace(ReadString(row, "ALTERNATIVE_NO")))
                        throw new InvalidOperationException(entityName + " có dòng thiếu ALTERNATIVE_NO.");
                }
                if (entityName == "BOM_COMPONENT")
                {
                    object value = ReadValue(row, "LINE_ITEM_NO");
                    if (value == null)
                        throw new InvalidOperationException("MANUF_STRUCTURE có dòng thiếu LINE_ITEM_NO.");
                    lineItem = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                }

                table.Rows.Add(
                    batchId,
                    entityName,
                    DbValue(siteCode),
                    partNo,
                    DbValue(ReadString(row, "ENG_CHG_LEVEL")),
                    DbValue(ReadString(row, "BOM_TYPE_DB")),
                    DbValue(ReadString(row, "ALTERNATIVE_NO")),
                    lineItem,
                    DbValue(ReadString(row, "OBJKEY")),
                    DbValue(ReadString(row, "OBJVERSION")),
                    json,
                    hash);
            }
            return table;
        }

        private static object ReadValue(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && row[column] != DBNull.Value
                ? row[column] : null;
        }

        private static string ReadString(DataRow row, string column)
        {
            object value = ReadValue(row, column);
            return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static object DbValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value;
        }
    }
}
