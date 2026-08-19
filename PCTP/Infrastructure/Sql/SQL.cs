using DevExpress.CodeParser;
using DevExpress.XtraEditors;
using Oracle.ManagedDataAccess.Client;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace PCTP.ClassSQL
{
    //public  class SQLPROVIDER
    //{
    //    public static List<string> c_Ns = new List<string>();
    //    //public string B7R2_FCCdb = @"Data Source=192.168.200.14\BRAVO;Initial Catalog=B7R2_FCC;User ID=sa;Password=fccbrv";
    //    public string B7R2_FCCdb = @"Data Source=192.168.200.57;Initial Catalog=B7R2_FCC;User ID=sa;Password=fccbrv";
    //    // Tạo connection to VIEWSTOCK
    //    //public string B7R2_FCCdbb = @"Data Source=192.168.200.14\BRAVO;Initial Catalog=VIEWSTOCK;User ID=sa;Password=fccbrv";
    //    public string B7R2_FCCdbb = @"Data Source=192.168.200.57;Initial Catalog=B7R2_FCC;User ID=sa;Password=fccbrv";


    //    // ════════════════════════════════════════════════════════════════════
    //    // TRANSACTION SUPPORT
    //    // B7R2_FCCdb và B7R2_FCCdbb hiện trỏ CÙNG server + CÙNG catalog
    //    // (192.168.200.57 / B7R2_FCC) → STOCKTP và Slot/SlotLot nằm chung 1 DB.
    //    // Vì vậy dùng được SqlTransaction cục bộ thật (không cần MSDTC,
    //    // không cần prefix catalog trong câu SQL).
    //    //
    //    // Orchestrator mở 1 SqlConnection duy nhất bằng BeginTransaction(),
    //    // truyền (conn, tran) xuống từng bước ghi, rồi Commit/Rollback 1 lần.
    //    // ════════════════════════════════════════════════════════════════════

    //    /// <summary>
    //    /// Mở 1 SqlConnection + bắt đầu transaction. Caller chịu trách nhiệm
    //    /// Commit/Rollback và Dispose (dùng using cho cả conn lẫn tran).
    //    /// Luôn dùng B7R2_FCCdb (hoặc B7R2_FCCdbb — nay là cùng 1 chuỗi).
    //    /// </summary>
    //    public SqlConnection BeginTransaction(string connectionSTR, out SqlTransaction tran)
    //    {
    //        var conn = new SqlConnection(connectionSTR);
    //        conn.Open();
    //        tran = conn.BeginTransaction();
    //        return conn;
    //    }
    //    public string GetProductNameByCode(string itemCode)
    //    {
    //        if (string.IsNullOrEmpty(itemCode)) return "";

    //        // Sử dụng Connection String của VIEWSTOCK để làm gốc truy vấn
    //        string query = @"
    //            SELECT TOP 1 Name 
    //            FROM B7R2_FCC.dbo.vB20Item 
    //            WHERE Code = @ItemCode";

    //        try
    //        {
    //            using (SqlConnection connection = new SqlConnection(B7R2_FCCdb))
    //            {
    //                using (SqlCommand command = new SqlCommand(query, connection))
    //                {
    //                    command.Parameters.AddWithValue("@ItemCode", itemCode);
    //                    connection.Open();

    //                    object result = command.ExecuteScalar();
    //                    return result != null ? result.ToString() : "Không tìm thấy tên SP";
    //                }
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            System.Diagnostics.Debug.WriteLine("Lỗi GetProductNameByCode: " + ex.Message);
    //            return "Lỗi DB: " + ex.Message;
    //        }
    //    }
    //    public void UpdateSlotAfterExport(Slot slot,int SlotId)
    //    {
    //        using (var conn = new SqlConnection(B7R2_FCCdb))
    //        {
    //            conn.Open();
    //            var cmd = new SqlCommand(@"
    //        UPDATE Slot 
    //        SET 
    //            LotNo = @LotNo,
    //            IsOccupied = @IsOccupied,
    //            ItemCode = @ItemCode,
    //            TemCode= @TemCode,
    //            Quantity = @Qty,
    //            Capacity = @Capacity,
    //            ImportDate =@ImportDate


    //        WHERE SlotId = @SlotId", conn);

    //            cmd.Parameters.AddWithValue("@Qty", slot.Quantity);
    //            cmd.Parameters.AddWithValue("@IsOccupied", slot.IsOccupied);
    //            cmd.Parameters.AddWithValue("@ItemCode", (object)slot.ItemCode ?? DBNull.Value);
    //            cmd.Parameters.AddWithValue("@Capacity", (object)slot.Capacity ?? DBNull.Value);
    //            cmd.Parameters.AddWithValue("@TemCode", (object)slot.TemCode ?? DBNull.Value);
    //            cmd.Parameters.AddWithValue("@ImportDate", (object)slot.ImportDate ?? DBNull.Value);
    //            cmd.Parameters.AddWithValue("@LotNo", (object)slot.LotNo ?? DBNull.Value);
    //            cmd.Parameters.AddWithValue("@SlotId", SlotId);
    //            //cmd.Parameters.AddWithValue("@Code", slot.Code);

    //            cmd.ExecuteNonQuery();
    //            // 2. Lấy SlotId từ TemCode (nếu cần log)



    //        }
    //    }


    //    public void SaveWarehouseToDatabase(Warehouse warehouse)
    //    {
    //        SQLPROVIDER sqlProvider = new SQLPROVIDER();
    //        try
    //        {
    //            foreach (var rack in warehouse.Racks)
    //            {
    //                // Insert Warehouse
    //                string insertWarehouseQuery = @"
    //            INSERT INTO Warehouse (Name) 
    //            OUTPUT INSERTED.WarehouseId 
    //            VALUES (@WarehouseName)";

    //                int warehouseId = Convert.ToInt32(
    //                    sqlProvider.ExecuteScalar(sqlProvider.B7R2_FCCdb, insertWarehouseQuery,
    //                    new[] { new SqlParameter("@WarehouseName", warehouse.Name) }));

    //                // Insert Rack
    //                string insertRackQuery = @"
    //            INSERT INTO Rack (WarehouseId, RackName) 
    //            OUTPUT INSERTED.RackId 
    //            VALUES (@WarehouseId, @RackName)";

    //                int rackId = Convert.ToInt32(
    //                    sqlProvider.ExecuteScalar(sqlProvider.B7R2_FCCdb, insertRackQuery, new[]
    //                    {
    //                new SqlParameter("@WarehouseId", warehouseId),
    //                new SqlParameter("@RackName",    rack.Name)
    //                    }));

    //                // ✅ Insert Slot đầy đủ
    //                foreach (var slot in rack.Slots)
    //                {
    //                    string insertSlotQuery = @"
    //                INSERT INTO Slot 
    //                    (RackId, SlotNumber, IsOccupied, Capacity,
    //                     TemCode, ItemCode, LotNo, Quantity, ImportDate)
    //                VALUES 
    //                    (@RackId, @SlotNumber, @IsOccupied, @Capacity,
    //                     @TemCode, @ItemCode, @LotNo, @Quantity, @ImportDate)";

    //                    sqlProvider.ExecuteScalar(sqlProvider.B7R2_FCCdb, insertSlotQuery, new[]
    //                     {
    //                        new SqlParameter("@RackId",     SqlDbType.Int)          { Value = rackId          },
    //                        new SqlParameter("@SlotNumber", SqlDbType.Int)          { Value = slot.SlotNumber },
    //                        new SqlParameter("@IsOccupied", SqlDbType.Bit)          { Value = false           },
    //                        new SqlParameter("@Capacity",   SqlDbType.Int)          { Value = slot.Capacity   },
    //                        new SqlParameter("@TemCode",    SqlDbType.NVarChar, -1) { Value = DBNull.Value    },
    //                        new SqlParameter("@ItemCode",   SqlDbType.NVarChar, -1) { Value = DBNull.Value    },
    //                        new SqlParameter("@LotNo",      SqlDbType.NVarChar, -1) { Value = DBNull.Value    },
    //                        new SqlParameter("@Quantity",   SqlDbType.Int)          { Value = 0               },
    //                        new SqlParameter("@ImportDate", SqlDbType.DateTime)     { Value = DBNull.Value    },
    //                    });
    //                }
    //            }

    //            MessageBox.Show("Lưu Warehouse thành công!", "Thông báo",
    //                MessageBoxButtons.OK, MessageBoxIcon.Information);
    //        }
    //        catch (Exception ex)
    //        {
    //            MessageBox.Show($"Lỗi khi lưu dữ liệu: {ex.Message}",
    //                "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
    //        }
    //    }
    //    public DataTable ExecuteQuery(string connectionSTR, string query, List<SqlParameter> parameter = null)
    //    {
    //        DataTable data = new DataTable();
    //        try
    //        {
    //            using (SqlConnection connection = new SqlConnection(connectionSTR))
    //            {
    //                //connection.Open();

    //                SqlCommand command = new SqlCommand(query, connection);

    //                if (parameter != null)
    //                {
    //                    command.CommandType = CommandType.StoredProcedure;
    //                    command.CommandText = query;
    //                    {
    //                        foreach (SqlParameter param in parameter)
    //                        {
    //                            command.Parameters.Add(param);
    //                        }
    //                    }
    //                }

    //                SqlDataAdapter adapter = new SqlDataAdapter(command);

    //                adapter.Fill(data);

    //                connection.Close();
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            MessageBox.Show(ex.Message, "Lỗi ExecuteQuery");
    //        }
    //        return data;
    //    }
    //    public string ExecuteReader(string connectionSTR, string query)
    //    {
    //        DataTable data = new DataTable();

    //        string _value = "";
    //       // try
    //       // {
    //            using (SqlConnection connection = new SqlConnection(connectionSTR))
    //            {
    //                connection.Open();

    //                SqlCommand command = new SqlCommand(query, connection);


    //                SqlDataReader MyReader = command.ExecuteReader();



    //                while (MyReader.Read())
    //                {

    //                    _value =   String.Format("{0}", MyReader[0]);
    //                }


    //                connection.Close();
    //            }
    //        //}
    //        //catch (Exception ex)
    //        //{
    //        //    MessageBox.Show(ex.Message, "Lỗi ExecuteQuery");
    //        //}
    //        return _value ;
    //    }
    //    public int ExecuteReaderint(string connectionSTR, string query)
    //    {
    //        DataTable data = new DataTable();

    //        int _value = 0;
    //        string value = "";
    //        try
    //        {
    //            using (SqlConnection connection = new SqlConnection(connectionSTR))
    //            {
    //                connection.Open();

    //                SqlCommand command = new SqlCommand(query, connection);


    //                SqlDataReader MyReader = command.ExecuteReader();



    //                while (MyReader.Read())
    //                {

    //                    value = MyReader[0].ToString();
    //                }
    //                _value = Convert.ToInt32(value);

    //                connection.Close();
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            MessageBox.Show(ex.Message, "Lỗi ExecuteQueryint");
    //        }
    //        return _value;
    //    }

    //    public byte[] ExecuteReaderByte(string query, string _feild)
    //    {

    //        byte[] _value = new byte[0];

    //        // try
    //        // {
    //        using (SqlConnection connection = new SqlConnection(B7R2_FCCdb))
    //        {
    //            connection.Open();

    //            SqlCommand command = new SqlCommand(query, connection);


    //            SqlDataReader MyReader = command.ExecuteReader();



    //            while (MyReader.Read())
    //            {
    //                //_value = String.IsNullOrEmpty(MyReader[_feild].ToString()) ? (Byte?)null : Byte.Parse(MyReader[_feild].ToString());
    //                _value = DBNull.Value.Equals(MyReader[_feild]) ? new byte[0] : (byte[])MyReader[_feild];
    //                //_value = ;
    //            }


    //            connection.Close();
    //        }
    //        //}
    //        //catch (Exception ex)
    //        //{
    //        //    MessageBox.Show(ex.Message, "Lỗi ExecuteQuery");
    //        //}
    //        return _value;
    //    }
    //    public DataSet ExecuteQuery_Dataset(string connectionSTR, string query, object[] parameter = null)
    //    {
    //        DataSet data = new DataSet();
    //        try
    //        {
    //            using (SqlConnection connection = new SqlConnection(connectionSTR))
    //            {
    //                connection.Open();

    //                SqlCommand command = new SqlCommand(query, connection);

    //                if (parameter != null)
    //                {
    //                    string[] listPara = query.Split(' ');
    //                    int i = 0;
    //                    foreach (string item in listPara)
    //                    {
    //                        if (item.Contains('@'))
    //                        {
    //                            command.Parameters.Add(item, parameter[i]);
    //                            i++;
    //                        }
    //                    }
    //                }

    //                SqlDataAdapter adapter = new SqlDataAdapter(command);

    //                adapter.Fill(data);

    //                connection.Close();
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            MessageBox.Show(ex.Message, "Lỗi ExecuteQuery_Dataset B7R2");
    //        }
    //        return data;
    //    }
    //    public DataSet ExecuteProcedureReturnDataSet(string connString, string procName,
    //params SqlParameter[] paramters)
    //    {
    //        DataSet result = null;
    //        using (var sqlConnection = new SqlConnection(connString))
    //        using (var command = sqlConnection.CreateCommand())
    //        using (SqlDataAdapter sda = new SqlDataAdapter(command))
    //        {
    //            command.CommandType = CommandType.StoredProcedure;
    //            command.CommandText = procName;
    //            sda.SelectCommand.CommandTimeout = 1200;

    //            if (paramters != null)
    //                command.Parameters.AddRange(paramters);

    //            // ── DEBUG: log tham số truyền vào ───────────────────────────
    //            System.Diagnostics.Debug.WriteLine($"[SP] {procName}");
    //            if (paramters != null)
    //                foreach (var p in paramters)
    //                    System.Diagnostics.Debug.WriteLine(
    //                        $"  Param: {p.ParameterName} = {p.Value} " +
    //                        $"(Length={p.Value?.ToString()?.Length})");

    //            try
    //            {
    //                result = new DataSet();
    //                sda.Fill(result);
    //            }
    //            catch (SqlException ex)
    //            {
    //                // ── Log chi tiết lỗi SQL ─────────────────────────────────
    //                System.Diagnostics.Debug.WriteLine(
    //                    $"[SQL ERROR] SP={procName}");
    //                System.Diagnostics.Debug.WriteLine(
    //                    $"  Message : {ex.Message}");
    //                System.Diagnostics.Debug.WriteLine(
    //                    $"  Number  : {ex.Number}");
    //                System.Diagnostics.Debug.WriteLine(
    //                    $"  State   : {ex.State}");
    //                System.Diagnostics.Debug.WriteLine(
    //                    $"  LineNum : {ex.LineNumber}"); // ← dòng lỗi trong SP

    //                // Log từng lỗi nếu có nhiều
    //                foreach (SqlError err in ex.Errors)
    //                    System.Diagnostics.Debug.WriteLine(
    //                        $"  SqlError: Line={err.LineNumber}, " +
    //                        $"Msg={err.Message}");

    //                throw;
    //            }
    //        }
    //        return result;
    //    }
    //    public DataTable LoadData(string connString, string procName, params SqlParameter[] paramList)
    //    {
    //        using (var sqlConnection = new SqlConnection(connString))
    //        {
    //            using (var cmd = sqlConnection.CreateCommand())
    //            {
    //                cmd.CommandType = System.Data.CommandType.StoredProcedure;
    //                cmd.CommandText = procName;
    //                cmd.Parameters.AddRange(paramList);
    //                using (SqlDataAdapter adap = new SqlDataAdapter(cmd))
    //                {
    //                    DataTable dt = new DataTable();
    //                    try
    //                    {
    //                        adap.Fill(dt);
    //                    }
    //                    catch (System.Exception ex)
    //                    {
    //                        var paramInfo = string.Join(", ", paramList.Select(p => $"{p.ParameterName}={p.Value}"));
    //                        System.Diagnostics.Debug.WriteLine(
    //                            $"[LoadData ERROR] Proc={procName}, Params=[{paramInfo}]\nMessage={ex.Message}");

    //                        XtraMessageBox.Show(
    //                            $"Đọc dữ liệu thất bại{Environment.NewLine}Proc: {procName}{Environment.NewLine}{ex.Message}");
    //                        dt = null;
    //                    }
    //                    cmd.Parameters.Clear();
    //                    return dt;
    //                }
    //            }
    //        }
    //    }
    //    public DataTable LoadData1(string connString, string procName, params SqlParameter[] paramList)
    //    {
    //        using (var sqlConnection = new SqlConnection(connString))
    //        {
    //            using (var cmd = sqlConnection.CreateCommand())
    //            {
    //                cmd.CommandType = System.Data.CommandType.Text;
    //                cmd.CommandText = procName;
    //                cmd.Parameters.AddRange(paramList);
    //                using (SqlDataAdapter adap = new SqlDataAdapter(cmd))
    //                {
    //                    DataTable dt = new DataTable();
    //                    try
    //                    {
    //                        adap.Fill(dt);
    //                    }
    //                    catch (System.Exception ex)
    //                    {
    //                        XtraMessageBox.Show("Đọc dữ liệu thất bại" + Environment.NewLine + ex.Message);
    //                        dt = null;
    //                    }
    //                    cmd.Parameters.Clear();
    //                    return dt;
    //                }
    //            }
    //        }
    //    }
    //    public int ExecuteNonQuery(SqlConnection conn, SqlTransaction tran,
    //       string query, params SqlParameter[] parameters)
    //    {
    //        using (SqlCommand command = new SqlCommand(query, conn, tran))
    //        {
    //            if (parameters != null && parameters.Length > 0)
    //                command.Parameters.AddRange(parameters);

    //            return command.ExecuteNonQuery();
    //        }
    //    }

    //    // ── ExecuteScalar (transaction-aware) ───────────────────────────────
    //    public object ExecuteScalar(
    //         string connectionSTR,
    //         string query,
    //         SqlParameter[] parameters = null)
    //    {
    //        using (SqlConnection connection = new SqlConnection(connectionSTR))
    //        using (SqlCommand command = new SqlCommand(query, connection))
    //        {
    //            if (parameters != null)
    //            {
    //                foreach (var p in parameters)
    //                {
    //                    command.Parameters.Add(new SqlParameter(p.ParameterName, p.SqlDbType)
    //                    {
    //                        Value = p.Value ?? DBNull.Value,
    //                        Direction = p.Direction,
    //                        IsNullable = p.IsNullable,
    //                        Size = p.Size
    //                    });
    //                }
    //            }

    //            connection.Open();
    //            return command.ExecuteScalar();
    //        }
    //    }

    //    public object ExecuteScalar(
    //SqlConnection conn,
    //SqlTransaction tran,
    //string query,
    //SqlParameter[] parameters = null)
    //    {
    //        using (SqlCommand command = new SqlCommand(query, conn, tran))
    //        {
    //            command.CommandType = CommandType.Text;   // ⚠ bắt buộc, không để default gây nhầm SP

    //            if (parameters != null)
    //            {
    //                foreach (var p in parameters)
    //                {
    //                    command.Parameters.Add(new SqlParameter(p.ParameterName, p.SqlDbType)
    //                    {
    //                        Value = p.Value ?? DBNull.Value,
    //                        Direction = p.Direction,
    //                        IsNullable = p.IsNullable,
    //                        Size = p.Size
    //                    });
    //                }
    //            }

    //            // KHÔNG connection.Open()/Close() ở đây — conn được quản lý bởi caller
    //            // (thường đã Open() từ lúc BeginTransaction), đóng ở đây sẽ phá transaction của caller.
    //            return command.ExecuteScalar();
    //        }
    //    }

    //    // ── ExecuteReader dạng scalar-string (transaction-aware) ────────────
    //    // Giữ đúng hành vi ExecuteReader(string,string) hiện có.
    //    public string ExecuteReader(SqlConnection conn, SqlTransaction tran, string query)
    //    {
    //        string value = "";
    //        using (SqlCommand command = new SqlCommand(query, conn, tran))
    //        using (SqlDataReader reader = command.ExecuteReader())
    //        {
    //            while (reader.Read())
    //                value = string.Format("{0}", reader[0]);
    //        }
    //        return value;
    //    }

    //    // ── ExecuteQuery (transaction-aware) ────────────────────────────────
    //    public DataTable ExecuteQuery(SqlConnection conn, SqlTransaction tran,
    //        string query, List<SqlParameter> parameters = null)
    //    {
    //        DataTable data = new DataTable();
    //        using (SqlCommand command = new SqlCommand(query, conn, tran))
    //        {
    //            if (parameters != null)
    //                command.Parameters.AddRange(parameters.ToArray());

    //            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
    //                adapter.Fill(data);
    //        }
    //        return data;
    //    }

    //    // ── LoadData1 (transaction-aware) ───────────────────────────────────
    //    public DataTable LoadData1(SqlConnection conn, SqlTransaction tran,
    //        string query, params SqlParameter[] paramList)
    //    {
    //        using (var cmd = new SqlCommand(query, conn, tran))
    //        {
    //            if (paramList != null && paramList.Length > 0)
    //                cmd.Parameters.AddRange(paramList);

    //            using (var adap = new SqlDataAdapter(cmd))
    //            {
    //                DataTable dt = new DataTable();
    //                adap.Fill(dt);
    //                return dt;
    //            }
    //        }
    //    }

    //    // ── Stored procedure (transaction-aware) — dùng cho các Usp_* hiện có ─
    //    public DataSet ExecuteProcedureReturnDataSet(SqlConnection conn, SqlTransaction tran,
    //        string procName, params SqlParameter[] parameters)
    //    {
    //        using (var command = conn.CreateCommand())
    //        using (SqlDataAdapter sda = new SqlDataAdapter(command))
    //        {
    //            command.Transaction = tran;
    //            command.CommandType = CommandType.StoredProcedure;
    //            command.CommandText = procName;
    //            sda.SelectCommand.CommandTimeout = 1200;

    //            if (parameters != null)
    //                command.Parameters.AddRange(parameters);

    //            var result = new DataSet();
    //            sda.Fill(result);
    //            return result;
    //        }
    //    }
    //    public int ExecuteNonQuery(string connectionSTR, string query, params SqlParameter[] parameters)
    //    {
    //        int data = 0;
    //        try
    //        {
    //            using (SqlConnection connection = new SqlConnection(connectionSTR))
    //            {
    //                connection.Open();
    //                using (SqlCommand command = new SqlCommand(query, connection))
    //                {
    //                    // Truyền trực tiếp SqlParameter, không cần Regex dò chuỗi cực kỳ rủi ro
    //                    if (parameters != null && parameters.Length > 0)
    //                    {
    //                        command.Parameters.AddRange(parameters);
    //                    }

    //                    data = command.ExecuteNonQuery();
    //                }
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            MessageBox.Show(ex.Message, "Lỗi ExecuteNonQuery AUTOWH");
    //            data = -1;
    //        }
    //        return data;
    //    }




    //    public List<Warehouse> GetAllWarehouses()
    //    {
    //        var warehouses = new List<Warehouse>();
    //        string query = @"
    //    SELECT w.WarehouseId, w.Name AS WarehouseName,
    //           r.RackId, r.RackName,
    //           s.SlotId, s.SlotNumber, s.ItemCode, s.LotNo, s.Quantity, s.ImportDate, s.TemCode, s.IsOccupied, s.Capacity
    //    FROM Warehouse w
    //    LEFT JOIN Rack r ON r.WarehouseId = w.WarehouseId
    //    LEFT JOIN Slot s ON s.RackId = r.RackId
    //    ORDER BY w.WarehouseId, r.RackId, s.SlotNumber";

    //        DataTable dt = ExecuteQuery(B7R2_FCCdb, query);

    //        var warehouseDict = new Dictionary<int, Warehouse>();
    //        var rackDict = new Dictionary<int, Rack>();

    //        foreach (DataRow row in dt.Rows)
    //        {
    //            int whId = (int)row["WarehouseId"];
    //            string whName = row["WarehouseName"].ToString();

    //            if (!warehouseDict.ContainsKey(whId))
    //            {
    //                warehouseDict[whId] = new Warehouse
    //                {
    //                    //WarehouseId = whId,
    //                    Name = whName,
    //                    Racks = new List<Rack>()
    //                };
    //            }

    //            int rackId = (int)row["RackId"];
    //            string rackName = row["RackName"].ToString();
    //            var currentWarehouse = warehouseDict[whId];

    //            Rack rack;
    //            if (!rackDict.TryGetValue(rackId, out rack))
    //            {
    //                rack = new Rack
    //                {
    //                    //RackId = rackId,
    //                    Name = rackName,
    //                    Slots = new List<Slot>()
    //                };
    //                rackDict[rackId] = rack;
    //                currentWarehouse.Racks.Add(rack);
    //            }

    //            if (row["SlotId"] != DBNull.Value)
    //            {
    //                var slot = new Slot
    //                {
    //                    SlotId = (int)row["SlotId"],
    //                    SlotNumber = row["SlotNumber"] != DBNull.Value ? (int)row["SlotNumber"] : 0,
    //                    ItemCode = row["ItemCode"] != DBNull.Value ? row["ItemCode"].ToString() : null,
    //                    Quantity = row["Quantity"] != DBNull.Value ? (int)row["Quantity"] : 0,
    //                    Capacity = row["Capacity"] != DBNull.Value ? (int)row["Capacity"] : 0,
    //                    ImportDate = row["ImportDate"] != DBNull.Value ? (DateTime?)row["ImportDate"] : null,
    //                    IsOccupied = row["IsOccupied"] != DBNull.Value && (bool)row["IsOccupied"]
    //                };

    //                string lotNo = row["LotNo"] != DBNull.Value ? row["LotNo"].ToString() : null;
    //                string temCode = row["TemCode"] != DBNull.Value ? row["TemCode"].ToString() : null;

    //                if (!string.IsNullOrWhiteSpace(lotNo) || !string.IsNullOrWhiteSpace(temCode))
    //                {
    //                    slot.Lots.Add(new LotInfo
    //                    {
    //                        LotNo = lotNo,
    //                        TemCode = temCode,
    //                        Quantity = slot.Quantity // hoặc lấy riêng nếu có cột Quantity theo lot
    //                    });
    //                }

    //                rack.Slots.Add(slot);
    //            }
    //        }

    //        warehouses = warehouseDict.Values.ToList();
    //        return warehouses;
    //    }


    //}
  
        /// <summary>
        /// Lớp truy cập dữ liệu chuẩn cho toàn hệ thống PCTP.
        ///
        /// QUY ƯỚC BẮT BUỘC khi gọi hoặc thêm method:
        ///   1. Đang ở trong 1 transaction (đã BeginTransaction) → LUÔN dùng bản
        ///      nhận (SqlConnection conn, SqlTransaction tran). Các bản này KHÔNG
        ///      tự Open()/Close() connection — vòng đời thuộc về caller.
        ///   2. Không cần transaction, chỉ 1 câu lệnh độc lập → dùng bản
        ///      standalone (connectionSTR string), tự mở/đóng connection riêng.
        ///   3. Muốn chạy Stored Procedure → dùng đúng hàm có "Procedure" trong
        ///      tên (LoadData / ExecuteProcedureReturnDataSet).
        ///   4. Muốn chạy câu lệnh Text (SELECT/UPDATE/INSERT/DELETE thường, có
        ///      tham số) → dùng LoadData1 / ExecuteQuery / ExecuteScalar /
        ///      ExecuteNonQuery. KHÔNG có hàm nào trong file này còn bug ép
        ///      nhầm CommandType — nếu code cũ gọi 1 hàm không còn tồn tại ở
        ///      đây, đó là dấu hiệu code cũ đang dùng cách sai, cần sửa lại
        ///      cách gọi theo đúng 1 trong các hàm bên dưới.
        /// </summary>
        public class SQLPROVIDER
        {
            public static List<string> c_Ns = new List<string>();

            // ════════════════════════════════════════════════════════════════════
            // CONNECTION STRINGS
            // ⚠ B7R2_FCCdb và B7R2_FCCdbb hiện trỏ CÙNG server + CÙNG catalog
            // (192.168.200.57 / B7R2_FCC) — KHÔNG phải 2 DB khác nhau, chỉ là 2 tên
            // lịch sử còn sót lại. Giữ cả 2 vì nhiều repository tham chiếu theo tên
            // cũ; đổi tên sẽ vỡ build hàng loạt.
            // ════════════════════════════════════════════════════════════════════
            public string B7R2_FCCdb = @"Data Source=192.168.200.57;Initial Catalog=B7R2_FCC;User ID=sa;Password=fccbrv";
            public string B7R2_FCCdbb = @"Data Source=192.168.200.57;Initial Catalog=B7R2_FCC;User ID=sa;Password=fccbrv";

            #region ══ TRANSACTION LIFECYCLE ══════════════════════════════════════

            /// <summary>
            /// Mở 1 SqlConnection + bắt đầu transaction cục bộ. Caller BẮT BUỘC
            /// dùng "using" cho SqlConnection trả về, và tự Commit()/Rollback()
            /// trên SqlTransaction trước khi Dispose.
            /// </summary>
            /// <example>
            /// using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
            /// {
            ///     try { _sql.ExecuteNonQuery(conn, tran, "...", ...); tran.Commit(); }
            ///     catch { tran.Rollback(); throw; }
            /// }
            /// </example>
            public SqlConnection BeginTransaction(string connectionSTR, out SqlTransaction tran)
            {
                var conn = new SqlConnection(connectionSTR);
                conn.Open();
                tran = conn.BeginTransaction();
                return conn;
            }

            #endregion

            #region ══ TEXT MODE — TRANSACTION-AWARE (dùng khi đang trong 1 transaction) ══

            /// <summary>Chạy UPDATE/INSERT/DELETE trong transaction hiện có. Ném exception nếu lỗi — caller tự Rollback() trong catch.</summary>
            public int ExecuteNonQuery(SqlConnection conn, SqlTransaction tran,
                string query, params SqlParameter[] parameters)
            {
                using (var command = new SqlCommand(query, conn, tran))
                {
                    command.CommandType = CommandType.Text;
                    if (parameters != null && parameters.Length > 0)
                        command.Parameters.AddRange(parameters);
                    return command.ExecuteNonQuery();
                }
            }

            /// <summary>Trả về giá trị đơn (COUNT, SUM, 1 ô...) trong transaction hiện có.</summary>
            public object ExecuteScalar(SqlConnection conn, SqlTransaction tran,
                string query, SqlParameter[] parameters = null)
            {
                using (var command = new SqlCommand(query, conn, tran))
                {
                    command.CommandType = CommandType.Text;
                    if (parameters != null)
                    {
                        foreach (var p in parameters)
                        {
                            command.Parameters.Add(new SqlParameter(p.ParameterName, p.SqlDbType)
                            {
                                Value = p.Value ?? DBNull.Value,
                                Direction = p.Direction,
                                IsNullable = p.IsNullable,
                                Size = p.Size
                            });
                        }
                    }
                    return command.ExecuteScalar();
                }
            }

            /// <summary>Trả về DataTable từ câu lệnh Text (SELECT có tham số) trong transaction hiện có.</summary>
            public DataTable ExecuteQuery(SqlConnection conn, SqlTransaction tran,
                string query, List<SqlParameter> parameters = null)
            {
                var data = new DataTable();
                using (var command = new SqlCommand(query, conn, tran))
                {
                    command.CommandType = CommandType.Text;
                    if (parameters != null)
                        command.Parameters.AddRange(parameters.ToArray());
                    using (var adapter = new SqlDataAdapter(command))
                        adapter.Fill(data);
                }
                return data;
            }

            /// <summary>Trả về DataTable từ câu lệnh Text (SELECT có tham số) trong transaction hiện có — bản params SqlParameter[].</summary>
            public DataTable LoadData1(SqlConnection conn, SqlTransaction tran,
                string query, params SqlParameter[] paramList)
            {
                using (var cmd = new SqlCommand(query, conn, tran))
                {
                    cmd.CommandType = CommandType.Text;
                    if (paramList != null && paramList.Length > 0)
                        cmd.Parameters.AddRange(paramList);
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        var dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }

            /// <summary>Đọc giá trị cột đầu tiên của dòng cuối cùng khớp điều kiện, trong transaction hiện có. Query KHÔNG tham số hoá — chỉ dùng cho câu lệnh tĩnh, không ghép input người dùng.</summary>
            public string ExecuteReader(SqlConnection conn, SqlTransaction tran, string query)
            {
                string value = "";
                using (var command = new SqlCommand(query, conn, tran) { CommandType = CommandType.Text })
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        value = string.Format("{0}", reader[0]);
                }
                return value;
            }

            /// <summary>Chạy stored procedure trong transaction hiện có, trả về DataSet.</summary>
            public DataSet ExecuteProcedureReturnDataSet(SqlConnection conn, SqlTransaction tran,
                string procName, params SqlParameter[] parameters)
            {
                using (var command = conn.CreateCommand())
                using (var sda = new SqlDataAdapter(command))
                {
                    command.Transaction = tran;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = procName;
                    sda.SelectCommand.CommandTimeout = 1200;
                    if (parameters != null)
                        command.Parameters.AddRange(parameters);
                    var result = new DataSet();
                    sda.Fill(result);
                    return result;
                }
            }

            #endregion

            #region ══ TEXT MODE — STANDALONE (không cần transaction, tự mở/đóng connection) ══

            /// <summary>
            /// Chạy UPDATE/INSERT/DELETE độc lập, tự mở/đóng connection riêng.
            /// Nuốt exception và hiển thị MessageBox, trả về -1 nếu lỗi —
            /// KHÔNG dùng hàm này bên trong 1 transaction đang mở (lỗi sẽ bị
            /// nuốt thay vì rollback đúng cách); trong trường hợp đó dùng bản
            /// nhận (conn, tran) ở trên.
            /// </summary>
            public int ExecuteNonQuery(string connectionSTR, string query, params SqlParameter[] parameters)
            {
                int data;
                try
                {
                    using (var connection = new SqlConnection(connectionSTR))
                    {
                        connection.Open();
                        using (var command = new SqlCommand(query, connection) { CommandType = CommandType.Text })
                        {
                            if (parameters != null && parameters.Length > 0)
                                command.Parameters.AddRange(parameters);
                            data = command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi ExecuteNonQuery");
                    data = -1;
                }
                return data;
            }

            /// <summary>Trả về giá trị đơn, tự mở/đóng connection riêng (không transaction).</summary>
            public object ExecuteScalar(string connectionSTR, string query, SqlParameter[] parameters = null)
            {
                using (var connection = new SqlConnection(connectionSTR))
                using (var command = new SqlCommand(query, connection) { CommandType = CommandType.Text })
                {
                    if (parameters != null)
                    {
                        foreach (var p in parameters)
                        {
                            command.Parameters.Add(new SqlParameter(p.ParameterName, p.SqlDbType)
                            {
                                Value = p.Value ?? DBNull.Value,
                                Direction = p.Direction,
                                IsNullable = p.IsNullable,
                                Size = p.Size
                            });
                        }
                    }
                    connection.Open();
                    return command.ExecuteScalar();
                }
            }

            /// <summary>
            /// Đọc dữ liệu bằng câu lệnh Text ad hoc (SELECT có tham số), tự
            /// mở/đóng connection riêng. Đây là hàm CHUẨN cho mọi query Text
            /// không cần transaction — dùng thay cho mọi cách viết SqlCommand
            /// thủ công lặp lại.
            /// </summary>
            public DataTable LoadData1(string connString, string query, params SqlParameter[] paramList)
            {
                using (var sqlConnection = new SqlConnection(connString))
                using (var cmd = sqlConnection.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = query;
                    if (paramList != null && paramList.Length > 0)
                        cmd.Parameters.AddRange(paramList);
                    using (var adap = new SqlDataAdapter(cmd))
                    {
                        var dt = new DataTable();
                        try
                        {
                            adap.Fill(dt);
                        }
                        catch (Exception ex)
                        {
                            XtraMessageBox.Show("Đọc dữ liệu thất bại" + Environment.NewLine + ex.Message);
                            dt = null;
                        }
                        cmd.Parameters.Clear();
                        return dt;
                    }
                }
            }

            #endregion

            #region ══ STORED PROCEDURE — STANDALONE ══════════════════════════════

            /// <summary>Đọc dữ liệu bằng stored procedure, tự mở/đóng connection riêng, trả DataTable. Log chi tiết lỗi nếu có.</summary>
            public DataTable LoadData(string connString, string procName, params SqlParameter[] paramList)
            {
                using (var sqlConnection = new SqlConnection(connString))
                using (var cmd = sqlConnection.CreateCommand())
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = procName;
                    cmd.Parameters.AddRange(paramList);
                    using (var adap = new SqlDataAdapter(cmd))
                    {
                        var dt = new DataTable();
                        try
                        {
                            adap.Fill(dt);
                        }
                        catch (Exception ex)
                        {
                            var paramInfo = string.Join(", ", paramList.Select(p => $"{p.ParameterName}={p.Value}"));
                            System.Diagnostics.Debug.WriteLine(
                                $"[LoadData ERROR] Proc={procName}, Params=[{paramInfo}]\nMessage={ex.Message}");
                            XtraMessageBox.Show(
                                $"Đọc dữ liệu thất bại{Environment.NewLine}Proc: {procName}{Environment.NewLine}{ex.Message}");
                            dt = null;
                        }
                        cmd.Parameters.Clear();
                        return dt;
                    }
                }
            }

            /// <summary>Chạy stored procedure, trả DataSet, tự mở/đóng connection riêng. Log chi tiết lỗi SQL nếu có.</summary>
            public DataSet ExecuteProcedureReturnDataSet(string connString, string procName,
                params SqlParameter[] paramters)
            {
                DataSet result;
                using (var sqlConnection = new SqlConnection(connString))
                using (var command = sqlConnection.CreateCommand())
                using (var sda = new SqlDataAdapter(command))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = procName;
                    sda.SelectCommand.CommandTimeout = 1200;
                    if (paramters != null)
                        command.Parameters.AddRange(paramters);

                    System.Diagnostics.Debug.WriteLine($"[SP] {procName}");
                    if (paramters != null)
                        foreach (var p in paramters)
                            System.Diagnostics.Debug.WriteLine(
                                $"  Param: {p.ParameterName} = {p.Value} (Length={p.Value?.ToString()?.Length})");

                    try
                    {
                        result = new DataSet();
                        sda.Fill(result);
                    }
                    catch (SqlException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SQL ERROR] SP={procName}");
                        System.Diagnostics.Debug.WriteLine($"  Message : {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"  Number  : {ex.Number}");
                        System.Diagnostics.Debug.WriteLine($"  State   : {ex.State}");
                        System.Diagnostics.Debug.WriteLine($"  LineNum : {ex.LineNumber}");
                        foreach (SqlError err in ex.Errors)
                            System.Diagnostics.Debug.WriteLine($"  SqlError: Line={err.LineNumber}, Msg={err.Message}");
                        throw;
                    }
                }
                return result;
            }

             public string ExecuteReader(string connectionSTR, string query)
        {
            string value = "";

            using (var connection = new SqlConnection(connectionSTR))
            using (var command = new SqlCommand(query, connection))
            {
                command.CommandType = CommandType.Text;

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        value = reader[0]?.ToString() ?? "";
                    }
                }
            }

            return value;
        }
            public int ExecuteStoredProcedure(
            string connectionString,
            string procedureName,
            params SqlParameter[] parameters)
                {
                    using (var connection = new SqlConnection(connectionString))
                    using (var command = new SqlCommand(procedureName, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        if (parameters != null && parameters.Length > 0)
                            command.Parameters.AddRange(parameters);

                        connection.Open();

                        return command.ExecuteNonQuery();
                    }
                }

        #endregion


    }
    
    class IFSPROVIDER
    {

        static string host = "192.168.200.12";
        static int port = 1521;
        static string sid = "fccprod";
        //static string sid = "FCCSTG"; // Tét 
        static string user = "IFSAPP";
       static string password = "fccifs";
        //static string password = "IFSAPP";

        private string connectionSTR = @"Data Source=(DESCRIPTION =(ADDRESS = (PROTOCOL = TCP)(HOST = "
                 + host + ")(PORT = " + port + "))(CONNECT_DATA = (SERVER = DEDICATED)(SERVICE_NAME = "
                 + sid + ")));Password=" + password + ";User ID=" + user;
        public DataTable fillDataTable(string table)
        {
            string query = "SELECT * FROM dstut.dbo." + table;

            SqlConnection sqlConn = new SqlConnection(connectionSTR);
            sqlConn.Open();
            SqlCommand cmd = new SqlCommand(query, sqlConn);
            SqlDataAdapter da = new SqlDataAdapter(cmd);
            DataTable dt = new DataTable();
            da.Fill(dt);
            sqlConn.Close();
            return dt;
        }
        public DataTable ExecuteQuery(string query, object[] parameter = null)
        {
            DataTable data = new DataTable();
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);

                                i++;
                            }
                        }
                    }

                    OracleDataAdapter adapter = new OracleDataAdapter(command);

                    adapter.Fill(data);

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery IFS");
            }
            return data;
        }

        public DataSet ExecuteQuery_Dataset(string query, object[] parameter = null)
        {
            DataSet data = new DataSet();
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    OracleDataAdapter adapter = new OracleDataAdapter(command);

                    adapter.Fill(data);

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery_Dataset IFS");
            }
            return data;
        }

        public int ExecuteNonQuery(string query, object[] parameter = null)
        {
            int data = 0;
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    data = command.ExecuteNonQuery();

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteNonQuery IFS");
            }
            return data;
        }

        public object ExecuteScalar(string query, object[] parameter = null)
        {
            object data = 0;
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    data = command.ExecuteScalar();

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteScalar IFS");
            }
            return data;
        }

        public string ExecuteReader(string query)
        {
            DataTable data = new DataTable();

            string _value = "";
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

          
                    OracleDataReader MyReader = command.ExecuteReader();



                    while (MyReader.Read())
                    {

                        _value = String.Format("{0}", MyReader[0]);
                    }


                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery");
            }
            return _value;
        }


       
    }
    // Ket Nối WH 4W
    class WH4SQLPROVIDER
    {
      
        public string AutoWH = @"Data Source=192.168.200.14\BRAVO;Initial Catalog=B7R2_FCC;User ID=sa;Password=fccbrv";
        //public string B7R2_FCCdb = @"Data Source=192.168.200.57;Initial Catalog=B7R2_FCC;User ID=sa;Password=fccbrv";
        public DataTable WH4ExecuteQuery(string connectionSTR, string query, List<SqlParameter> parameter = null)
        {
            DataTable data = new DataTable();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionSTR))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand(query, connection);

                    if (parameter != null)
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandText = query;
                        {
                            foreach (SqlParameter param in parameter)
                            {
                                command.Parameters.Add(param);
                            }
                        }
                    }

                    SqlDataAdapter adapter = new SqlDataAdapter(command);

                    adapter.Fill(data);

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery");
            }
            return data;
        }
        public string WH4ExecuteReader(string connectionSTR, string query)
        {
            DataTable data = new DataTable();

            string _value = "";
            // try
            // {
            using (SqlConnection connection = new SqlConnection(connectionSTR))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(query, connection);


                SqlDataReader MyReader = command.ExecuteReader();



                while (MyReader.Read())
                {

                    _value = String.Format("{0}", MyReader[0]);
                }


                connection.Close();
            }
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message, "Lỗi ExecuteQuery");
            //}
            return _value;
        }
       
        public int WH4ExecuteReaderint(string connectionSTR, string query)
        {
            DataTable data = new DataTable();

            int _value = 0;
            string value = "";
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionSTR))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand(query, connection);


                    SqlDataReader MyReader = command.ExecuteReader();



                    while (MyReader.Read())
                    {

                        value = MyReader[0].ToString();
                    }
                    _value = Convert.ToInt32(value);

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQueryint");
            }
            return _value;
        }
        public DataSet WH4ExecuteQuery_Dataset(string connectionSTR, string query, object[] parameter = null)
        {
            DataSet data = new DataSet();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionSTR))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    SqlDataAdapter adapter = new SqlDataAdapter(command);

                    adapter.Fill(data);

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery_Dataset B7R2");
            }
            return data;
        }
        public DataSet WH4ExecuteProcedureReturnDataSet(string connString, string procName,
            params SqlParameter[] paramters)
        {
            DataSet result = null;
            using (var sqlConnection = new SqlConnection(connString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    using (SqlDataAdapter sda = new SqlDataAdapter(command))
                    {
                        command.CommandType = System.Data.CommandType.StoredProcedure;
                        command.CommandText = procName;
                        if (paramters != null)
                        {
                            command.Parameters.AddRange(paramters);
                        }
                        result = new DataSet();
                        sda.Fill(result);
                    }
                }
            }
            return result;
        }
        public int WH4ExecuteNonQuery(string connectionSTR, string query, object[] parameter = null)
        {
            int data = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionSTR))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    data = command.ExecuteNonQuery();

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteNonQuery AUTOWH");
                data = -1;
            }
            return data;
        }

        public object WH4ExecuteScalar(string connectionSTR, string query, object[] parameter = null)
        {
            object data = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionSTR))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    data = command.ExecuteScalar();

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteScalar B7R2");
            }
            return data;
        }
       
    }
    class WMSPROVIDER
    {

        static string host = "192.168.2.10";
        static int port = 8088;
        static string sid = "ORCL";
        //static string sid = "FCCSTG"; // Tét 
        static string user = "wms";
        static string password = "wms";
        //static string password = "IFSAPP";

        private string connectionSTR = @"Data Source=(DESCRIPTION =(ADDRESS = (PROTOCOL = TCP)(HOST = "
                 + host + ")(PORT = " + port + "))(CONNECT_DATA = (SERVER = DEDICATED)(SERVICE_NAME = "
                 + sid + ")));Password=" + password + ";User ID=" + user;

        public DataTable WMSExecuteQuery(string query, object[] parameter = null)
        {
            DataTable data = new DataTable();
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);

                                i++;
                            }
                        }
                    }

                    OracleDataAdapter adapter = new OracleDataAdapter(command);

                    adapter.Fill(data);

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery IFS");
            }
            return data;
        }

        public DataSet WMSExecuteQuery_Dataset(string query, object[] parameter = null)
        {
            DataSet data = new DataSet();
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    OracleDataAdapter adapter = new OracleDataAdapter(command);

                    adapter.Fill(data);

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery_Dataset IFS");
            }
            return data;
        }

        public int WMSExecuteNonQuery(string query, object[] parameter = null)
        {
            int data = 0;
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    data = command.ExecuteNonQuery();

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteNonQuery IFS");
            }
            return data;
        }

        public object WMSExecuteScalar(string query, object[] parameter = null)
        {
            object data = 0;
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    data = command.ExecuteScalar();

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteScalar IFS");
            }
            return data;
        }

        public string WMSExecuteReader(string query)
        {
            DataTable data = new DataTable();

            string _value = "";
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);


                    OracleDataReader MyReader = command.ExecuteReader();



                    while (MyReader.Read())
                    {

                        _value = String.Format("{0}", MyReader[0]);
                    }


                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery");
            }
            return _value;
        }


        // kết nối SQL và WMS where house 4W

    }
}
