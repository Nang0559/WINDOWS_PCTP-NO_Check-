using PCTP.Modules.BaoCao.Application.Contracts.Models;
using PCTP.Modules.BaoCao.Application.Contracts.Repositories;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Modules.BaoCao.Infrastructure.Repositories
{
    /// <summary>
    /// Read-only adapter for LOT -> physical location.
    /// No write operation belongs here.
    /// </summary>
    public sealed class LotLocationRepository : SqlRepositoryBase, ILotLocationRepository
    {
        public LotLocationRepository(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow)
        {
        }

        public IReadOnlyList<LotLocationRow> GetByLot(string lotNo)
        {
            var result = new List<LotLocationRow>();

            if (string.IsNullOrWhiteSpace(lotNo))
                return result;

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
INNER JOIN Slot s
    ON s.SlotId = sl.SlotId
INNER JOIN Rack r
    ON r.RackId = s.RackId
INNER JOIN Warehouse w
    ON w.WarehouseId = r.WarehouseId
WHERE sl.LotNo = @LotNo
ORDER BY
    sl.PhieuStatus,
    sl.ImportDate;";

            DataTable dt = LoadData(
                sql,
                new SqlParameter("@LotNo", SqlDbType.NVarChar, 100)
                {
                    Value = lotNo.Trim()
                });

            if (dt == null || dt.Rows.Count == 0)
                return result;

            foreach (DataRow row in dt.Rows)
            {
                result.Add(new LotLocationRow
                {
                    SlotId = DbValueHelper.ToInt(row["SlotId"]),
                    LotNo = row["LotNo"] == DBNull.Value ? null : Convert.ToString(row["LotNo"]),
                    Quantity = DbValueHelper.ToInt(row["Quantity"]),
                    MaPhieu = row["MaPhieu"] == DBNull.Value ? null : Convert.ToString(row["MaPhieu"]),
                    ParentSoPhieu = row["ParentSoPhieu"] == DBNull.Value ? null : Convert.ToString(row["ParentSoPhieu"]),
                    SoPhieuTong = row["SoPhieuTong"] == DBNull.Value ? null : Convert.ToString(row["SoPhieuTong"]),
                    PhieuStatus = DbValueHelper.ToInt(row["PhieuStatus"]),
                    ImportDate = row["ImportDate"] == DBNull.Value
                        ? (DateTime?)null
                        : Convert.ToDateTime(row["ImportDate"]),
                    SlotNumber = DbValueHelper.ToInt(row["SlotNumber"]),
                    RackName = row["RackName"] == DBNull.Value ? null : Convert.ToString(row["RackName"]),
                    WarehouseName = row["WarehouseName"] == DBNull.Value ? null : Convert.ToString(row["WarehouseName"])
                });
            }

            return result;
        }
    }
}
