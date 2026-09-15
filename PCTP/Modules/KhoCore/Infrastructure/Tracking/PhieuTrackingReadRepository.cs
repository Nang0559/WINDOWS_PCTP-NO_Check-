using PCTP.Infrastructure.Repositories;
using PCTP.Models;
using PCTP.Modules.KhoCore.Application.Contracts.Tracking;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Modules.KhoCore.Infrastructure.Tracking
{
    /// <summary>
    /// Read-only adapter for legacy SlotLot tracking queries.
    /// No INSERT/UPDATE is allowed here.
    /// </summary>
    public sealed class PhieuTrackingReadRepository : SqlRepositoryBase, IPhieuTrackingReadRepository
    {
        public PhieuTrackingReadRepository(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow)
        {
        }

        public List<PhieuLocationInfo> GetPhieuTheoLot(string lotNo)
        {
            if (string.IsNullOrWhiteSpace(lotNo))
                return new List<PhieuLocationInfo>();

            const string sql = @"
SELECT
    sl.SlotId,
    sl.LotNo,
    sl.Quantity,
    sl.MaPhieu,
    sl.ParentSoPhieu,
    sl.SoPhieuTong,
    sl.PhieuStatus,
    sl.ImportDate,
    s.SlotNumber,
    r.RackName,
    w.Name AS WarehouseName
FROM SlotLot sl
INNER JOIN Slot s ON s.SlotId = sl.SlotId
INNER JOIN Rack r ON r.RackId = s.RackId
INNER JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
WHERE sl.LotNo = @LotNo
ORDER BY sl.PhieuStatus, sl.ImportDate;";

            DataTable dt = LoadData(sql,
                new SqlParameter("@LotNo", SqlDbType.NVarChar, 100)
                {
                    Value = lotNo.Trim()
                });

            var list = new List<PhieuLocationInfo>();
            if (dt == null || dt.Rows.Count == 0)
                return list;

            foreach (DataRow r in dt.Rows)
            {
                list.Add(new PhieuLocationInfo
                {
                    LotNo = r["LotNo"] as string,
                    MaPhieu = r["MaPhieu"] as string,
                    ParentSoPhieu = r["ParentSoPhieu"] as string,
                    SoPhieuTong = r["SoPhieuTong"] as string,
                    Status = (PhieuStatus)DbValueHelper.ToInt(r["PhieuStatus"]),
                    Quantity = DbValueHelper.ToInt(r["Quantity"]),
                    SlotId = DbValueHelper.ToInt(r["SlotId"]),
                    SlotNumber = DbValueHelper.ToInt(r["SlotNumber"]),
                    RackName = r["RackName"] as string,
                    WarehouseName = r["WarehouseName"] as string,
                    ImportDate = r["ImportDate"] == DBNull.Value
                        ? (DateTime?)null
                        : Convert.ToDateTime(r["ImportDate"])
                });
            }

            return list;
        }

        public int GetTongSlActiveTheoLot(string lotNo)
        {
            if (string.IsNullOrWhiteSpace(lotNo))
                return 0;

            const string sql = @"
SELECT ISNULL(SUM(Quantity), 0)
FROM SlotLot
WHERE LotNo = @LotNo
  AND PhieuStatus = 0;";

            object result = ExecuteScalar(sql,
                new SqlParameter("@LotNo", SqlDbType.NVarChar, 100)
                {
                    Value = lotNo.Trim()
                });

            return DbValueHelper.ToInt(result);
        }

        public bool ExistsQrData(string qrData)
        {
            if (string.IsNullOrWhiteSpace(qrData))
                return false;

            const string sql = @"
SELECT COUNT(*)
FROM SlotLot
WHERE QrData = @QrData;";

            object result = ExecuteScalar(sql,
                new SqlParameter("@QrData", SqlDbType.NVarChar, -1)
                {
                    Value = qrData.Trim()
                });

            return DbValueHelper.ToInt(result) > 0;
        }
    }
}
