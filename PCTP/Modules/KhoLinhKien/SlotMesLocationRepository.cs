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
        /// <summary>
        /// Mọi Slot đang hoạt động của Site (Kho › Rack › Slot của MES) kèm vùng (PART / WIP), Kho IFS + Location IFS kế thừa từ Kho và MapStatus
        /// (READY / NO_AREA / NO_LOCATION / LOCATION_INACTIVE). WarehouseMapStatus: OK / NO_IFS_MAP / NO_WH_LOCATION / OUT_OF_SYNC / WAREHOUSE_MISMATCH.
        /// </summary>
        DataTable GetStatus(string siteCode);

        /// <summary>Gán vùng PART / WIP cho các Slot; trả về số Slot đã gán, lỗi từng Slot nằm trong failures. Location IFS do Kho quyết định, không gán ở Slot.</summary>
        int Assign(string siteCode, IEnumerable<int> slotIds, string areaCode, IList<string> failures);

        /// <summary>Gỡ vùng của các Slot (giữ lịch sử). Location IFS vẫn thuộc Kho.</summary>
        int Clear(string siteCode, IEnumerable<int> slotIds, IList<string> failures);
    }

    /// <summary>
    /// Vùng PART / WIP của Slot kho MES. IFS chỉ có Kho + Location (không có Rack / Slot): Location IFS chọn ở cấp Kho (FVN_Warehouse.IfsLocationNo,
    /// màn Map Kho PCTP ↔ Kho IFS) và Slot kế thừa. Nhập kho linh kiện, tra cứu tồn và Part → WIP chỉ thấy Slot đã có vùng lẫn Location của Kho.
    /// Quyền WAREHOUSE.SLOT_MES_LOCATION (VIEW xem, EDIT gán / gỡ vùng, theo Site); ghi qua MES.usp_FVN_SlotMesLocation_Assign / _Clear (083, sửa ở 086).
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
SELECT SiteCode, SlotId, WarehouseName, RackName, SlotNumber, AreaCode, LocationNo, LocationWarehouseCode, LocationGroupCode, LocationDescription, MapStatus,
       WarehouseIfsCode, WarehouseLocationNo, WarehouseMapStatus
FROM MES.vw_FVN_SlotMesLocationStatus
WHERE SiteCode = @SiteCode
ORDER BY WarehouseName, RackName, SlotNumber;", site);
        }

        public int Assign(string siteCode, IEnumerable<int> slotIds, string areaCode, IList<string> failures)
        {
            string site = RequireSite(siteCode);
            string actor = AuthorizationGuard.DemandActionAndScope(FunctionCode, "EDIT", "SITE", site, site).ActorCode;
            string area = (areaCode ?? string.Empty).Trim().ToUpperInvariant();
            if (area != "PART" && area != "WIP") throw new ArgumentException("Vùng kho phải là PART hoặc WIP.", "areaCode");
            return Run(site, slotIds, failures, (cmd, slotId) =>
            {
                cmd.CommandText = "MES.usp_FVN_SlotMesLocation_Assign";
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = site;
                cmd.Parameters.Add("@SlotId", SqlDbType.Int).Value = slotId;
                cmd.Parameters.Add("@AreaCode", SqlDbType.VarChar, 8).Value = area;
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
