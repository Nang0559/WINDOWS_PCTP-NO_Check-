using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
namespace PCTP.Modules.NhapKho.ComponentReceiving
{
    public sealed class ComponentSlotLookupRepository : IComponentSlotLookupRepository
    {
        private readonly string _connectionString;
        public ComponentSlotLookupRepository(string connectionString = null)
        {
            _connectionString = connectionString ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString)) throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }
        public DataTable GetActiveSlots(string siteCode)
        {
            var table = new DataTable();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(@"
SELECT s.SlotId,
       CONCAT(w.Name, N' / ', r.RackName, N' / Slot ', s.SlotNumber,
              N' / ', sm.LocationNo) AS SlotDisplay,
       sm.LocationNo
FROM dbo.FVN_Slot s
JOIN dbo.FVN_Rack r ON r.SiteCode=s.SiteCode AND r.RackId=s.RackId AND r.IsActive=1
JOIN dbo.FVN_Warehouse w ON w.SiteCode=r.SiteCode AND w.WarehouseId=r.WarehouseId AND w.IsActive=1
JOIN dbo.FVN_SlotMesLocation sm ON sm.SiteCode=s.SiteCode AND sm.SlotId=s.SlotId
 AND sm.IsPrimary=1 AND sm.IsActive=1
JOIN MES.FVN_IfsInventoryLocation loc ON loc.SiteCode=sm.SiteCode AND loc.LocationNo=sm.LocationNo AND loc.IsActive=1
WHERE s.SiteCode=@SiteCode AND s.IsActive=1
ORDER BY w.Name,r.RackName,s.SlotNumber;", cn))
            {
                cmd.Parameters.Add("@SiteCode", SqlDbType.NVarChar, 30).Value = siteCode;
                using (var da = new SqlDataAdapter(cmd)) da.Fill(table);
            }
            return table;
        }
    }
}