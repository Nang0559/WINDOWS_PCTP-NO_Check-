using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using PCTP.Infrastructure.Security;

namespace PCTP.Modules.IfsSync
{
    public interface IIfsMapCenterRepository
    {
        /// <summary>Một dòng mỗi công đoạn (SITE, WORK_CENTER, PRODUCTION_LINE, WAREHOUSE, LOCATION, SLOT): Total / Mapped / Pending của Site.</summary>
        DataTable GetHub(string siteCode);

        /// <summary>Kho PCTP (dbo.FVN_Warehouse) và Kho IFS chưa có Kho PCTP, kèm MapStatus MAPPED / NO_IFS_MAP / IFS_ONLY / IFS_INACTIVE.</summary>
        DataTable GetWarehouseMap(string siteCode);

        /// <summary>Kho IFS đang hoạt động của Site (để chọn khi map).</summary>
        DataTable GetIfsWarehouses(string siteCode);

        /// <summary>Map một Kho PCTP sang Kho IFS; ifsWarehouseCode rỗng = gỡ map. Đổi / gỡ Kho IFS làm Location của Kho (nếu không còn thuộc Kho IFS mới) bị bỏ.</summary>
        void MapWarehouse(string siteCode, int warehouseId, string ifsWarehouseCode);

        /// <summary>Location IFS đang hoạt động thuộc đúng một Kho IFS (IFS chỉ có Kho + Location, không có Rack / Slot).</summary>
        DataTable GetIfsLocations(string siteCode, string ifsWarehouseCode);

        /// <summary>Chọn Location IFS cho một Kho PCTP (mọi Slot của Kho kế thừa); locationNo rỗng = bỏ Location của Kho.</summary>
        void SetWarehouseLocation(string siteCode, int warehouseId, string locationNo);

        /// <summary>Đồng bộ lại Location của Kho xuống các Slot (Kho đã có Location); warehouseId null = mọi Kho của Site. Trả về số Slot đã đồng bộ.</summary>
        int SyncSlotLocations(string siteCode, int? warehouseId);

        /// <summary>Xem trước (preview = true) hoặc tạo Kho PCTP từ Kho IFS; ifsWarehouseCode rỗng = mọi Kho IFS chưa có Kho PCTP.</summary>
        DataTable CreateWarehousesFromIfs(string siteCode, string ifsWarehouseCode, bool preview);
    }

    /// <summary>
    /// Trung tâm đồng bộ &amp; map IFS: đọc MES.vw_FVN_IfsMapHub / MES.vw_FVN_WarehouseIfsMapStatus (migration 085) và ghi map Kho qua
    /// MES.usp_FVN_Warehouse_MapIfs / _CreateFromIfs / _SetLocation / _SyncSlotLocations (086). Xem hub: MES.IFS_MAP_CENTER (VIEW). Map Kho: WAREHOUSE.WAREHOUSE_IFS_MAP (VIEW / EDIT theo Site).
    /// </summary>
    public sealed class IfsMapCenterRepository : IIfsMapCenterRepository
    {
        public const string HubFunctionCode = "MES.IFS_MAP_CENTER";
        public const string WarehouseMapFunctionCode = "WAREHOUSE.WAREHOUSE_IFS_MAP";

        private readonly string _connectionString;

        public IfsMapCenterRepository(string connectionString = null)
        {
            _connectionString = connectionString ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable GetHub(string siteCode)
        {
            string site = RequireSite(siteCode);
            AuthorizationGuard.DemandScope(HubFunctionCode, "SITE", site, site);
            return Query(@"
SELECT SiteCode, StageCode, StageOrder, TotalCount, MappedCount, PendingCount, LastSyncUtc
FROM MES.vw_FVN_IfsMapHub
WHERE SiteCode = @SiteCode
ORDER BY StageOrder;", site);
        }

        public DataTable GetWarehouseMap(string siteCode)
        {
            string site = RequireSite(siteCode);
            AuthorizationGuard.DemandScope(WarehouseMapFunctionCode, "SITE", site, site);
            return Query(@"
SELECT SiteCode, WarehouseId, WarehouseName, IfsWarehouseCode, IfsDescription, RackCount, SlotCount, IfsLocationCount, MapStatus,
       IfsLocationNo, IfsLocationDescription, LocationMapStatus, SlotLocationCount
FROM MES.vw_FVN_WarehouseIfsMapStatus
WHERE SiteCode = @SiteCode
ORDER BY CASE MapStatus WHEN 'NO_IFS_MAP' THEN 0 WHEN 'IFS_ONLY' THEN 1 WHEN 'IFS_INACTIVE' THEN 2 ELSE 3 END, WarehouseName, IfsWarehouseCode;", site);
        }

        public DataTable GetIfsWarehouses(string siteCode)
        {
            string site = RequireSite(siteCode);
            AuthorizationGuard.DemandScope(WarehouseMapFunctionCode, "SITE", site, site);
            return Query(@"
SELECT WarehouseCode, Description
FROM MES.FVN_IfsWarehouse
WHERE SiteCode = @SiteCode AND IsActive = 1
ORDER BY WarehouseCode;", site);
        }

        public DataTable GetIfsLocations(string siteCode, string ifsWarehouseCode)
        {
            string site = RequireSite(siteCode);
            AuthorizationGuard.DemandScope(WarehouseMapFunctionCode, "SITE", site, site);
            if (string.IsNullOrWhiteSpace(ifsWarehouseCode)) throw new ArgumentException("Kho IFS là bắt buộc.", "ifsWarehouseCode");
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(@"
SELECT LocationNo, LocationGroupCode, LocationType, Description
FROM MES.FVN_IfsInventoryLocation
WHERE SiteCode = @SiteCode AND WarehouseCode = @WarehouseCode AND IsActive = 1
ORDER BY LocationNo;", cn) { CommandTimeout = 60 })
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                cmd.Parameters.Add("@WarehouseCode", SqlDbType.NVarChar, 60).Value = ifsWarehouseCode.Trim();
                new SqlDataAdapter(cmd).Fill(table);
            }
            return table;
        }

        public void SetWarehouseLocation(string siteCode, int warehouseId, string locationNo)
        {
            string site = RequireSite(siteCode);
            string actor = AuthorizationGuard.DemandActionAndScope(WarehouseMapFunctionCode, "EDIT", "SITE", site, site).ActorCode;
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_Warehouse_SetLocation", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 })
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                cmd.Parameters.Add("@WarehouseId", SqlDbType.Int).Value = warehouseId;
                cmd.Parameters.Add("@LocationNo", SqlDbType.NVarChar, 100).Value =
                    string.IsNullOrWhiteSpace(locationNo) ? (object)DBNull.Value : locationNo.Trim();
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                cn.Open();
                try { cmd.ExecuteNonQuery(); }
                catch (SqlException ex) when (ex.Number >= 50000 && ex.Number <= 59999)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }
            }
        }

        public int SyncSlotLocations(string siteCode, int? warehouseId)
        {
            string site = RequireSite(siteCode);
            string actor = AuthorizationGuard.DemandActionAndScope(WarehouseMapFunctionCode, "EDIT", "SITE", site, site).ActorCode;
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_Warehouse_SyncSlotLocations", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 })
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                cmd.Parameters.Add("@WarehouseId", SqlDbType.Int).Value = warehouseId.HasValue ? (object)warehouseId.Value : DBNull.Value;
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                cn.Open();
                try { return Convert.ToInt32(cmd.ExecuteScalar() ?? 0); }
                catch (SqlException ex) when (ex.Number >= 50000 && ex.Number <= 59999)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }
            }
        }

        public void MapWarehouse(string siteCode, int warehouseId, string ifsWarehouseCode)
        {
            string site = RequireSite(siteCode);
            string actor = AuthorizationGuard.DemandActionAndScope(WarehouseMapFunctionCode, "EDIT", "SITE", site, site).ActorCode;
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_Warehouse_MapIfs", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 })
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                cmd.Parameters.Add("@WarehouseId", SqlDbType.Int).Value = warehouseId;
                cmd.Parameters.Add("@IfsWarehouseCode", SqlDbType.NVarChar, 60).Value =
                    string.IsNullOrWhiteSpace(ifsWarehouseCode) ? (object)DBNull.Value : ifsWarehouseCode.Trim();
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                cn.Open();
                try { cmd.ExecuteNonQuery(); }
                catch (SqlException ex) when (ex.Number >= 50000 && ex.Number <= 59999)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }
            }
        }

        public DataTable CreateWarehousesFromIfs(string siteCode, string ifsWarehouseCode, bool preview)
        {
            string site = RequireSite(siteCode);
            string actor = AuthorizationGuard.DemandActionAndScope(WarehouseMapFunctionCode, "EDIT", "SITE", site, site).ActorCode;
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_Warehouse_CreateFromIfs", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 })
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                cmd.Parameters.Add("@IfsWarehouseCode", SqlDbType.NVarChar, 60).Value =
                    string.IsNullOrWhiteSpace(ifsWarehouseCode) ? (object)DBNull.Value : ifsWarehouseCode.Trim();
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
                cmd.Parameters.Add("@Preview", SqlDbType.Bit).Value = preview;
                var table = new DataTable();
                try { new SqlDataAdapter(cmd).Fill(table); }
                catch (SqlException ex) when (ex.Number >= 50000 && ex.Number <= 59999)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }
                return table;
            }
        }

        private static string RequireSite(string siteCode)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            return siteCode.Trim().ToUpperInvariant();
        }

        private DataTable Query(string sql, string site)
        {
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 })
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                new SqlDataAdapter(cmd).Fill(table);
            }
            return table;
        }
    }
}
