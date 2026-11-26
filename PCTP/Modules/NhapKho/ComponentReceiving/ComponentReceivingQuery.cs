using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using PCTP.Infrastructure.Security;

namespace PCTP.Modules.NhapKho.ComponentReceiving
{
    /// <summary>Read model cho màn hình "Kiểm kê hàng về": danh sách yêu cầu (PO đã phát hành) và các tem/LOT của từng yêu cầu.</summary>
    public interface IComponentReceivingQueryRepository
    {
        DataTable ListRequests();
        DataTable ListLots(long purchaseOrderId);
    }

    public interface IComponentReceivingQueryService
    {
        /// <summary>Các yêu cầu hàng về thuộc phạm vi Site của tài khoản.</summary>
        DataTable ListRequests();

        /// <summary>Các tem/LOT của một yêu cầu; kiểm tra phạm vi Site của yêu cầu đó.</summary>
        DataTable ListLots(long purchaseOrderId);
    }

    public sealed class ComponentReceivingQueryRepository : IComponentReceivingQueryRepository
    {
        private const string RequestSql = @"
SELECT TOP (3000)
       h.PurchaseOrderId, h.SiteCode, h.PurchaseOrderNo, h.OrderDate,
       h.SupplierCode, h.SupplierNameSnapshot AS SupplierName, h.Status,
       CASE h.Status
            WHEN 'RELEASED'           THEN N'1. Chờ nhận'
            WHEN 'PARTIALLY_RECEIVED' THEN N'2. Đang nhận'
            WHEN 'RECEIVED'           THEN N'3. Đã nhận đủ'
            WHEN 'CLOSED'             THEN N'4. Đã đóng'
            ELSE h.Status END AS StatusText,
       h.ReleasedBy,
       COALESCE(h.UpdatedAtUtc, h.ReleasedAtUtc, h.CreatedAtUtc) AS LastUpdateUtc,
       ISNULL(t.LineCount, 0) AS LineCount,
       ISNULL(t.OrderQty, 0) AS OrderQty,
       ISNULL(t.ReceivedQty, 0) AS ReceivedQty
FROM MES.FVN_ComponentPurchaseOrder h
OUTER APPLY (SELECT COUNT(*) AS LineCount, SUM(x.OrderQty) AS OrderQty, SUM(x.ReceivedQty) AS ReceivedQty
             FROM MES.FVN_ComponentPurchaseOrderLine x
             WHERE x.PurchaseOrderId = h.PurchaseOrderId) t
WHERE h.Status IN ('RELEASED','PARTIALLY_RECEIVED','RECEIVED','CLOSED')
ORDER BY h.OrderDate DESC, h.PurchaseOrderId DESC;";

        private const string LotSql = @"
SELECT l.ComponentLotId, h.PurchaseOrderId, h.SiteCode, h.PurchaseOrderNo,
       h.SupplierCode, h.SupplierNameSnapshot AS SupplierName,
       p.[LineNo], p.PartNo, p.PartDescription, p.UnitCode,
       h.OrderDate, p.NeedByDate, p.OrderQty,
       l.LotNo, l.QrPayload, l.Status AS LotStatus,
       r.LastReceivedAtUtc AS ArrivalDateUtc,
       l.IssuedQty, l.ReceivedQty,
       l.IssuedQty - l.ReceivedQty AS DiffQty,
       ISNULL(lb.PrintCount, 0) AS LabelPrintCount
FROM MES.FVN_ComponentLot l
JOIN MES.FVN_ComponentPurchaseOrderLine p ON p.PurchaseOrderLineId = l.PurchaseOrderLineId
JOIN MES.FVN_ComponentPurchaseOrder h ON h.PurchaseOrderId = p.PurchaseOrderId
OUTER APPLY (SELECT MAX(x.ReceivedAtUtc) AS LastReceivedAtUtc
             FROM MES.FVN_ComponentReceiptLine x WHERE x.ComponentLotId = l.ComponentLotId) r
OUTER APPLY (SELECT SUM(y.CopyCount) AS PrintCount
             FROM MES.FVN_ComponentLotLabelHistory y WHERE y.ComponentLotId = l.ComponentLotId) lb
WHERE h.PurchaseOrderId = @PurchaseOrderId
ORDER BY p.[LineNo], l.ComponentLotId;";

        private readonly string _connectionString;

        public ComponentReceivingQueryRepository(string connectionString = null)
        {
            _connectionString = connectionString
                ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable ListRequests()
        {
            return Fill(RequestSql, null);
        }

        public DataTable ListLots(long purchaseOrderId)
        {
            return Fill(LotSql, cmd => cmd.Parameters.Add("@PurchaseOrderId", SqlDbType.BigInt).Value = purchaseOrderId);
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

    public sealed class ComponentReceivingQueryService : IComponentReceivingQueryService
    {
        private const string Function = "WAREHOUSE.COMPONENT_RECEIPT";
        private readonly IComponentReceivingQueryRepository _repository;

        public ComponentReceivingQueryService(IComponentReceivingQueryRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public DataTable ListRequests()
        {
            var session = AuthorizationGuard.Demand(Function, "VIEW");
            DataTable table = _repository.ListRequests();

            // Chỉ giữ yêu cầu thuộc Site mà tài khoản có phạm vi dữ liệu.
            for (int i = table.Rows.Count - 1; i >= 0; i--)
            {
                string site = Convert.ToString(table.Rows[i]["SiteCode"]);
                if (!session.HasDataScope(Function, "SITE", site, site))
                    table.Rows.RemoveAt(i);
            }
            table.AcceptChanges();
            return table;
        }

        public DataTable ListLots(long purchaseOrderId)
        {
            if (purchaseOrderId <= 0) throw new ArgumentException("Yêu cầu không hợp lệ.", nameof(purchaseOrderId));
            AuthorizationGuard.Demand(Function, "VIEW");
            DataTable table = _repository.ListLots(purchaseOrderId);
            if (table.Rows.Count > 0)
            {
                string site = Convert.ToString(table.Rows[0]["SiteCode"]);
                AuthorizationGuard.DemandScope(Function, "SITE", site, site);
            }
            return table;
        }
    }
}
