using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
namespace PCTP.Modules.MesPlanning
{
    public sealed class ComponentDemandRepository : IComponentDemandRepository
    {
        private readonly string _cs;
        public ComponentDemandRepository(string connectionString = null)
        {
            _cs=connectionString ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if(string.IsNullOrWhiteSpace(_cs)) throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }
        public DataTable GetPlanLines(string siteCode)
        {
            using(var cn=new SqlConnection(_cs))
            using(var cmd=new SqlCommand("MES.usp_FVN_ProductionPlan_List",cn))
            {
                cmd.CommandType=CommandType.StoredProcedure;
                cmd.Parameters.Add("@SiteCode",SqlDbType.NVarChar,30).Value=string.IsNullOrWhiteSpace(siteCode)?(object)DBNull.Value:siteCode.Trim();
                var dt=new DataTable(); using(var da=new SqlDataAdapter(cmd)) da.Fill(dt); return dt;
            }
        }
        public long GenerateDemand(long planLineId,string user)
        {
            using(var cn=new SqlConnection(_cs))
            using(var cmd=new SqlCommand("MES.usp_FVN_GenerateComponentDemand",cn))
            {
                cmd.CommandType=CommandType.StoredProcedure;
                cmd.Parameters.Add("@ProductionPlanLineId",SqlDbType.BigInt).Value=planLineId;
                cmd.Parameters.Add("@CreatedBy",SqlDbType.NVarChar,100).Value=user;
                cn.Open(); using(var rd=cmd.ExecuteReader())
                { if(!rd.Read()) throw new InvalidOperationException("Không nhận được mã yêu cầu linh kiện."); return Convert.ToInt64(rd["ComponentDemandId"]); }
            }
        }
        public DataTable GetDemandLines(long planLineId)
        {
            using(var cn=new SqlConnection(_cs))
            using(var cmd=new SqlCommand("MES.usp_FVN_ComponentDemand_List",cn))
            {
                cmd.CommandType=CommandType.StoredProcedure; cmd.Parameters.Add("@ProductionPlanLineId",SqlDbType.BigInt).Value=planLineId;
                var dt=new DataTable(); using(var da=new SqlDataAdapter(cmd)) da.Fill(dt); return dt;
            }
        }
        public DataTable GetPartLots(string site,string partNo)
        {
            using(var cn=new SqlConnection(_cs))
            using(var cmd=new SqlCommand(@"
SELECT b.MaterialLotId,lot.LotNo,sm.SlotId,
       CONCAT(lot.LotNo,N' | Slot ',sm.SlotId,N' | khả dụng ',CONVERT(nvarchar(40),b.OnHandQty-b.ReservedQty)) AS LotSlotDisplay,
       b.OnHandQty-b.ReservedQty AS AvailableQty
FROM MES.FVN_InventoryBalance b
JOIN MES.FVN_MaterialLot lot ON lot.MaterialLotId=b.MaterialLotId AND lot.SiteCode=b.SiteCode AND lot.PartNo=b.PartNo
JOIN dbo.FVN_SlotMesLocation sm ON sm.SiteCode=b.SiteCode AND sm.LocationNo=b.LocationNo AND sm.IsPrimary=1 AND sm.IsActive=1
JOIN dbo.FVN_InventoryAreaSlot ar ON ar.SiteCode=sm.SiteCode AND ar.SlotId=sm.SlotId AND ar.AreaCode='PART' AND ar.IsActive=1
WHERE b.SiteCode=@SiteCode AND b.PartNo=@PartNo AND b.StockState='AVAILABLE'
 AND b.OnHandQty>b.ReservedQty AND lot.LotStatus IN('AVAILABLE','RECEIVED')
ORDER BY lot.LotNo,sm.SlotId;",cn))
            {
                cmd.Parameters.Add("@SiteCode",SqlDbType.NVarChar,30).Value=site;
                cmd.Parameters.Add("@PartNo",SqlDbType.NVarChar,100).Value=partNo;
                var dt=new DataTable(); using(var da=new SqlDataAdapter(cmd)) da.Fill(dt); return dt;
            }
        }
        public DataTable GetWipSlots(string site)
        {
            using(var cn=new SqlConnection(_cs))
            using(var cmd=new SqlCommand(@"
SELECT s.SlotId,CONCAT(w.Name,N' / ',r.RackName,N' / Slot ',s.SlotNumber,N' / ',sm.LocationNo) AS SlotDisplay
FROM dbo.FVN_Slot s
JOIN dbo.FVN_Rack r ON r.SiteCode=s.SiteCode AND r.RackId=s.RackId AND r.IsActive=1
JOIN dbo.FVN_Warehouse w ON w.SiteCode=r.SiteCode AND w.WarehouseId=r.WarehouseId AND w.IsActive=1
JOIN dbo.FVN_SlotMesLocation sm ON sm.SiteCode=s.SiteCode AND sm.SlotId=s.SlotId AND sm.IsPrimary=1 AND sm.IsActive=1
JOIN dbo.FVN_InventoryAreaSlot ar ON ar.SiteCode=s.SiteCode AND ar.SlotId=s.SlotId AND ar.AreaCode='WIP' AND ar.IsActive=1
WHERE s.SiteCode=@SiteCode AND s.IsActive=1 ORDER BY w.Name,r.RackName,s.SlotNumber;",cn))
            {
                cmd.Parameters.Add("@SiteCode",SqlDbType.NVarChar,30).Value=site;
                var dt=new DataTable(); using(var da=new SqlDataAdapter(cmd)) da.Fill(dt); return dt;
            }
        }
        public DataTable PostIssue(string site,string issueNo,long demandLineId,long lotId,int fromSlot,int toSlot,decimal qty,string user,string key)
        {
            using(var cn=new SqlConnection(_cs))
            using(var cmd=new SqlCommand("MES.usp_FVN_ComponentIssue_Post",cn))
            {
                cmd.CommandType=CommandType.StoredProcedure; cmd.CommandTimeout=60;
                cmd.Parameters.Add("@SiteCode",SqlDbType.NVarChar,30).Value=site;
                cmd.Parameters.Add("@IssueNo",SqlDbType.NVarChar,80).Value=issueNo;
                cmd.Parameters.Add("@ComponentDemandLineId",SqlDbType.BigInt).Value=demandLineId;
                cmd.Parameters.Add("@MaterialLotId",SqlDbType.BigInt).Value=lotId;
                cmd.Parameters.Add("@FromSlotId",SqlDbType.Int).Value=fromSlot;
                cmd.Parameters.Add("@ToSlotId",SqlDbType.Int).Value=toSlot;
                var p=cmd.Parameters.Add("@Quantity",SqlDbType.Decimal); p.Precision=19;p.Scale=6;p.Value=qty;
                cmd.Parameters.Add("@IssuedBy",SqlDbType.NVarChar,100).Value=user;
                cmd.Parameters.Add("@IdempotencyKey",SqlDbType.NVarChar,180).Value=key;
                var dt=new DataTable(); using(var da=new SqlDataAdapter(cmd)) da.Fill(dt); return dt;
            }
        }
    }
}