using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Shared.Common;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;


namespace PCTP.Modules.KhoVatLy.Repositories
{
    public sealed class RackRepository
      : SqlRepositoryBase, IRackRepository
    {
        private readonly ISiteContext _site;

        public RackRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow,
            ISiteContext site = null)
            : base(db, uow)
        {
            _site = site ?? AppSettingsSiteContext.Default;
        }

        private SqlParameter SiteParam()
            => new SqlParameter("@site", SqlDbType.NVarChar, 30) { Value = _site.SiteCode };

        public string GetWarehouseNameById(int warehouseId)
        {
            object value = ExecuteScalar(
                "SELECT TOP 1 Name FROM dbo.FVN_Warehouse WHERE SiteCode=@site AND WarehouseId=@Id;",
                SiteParam(), new SqlParameter("@Id", SqlDbType.Int) { Value = warehouseId });
            return value == null || value == DBNull.Value ? null : Convert.ToString(value);
        }

        public string GetWarehouseNameByRackId(int rackId)
        {
            object value = ExecuteScalar(@"
SELECT TOP 1 w.Name
FROM dbo.FVN_Rack r
JOIN dbo.FVN_Warehouse w ON w.SiteCode=r.SiteCode AND w.WarehouseId=r.WarehouseId
WHERE r.SiteCode=@site AND r.RackId=@Id;",
                SiteParam(), new SqlParameter("@Id", SqlDbType.Int) { Value = rackId });
            return value == null || value == DBNull.Value ? null : Convert.ToString(value);
        }
        public int Create(int warehouseId, string rackName, int rowCount, int columnCount)
        {
            if (warehouseId <= 0)
                throw new ArgumentException("WarehouseId không hợp lệ.", nameof(warehouseId));
            if (string.IsNullOrWhiteSpace(rackName))
                throw new ArgumentException("Tên Rack không được rỗng.", nameof(rackName));

            const string sql = @"
        INSERT INTO dbo.FVN_Rack (SiteCode, WarehouseId, RackName, RackRowCount, ColumnCount)
        OUTPUT INSERTED.RackId
        VALUES (@site, @WarehouseId, @RackName, @RowCount, @ColumnCount);";

            object result = ExecuteScalar(sql,
                SiteParam(),
                new SqlParameter("@WarehouseId", SqlDbType.Int) { Value = warehouseId },
                new SqlParameter("@RackName", SqlDbType.NVarChar, 100) { Value = rackName.Trim() },
                new SqlParameter("@RowCount", SqlDbType.Int) { Value = rowCount },
                new SqlParameter("@ColumnCount", SqlDbType.Int) { Value = columnCount });

            int rackId = DbValueHelper.ToInt(result);
            if (rackId <= 0)
                throw new InvalidOperationException("Không thể tạo Rack.");

            return rackId;
        }
        // ============================================================
        // GET RACK BY ID
        // ============================================================

        public Rack GetById(int rackId)
        {
            if (rackId <= 0)
                return null;

            const string sql = @"
            SELECT
                RackId,
                WarehouseId,
                RackName,
                RackRowCount,
                ColumnCount
            FROM dbo.FVN_Rack
            WHERE SiteCode=@site AND RackId = @RackId;";

            DataTable dt = LoadData(
                sql,
                SiteParam(),
                new SqlParameter("@RackId", SqlDbType.Int)
                {
                    Value = rackId
                });

            if (dt == null || dt.Rows.Count == 0)
                return null;

            return MapRack(dt.Rows[0]);
        }

        // ============================================================
        // GET RACKS BY WAREHOUSE
        // ============================================================

        public List<Rack> GetRacksByWarehouse(int warehouseId)
        {
            if (warehouseId <= 0)
                return new List<Rack>();

            const string sql = @"
            SELECT
                RackId,
                WarehouseId,
                RackName,
                RackRowCount,
                ColumnCount
            FROM dbo.FVN_Rack
            WHERE SiteCode=@site AND WarehouseId = @WarehouseId
            ORDER BY RackId;";

            DataTable dt = LoadData(
                sql,
                SiteParam(),
                new SqlParameter("@WarehouseId", SqlDbType.Int)
                {
                    Value = warehouseId
                });

            var racks = new List<Rack>();

            if (dt == null)
                return racks;

            foreach (DataRow row in dt.Rows)
                racks.Add(MapRack(row));

            return racks;
        }

        // ============================================================
        // INSERT
        // ============================================================

        public int Insert(int warehouseId, Rack rack)
        {
            if (warehouseId <= 0)
                throw new ArgumentException(
                    "WarehouseId không hợp lệ.",
                    nameof(warehouseId));

            if (rack == null)
                throw new ArgumentNullException(nameof(rack));

            if (string.IsNullOrWhiteSpace(rack.Name))
                throw new ArgumentException(
                    "Tên Rack không được rỗng.",
                    nameof(rack));

            const string sql = @"
            INSERT INTO dbo.FVN_Rack
            (
                SiteCode,
                WarehouseId,
                RackName,
                RackRowCount,
                ColumnCount
            )
            OUTPUT INSERTED.RackId
            VALUES
            (
                @site,
                @WarehouseId,
                @RackName,
                @RackRowCount,
                @ColumnCount
            );";

            object result = ExecuteScalar(
                sql,
                SiteParam(),
                new SqlParameter("@WarehouseId", SqlDbType.Int)
                {
                    Value = warehouseId
                },
                new SqlParameter("@RackName", SqlDbType.NVarChar, 100)
                {
                    Value = rack.Name.Trim()
                },
                new SqlParameter("@RackRowCount", SqlDbType.Int)
                {
                    Value = rack.RackRowCount
                },
                new SqlParameter("@ColumnCount", SqlDbType.Int)
                {
                    Value = rack.ColumnCount
                });

            return DbValueHelper.ToInt(result);
        }

        // ============================================================
        // UPDATE
        // ============================================================

        public void Update(Rack rack)
        {
            if (rack == null)
                throw new ArgumentNullException(nameof(rack));

            if (rack.RackId <= 0)
                throw new ArgumentException(
                    "RackId không hợp lệ.",
                    nameof(rack));

            if (string.IsNullOrWhiteSpace(rack.Name))
                throw new ArgumentException(
                    "Tên Rack không được rỗng.",
                    nameof(rack));

            const string sql = @"
            UPDATE dbo.FVN_Rack
            SET
                WarehouseId = @WarehouseId,
                RackName = @RackName,
                RackRowCount = @RackRowCount,
                ColumnCount = @ColumnCount
            WHERE SiteCode=@site AND RackId = @RackId;";

            int affected = ExecuteNonQuery(
                sql,
                SiteParam(),
                new SqlParameter("@RackId", SqlDbType.Int)
                {
                    Value = rack.RackId
                },
                new SqlParameter("@WarehouseId", SqlDbType.Int)
                {
                    Value = rack.WarehouseId
                },
                new SqlParameter("@RackName", SqlDbType.NVarChar, 100)
                {
                    Value = rack.Name.Trim()
                },
                new SqlParameter("@RackRowCount", SqlDbType.Int)
                {
                    Value = rack.RackRowCount
                },
                new SqlParameter("@ColumnCount", SqlDbType.Int)
                {
                    Value = rack.ColumnCount
                });

            if (affected == 0)
                throw new InvalidOperationException(
                    $"Không tìm thấy RackId [{rack.RackId}].");
        }

        // ============================================================
        // DELETE
        // ============================================================

        public void Delete(int rackId)
        {
            if (rackId <= 0)
                throw new ArgumentException(
                    "RackId không hợp lệ.",
                    nameof(rackId));

            const string sql = @"
            DELETE FROM dbo.FVN_Rack
            WHERE SiteCode=@site AND RackId = @RackId;";

            ExecuteNonQuery(
                sql,
                SiteParam(),
                new SqlParameter("@RackId", SqlDbType.Int)
                {
                    Value = rackId
                });
        }

        // ============================================================
        // CHECK EXIST
        // ============================================================

        public bool Exists(int warehouseId, string rackName)
        {
            if (warehouseId <= 0)
                return false;

            if (string.IsNullOrWhiteSpace(rackName))
                return false;

            const string sql = @"
            SELECT COUNT(1)
            FROM dbo.FVN_Rack
            WHERE SiteCode=@site AND WarehouseId = @WarehouseId
              AND RackName = @RackName;";

            object result = ExecuteScalar(
                sql,
                SiteParam(),
                new SqlParameter("@WarehouseId", SqlDbType.Int)
                {
                    Value = warehouseId
                },
                new SqlParameter("@RackName", SqlDbType.NVarChar, 100)
                {
                    Value = rackName.Trim()
                });

            return DbValueHelper.ToInt(result) > 0;
        }

        // ============================================================
        // GET RACK ID
        // ============================================================

        public int GetRackId(
            int warehouseId,
            string rackName)
        {
            if (warehouseId <= 0)
                return 0;

            if (string.IsNullOrWhiteSpace(rackName))
                return 0;

            const string sql = @"
            SELECT TOP (1)
                RackId
            FROM dbo.FVN_Rack
            WHERE SiteCode=@site AND WarehouseId = @WarehouseId
              AND RackName = @RackName
            ORDER BY RackId;";

            object result = ExecuteScalar(
                sql,
                SiteParam(),
                new SqlParameter("@WarehouseId", SqlDbType.Int)
                {
                    Value = warehouseId
                },
                new SqlParameter("@RackName", SqlDbType.NVarChar, 100)
                {
                    Value = rackName.Trim()
                });

            return DbValueHelper.ToInt(result);
        }

        // ============================================================
        // UPDATE LAYOUT
        // ============================================================

        public void UpdateLayout(
            int rackId,
            int rowCount,
            int columnCount)
        {
            if (rackId <= 0)
                throw new ArgumentException(
                    "RackId không hợp lệ.",
                    nameof(rackId));

            if (rowCount < 0)
                throw new ArgumentException(
                    "Số dòng không hợp lệ.",
                    nameof(rowCount));

            if (columnCount < 0)
                throw new ArgumentException(
                    "Số cột không hợp lệ.",
                    nameof(columnCount));

            const string sql = @"
            UPDATE dbo.FVN_Rack
            SET
                RackRowCount = @RackRowCount,
                ColumnCount = @ColumnCount
            WHERE SiteCode=@site AND RackId = @RackId;";

            ExecuteNonQuery(
                sql,
                SiteParam(),
                new SqlParameter("@RackId", SqlDbType.Int)
                {
                    Value = rackId
                },
                new SqlParameter("@RackRowCount", SqlDbType.Int)
                {
                    Value = rowCount
                },
                new SqlParameter("@ColumnCount", SqlDbType.Int)
                {
                    Value = columnCount
                });
        }
        public void InsertSlot(int rackId, int slotNumber, int capacity)
        {
            if (rackId <= 0)
                throw new ArgumentException("RackId không hợp lệ.", nameof(rackId));

            const string sql = @"
        INSERT INTO dbo.FVN_Slot (SiteCode, RackId, SlotNumber, IsOccupied, Capacity, Quantity)
        VALUES (@site, @RackId, @SlotNumber, 0, @Capacity, 0);";

            ExecuteNonQuery(sql,
                SiteParam(),
                new SqlParameter("@RackId", SqlDbType.Int) { Value = rackId },
                new SqlParameter("@SlotNumber", SqlDbType.Int) { Value = slotNumber },
                new SqlParameter("@Capacity", SqlDbType.Int) { Value = capacity });
        }

        public List<RackRenderInfo> GetRackRenderInfos()
        {
            const string sql = @"
        SELECT
            w.Name AS WarehouseName, r.RackName, r.RackId,
            s.SlotId, s.SlotNumber, s.ItemCode, s.Quantity, s.Capacity,
            s.ImportDate, s.IsOccupied,
            sl.LotNo, sl.ItemCode AS LotItemCode, sl.Quantity AS LotQuantity,
            sl.TemCode AS LotTemCode, sl.QrData, sl.ImportDate AS LotImportDate,
            sl.NgaySX, sl.SoPhieuTong, sl.MaPhieu
        FROM dbo.FVN_Warehouse w
        INNER JOIN dbo.FVN_Rack r ON r.SiteCode=w.SiteCode AND r.WarehouseId = w.WarehouseId
        LEFT JOIN dbo.FVN_Slot s ON s.SiteCode=r.SiteCode AND s.RackId = r.RackId
        LEFT JOIN dbo.FVN_SlotLot sl ON sl.SiteCode=s.SiteCode AND sl.SlotId = s.SlotId AND sl.PhieuStatus <> 3
        WHERE w.SiteCode=@site AND r.SiteCode=@site
        ORDER BY w.Name, r.RackName, s.SlotNumber, sl.LotNo";

            DataTable dt = LoadData(sql, SiteParam());

            var rackDict = new Dictionary<string, RackRenderInfo>();
            var slotDict = new Dictionary<int, Slot>();

            foreach (DataRow row in dt.Rows)
            {
                string whName = row["WarehouseName"].ToString();
                string rackName = row["RackName"].ToString();
                string key = $"{whName}_{rackName}";

                if (!rackDict.TryGetValue(key, out var rackInfo))
                {
                    rackInfo = new RackRenderInfo
                    {
                        WarehouseName = whName,
                        RackName = rackName,
                        RackId = Convert.ToInt32(row["RackId"]),
                        Slots = new List<SlotRenderInfo>(),
                        ItemSummary = new Dictionary<string, (int, int)>()
                    };
                    rackDict[key] = rackInfo;
                }

                if (row["SlotNumber"] is DBNull) continue;

                int slotId = Convert.ToInt32(row["SlotId"]);
                if (!slotDict.TryGetValue(slotId, out var slot))
                {
                    slot = new Slot
                    {
                        SlotId = slotId,
                        whname = whName,
                        RackName = rackName,
                        Rackid = Convert.ToInt32(row["RackId"]),
                        SlotNumber = Convert.ToInt32(row["SlotNumber"]),
                        ItemCode = row["ItemCode"]?.ToString(),
                        Quantity = row["Quantity"] is DBNull ? 0 : Convert.ToInt32(row["Quantity"]),
                        Capacity = row["Capacity"] is DBNull ? 0 : Convert.ToInt32(row["Capacity"]),
                        ImportDate = row["ImportDate"] is DBNull ? DateTime.MinValue : Convert.ToDateTime(row["ImportDate"]),
                        IsOccupied = row["IsOccupied"] is DBNull ? false : Convert.ToBoolean(row["IsOccupied"]),
                        Lots = new List<LotInfo>()
                    };
                    slotDict[slotId] = slot;
                    rackInfo.Slots.Add(new SlotRenderInfo { Slot = slot, RackName = rackName, WarehouseName = whName });
                }

                if (row["LotNo"] != DBNull.Value)
                {
                    slot.Lots.Add(new LotInfo
                    {
                        LotNo = row["LotNo"].ToString(),
                        Quantity = row["LotQuantity"] is DBNull ? 0 : Convert.ToInt32(row["LotQuantity"]),
                        TemCode = row["LotTemCode"]?.ToString(),
                        RawQr = row["QrData"]?.ToString(),
                        QRInfo = new QRCodeInfo
                        {
                            ItemCode = row["LotItemCode"]?.ToString(),
                            NgaySX = row["NgaySX"]?.ToString(),
                            SoPhieuTong = row["SoPhieuTong"]?.ToString(),
                            MaPhieu = row["MaPhieu"]?.ToString(),
                            RawQr = row["QrData"]?.ToString()
                        }
                    });
                }
            }

            foreach (var info in rackDict.Values)
            {
                info.SlotCount = info.Slots.Count;
                info.EmptySlotCount = info.Slots.Count(s => !s.Slot.IsOccupied);

                foreach (var sr in info.Slots)
                    foreach (var lot in sr.Slot.Lots)
                    {
                        string itemCode = lot.QRInfo?.ItemCode;
                        if (string.IsNullOrEmpty(itemCode) || lot.Quantity <= 0) continue;

                        if (info.ItemSummary.TryGetValue(itemCode, out var s))
                            info.ItemSummary[itemCode] = (s.Item1 + 1, s.Item2 + lot.Quantity);
                        else
                            info.ItemSummary[itemCode] = (1, lot.Quantity);
                    }
            }

            return rackDict.Values.ToList();
        }

        public void DeleteCascade(int rackId)
        {
            if (rackId <= 0) throw new ArgumentException("RackId không hợp lệ.", nameof(rackId));

            // Xoá Slot rồi Rack phải atomic: không để lại Rack mất Slot (hoặc ngược lại).
            InTransaction(() =>
            {
                ExecuteNonQuery("DELETE FROM dbo.FVN_Slot WHERE SiteCode=@site AND RackId = @RackId",
                    SiteParam(), new SqlParameter("@RackId", SqlDbType.Int) { Value = rackId });
                ExecuteNonQuery("DELETE FROM dbo.FVN_Rack WHERE SiteCode=@site AND RackId = @RackId",
                    SiteParam(), new SqlParameter("@RackId", SqlDbType.Int) { Value = rackId });
            });
        }
        // ============================================================
        // MAPPING
        // ============================================================

        private static Rack MapRack(DataRow row)
        {
            return new Rack
            {
                RackId = DbValueHelper.ToInt(row["RackId"]),

                WarehouseId = DbValueHelper.ToInt(row["WarehouseId"]),

                Name = row["RackName"] == DBNull.Value
                    ? string.Empty
                    : row["RackName"].ToString(),

                RackRowCount = DbValueHelper.ToInt(row["RackRowCount"]),

                ColumnCount = DbValueHelper.ToInt(row["ColumnCount"]),

                Slots = new List<Slot>()
            };
        }
    }
}
