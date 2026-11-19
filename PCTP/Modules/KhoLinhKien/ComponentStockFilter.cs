using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using PCTP.Infrastructure.Security;
using PCTP.Modules.NhapKho.ComponentReceiving;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>Bộ lọc tra cứu tồn kho linh kiện PART. Trường để trống = không lọc.</summary>
    public sealed class ComponentStockFilter
    {
        public string SiteCode { get; set; }
        public string PartNo { get; set; }
        public string LotNo { get; set; }
        /// <summary>Tìm theo kho / giá đỡ / số slot / vị trí MES.</summary>
        public string SlotText { get; set; }
        public bool IncludeZero { get; set; }
    }

    public interface IComponentStockRepository
    {
        DataTable Search(ComponentStockFilter filter);
        DataTable ListSites();
        DataTable ListTransfers(string siteCode);
        DataTable Transfer(string siteCode, string transferNo, long materialLotId, int fromSlotId, int toSlotId,
            decimal quantity, string actor, string idempotencyKey, string reason);
    }

    public interface IComponentStockService
    {
        /// <summary>Tồn kho linh kiện PART theo PartNo / LOT / Site / Slot (WAREHOUSE.COMPONENT_STOCK).</summary>
        DataTable Search(ComponentStockFilter filter);
        DataTable ListSites();
        /// <summary>Slot PART đang hoạt động của Site (đích điều chuyển).</summary>
        DataTable ListSlots(string siteCode);
        DataTable ListTransfers(string siteCode);
        DataTable Transfer(string siteCode, string transferNo, long materialLotId, int fromSlotId, int toSlotId,
            decimal quantity, string reason, string idempotencyKey);
    }

    public sealed class ComponentStockRepository : IComponentStockRepository
    {
        // Chỉ tính các slot thuộc khu PART đang hoạt động; số dư lấy từ sổ tồn MES chuẩn.
        private const string StockSql = @"
SELECT TOP (5000)
       b.SiteCode, b.PartNo, ml.MaterialLotId, ml.LotNo, ml.LotStatus,
       cl.SupplierCode, po.PurchaseOrderNo,
       s.SlotId, w.Name AS WarehouseName, r.RackName, s.SlotNumber, b.LocationNo,
       b.StockState, b.OnHandQty, b.ReservedQty, b.OnHandQty - b.ReservedQty AS AvailableQty,
       b.UpdatedAtUtc
FROM MES.FVN_InventoryBalance b
JOIN MES.FVN_MaterialLot ml ON ml.SiteCode = b.SiteCode AND ml.PartNo = b.PartNo AND ml.MaterialLotId = b.MaterialLotId
JOIN dbo.FVN_SlotMesLocation sm ON sm.SiteCode = b.SiteCode AND sm.LocationNo = b.LocationNo
 AND sm.IsPrimary = 1 AND sm.IsActive = 1
JOIN dbo.FVN_Slot s ON s.SiteCode = sm.SiteCode AND s.SlotId = sm.SlotId
JOIN dbo.FVN_Rack r ON r.SiteCode = s.SiteCode AND r.RackId = s.RackId
JOIN dbo.FVN_Warehouse w ON w.SiteCode = r.SiteCode AND w.WarehouseId = r.WarehouseId
JOIN dbo.FVN_InventoryAreaSlot ar ON ar.SiteCode = s.SiteCode AND ar.SlotId = s.SlotId
 AND ar.AreaCode = 'PART' AND ar.IsActive = 1
LEFT JOIN MES.FVN_ComponentLot cl ON cl.MaterialLotId = ml.MaterialLotId
LEFT JOIN MES.FVN_ComponentPurchaseOrderLine pl ON pl.PurchaseOrderLineId = cl.PurchaseOrderLineId
LEFT JOIN MES.FVN_ComponentPurchaseOrder po ON po.PurchaseOrderId = pl.PurchaseOrderId
WHERE (@SiteCode IS NULL OR b.SiteCode = @SiteCode)
  AND (@PartNo IS NULL OR b.PartNo LIKE @PartNo ESCAPE N'\')
  AND (@LotNo IS NULL OR ml.LotNo LIKE @LotNo ESCAPE N'\')
  AND (@Slot IS NULL OR CONCAT(w.Name, N' ', r.RackName, N' ', CONVERT(nvarchar(30), s.SlotNumber), N' ', b.LocationNo)
        LIKE @Slot ESCAPE N'\')
  AND (@IncludeZero = 1 OR b.OnHandQty > 0)
ORDER BY b.SiteCode, b.PartNo, ml.LotNo, w.Name, r.RackName, s.SlotNumber;";

        private const string SiteSql =
            "SELECT SiteCode FROM dbo.FVN_PCTPSite WHERE IsActive = 1 ORDER BY SiteCode;";

        private const string TransferSql = @"
SELECT TOP (200)
       t.TransferredAtUtc, t.TransferNo, t.SiteCode, t.PartNo, ml.LotNo, t.Quantity,
       fw.Name + N' / ' + fr.RackName + N' / ' + CONVERT(nvarchar(30), fs.SlotNumber) AS FromSlot,
       tw.Name + N' / ' + tr.RackName + N' / ' + CONVERT(nvarchar(30), ts.SlotNumber) AS ToSlot,
       t.Reason, t.TransferredBy, t.InventoryTransactionId
FROM MES.FVN_ComponentLotTransfer t
JOIN MES.FVN_MaterialLot ml ON ml.MaterialLotId = t.MaterialLotId
JOIN dbo.FVN_Slot fs ON fs.SiteCode = t.SiteCode AND fs.SlotId = t.FromSlotId
JOIN dbo.FVN_Rack fr ON fr.SiteCode = fs.SiteCode AND fr.RackId = fs.RackId
JOIN dbo.FVN_Warehouse fw ON fw.SiteCode = fr.SiteCode AND fw.WarehouseId = fr.WarehouseId
JOIN dbo.FVN_Slot ts ON ts.SiteCode = t.SiteCode AND ts.SlotId = t.ToSlotId
JOIN dbo.FVN_Rack tr ON tr.SiteCode = ts.SiteCode AND tr.RackId = ts.RackId
JOIN dbo.FVN_Warehouse tw ON tw.SiteCode = tr.SiteCode AND tw.WarehouseId = tr.WarehouseId
WHERE t.SiteCode = @SiteCode
ORDER BY t.TransferredAtUtc DESC, t.ComponentLotTransferId DESC;";

        private readonly string _connectionString;

        public ComponentStockRepository(string connectionString = null)
        {
            _connectionString = connectionString
                ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable Search(ComponentStockFilter filter)
        {
            return Fill(StockSql, c =>
            {
                c.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = NullIfBlank(filter.SiteCode);
                c.Parameters.Add("@PartNo", SqlDbType.NVarChar, 210).Value = Like(filter.PartNo);
                c.Parameters.Add("@LotNo", SqlDbType.NVarChar, 250).Value = Like(filter.LotNo);
                c.Parameters.Add("@Slot", SqlDbType.NVarChar, 400).Value = Like(filter.SlotText);
                c.Parameters.Add("@IncludeZero", SqlDbType.Bit).Value = filter.IncludeZero;
            });
        }

        public DataTable ListSites() { return Fill(SiteSql, null); }

        public DataTable ListTransfers(string siteCode)
        {
            return Fill(TransferSql, c => c.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = siteCode);
        }

        public DataTable Transfer(string siteCode, string transferNo, long materialLotId, int fromSlotId, int toSlotId,
            decimal quantity, string actor, string idempotencyKey, string reason)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_ComponentLot_Transfer", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 90;
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = siteCode;
                cmd.Parameters.Add("@TransferNo", SqlDbType.NVarChar, 80).Value = transferNo;
                cmd.Parameters.Add("@MaterialLotId", SqlDbType.BigInt).Value = materialLotId;
                cmd.Parameters.Add("@FromSlotId", SqlDbType.Int).Value = fromSlotId;
                cmd.Parameters.Add("@ToSlotId", SqlDbType.Int).Value = toSlotId;
                var qty = cmd.Parameters.Add("@Quantity", SqlDbType.Decimal);
                qty.Precision = 19;
                qty.Scale = 6;
                qty.Value = quantity;
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                cmd.Parameters.Add("@IdempotencyKey", SqlDbType.NVarChar, 170).Value = idempotencyKey;
                cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 300).Value =
                    string.IsNullOrWhiteSpace(reason) ? (object)DBNull.Value : reason;
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }

        private static object NullIfBlank(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim().ToUpperInvariant();
        }

        /// <summary>Chuyển ô tìm kiếm thành mẫu LIKE '%x%' và vô hiệu hóa ký tự đại diện do người dùng gõ.</summary>
        private static object Like(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DBNull.Value;
            string escaped = value.Trim()
                .Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
            return "%" + escaped + "%";
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

    public sealed class ComponentStockService : IComponentStockService
    {
        public const string StockFunction = "WAREHOUSE.COMPONENT_STOCK";
        public const string TransferFunction = "WAREHOUSE.COMPONENT_TRANSFER";

        private readonly IComponentStockRepository _repository;
        private readonly IComponentSlotLookupRepository _slots;

        public ComponentStockService(IComponentStockRepository repository, IComponentSlotLookupRepository slots)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _slots = slots ?? throw new ArgumentNullException(nameof(slots));
        }

        public DataTable Search(ComponentStockFilter filter)
        {
            if (filter == null) filter = new ComponentStockFilter();
            var session = AuthorizationGuard.Demand(StockFunction, "VIEW");
            string site = string.IsNullOrWhiteSpace(filter.SiteCode) ? null : filter.SiteCode.Trim().ToUpperInvariant();
            if (site != null) AuthorizationGuard.DemandScope(StockFunction, "SITE", site, site);

            filter.SiteCode = site;
            DataTable table = _repository.Search(filter);

            // Không chọn Site: chỉ giữ dòng thuộc Site mà tài khoản có phạm vi dữ liệu.
            for (int i = table.Rows.Count - 1; i >= 0; i--)
            {
                string rowSite = Convert.ToString(table.Rows[i]["SiteCode"]);
                if (!session.HasDataScope(StockFunction, "SITE", rowSite, rowSite))
                    table.Rows.RemoveAt(i);
            }
            table.AcceptChanges();
            return table;
        }

        public DataTable ListSites()
        {
            var session = AuthorizationGuard.Demand(StockFunction, "VIEW");
            DataTable table = _repository.ListSites();
            for (int i = table.Rows.Count - 1; i >= 0; i--)
            {
                string site = Convert.ToString(table.Rows[i]["SiteCode"]);
                if (!session.HasDataScope(StockFunction, "SITE", site, site))
                    table.Rows.RemoveAt(i);
            }
            table.AcceptChanges();
            return table;
        }

        public DataTable ListSlots(string siteCode)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) return new DataTable();
            string site = siteCode.Trim().ToUpperInvariant();
            AuthorizationGuard.DemandScope(TransferFunction, "SITE", site, site);
            return _slots.GetActiveSlots(site);
        }

        public DataTable ListTransfers(string siteCode)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) return new DataTable();
            string site = siteCode.Trim().ToUpperInvariant();
            AuthorizationGuard.DemandScope(TransferFunction, "SITE", site, site);
            return _repository.ListTransfers(site);
        }

        public DataTable Transfer(string siteCode, string transferNo, long materialLotId, int fromSlotId, int toSlotId,
            decimal quantity, string reason, string idempotencyKey)
        {
            string site = (siteCode ?? string.Empty).Trim().ToUpperInvariant();
            AuthorizationGuard.DemandActionAndScope(TransferFunction, "CONFIRM", "SITE", site, site);
            string actor = AuthorizationGuard.ActorCode;

            transferNo = (transferNo ?? string.Empty).Trim();
            idempotencyKey = (idempotencyKey ?? string.Empty).Trim();
            if (site.Length == 0) throw new ArgumentException("Chưa chọn Site.");
            if (transferNo.Length == 0) throw new ArgumentException("Chưa có số phiếu điều chuyển.");
            if (materialLotId <= 0) throw new ArgumentException("LOT không hợp lệ.");
            if (fromSlotId <= 0 || toSlotId <= 0) throw new ArgumentException("Slot nguồn / đích không hợp lệ.");
            if (fromSlotId == toSlotId) throw new ArgumentException("Slot đích phải khác slot nguồn.");
            if (quantity <= 0) throw new ArgumentException("Số lượng điều chuyển phải lớn hơn 0.");
            if (idempotencyKey.Length == 0 || idempotencyKey.Length > 170)
                throw new ArgumentException("Khóa chống ghi nhận trùng không hợp lệ.");

            return _repository.Transfer(site, transferNo, materialLotId, fromSlotId, toSlotId, quantity,
                actor, idempotencyKey, reason);
        }
    }
}