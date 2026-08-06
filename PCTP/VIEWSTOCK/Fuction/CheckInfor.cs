using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PCTP.ClassSQL;
using DevExpress.XtraEditors;
using PCTP.VIEWSTOCK.Models;

namespace PCTP.VIEWSTOCK.Fuction
{
    public class CheckInfor
    {
        private SQLPROVIDER sql;


        public CheckInfor()
        {
            sql = new SQLPROVIDER();
        }
        public bool IsWarehouseExists(string warehouseName)
        {
            string query = "SELECT COUNT(*) FROM Warehouse WHERE Name = @Name";
            SqlParameter[] parameters = {
                new SqlParameter("@Name", warehouseName)
            };

            object result = sql.ExecuteScalar(sql.B7R2_FCCdbb, query, parameters);
            return Convert.ToInt32(result) > 0;
        }
        public void LoadWarehouseData(string warehouseName, ListBoxControl cmbRack)
        {
            string query = @"
                SELECT w.WarehouseId, r.RackName 
                FROM Warehouse w
                LEFT JOIN Rack r ON r.WarehouseId = w.WarehouseId
                WHERE w.Name = @Name";

            SqlParameter[] parameters = { new SqlParameter("@Name", warehouseName) };
            DataTable dt = sql.LoadData1(sql.B7R2_FCCdbb, query, parameters);

            if (dt.Rows.Count > 0)
            {
                cmbRack.Items.Clear();

                foreach (DataRow row in dt.Rows)
                {
                    if (row["RackName"] != DBNull.Value)
                        cmbRack.Items.Add(row["RackName"].ToString());
                }

                MessageBox.Show("Warehouse đã tồn tại. Dữ liệu đã được tải.", "Thông báo");
            }
        }
        public void AddRackToExistingWarehouse(string warehouseName, Rack newRack)
        {
            string findQuery = "SELECT WarehouseId FROM Warehouse WHERE Name = @Name";
            object warehouseIdObj = sql.ExecuteScalar(sql.B7R2_FCCdbb, findQuery,
                new[] { new SqlParameter("@Name", warehouseName) });

            if (warehouseIdObj == null || warehouseIdObj == DBNull.Value)
            {
                MessageBox.Show($"Không tìm thấy kho '{warehouseName}'!",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int warehouseId = Convert.ToInt32(warehouseIdObj);

            string insertRackQuery = @"
        INSERT INTO Rack (RackName, WarehouseId) 
        VALUES (@RackName, @WarehouseId); 
        SELECT SCOPE_IDENTITY();";

            int newRackId = Convert.ToInt32(
                sql.ExecuteScalar(sql.B7R2_FCCdbb, insertRackQuery, new[]
                {
            new SqlParameter("@RackName",    SqlDbType.NVarChar) { Value = newRack.Name },
            new SqlParameter("@WarehouseId", SqlDbType.Int)      { Value = warehouseId  }
                }));

            foreach (var slot in newRack.Slots)
            {
                // ✅ KIẾN TRÚC MỚI: bảng Slot không còn cột TemCode/LotNo (đã chuyển hẳn sang
                // bảng SlotLot). Slot mới tạo luôn trống — chưa có Lot nào — nên không cần
                // (và không được) insert 2 cột này vào Slot nữa.
                string insertSlotQuery = @"
            INSERT INTO Slot 
                (SlotNumber, IsOccupied, RackId, Capacity, 
                 ItemCode,   Quantity, ImportDate)
            VALUES 
                (@SlotNumber, @IsOccupied, @RackId, @Capacity,
                 @ItemCode,   @Quantity, @ImportDate)";

                sql.ExecuteScalar(sql.B7R2_FCCdbb, insertSlotQuery, new[]
                {
            new SqlParameter("@SlotNumber", SqlDbType.Int)          { Value = slot.SlotNumber },
            new SqlParameter("@IsOccupied", SqlDbType.Bit)          { Value = false           },
            new SqlParameter("@RackId",     SqlDbType.Int)          { Value = newRackId       },
            new SqlParameter("@Capacity",   SqlDbType.Int)          { Value = slot.Capacity   },
            new SqlParameter("@ItemCode",   SqlDbType.NVarChar, -1) { Value = DBNull.Value    },
            new SqlParameter("@Quantity",   SqlDbType.Int)          { Value = 0               },
            new SqlParameter("@ImportDate", SqlDbType.DateTime)     { Value = DBNull.Value    },
        });
            }
        }
        public InspectionConfig GetInspectionConfig(string itemCode)
        {
            string q = @"
        SELECT ConfigId, ItemCode, DefaultQty, 
               CheckItemCode, CheckLotNo, CheckNSX
        FROM   InspectionConfig
        WHERE  ItemCode  = @ItemCode 
          AND  IsActive  = 1";

            DataTable dt = sql.LoadData1(sql.B7R2_FCCdbb, q,
                new[] { new SqlParameter("@ItemCode", itemCode) });

            if (dt.Rows.Count == 0) return null; // ✅ null = không cần kiểm tra

            var row = dt.Rows[0];
            return new InspectionConfig
            {
                ConfigId = Convert.ToInt32(row["ConfigId"]),
                ItemCode = row["ItemCode"].ToString(),
                DefaultQty = Convert.ToInt32(row["DefaultQty"]),
                CheckItemCode = Convert.ToBoolean(row["CheckItemCode"]),
                CheckLotNo = Convert.ToBoolean(row["CheckLotNo"]),
                CheckNSX = Convert.ToBoolean(row["CheckNSX"]),
            };
        }
        public List<string> GetEmptySlots(string warehouseCode, string itemCode, int soLuongNhap)
        {
            List<string> emptySlots = new List<string>();
            using (SqlConnection conn = new SqlConnection(sql.B7R2_FCCdbb))
            {
                conn.Open();
                // ✅ KIẾN TRÚC MỚI: bảng Slot không còn cột TemCode — gộp TemCode của các Lot
                // đang nằm trong Slot (bảng SlotLot) bằng FOR XML PATH (tương thích SQL Server cũ, không cần STRING_AGG 2017+)
                string query = @"
                    SELECT w.Name AS WarehouseName, r.RackName, s.SlotNumber, 
                           s.IsOccupied, s.Quantity, s.Capacity,
                           STUFF((
                               SELECT ',' + sl.TemCode
                               FROM SlotLot sl
                               WHERE sl.SlotId = s.SlotId
                               FOR XML PATH(''), TYPE
                           ).value('.', 'NVARCHAR(MAX)'), 1, 1, '') AS TemCode
                    FROM Slot s
                    JOIN Rack r     ON s.RackId      = r.RackId
                    JOIN Warehouse w ON r.WarehouseId = w.WarehouseId
                    WHERE 
                        (s.ItemCode = @ItemCode AND (s.Capacity - s.Quantity) >= @SoLuongNhap)
                        OR 
                        (s.IsOccupied = 0)
                    ORDER BY 
                        CASE WHEN s.ItemCode = @ItemCode THEN 0 ELSE 1 END,
                        w.Name, r.RackName, s.SlotNumber";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ItemCode", itemCode);
                    cmd.Parameters.AddWithValue("@SoLuongNhap", soLuongNhap);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string wh = reader["WarehouseName"].ToString();
                            string rack = reader["RackName"].ToString();
                            int slotNum = Convert.ToInt32(reader["SlotNumber"]);
                            int capacity = reader.IsDBNull(reader.GetOrdinal("Capacity"))
                                                ? 0 : Convert.ToInt32(reader["Capacity"]);
                            bool isOccupied = Convert.ToBoolean(reader["IsOccupied"]);
                            string display = $"WH : {wh} - Rack : {rack} - Slot : {slotNum} - Capacity : {capacity}";
                            if (isOccupied)
                            {
                                string temCode = reader["TemCode"]?.ToString() ?? "";
                                int quantity = reader["Quantity"] is DBNull ? 0 : Convert.ToInt32(reader["Quantity"]);
                                display += $" - TemCode: {temCode} - Qty: {quantity}";
                            }
                            emptySlots.Add(display);
                        }
                    }
                }
            }
            return emptySlots;
        }


    }
}
