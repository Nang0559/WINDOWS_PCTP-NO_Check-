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
        public int WarehouseRows { get; internal set; }
        public int SupplierRows { get; internal set; }
        public int ProductionLineRows { get; internal set; }
        public int SiteRows { get; internal set; }
        public int WorkCenterRows { get; internal set; }
        public int InventoryLocationRows { get; internal set; }
        public string Status { get; internal set; }
    }

    /// <summary>
    /// One-way IFSAPP view -> FVN_PCTP staging -> normalized IFS snapshot.
    /// It intentionally never uses SQLPROVIDER.CreateConnection(), because that
    /// legacy factory is pinned to B7R2_FCC.
    /// </summary>
    public sealed class IfsSyncService
    {
        private readonly string _oracleConnectionString;
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

            _oracleConnectionString = oracleConnectionString;
            _targetConnectionString = targetSqlConnectionString;
        }

        public IfsSyncResult SyncAll()
        {
            return SyncAll(null);
        }

        private static readonly IDictionary<string, string> EntityQueries =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "PART_CATALOG", "SELECT * FROM IFSAPP.PART_CATALOG ORDER BY PART_NO" },
                { "INVENTORY_PART", "SELECT * FROM IFSAPP.INVENTORY_PART ORDER BY CONTRACT, PART_NO" },
                { "BOM_HEADER", "SELECT * FROM IFSAPP.MANUF_STRUCTURE_HEAD ORDER BY CONTRACT, PART_NO, ENG_CHG_LEVEL, BOM_TYPE_DB" },
                { "BOM_ALTERNATIVE", "SELECT * FROM IFSAPP.MANUF_STRUCT_ALTERNATE ORDER BY CONTRACT, PART_NO, ENG_CHG_LEVEL, BOM_TYPE_DB, ALTERNATIVE_NO" },
                { "BOM_COMPONENT", "SELECT * FROM IFSAPP.MANUF_STRUCTURE ORDER BY CONTRACT, PART_NO, ENG_CHG_LEVEL, BOM_TYPE_DB, ALTERNATIVE_NO, LINE_ITEM_NO" },
                { "WAREHOUSE", "SELECT CONTRACT, WAREHOUSE_ID, DESCRIPTION, OBJKEY, OBJVERSION FROM IFSAPP.WAREHOUSE ORDER BY CONTRACT, WAREHOUSE_ID" },
                // Location kho IFS (bin/location). Location IFS chỉ chọn trong bảng này, ở cấp Kho PCTP (IFS không có Rack / Slot).
                // SELECT * để PayloadJson giữ đủ cột; IFS.usp_ApplyInventoryLocationBatch (migration 084) đọc các khóa JSON đã biết.
                { "INVENTORY_LOCATION", "SELECT * FROM IFSAPP.INVENTORY_LOCATION ORDER BY CONTRACT, LOCATION_NO" },
                { "SUPPLIER", "SELECT SUPPLIER_ID, NAME, OBJKEY, OBJVERSION FROM IFSAPP.SUPPLIER_INFO_GENERAL ORDER BY SUPPLIER_ID" },
                // Logical Unit ProductionLine (frmProdLineMgt). SELECT * giữ nguyên mọi cột IFS trong PayloadJson.
                { "PRODUCTION_LINE", "SELECT * FROM IFSAPP.PRODUCTION_LINE ORDER BY CONTRACT, PRODUCTION_LINE" },
                // Site và Work Center (= bộ phận sản xuất PCTP). SELECT * để payload JSON giữ đủ cột IFS;
                // IFS.usp_ApplySiteWorkCenterBatch chỉ đọc các khóa JSON đã biết và bỏ qua phần còn lại.
                { "SITE", "SELECT * FROM IFSAPP.SITE ORDER BY CONTRACT" },
                { "WORK_CENTER", "SELECT * FROM IFSAPP.WORK_CENTER ORDER BY CONTRACT, WORK_CENTER_NO" }
            };

        public IfsSyncResult SyncAll(Action<string> phaseChanged)
        {
            return SyncEntities(new List<string>(EntityQueries.Keys), "FULL", phaseChanged);
        }

        public IfsSyncResult SyncSelected(IEnumerable<string> selectedEntities, Action<string> phaseChanged)
        {
            if (selectedEntities == null)
                throw new ArgumentNullException("selectedEntities");

            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string entity in selectedEntities)
            {
                if (string.IsNullOrWhiteSpace(entity)) continue;
                string normalized = entity.Trim().ToUpperInvariant();
                if (!EntityQueries.ContainsKey(normalized))
                    throw new ArgumentException("Entity IFS không được hỗ trợ: " + entity, "selectedEntities");
                selected.Add(normalized);
            }

            if (selected.Count == 0)
                throw new InvalidOperationException("Hãy chọn ít nhất một nhóm dữ liệu IFS.");

            // Preserve BOM foreign-key dependencies. Selecting a child automatically
            // includes its parents so the same batch remains transactionally applicable.
            // Mỗi Location phải có Warehouse IFS (FK_MES_IfsLoc_Wh): chọn INVENTORY_LOCATION tự kèm WAREHOUSE.
            if (selected.Contains("INVENTORY_LOCATION"))
                selected.Add("WAREHOUSE");

            if (selected.Contains("BOM_COMPONENT"))
            {
                selected.Add("BOM_ALTERNATIVE");
                selected.Add("BOM_HEADER");
            }
            else if (selected.Contains("BOM_ALTERNATIVE"))
            {
                selected.Add("BOM_HEADER");
            }

            var ordered = new List<string>();
            foreach (string entity in EntityQueries.Keys)
                if (selected.Contains(entity)) ordered.Add(entity);

            return SyncEntities(ordered, "SELECTIVE", phaseChanged);
        }

        private IfsSyncResult SyncEntities(IList<string> entities, string syncMode, Action<string> phaseChanged)
        {
            using (var connection = new SqlConnection(_targetConnectionString))
            {
                connection.Open();
                string selectedEntities = string.Join(",", entities);
                long batchId = BeginBatch(connection, syncMode, selectedEntities);
                string currentEntity = null;
                bool locationApplyFailedAfterBase = false;
                var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    if (phaseChanged != null)
                        phaseChanged("Giai đoạn 1/2: Đang đọc " + entities.Count +
                            " nhóm Oracle IFS và ghi dữ liệu vào staging...");
                    foreach (string entity in entities)
                    {
                        currentEntity = entity;
                        if (phaseChanged != null)
                            phaseChanged("Đang đồng bộ " + entity + " (" +
                                (entities.IndexOf(entity) + 1) + "/" + entities.Count + ")...");
                        counts[entity] = StageEntity(connection, batchId, entity, EntityQueries[entity]);
                    }

                    using (var command = new SqlCommand(@"
UPDATE IFS.FVN_SyncBatch
SET Status='STAGED',
    PartCatalogRows=@PartCatalogRows,
    InventoryPartRows=@InventoryPartRows,
    BomHeaderRows=@BomHeaderRows,
    BomAlternativeRows=@BomAlternativeRows,
    BomComponentRows=@BomComponentRows,
    WarehouseRows=@WarehouseRows,
    SupplierRows=@SupplierRows,
    ProductionLineRows=@ProductionLineRows,
    SiteRows=@SiteRows,
    WorkCenterRows=@WorkCenterRows,
    InventoryLocationRows=@InventoryLocationRows
WHERE SyncBatchId=@SyncBatchId AND Status='RUNNING';", connection))
                    {
                        command.Parameters.AddWithValue("@PartCatalogRows", GetCount(counts, "PART_CATALOG"));
                        command.Parameters.AddWithValue("@InventoryPartRows", GetCount(counts, "INVENTORY_PART"));
                        command.Parameters.AddWithValue("@BomHeaderRows", GetCount(counts, "BOM_HEADER"));
                        command.Parameters.AddWithValue("@BomAlternativeRows", GetCount(counts, "BOM_ALTERNATIVE"));
                        command.Parameters.AddWithValue("@BomComponentRows", GetCount(counts, "BOM_COMPONENT"));
                        command.Parameters.AddWithValue("@WarehouseRows", GetCount(counts, "WAREHOUSE"));
                        command.Parameters.AddWithValue("@SupplierRows", GetCount(counts, "SUPPLIER"));
                        command.Parameters.AddWithValue("@ProductionLineRows", GetCount(counts, "PRODUCTION_LINE"));
                        command.Parameters.AddWithValue("@SiteRows", GetCount(counts, "SITE"));
                        command.Parameters.AddWithValue("@WorkCenterRows", GetCount(counts, "WORK_CENTER"));
                        command.Parameters.AddWithValue("@InventoryLocationRows", GetCount(counts, "INVENTORY_LOCATION"));
                        command.Parameters.AddWithValue("@SyncBatchId", batchId);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("Không thể chuyển SyncBatch sang STAGED.");
                    }

                    currentEntity = null;
                    if (phaseChanged != null)
                        phaseChanged("Giai đoạn 2/2: Đang Apply " + entities.Count +
                            " nhóm dữ liệu đã chọn vào FVN_PCTP...");

                    // Site/Work Center có proc apply riêng (không đụng IFS.usp_ApplySyncBatch). Chạy TRƯỚC khi batch
                    // chuyển APPLIED: nếu lỗi, batch được đánh dấu FAILED và các entity khác chưa được apply.
                    if (counts.ContainsKey("SITE") || counts.ContainsKey("WORK_CENTER"))
                    {
                        using (var command = new SqlCommand("IFS.usp_ApplySiteWorkCenterBatch", connection))
                        {
                            command.CommandType = CommandType.StoredProcedure;
                            command.CommandTimeout = 600;
                            command.Parameters.AddWithValue("@SyncBatchId", batchId);
                            command.ExecuteNonQuery();
                        }
                    }

                    using (var command = new SqlCommand("IFS.usp_ApplySyncBatch", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 1800;
                        command.Parameters.AddWithValue("@SyncBatchId", batchId);
                        command.ExecuteNonQuery();
                    }

                    // Location chạy SAU IFS.usp_ApplySyncBatch vì cần Warehouse IFS đã có trong MES.FVN_IfsWarehouse.
                    // usp_ApplySyncBatch đã đặt batch APPLIED; nếu Location lỗi thì phải đánh dấu lại FAILED (xem catch).
                    if (counts.ContainsKey("INVENTORY_LOCATION"))
                    {
                        locationApplyFailedAfterBase = true;
                        currentEntity = "INVENTORY_LOCATION";
                        using (var command = new SqlCommand("IFS.usp_ApplyInventoryLocationBatch", connection))
                        {
                            command.CommandType = CommandType.StoredProcedure;
                            command.CommandTimeout = 600;
                            command.Parameters.AddWithValue("@SyncBatchId", batchId);
                            command.ExecuteNonQuery();
                        }
                        locationApplyFailedAfterBase = false;
                        currentEntity = null;
                    }

                    return new IfsSyncResult
                    {
                        SyncBatchId = batchId,
                        PartCatalogRows = GetCount(counts, "PART_CATALOG"),
                        InventoryPartRows = GetCount(counts, "INVENTORY_PART"),
                        BomHeaderRows = GetCount(counts, "BOM_HEADER"),
                        BomAlternativeRows = GetCount(counts, "BOM_ALTERNATIVE"),
                        BomComponentRows = GetCount(counts, "BOM_COMPONENT"),
                        WarehouseRows = GetCount(counts, "WAREHOUSE"),
                        SupplierRows = GetCount(counts, "SUPPLIER"),
                        ProductionLineRows = GetCount(counts, "PRODUCTION_LINE"),
                        SiteRows = GetCount(counts, "SITE"),
                        WorkCenterRows = GetCount(counts, "WORK_CENTER"),
                        InventoryLocationRows = GetCount(counts, "INVENTORY_LOCATION"),
                        Status = "APPLIED"
                    };
                }
                catch (Exception ex)
                {
                    try
                    {
                        using (var command = new SqlCommand(@"
UPDATE IFS.FVN_SyncBatch
SET Status='FAILED', CompletedAtUtc=SYSUTCDATETIME(),
    ErrorSummary=@ErrorSummary
WHERE SyncBatchId=@SyncBatchId AND (Status<>'APPLIED' OR @ForceFail=1);", connection))
                        {
                            command.Parameters.AddWithValue("@ForceFail", locationApplyFailedAfterBase ? 1 : 0);
                            command.Parameters.Add("@ErrorSummary", SqlDbType.NVarChar, 2000).Value =
                                ex.ToString().Length > 2000 ? ex.ToString().Substring(0, 2000) : ex.ToString();
                            command.Parameters.AddWithValue("@SyncBatchId", batchId);
                            command.ExecuteNonQuery();
                        }
                        using (var error = new SqlCommand(@"
INSERT IFS.SyncError(SyncBatchId, EntityName, ErrorCode, ErrorMessage, ErrorDetails, LoggedAtUtc)
VALUES(@SyncBatchId, @EntityName, 'SYNC_FAILED', @ErrorMessage, @ErrorDetails, SYSUTCDATETIME());", connection))
                        {
                            error.Parameters.AddWithValue("@SyncBatchId", batchId);
                            error.Parameters.Add("@EntityName", SqlDbType.VarChar, 30).Value =
                                string.IsNullOrWhiteSpace(currentEntity) ? (object)DBNull.Value : currentEntity;
                            error.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, 2000).Value =
                                ex.Message.Length > 2000 ? ex.Message.Substring(0, 2000) : ex.Message;
                            error.Parameters.Add("@ErrorDetails", SqlDbType.NVarChar, -1).Value = ex.ToString();
                            error.ExecuteNonQuery();
                        }
                    }
                    catch { }
                    throw;
                }
            }
        }

        private static int GetCount(IDictionary<string, int> counts, string entity)
        {
            int value;
            return counts.TryGetValue(entity, out value) ? value : 0;
        }

        private static long BeginBatch(SqlConnection connection, string syncMode, string selectedEntities)
        {
            using (var command = new SqlCommand(@"
INSERT IFS.FVN_SyncBatch
    (SyncMode, SelectedEntities, Status, StartedAtUtc, InitiatedBy)
OUTPUT INSERTED.SyncBatchId
VALUES (@SyncMode, @SelectedEntities, 'RUNNING', SYSUTCDATETIME(), @InitiatedBy);", connection))
            {
                command.Parameters.Add("@SyncMode", SqlDbType.VarChar, 12).Value = syncMode;
                command.Parameters.Add("@SelectedEntities", SqlDbType.NVarChar, 500).Value = selectedEntities;
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
                // Read only the selected IFS entity directly. ExecuteReaderStrict in the
                // legacy IFSPROVIDER performs a cross-view "required views" check and
                // rejects an empty unrelated view, which breaks selective sync.
                // This query comes only from the private EntityQueries allow-list.
                using (var oracleConnection = new OracleConnection(_oracleConnectionString))
                using (var oracleCommand = new OracleCommand(oracleSql, oracleConnection))
                {
                    oracleCommand.CommandTimeout = 1800;
                    oracleConnection.Open();
                    using (OracleDataReader reader = oracleCommand.ExecuteReader(CommandBehavior.SequentialAccess))
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
                    }
                }
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
            if (stage == null || stage.Rows.Count == 0)
                return;

            // Do not use SqlBulkCopy here. On some SQL Server/provider combinations
            // its destination metadata lookup fails with "Failed to obtain column
            // collation information", even when the target table is accessible.
            // Parameterized INSERT avoids that metadata path entirely.
            const int rowsPerCommand = 150; // 150 x 12 parameters = 1800 (< SQL Server 2100 limit)
            const int parameterCount = 12;
            string[] columns =
            {
                "SyncBatchId", "EntityName", "SiteCode", "PartNo", "EngChgLevel",
                "BomTypeDb", "AlternativeNo", "LineItemNo", "SourceRowKey",
                "SourceObjVersion", "PayloadJson", "SourceHash"
            };

            for (int offset = 0; offset < stage.Rows.Count; offset += rowsPerCommand)
            {
                int take = Math.Min(rowsPerCommand, stage.Rows.Count - offset);
                var sql = new StringBuilder();
                sql.Append("INSERT INTO [IFS].[SyncStageRow] (");
                sql.Append(string.Join(",", columns));
                sql.Append(") VALUES ");

                using (var command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandTimeout = 1800;

                    for (int rowIndex = 0; rowIndex < take; rowIndex++)
                    {
                        if (rowIndex > 0) sql.Append(",");
                        sql.Append("(");

                        for (int columnIndex = 0; columnIndex < parameterCount; columnIndex++)
                        {
                            if (columnIndex > 0) sql.Append(",");
                            string parameterName = "@p" + rowIndex + "_" + columnIndex;
                            sql.Append(parameterName);

                            DataRow row = stage.Rows[offset + rowIndex];
                            object value = row[columns[columnIndex]];
                            SqlParameter parameter;

                            switch (columnIndex)
                            {
                                case 0:
                                    parameter = command.Parameters.Add(parameterName, SqlDbType.BigInt);
                                    break;
                                case 1:
                                    parameter = command.Parameters.Add(parameterName, SqlDbType.VarChar, 30);
                                    break;
                                case 2:
                                    parameter = command.Parameters.Add(parameterName, SqlDbType.NVarChar, 30);
                                    break;
                                case 3:
                                    parameter = command.Parameters.Add(parameterName, SqlDbType.NVarChar, 100);
                                    break;
                                case 4:
                                    parameter = command.Parameters.Add(parameterName, SqlDbType.NVarChar, 24);
                                    break;
                                case 5:
                                    parameter = command.Parameters.Add(parameterName, SqlDbType.NVarChar, 80);
                                    break;
                                case 6:
                                    parameter = command.Parameters.Add(parameterName, SqlDbType.NVarChar, 80);
                                    break;
                                case 7:
                                    parameter = command.Parameters.Add(parameterName, SqlDbType.Decimal);
                                    parameter.Precision = 18;
                                    parameter.Scale = 0;
                                    break;
                                case 8:
                                    parameter = command.Parameters.Add(parameterName, SqlDbType.NVarChar, 200);
                                    break;
                                case 9:
                                    parameter = command.Parameters.Add(parameterName, SqlDbType.NVarChar, 56);
                                    break;
                                case 10:
                                    parameter = command.Parameters.Add(parameterName, SqlDbType.NVarChar, -1);
                                    break;
                                default:
                                    parameter = command.Parameters.Add(parameterName, SqlDbType.VarBinary, 32);
                                    break;
                            }

                            parameter.Value = value ?? DBNull.Value;
                        }

                        sql.Append(")");
                    }

                    command.CommandText = sql.ToString();
                    command.ExecuteNonQuery();
                }
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

            string partNo = entityName == "WAREHOUSE"
                ? ReadString(reader, "WAREHOUSE_ID")
                : entityName == "SUPPLIER"
                    ? ReadString(reader, "SUPPLIER_ID")
                    : entityName == "PRODUCTION_LINE"
                        ? ReadString(reader, "PRODUCTION_LINE")
                        : entityName == "SITE"
                            ? ReadString(reader, "CONTRACT")
                            : entityName == "WORK_CENTER"
                                ? ReadString(reader, "WORK_CENTER_NO")
                                : entityName == "INVENTORY_LOCATION"
                                    ? ReadString(reader, "LOCATION_NO")
                                    : ReadString(reader, "PART_NO");
            if (string.IsNullOrWhiteSpace(partNo))
                throw new InvalidOperationException(entityName == "WAREHOUSE"
                    ? "WAREHOUSE có dòng thiếu WAREHOUSE_ID."
                    : entityName == "SUPPLIER"
                        ? "SUPPLIER_INFO_GENERAL có dòng thiếu SUPPLIER_ID."
                        : entityName == "PRODUCTION_LINE"
                            ? "PRODUCTION_LINE có dòng thiếu PRODUCTION_LINE."
                            : entityName == "SITE"
                                ? "SITE có dòng thiếu CONTRACT."
                                : entityName == "WORK_CENTER"
                                    ? "WORK_CENTER có dòng thiếu WORK_CENTER_NO (kiểm tra tên cột của view IFSAPP.WORK_CENTER)."
                                    : entityName == "INVENTORY_LOCATION"
                                        ? "INVENTORY_LOCATION có dòng thiếu LOCATION_NO (kiểm tra tên cột của view IFSAPP.INVENTORY_LOCATION)."
                                        : entityName + " có dòng thiếu PART_NO.");

            string siteCode = (entityName == "PART_CATALOG" || entityName == "SUPPLIER") ? null : ReadString(reader, "CONTRACT");
            if (entityName != "PART_CATALOG" && entityName != "SUPPLIER" && string.IsNullOrWhiteSpace(siteCode))
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
