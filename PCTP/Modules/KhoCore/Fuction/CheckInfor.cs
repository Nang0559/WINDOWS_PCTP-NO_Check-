using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Windows.Forms;
using PCTP.ClassSQL;
using DevExpress.XtraEditors;
using PCTP.Modules.KhoCore.Models;
using PCTP.Shared.Models;

namespace PCTP.Modules.KhoCore.Fuction
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
            if (string.IsNullOrWhiteSpace(warehouseName))
                throw new ArgumentException("Tên kho không được để trống.", nameof(warehouseName));
            if (newRack == null)
                throw new ArgumentNullException(nameof(newRack));
            if (newRack.Slots == null)
                throw new ArgumentException("Danh sách Slot không được null.", nameof(newRack));

            // Rack header and every Slot row form one business operation. Use one
            // connection/transaction so a failed Slot insert cannot leave a partial Rack.
            using (SqlConnection conn = new SqlConnection(sql.B7R2_FCCdbb))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        int warehouseId;
                        using (SqlCommand find = new SqlCommand(@"
SELECT WarehouseId
FROM Warehouse WITH (UPDLOCK, HOLDLOCK)
WHERE Name = @Name;", conn, transaction))
                        {
                            find.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = warehouseName.Trim();
                            object value = find.ExecuteScalar();
                            if (value == null || value == DBNull.Value)
                                throw new InvalidOperationException("Không tìm thấy kho '" + warehouseName + "'.");
                            warehouseId = Convert.ToInt32(value);
                        }

                        int newRackId;
                        using (SqlCommand insertRack = new SqlCommand(@"
INSERT INTO Rack (RackName, WarehouseId)
VALUES (@RackName, @WarehouseId);
SELECT CAST(SCOPE_IDENTITY() AS int);", conn, transaction))
                        {
                            insertRack.Parameters.Add("@RackName", SqlDbType.NVarChar, 255).Value =
                                (object)newRack.Name ?? DBNull.Value;
                            insertRack.Parameters.Add("@WarehouseId", SqlDbType.Int).Value = warehouseId;
                            object value = insertRack.ExecuteScalar();
                            if (value == null || value == DBNull.Value)
                                throw new InvalidOperationException("Không lấy được RackId vừa tạo.");
                            newRackId = Convert.ToInt32(value);
                        }

                        foreach (var slot in newRack.Slots)
                        {
                            if (slot == null)
                                throw new InvalidOperationException("Danh sách Slot chứa phần tử null.");
                            if (slot.SlotNumber <= 0)
                                throw new InvalidOperationException("SlotNumber phải lớn hơn 0.");
                            if (slot.Capacity < 0)
                                throw new InvalidOperationException("Capacity không được âm.");

                            using (SqlCommand insertSlot = new SqlCommand(@"
INSERT INTO Slot
    (SlotNumber, IsOccupied, RackId, Capacity, ItemCode, Quantity, ImportDate)
VALUES
    (@SlotNumber, 0, @RackId, @Capacity, NULL, 0, NULL);", conn, transaction))
                            {
                                insertSlot.Parameters.Add("@SlotNumber", SqlDbType.Int).Value = slot.SlotNumber;
                                insertSlot.Parameters.Add("@RackId", SqlDbType.Int).Value = newRackId;
                                insertSlot.Parameters.Add("@Capacity", SqlDbType.Int).Value = slot.Capacity;
                                insertSlot.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        try { transaction.Rollback(); }
                        catch { /* Preserve the original exception. */ }
                        throw;
                    }
                }
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
                    WHERE w.Name <> @BulkWh   -- ✅ THÊM: loại bỏ Slot ảo khỏi kết quả chọn thường
                      AND (
                        (s.ItemCode = @ItemCode AND (s.Capacity - s.Quantity) >= @SoLuongNhap)
                        OR 
                        (s.IsOccupied = 0)
                      )
                    ORDER BY 
                        CASE WHEN s.ItemCode = @ItemCode THEN 0 ELSE 1 END,
                        w.Name, r.RackName, s.SlotNumber";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ItemCode", itemCode);
                    cmd.Parameters.AddWithValue("@SoLuongNhap", soLuongNhap);
                    cmd.Parameters.AddWithValue("@BulkWh", BulkImportConfig.WarehouseName);
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
