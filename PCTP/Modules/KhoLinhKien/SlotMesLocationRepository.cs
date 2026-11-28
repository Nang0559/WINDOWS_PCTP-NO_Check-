using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using PCTP.Infrastructure.Security;

namespace PCTP.Modules.KhoLinhKien
{
    public interface ISlotMesLocationRepository
    {
        /// <summary>Mọi Slot đang hoạt động của Site kèm vùng (PART / WIP), MES Location chính và MapStatus (READY / NO_AREA / NO_LOCATION / LOCATION_INACTIVE).</summary>
        DataTable GetStatus(string siteCode);

        /// <summary>MES Location đang hoạt động của Site (đồng bộ từ IFS) để chọn.</summary>
        DataTable GetLocations(string siteCode);

        /// <summary>Gán vùng và MES Location chính cho các Slot; trả về số Slot đã gán, lỗi từng Slot nằm trong failures.</summary>
        int Assign(string siteCode, IEnumerable<int> slotIds, string areaCode, string locationNo, IList<string> failures);

        /// <summary>Gỡ vùng và MES Location của các Slot (giữ lịch sử).</summary>
        int Clear(string siteCode, IEnumerable<int> slotIds, IList<string> failures);
    }

    /// <summary>
    /// Map Slot kho ↔ MES Location (IFS) và vùng PART / WIP. Nhập kho linh kiện, tra cứu tồn và Part → WIP chỉ thấy Slot đã có đủ cả hai.
    /// Quyền WAREHOUSE.SLOT_MES_LOCATION (VIEW xem, EDIT gán / gỡ, theo Site); ghi qua MES.usp_FVN_SlotMesLocation_Assign / _Clear (migration 083).
    /// </summary>
    public sealed class SlotMesLocationRepository : ISlotMesLocationRepository
    {
        public const string FunctionCode = "WAREHOUSE.SLOT_MES_LOCATION";

        private readonly string _connectionString;

        public SlotMesLocationRepository(string connectionString = null)
        {
            _connectionString = connectionString ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable GetStatus(string siteCode)
        {
            string site = RequireSite(siteCode);
            AuthorizationGuard.DemandScope(FunctionCode, "SITE", site, site);
            return Query(@"
SELECT SiteCode, SlotId, WarehouseName, RackName, SlotNumber, AreaCode, LocationNo, LocationWarehouseCode, LocationGroupCode, LocationDescription, MapStatus
FROM MES.vw_FVN_SlotMesLocationStatus
WHERE SiteCode = @SiteCode
ORDER BY WarehouseName, RackName, SlotNumber;", site);
        }

        public DataTable GetLocations(string siteCode)
        {
            string site = RequireSite(siteCode);
            AuthorizationGuard.DemandScope(FunctionCode, "SITE", site, site);
            return Query(@"
SELECT LocationNo, WarehouseCode, LocationGroupCode, LocationType, Description
FROM MES.FVN_IfsInventoryLocation
WHERE SiteCode = @SiteCode AND IsActive = 1
ORDER BY WarehouseCode, LocationNo;", site);
        }

        public int Assign(string siteCode, IEnumerable<int> slotIds, string areaCode, string locationNo, IList<string> failures)
        {
            string site = RequireSite(siteCode);
            string actor = AuthorizationGuard.DemandActionAndScope(FunctionCode, "EDIT", "SITE", site, site).ActorCode;
            string area = (areaCode ?? string.Empty).Trim().ToUpperInvariant();
            if (area != "PART" && area != "WIP") throw new ArgumentException("Vùng kho phải là PART hoặc WIP.", "areaCode");
            if (string.IsNullOrWhiteSpace(locationNo)) throw new ArgumentException("Chọn MES Location từ danh sách.", "locationNo");
            return Run(site, slotIds, failures, (cmd, slotId) =>
            {
                cmd.CommandText = "MES.usp_FVN_SlotMesLocation_Assign";
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                cmd.Parameters.Add("@SlotId", SqlDbType.Int).Value = slotId;
                cmd.Parameters.Add("@AreaCode", SqlDbType.VarChar, 8).Value = area;
                cmd.Parameters.Add("@LocationNo", SqlDbType.NVarChar, 100).Value = locationNo.Trim();
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
            });
        }

        public int Clear(string siteCode, IEnumerable<int> slotIds, IList<string> failures)
        {
            string site = RequireSite(siteCode);
            string actor = AuthorizationGuard.DemandActionAndScope(FunctionCode, "EDIT", "SITE", site, site).ActorCode;
            return Run(site, slotIds, failures, (cmd, slotId) =>
            {
                cmd.CommandText = "MES.usp_FVN_SlotMesLocation_Clear";
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                cmd.Parameters.Add("@SlotId", SqlDbType.Int).Value = slotId;
                cmd.Parameters.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actor;
            });
        }

        /// <summary>Mỗi Slot một transaction riêng trong proc: Slot lỗi không làm hỏng các Slot còn lại.</summary>
        private int Run(string site, IEnumerable<int> slotIds, IList<string> failures, Action<SqlCommand, int> bind)
        {
            int done = 0;
            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                foreach (int slotId in slotIds)
                {
                    using (var cmd = new SqlCommand { Connection = cn, CommandType = CommandType.StoredProcedure, CommandTimeout = 60 })
                    {
                        bind(cmd, slotId);
                        try { cmd.ExecuteNonQuery(); done++; }
                        catch (SqlException ex)
                        {
                            if (ex.Number < 50000 || ex.Number > 59999) throw;
                            if (failures != null) failures.Add("Slot " + slotId + ": " + ex.Message);
                        }
                    }
                }
            }
            return done;
        }

        private DataTable Query(string sql, string site)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 })
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }

        private static string RequireSite(string siteCode)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            return siteCode.Trim().ToUpperInvariant();
        }
    }
}
