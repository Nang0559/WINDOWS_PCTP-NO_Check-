using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using PCTP.Modules.GiaoHangKhach.Contracts.Delivery;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Shared.Common;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    /// <summary>
    /// Closes the pending-delivery rows of a delivery command inside the handler's transaction.
    /// Mirrors the legacy CapNhapKhoYMVN steps, per STT, in the same order:
    ///   1. archive the TMP row (STATUS='NG') into LUUPHIEUGIAOHANG,
    ///   2. TMP STATUS NG -> OK, exactly one row (otherwise throw: replay/changed state),
    ///   3. P13 (F-Y1 = part-scoped): archive the part's DG scan rows into LUUDOCQRCODE, delete them,
    ///      and require archived == deleted (history is saved before the source rows disappear).
    ///
    /// Schema facts (DBA-supplied): dbo.LUUPHIEUGIAOHANG has the 8-column UNIQUE index UQ_LUUPHIEU
    /// (STT, MAHANG, NGAYGIAO, CUA, TRUYEN, SOLUONG, GIOGIAO, NHAMAY); STT alone is NOT a key. A violation
    /// is reported as a business error naming the index; the handler rolls the whole command back.
    /// LUUDOCQRCODE.NGAYXUAT is smalldatetime, so the date is passed as a typed value (no text format).
    ///
    /// Columns that exist on BOTH sides are preserved instead of dropped (HOP, STATUSDOC, Note, PO_NO,
    /// PO_ITEM; TMP.GEAR -> LUU.GearYMVN when the TMP family has GEAR - P11). Tables are injected (P12).
    /// Returns the number of STT actually closed so the handler can enforce F-Y4.
    /// </summary>
    public sealed class DeliveryTmpPendingGate : SqlRepositoryBase, IDeliveryPendingGate
    {
        public const string LuuPhieuUniqueIndex = "UQ_LUUPHIEU";
        private static readonly string[] PreservedColumns = { "HOP", "STATUSDOC", "Note", "PO_NO", "PO_ITEM" };

        private readonly string _tmp, _docQr, _luuPhieu, _luuDocQr;

        public DeliveryTmpPendingGate(
            PhieuSqlExecutor db, IUnitOfWork uow,
            string tmpTable, string docQrTable,
            string luuPhieuTable = "LUUPHIEUGIAOHANG",
            string luuDocQrTable = "LUUDOCQRCODE")
            : base(db, uow)
        {
            foreach (var t in new[] { tmpTable, docQrTable, luuPhieuTable, luuDocQrTable })
                db.ValidateTableName(t);
            _tmp = tmpTable; _docQr = docQrTable; _luuPhieu = luuPhieuTable; _luuDocQr = luuDocQrTable;
        }

        public int ClosePending(DeliveryStockCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!HasTransaction)
                throw new InvalidOperationException("ClosePending must run inside the delivery command transaction.");

            var tmpCols = ColumnsOf(_tmp);
            var luuCols = ColumnsOf(_luuPhieu);
            var keep = PreservedColumns.Where(c => tmpCols.Contains(c) && luuCols.Contains(c)).ToList();
            var cols = "STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GIOGIAO, STATUS, TTPHIEU, NHAMAY, GIOGIAOFCC"
                       + string.Concat(keep.Select(c => ", " + c));
            var vals = "STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GIOGIAO, 'OK', ISNULL(TTPHIEU,''), ISNULL(NHAMAY,''), CONVERT(VARCHAR(8), GETDATE(), 108)"
                       + string.Concat(keep.Select(c => c == "PO_NO" ? ", ISNULL(PO_NO,'')" : ", " + c));
            if (tmpCols.Contains("GEAR") && luuCols.Contains("GearYMVN")) // P11
            {
                cols += ", GearYMVN";
                vals += ", ISNULL(GEAR,'')";
            }
            bool archiveHasGioGiao = ColumnsOf(_luuDocQr).Contains("GIOGIAO");

            int closed = 0;
            foreach (int stt in command.PendingStt)
            {
                DataTable row = LoadData(
                    $"SELECT TOP 1 MAHANG, NGAYGIAO, ISNULL(GIOGIAO,'') AS GIOGIAO, ISNULL(NHAMAY,'') AS NHAMAY FROM [{_tmp}] WITH (UPDLOCK, HOLDLOCK) WHERE STT = @stt AND STATUS = 'NG'",
                    new SqlParameter("@stt", SqlDbType.Int) { Value = stt });
                if (row.Rows.Count == 0) break; // not pending any more: handler sees closed < expected and rolls back

                string maHang = Convert.ToString(row.Rows[0]["MAHANG"]) ?? "";
                string nhaMay = Convert.ToString(row.Rows[0]["NHAMAY"]) ?? "";
                string gio = Convert.ToString(row.Rows[0]["GIOGIAO"]) ?? "";
                object ngay = row.Rows[0]["NGAYGIAO"] == DBNull.Value ? (object)DBNull.Value : Convert.ToDateTime(row.Rows[0]["NGAYGIAO"]);

                try
                {
                    ExecuteNonQuery(
                        $"INSERT INTO [{_luuPhieu}] ({cols}) SELECT {vals} FROM [{_tmp}] WHERE STT = @stt AND STATUS = 'NG'",
                        new SqlParameter("@stt", SqlDbType.Int) { Value = stt });
                }
                catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                {
                    throw new InvalidOperationException(
                        $"Phiếu giao STT={stt} đã tồn tại trong {_luuPhieu} (vi phạm unique {LuuPhieuUniqueIndex}: STT, MAHANG, NGAYGIAO, CUA, TRUYEN, SOLUONG, GIOGIAO, NHAMAY); huỷ toàn bộ command.", ex);
                }

                int updated = ExecuteNonQuery(
                    $"UPDATE [{_tmp}] SET STATUS = 'OK' WHERE STT = @stt AND STATUS = 'NG'",
                    new SqlParameter("@stt", SqlDbType.Int) { Value = stt });
                if (updated != 1)
                    throw new InvalidOperationException($"Không thể xác nhận phiếu giao STT={stt}: trạng thái đã thay đổi hoặc không còn tồn tại.");

                // P13 / F-Y1: scan rows are scoped by PART (MAHANGFCC), not by STT. Archive first, then delete.
                int archived = ExecuteNonQuery(
                    $@"INSERT INTO [{_luuDocQr}]
                        (LOTFCC, MAHANGFCC, SLTEMFCC, LOTHVN, MAHANGHVN, SLTEMHVN,
                         STATUS, MAFCC, STT, KETQUA, NGAYXUAT, GIOXUAT, NHAMAY{(archiveHasGioGiao ? ", GIOGIAO" : "")})
                       SELECT LEFT(LOTFCC, 500), LEFT(MAHANGFCC, 60), SLTEMFCC,
                              LEFT(LOTHVN, 500), LEFT(MAHANGHVN, 60), SLTEMHVN,
                              STATUS, LEFT(MAFCC, 50), STT, KETQUA, @ng, @gio, @nm{(archiveHasGioGiao ? ", @gio" : "")}
                       FROM [{_docQr}] WHERE MAHANGFCC = @mh AND KETQUA = 'DG'",
                    new SqlParameter("@ng", SqlDbType.SmallDateTime) { Value = ngay },
                    new SqlParameter("@gio", SqlDbType.NVarChar, 100) { Value = gio },
                    new SqlParameter("@nm", SqlDbType.NVarChar, 400) { Value = nhaMay },
                    new SqlParameter("@mh", SqlDbType.NVarChar, 120) { Value = maHang });
                int deleted = ExecuteNonQuery(
                    $"DELETE FROM [{_docQr}] WHERE MAHANGFCC = @mh AND KETQUA = 'DG'",
                    new SqlParameter("@mh", SqlDbType.NVarChar, 120) { Value = maHang });
                if (archived != deleted)
                    throw new InvalidOperationException(
                        $"Lưu lịch sử QR không khớp xóa nguồn (lưu {archived}, xóa {deleted}) cho {maHang}; huỷ để không mất lịch sử.");

                closed++;
            }
            return closed;
        }

        private HashSet<string> ColumnsOf(string table)
        {
            bool temp = table.StartsWith("#", StringComparison.Ordinal);
            string sql = temp
                ? "SELECT name FROM tempdb.sys.columns WHERE object_id = OBJECT_ID(N'tempdb..' + @t)"
                : "SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(@t)";
            DataTable dt = LoadData(sql, new SqlParameter("@t", SqlDbType.NVarChar, 256) { Value = table });
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in dt.Rows) set.Add(Convert.ToString(r["name"]));
            if (set.Count == 0) throw new InvalidOperationException($"Không đọc được cột của bảng {table}.");
            return set;
        }
    }
}
