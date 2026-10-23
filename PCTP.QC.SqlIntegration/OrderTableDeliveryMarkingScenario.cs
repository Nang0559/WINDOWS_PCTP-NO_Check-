using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Shared.Common;

namespace PCTP.QC.SqlIntegration
{
/// <summary>
/// DEL-010: một dòng đơn trong bảng đơn riêng được định danh bằng PO + mã hàng + ngày + giờ:phút.
/// Kịch bản này chạy câu UPDATE thật (<see cref="OrderTableSql.MarkDelivered"/>, cùng câu mà
/// PhieuKhoRepository.DanhDauDaGiao dùng) trên bảng fixture và kiểm tra rằng giao một đợt giờ
/// KHÔNG đánh dấu các đợt khác giờ của cùng PO/mã hàng/ngày.
///
/// Bảng fixture được tạo và ghi trong một UnitOfWork rồi ROLLBACK: không để lại đối tượng nào trong DB test.
/// Tham số bên dưới phải khớp PhieuKhoRepository.DanhDauDaGiao (kiểu NVarChar(100)/Date/Int).
/// </summary>
internal static class OrderTableDeliveryMarkingScenario
{
    public static void Run(string connectionString)
    {
        var provider = new PCTP.ClassSQL.SQLPROVIDER { B7R2_FCCdb = connectionString };
        using var uow = new UnitOfWork(provider);
        uow.Begin();

        var table = "__PCTP_QC_ORD_" + Guid.NewGuid().ToString("N");
        var day = new DateTime(2026, 10, 23);

        try
        {
            Execute(uow,
                "CREATE TABLE [" + table + "] (" +
                "Oder_no nvarchar(100) NOT NULL, Part_no nvarchar(100) NOT NULL, " +
                "NgayGiao datetime NOT NULL, Slgiao int NOT NULL, " +
                "IsDelivered bit NOT NULL DEFAULT 0, DeliveredDate datetime NULL)");

            // Slgiao dùng làm id dòng để assert.
            Insert(uow, table, 1, "PO-A", "PART-1", day.AddHours(8));                       // 23/10 08:00
            Insert(uow, table, 2, "PO-A", "PART-1", day.AddHours(8).AddMinutes(30).AddSeconds(45)); // 08:30:45
            Insert(uow, table, 3, "PO-A", "PART-1", day.AddHours(14));                      // 14:00
            Insert(uow, table, 4, "PO-A", "PART-1", day.AddDays(1).AddHours(14));           // ngày khác
            Insert(uow, table, 5, "PO-B", "PART-1", day.AddHours(14));                      // PO khác
            Insert(uow, table, 6, "PO-A", "PART-2", day.AddHours(14));                      // mã hàng khác

            Require(MarkDelivered(uow, table, Key("PO-A", "PART-1", day, 14, 0)) == 1,
                "Marking the 14:00 delivery must update exactly one order row.");
            RequireDelivered(uow, table, new[] { 3 },
                "Delivering the 14:00 slot must not mark the 08:00/08:30 slots of the same PO/part/date, nor other PO/part/date.");

            Require(MarkDelivered(uow, table, Key("PO-A", "PART-1", day, 14, 0)) == 0,
                "Replaying the same delivery must not update any row (idempotent).");
            RequireDelivered(uow, table, new[] { 3 }, "Replay changed the delivered rows.");

            Require(MarkDelivered(uow, table, Key("PO-A", "PART-1", day, 8, 30)) == 1,
                "Marking 08:30 must match the 08:30:45 row (seconds are ignored).");
            RequireDelivered(uow, table, new[] { 2, 3 }, "08:30 delivery must leave 08:00 untouched.");

            Require(MarkDelivered(uow, table, Key("PO-A", "PART-1", day, 8, 0)) == 1,
                "Marking 08:00 must update exactly one row.");
            RequireDelivered(uow, table, new[] { 1, 2, 3 }, "Unexpected delivered rows after marking 08:00.");

            Require(MarkDelivered(uow, table, Key("PO-A", "PART-1", day, 9, 0)) == 0,
                "A time with no order row must not match anything (under-marking is the safe failure).");
            RequireDelivered(uow, table, new[] { 1, 2, 3 },
                "A non-existent slot changed delivered rows. Rows 4 (other day), 5 (other PO) and 6 (other part) must stay undelivered.");

            uow.Rollback();
            Console.WriteLine("PASS: order-table delivery marking keyed by PO + part + date + hour:minute executed on an isolated rolled-back fixture.");
        }
        catch
        {
            try { uow.Rollback(); } catch { }
            throw;
        }
    }

    private static OrderLineKey Key(string po, string part, DateTime day, int hh, int mm) =>
        OrderLineKey.Create(po, part, day, new TimeSpan(hh, mm, 0));

    // Mirrors PhieuKhoRepository.DanhDauDaGiao parameter types exactly.
    private static int MarkDelivered(UnitOfWork uow, string table, OrderLineKey key)
    {
        using var cmd = uow.Connection.CreateCommand();
        cmd.Transaction = uow.Transaction;
        cmd.CommandText = OrderTableSql.MarkDelivered(table);
        cmd.Parameters.Add(new SqlParameter("@po", SqlDbType.NVarChar, 100) { Value = key.PoNo });
        cmd.Parameters.Add(new SqlParameter("@pno", SqlDbType.NVarChar, 100) { Value = key.MaHang });
        cmd.Parameters.Add(new SqlParameter("@ng", SqlDbType.Date) { Value = key.NgayGiao });
        cmd.Parameters.Add(new SqlParameter("@hh", SqlDbType.Int) { Value = key.GioGiao.Hours });
        cmd.Parameters.Add(new SqlParameter("@mi", SqlDbType.Int) { Value = key.GioGiao.Minutes });
        return cmd.ExecuteNonQuery();
    }

    private static void Insert(UnitOfWork uow, string table, int id, string po, string part, DateTime when)
    {
        using var cmd = uow.Connection.CreateCommand();
        cmd.Transaction = uow.Transaction;
        cmd.CommandText = "INSERT INTO [" + table + "] (Oder_no, Part_no, NgayGiao, Slgiao) VALUES (@po, @pno, @ng, @id)";
        cmd.Parameters.Add(new SqlParameter("@po", SqlDbType.NVarChar, 100) { Value = po });
        cmd.Parameters.Add(new SqlParameter("@pno", SqlDbType.NVarChar, 100) { Value = part });
        cmd.Parameters.Add(new SqlParameter("@ng", SqlDbType.DateTime) { Value = when });
        cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });
        cmd.ExecuteNonQuery();
    }

    private static void Execute(UnitOfWork uow, string sql)
    {
        using var cmd = uow.Connection.CreateCommand();
        cmd.Transaction = uow.Transaction;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void RequireDelivered(UnitOfWork uow, string table, int[] expected, string message)
    {
        var actual = new List<int>();
        using (var cmd = uow.Connection.CreateCommand())
        {
            cmd.Transaction = uow.Transaction;
            cmd.CommandText = "SELECT Slgiao FROM [" + table + "] WHERE IsDelivered = 1 ORDER BY Slgiao";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) actual.Add(reader.GetInt32(0));
        }

        Require(actual.Count == expected.Length && System.Linq.Enumerable.SequenceEqual(actual, expected),
            message + " Expected delivered ids [" + string.Join(",", expected) + "], actual [" + string.Join(",", actual) + "].");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
}
