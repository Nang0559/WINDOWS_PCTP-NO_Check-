using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using PCTP.Infrastructure.Security;
using PCTP.Shared.UiMd;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>
    /// Yêu cầu mua hàng linh kiện (PO) cho kho PART: upload Excel → DRAFT → VALIDATED → RELEASED
    /// → (nhận QR) PARTIALLY_RECEIVED / RECEIVED → CLOSED, hoặc CANCELLED.
    /// Trạng thái dùng bảng workflow chuẩn dbo.FVN_sys_WorkflowTransitions (ProcessCode MES_COMPONENT_PO);
    /// SQL kiểm tra lại transition và ghi nhật ký trong cùng transaction (migration 043).
    /// </summary>
    public static class ComponentPoStatus
    {
        public const string ProcessCode = "MES_COMPONENT_PO";
        public const string Function = "WAREHOUSE.COMPONENT_PO";

        public const int Draft = 0;
        public const int Validated = 10;
        public const int Released = 20;
        public const int PartiallyReceived = 30;
        public const int Received = 40;
        public const int Closed = 50;
        public const int Cancelled = 900;

        /// <summary>30 / 40 do thủ tục nhận hàng QR ghi nhận, không đổi thủ công.</summary>
        public static bool IsReceiptDriven(int statusCode)
        {
            return statusCode == PartiallyReceived || statusCode == Received;
        }

        public static string Text(int statusCode)
        {
            switch (statusCode)
            {
                case Draft: return "0. Nháp (mới upload)";
                case Validated: return "1. Đã kiểm tra";
                case Released: return "2. Đã phát hành (chờ nhận)";
                case PartiallyReceived: return "3. Đang nhận";
                case Received: return "4. Đã nhận đủ";
                case Closed: return "5. Đã đóng";
                case Cancelled: return "Đã hủy";
                default: return statusCode.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Quyền (action) cần có để đưa PO tới trạng thái đích.</summary>
        public static string RequiredAction(int toStatus)
        {
            switch (toStatus)
            {
                case Validated: return "CONFIRM";
                case Released: return "APPROVE";
                case Closed: return "APPROVE";
                case Cancelled: return "CANCEL";
                default: return null;
            }
        }
    }

    public sealed class ComponentPoImportRow
    {
        public string SiteCode { get; set; }
        public string PurchaseOrderNo { get; set; }
        public string SupplierCode { get; set; }
        public DateTime OrderDate { get; set; }
        public int LineNo { get; set; }
        public string PartNo { get; set; }
        public string PartDescription { get; set; }
        public decimal OrderQty { get; set; }
        public string UnitCode { get; set; }
        public DateTime? NeedByDate { get; set; }
        public string Remarks { get; set; }
    }

    public interface IComponentPurchaseOrderRepository
    {
        DataTable ListOrders();
        DataTable ListLines(long purchaseOrderId);
        DataTable ListEvents(long purchaseOrderId);
        string GetSiteCode(long purchaseOrderId);
        int ImportOrders(IReadOnlyList<ComponentPoImportRow> rows, string actor, string sourceFile, Guid correlationId);
        DataTable Transition(long purchaseOrderId, int fromStatus, int toStatus, string actor, string reason, Guid correlationId);
    }

    public interface IComponentPurchaseOrderService
    {
        DataTable ListOrders();
        DataTable ListLines(long purchaseOrderId);
        DataTable ListEvents(long purchaseOrderId);
        /// <summary>Các trạng thái đích thủ công hợp lệ từ trạng thái hiện tại theo bảng workflow chuẩn.</summary>
        IReadOnlyList<WorkflowTransition> GetAvailableActions(int fromStatus);
        /// <summary>Upload các dòng PO; mọi PO trong lô nằm trong một transaction. Trả về số PO.</summary>
        int Import(IReadOnlyList<ComponentPoImportRow> rows, string sourceFile);
        DataTable Transition(long purchaseOrderId, int fromStatus, int toStatus, string reason);
    }

    /// <summary>Đọc bảng workflow chuẩn bằng kết nối FVN_PCTP (cùng nguồn với SQL kiểm tra transition).</summary>
    public sealed class ComponentPoWorkflowRepository : IWorkflowRepository
    {
        private readonly string _connectionString;

        public ComponentPoWorkflowRepository(string connectionString = null)
        {
            _connectionString = connectionString
                ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public IReadOnlyList<WorkflowTransition> GetTransitions(string processCode)
        {
            var result = new List<WorkflowTransition>();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(
                "SELECT Id, FromStatus, ToStatus, ActionName, Description FROM dbo.FVN_sys_WorkflowTransitions " +
                "WHERE ProcessCode=@pc AND IsActive=1 ORDER BY Id", cn))
            {
                cmd.Parameters.Add("@pc", SqlDbType.NVarChar, 50).Value = processCode ?? (object)DBNull.Value;
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                        result.Add(new WorkflowTransition
                        {
                            Id = Convert.ToInt32(rd["Id"]),
                            ProcessCode = processCode,
                            FromStatus = Convert.ToInt32(rd["FromStatus"]),
                            ToStatus = Convert.ToInt32(rd["ToStatus"]),
                            ActionName = Convert.ToString(rd["ActionName"]),
                            Description = rd["Description"] == DBNull.Value ? null : Convert.ToString(rd["Description"]),
                            IsActive = true
                        });
            }
            return result;
        }

        public void InvalidateCache(string processCode = null) { }
    }

    public sealed class ComponentPurchaseOrderRepository : IComponentPurchaseOrderRepository
    {
        private const string OrderSql = @"
SELECT TOP (3000)
       h.PurchaseOrderId, h.SiteCode, h.PurchaseOrderNo, h.OrderDate,
       h.SupplierCode, h.SupplierNameSnapshot AS SupplierName, h.Status,
       MES.fn_FVN_ComponentPoStatusCode(h.Status) AS StatusCode,
       h.Remarks,
       h.CreatedBy, h.CreatedAtUtc,
       COALESCE(h.UpdatedAtUtc, h.ReleasedAtUtc, h.CreatedAtUtc) AS LastUpdateUtc,
       ISNULL(t.LineCount, 0) AS LineCount,
       ISNULL(t.OrderQty, 0) AS OrderQty,
       ISNULL(t.ReceivedQty, 0) AS ReceivedQty
FROM MES.FVN_ComponentPurchaseOrder h
OUTER APPLY (SELECT COUNT(*) AS LineCount, SUM(x.OrderQty) AS OrderQty, SUM(x.ReceivedQty) AS ReceivedQty
             FROM MES.FVN_ComponentPurchaseOrderLine x
             WHERE x.PurchaseOrderId = h.PurchaseOrderId) t
ORDER BY h.OrderDate DESC, h.PurchaseOrderId DESC;";

        private const string LineSql = @"
SELECT l.PurchaseOrderLineId, l.[LineNo], l.PartNo, l.PartDescription, l.UnitCode, l.NeedByDate,
       l.OrderQty, l.ReceivedQty, l.OrderQty - l.ReceivedQty AS RemainingQty,
       ISNULL(c.LotCount, 0) AS LotCount
FROM MES.FVN_ComponentPurchaseOrderLine l
OUTER APPLY (SELECT COUNT(*) AS LotCount FROM MES.FVN_ComponentLot x
             WHERE x.PurchaseOrderLineId = l.PurchaseOrderLineId) c
WHERE l.PurchaseOrderId = @PurchaseOrderId
ORDER BY l.[LineNo];";

        private const string EventSql = @"
SELECT e.OccurredAtUtc, e.ActionName, e.FromStatus, e.ToStatus, e.ActorCode, e.Reason
FROM MES.FVN_ComponentPoWorkflowEvent e
WHERE e.PurchaseOrderId = @PurchaseOrderId
ORDER BY e.OccurredAtUtc DESC, e.WorkflowEventId DESC;";

        private readonly string _connectionString;

        public ComponentPurchaseOrderRepository(string connectionString = null)
        {
            _connectionString = connectionString
                ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable ListOrders() { return Fill(OrderSql, null); }

        public DataTable ListLines(long purchaseOrderId)
        {
            return Fill(LineSql, c => c.Parameters.Add("@PurchaseOrderId", SqlDbType.BigInt).Value = purchaseOrderId);
        }

        public DataTable ListEvents(long purchaseOrderId)
        {
            return Fill(EventSql, c => c.Parameters.Add("@PurchaseOrderId", SqlDbType.BigInt).Value = purchaseOrderId);
        }

        public string GetSiteCode(long purchaseOrderId)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(
                "SELECT SiteCode FROM MES.FVN_ComponentPurchaseOrder WHERE PurchaseOrderId=@Id", cn))
            {
                cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = purchaseOrderId;
                cn.Open();
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value);
            }
        }

        public int ImportOrders(IReadOnlyList<ComponentPoImportRow> rows, string actor, string sourceFile, Guid correlationId)
        {
            var groups = rows
                .GroupBy(r => new { Site = r.SiteCode, No = r.PurchaseOrderNo })
                .ToList();

            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    foreach (var group in groups)
                    {
                        var first = group.First();
                        var lines = new DataTable();
                        lines.Columns.Add("LineNo", typeof(int));
                        lines.Columns.Add("PartNo", typeof(string));
                        lines.Columns.Add("PartDescription", typeof(string));
                        lines.Columns.Add("OrderQty", typeof(decimal));
                        lines.Columns.Add("UnitCode", typeof(string));
                        lines.Columns.Add("NeedByDate", typeof(DateTime));
                        foreach (var r in group.OrderBy(x => x.LineNo))
                            lines.Rows.Add(r.LineNo, r.PartNo,
                                string.IsNullOrWhiteSpace(r.PartDescription) ? (object)DBNull.Value : r.PartDescription,
                                r.OrderQty,
                                string.IsNullOrWhiteSpace(r.UnitCode) ? (object)DBNull.Value : r.UnitCode,
                                r.NeedByDate.HasValue ? (object)r.NeedByDate.Value.Date : DBNull.Value);

                        string remarks = group.Select(x => x.Remarks).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                        string note = string.IsNullOrWhiteSpace(sourceFile) ? null : "Upload: " + sourceFile;
                        remarks = string.Join(" | ", new[] { remarks, note }.Where(x => !string.IsNullOrWhiteSpace(x)));
                        if (remarks.Length > 500) remarks = remarks.Substring(0, 500);

                        using (var cmd = new SqlCommand("MES.usp_FVN_ComponentPO_Import", cn, tx))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.CommandTimeout = 120;
                            cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = first.SiteCode;
                            cmd.Parameters.Add("@PurchaseOrderNo", SqlDbType.NVarChar, 60).Value = first.PurchaseOrderNo;
                            cmd.Parameters.Add("@SupplierCode", SqlDbType.NVarChar, 60).Value = first.SupplierCode;
                            cmd.Parameters.Add("@OrderDate", SqlDbType.Date).Value = first.OrderDate.Date;
                            cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                            var tvp = cmd.Parameters.Add("@Lines", SqlDbType.Structured);
                            tvp.TypeName = "MES.FVN_ComponentPoImportLine";
                            tvp.Value = lines;
                            cmd.Parameters.Add("@Remarks", SqlDbType.NVarChar, 500).Value =
                                remarks.Length == 0 ? (object)DBNull.Value : remarks;
                            cmd.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value = correlationId;
                            cmd.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                }
            }
            return groups.Count;
        }

        public DataTable Transition(long purchaseOrderId, int fromStatus, int toStatus, string actor, string reason, Guid correlationId)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ComponentPO_Transition", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 120;
                cmd.Parameters.Add("@PurchaseOrderId", SqlDbType.BigInt).Value = purchaseOrderId;
                cmd.Parameters.Add("@FromStatus", SqlDbType.Int).Value = fromStatus;
                cmd.Parameters.Add("@ToStatus", SqlDbType.Int).Value = toStatus;
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 500).Value =
                    string.IsNullOrWhiteSpace(reason) ? (object)DBNull.Value : reason;
                cmd.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value = correlationId;
                var set = new DataSet();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(set);
                // Phát hành trả thêm danh sách LOT trước; bảng cuối luôn là tóm tắt transition.
                return set.Tables.Count == 0 ? new DataTable() : set.Tables[set.Tables.Count - 1];
            }
        }

        private DataTable Fill(string sql, Action<SqlCommand> bind)
        {
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 })
            {
                if (bind != null) bind(cmd);
                using (var da = new SqlDataAdapter(cmd)) da.Fill(table);
            }
            return table;
        }
    }

    public sealed class ComponentPurchaseOrderService : IComponentPurchaseOrderService
    {
        private const string Function = ComponentPoStatus.Function;
        private readonly IComponentPurchaseOrderRepository _repository;
        private readonly IWorkflowTransitionService _transitions;

        public ComponentPurchaseOrderService(IComponentPurchaseOrderRepository repository,
            IWorkflowTransitionService transitions)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        }

        public DataTable ListOrders()
        {
            var session = AuthorizationGuard.Demand(Function, "VIEW");
            DataTable table = _repository.ListOrders();
            for (int i = table.Rows.Count - 1; i >= 0; i--)
            {
                string site = Convert.ToString(table.Rows[i]["SiteCode"]);
                if (!session.HasDataScope(Function, "SITE", site, site))
                    table.Rows.RemoveAt(i);
            }
            table.AcceptChanges();
            return table;
        }

        public DataTable ListLines(long purchaseOrderId)
        {
            DemandOrderScope(purchaseOrderId, "VIEW");
            return _repository.ListLines(purchaseOrderId);
        }

        public DataTable ListEvents(long purchaseOrderId)
        {
            DemandOrderScope(purchaseOrderId, "VIEW");
            return _repository.ListEvents(purchaseOrderId);
        }

        public IReadOnlyList<WorkflowTransition> GetAvailableActions(int fromStatus)
        {
            return _transitions.GetAvailableTransitions(ComponentPoStatus.ProcessCode, fromStatus)
                .Where(t => !ComponentPoStatus.IsReceiptDriven(t.ToStatus))
                .ToList();
        }

        public int Import(IReadOnlyList<ComponentPoImportRow> rows, string sourceFile)
        {
            string actor = AuthorizationGuard.ActorCode;
            if (rows == null || rows.Count == 0)
                throw new ArgumentException("File không có dòng PO nào để upload.");

            foreach (var r in rows)
            {
                r.SiteCode = (r.SiteCode ?? string.Empty).Trim().ToUpperInvariant();
                r.PurchaseOrderNo = (r.PurchaseOrderNo ?? string.Empty).Trim();
                r.SupplierCode = (r.SupplierCode ?? string.Empty).Trim();
                r.PartNo = (r.PartNo ?? string.Empty).Trim();
                if (r.SiteCode.Length == 0 || r.PurchaseOrderNo.Length == 0 || r.SupplierCode.Length == 0 || r.PartNo.Length == 0)
                    throw new ArgumentException("Thiếu SiteCode / PurchaseOrderNo / SupplierCode / PartNo ở dòng LineNo " + r.LineNo + ".");
                if (r.SiteCode.Length > 30 || r.PurchaseOrderNo.Length > 60 || r.SupplierCode.Length > 60 || r.PartNo.Length > 100)
                    throw new ArgumentException("Độ dài mã vượt giới hạn ở PO " + r.PurchaseOrderNo + ", LineNo " + r.LineNo + ".");
                if (r.LineNo <= 0)
                    throw new ArgumentException("LineNo phải lớn hơn 0 (PO " + r.PurchaseOrderNo + ").");
                if (r.OrderQty <= 0)
                    throw new ArgumentException("OrderQty phải lớn hơn 0 (PO " + r.PurchaseOrderNo + ", LineNo " + r.LineNo + ").");
            }

            foreach (var site in rows.Select(r => r.SiteCode).Distinct())
                AuthorizationGuard.DemandActionAndScope(Function, "CREATE", "SITE", site, site);

            foreach (var po in rows.GroupBy(r => new { r.SiteCode, r.PurchaseOrderNo }))
            {
                if (po.Select(r => r.SupplierCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                    throw new ArgumentException("PO " + po.Key.PurchaseOrderNo + " có nhiều SupplierCode khác nhau.");
                if (po.Select(r => r.OrderDate.Date).Distinct().Count() > 1)
                    throw new ArgumentException("PO " + po.Key.PurchaseOrderNo + " có nhiều OrderDate khác nhau.");
                var dup = po.GroupBy(r => r.LineNo).FirstOrDefault(g => g.Count() > 1);
                if (dup != null)
                    throw new ArgumentException("PO " + po.Key.PurchaseOrderNo + " trùng LineNo " + dup.Key + ".");
            }

            return _repository.ImportOrders(rows, actor, sourceFile, Guid.NewGuid());
        }

        public DataTable Transition(long purchaseOrderId, int fromStatus, int toStatus, string reason)
        {
            string actor = AuthorizationGuard.ActorCode;
            if (purchaseOrderId <= 0) throw new ArgumentException("Yêu cầu mua hàng không hợp lệ.");
            if (fromStatus == toStatus) throw new ArgumentException("Trạng thái không thay đổi.");
            if (ComponentPoStatus.IsReceiptDriven(toStatus))
                throw new InvalidOperationException("Trạng thái nhận hàng do thủ tục nhận QR ghi nhận, không đổi thủ công.");

            string action = ComponentPoStatus.RequiredAction(toStatus);
            if (action == null) throw new InvalidOperationException("Trạng thái đích không hỗ trợ.");
            DemandOrderScope(purchaseOrderId, action);

            _transitions.EnsureCanTransition(ComponentPoStatus.ProcessCode, fromStatus, toStatus);
            if ((toStatus == ComponentPoStatus.Cancelled || (toStatus == ComponentPoStatus.Closed && fromStatus == ComponentPoStatus.PartiallyReceived))
                && string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Bắt buộc nhập lý do.");

            return _repository.Transition(purchaseOrderId, fromStatus, toStatus, actor,
                string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), Guid.NewGuid());
        }

        private void DemandOrderScope(long purchaseOrderId, string actionCode)
        {
            if (purchaseOrderId <= 0) throw new ArgumentException("Yêu cầu mua hàng không hợp lệ.");
            string site = _repository.GetSiteCode(purchaseOrderId);
            if (string.IsNullOrWhiteSpace(site))
                throw new UnauthorizedAccessException("Không xác định được Site của yêu cầu mua hàng.");
            AuthorizationGuard.DemandActionAndScope(Function, actionCode, "SITE", site, site);
        }
    }

    /// <summary>
    /// Đọc file Excel PO: mỗi dòng một dòng hàng. Cột bắt buộc: SiteCode, PurchaseOrderNo, SupplierCode,
    /// OrderDate, LineNo, PartNo, OrderQty. Cột tùy chọn: PartDescription, UnitCode, NeedByDate, Remarks.
    /// </summary>
    public static class ComponentPoExcelReader
    {
        private static readonly string[] Required =
            { "SiteCode", "PurchaseOrderNo", "SupplierCode", "OrderDate", "LineNo", "PartNo", "OrderQty" };

        public static IList<string> GetSheetNames(string path)
        {
            using (var package = new ExcelPackage(new FileInfo(path)))
                return package.Workbook.Worksheets.Select(w => w.Name).ToList();
        }

        public static List<ComponentPoImportRow> Read(string path, string sheetName)
        {
            using (var package = new ExcelPackage(new FileInfo(path)))
            {
                var ws = package.Workbook.Worksheets[sheetName];
                if (ws == null || ws.Dimension == null)
                    throw new InvalidOperationException("Sheet không có dữ liệu.");

                var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int c = 1; c <= ws.Dimension.End.Column; c++)
                {
                    string h = (ws.Cells[1, c].Text ?? string.Empty).Trim();
                    if (h.Length > 0) headers[h] = c;
                }
                foreach (var h in Required)
                    if (!headers.ContainsKey(h))
                        throw new InvalidOperationException("Thiếu cột bắt buộc: " + h);

                Func<int, string, string> text = (row, key) =>
                    headers.ContainsKey(key) ? (ws.Cells[row, headers[key]].Text ?? string.Empty).Trim() : null;

                var result = new List<ComponentPoImportRow>();
                for (int r = 2; r <= ws.Dimension.End.Row; r++)
                {
                    bool empty = true;
                    for (int c = 1; c <= ws.Dimension.End.Column && empty; c++)
                        if (!string.IsNullOrWhiteSpace(ws.Cells[r, c].Text)) empty = false;
                    if (empty) continue;

                    DateTime orderDate;
                    if (!TryDate(ws.Cells[r, headers["OrderDate"]].Value, text(r, "OrderDate"), out orderDate))
                        throw new InvalidOperationException("OrderDate không hợp lệ tại dòng " + r + ".");

                    int lineNo;
                    if (!int.TryParse(text(r, "LineNo"), NumberStyles.Integer, CultureInfo.InvariantCulture, out lineNo))
                        throw new InvalidOperationException("LineNo không hợp lệ tại dòng " + r + ".");

                    decimal qty;
                    if (!TryDecimal(ws.Cells[r, headers["OrderQty"]].Value, text(r, "OrderQty"), out qty))
                        throw new InvalidOperationException("OrderQty không hợp lệ tại dòng " + r + ".");

                    DateTime? needBy = null;
                    if (headers.ContainsKey("NeedByDate") && !string.IsNullOrWhiteSpace(text(r, "NeedByDate")))
                    {
                        DateTime nb;
                        if (!TryDate(ws.Cells[r, headers["NeedByDate"]].Value, text(r, "NeedByDate"), out nb))
                            throw new InvalidOperationException("NeedByDate không hợp lệ tại dòng " + r + ".");
                        needBy = nb;
                    }

                    result.Add(new ComponentPoImportRow
                    {
                        SiteCode = text(r, "SiteCode"),
                        PurchaseOrderNo = text(r, "PurchaseOrderNo"),
                        SupplierCode = text(r, "SupplierCode"),
                        OrderDate = orderDate,
                        LineNo = lineNo,
                        PartNo = text(r, "PartNo"),
                        PartDescription = text(r, "PartDescription"),
                        OrderQty = qty,
                        UnitCode = text(r, "UnitCode"),
                        NeedByDate = needBy,
                        Remarks = text(r, "Remarks")
                    });
                }
                return result;
            }
        }

        private static bool TryDate(object value, string text, out DateTime date)
        {
            if (value is DateTime) { date = (DateTime)value; return true; }
            if (value is double) { date = DateTime.FromOADate((double)value); return true; }
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date)
                || DateTime.TryParse(text, out date);
        }

        private static bool TryDecimal(object value, string text, out decimal number)
        {
            if (value is double) { number = Convert.ToDecimal((double)value); return true; }
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out number)
                || decimal.TryParse(text, out number);
        }
    }
}