using System;
using System.Data;
using System.Data.SqlClient;
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
    ///   3. P13 (F-Y1 = part-scoped): archive the part's DG scan rows into LUUDOCQRCODE and delete them.
    /// TMP shape comes from the DBA-supplied dbo.TMPPHIEUGIAOHANG schema (STT smallint, MAHANG, NHAMAY,
    /// NGAYGIAO smalldatetime, GIOGIAO, STATUS, TTPHIEU, PO_NO ...). The customer tables are injected
    /// (P12 MP/SP/HTN/YMVN variants); P11 Gear is read from TMP.GEAR only when hasGear is true.
    /// Returns the number of STT actually closed so the handler can enforce F-Y4.
    /// </summary>
    public sealed class DeliveryTmpPendingGate : SqlRepositoryBase, IDeliveryPendingGate
    {
        private readonly string _tmp, _docQr, _luuPhieu, _luuDocQr, _archiveDateFormat;
        private readonly bool _hasGear;

        public DeliveryTmpPendingGate(
            PhieuSqlExecutor db, IUnitOfWork uow,
            string tmpTable, string docQrTable,
            bool hasGear,
            string archiveDateFormat,
            string luuPhieuTable = "LUUPHIEUGIAOHANG",
            string luuDocQrTable = "LUUDOCQRCODE")
            : base(db, uow)
        {
            foreach (var t in new[] { tmpTable, docQrTable, luuPhieuTable, luuDocQrTable })
                db.ValidateTableName(t);
            if (string.IsNullOrWhiteSpace(archiveDateFormat))
                throw new ArgumentException("archiveDateFormat is required (legacy HVN uses yyyy-MM-dd, YMVN MM/dd/yyyy).", nameof(archiveDateFormat));
            _tmp = tmpTable; _docQr = docQrTable; _luuPhieu = luuPhieuTable; _luuDocQr = luuDocQrTable;
            _hasGear = hasGear; _archiveDateFormat = archiveDateFormat;
        }

        public int ClosePending(DeliveryStockCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!HasTransaction)
                throw new InvalidOperationException("ClosePending must run inside the delivery command transaction.");

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
                string ngay = row.Rows[0]["NGAYGIAO"] == DBNull.Value
                    ? "" : Convert.ToDateTime(row.Rows[0]["NGAYGIAO"]).ToString(_archiveDateFormat, System.Globalization.CultureInfo.InvariantCulture);
                string gear = _hasGear ? "ISNULL(GEAR,'')" : "''"; // P11

                ExecuteNonQuery(
                    $@"INSERT INTO [{_luuPhieu}]
                        (STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG,
                         NGAYGIAO, GIOGIAO, STATUS, GearYMVN, NHAMAY, GIOGIAOFCC, PO_NO, TTPHIEU)
                       SELECT STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG,
                              NGAYGIAO, GIOGIAO, 'OK', {gear}, ISNULL(NHAMAY,''),
                              CONVERT(VARCHAR(8), GETDATE(), 108), ISNULL(PO_NO,''), ISNULL(TTPHIEU,'')
                       FROM [{_tmp}] WHERE STT = @stt AND STATUS = 'NG'",
                    new SqlParameter("@stt", SqlDbType.Int) { Value = stt });

                int updated = ExecuteNonQuery(
                    $"UPDATE [{_tmp}] SET STATUS = 'OK' WHERE STT = @stt AND STATUS = 'NG'",
                    new SqlParameter("@stt", SqlDbType.Int) { Value = stt });
                if (updated != 1)
                    throw new InvalidOperationException($"Không thể xác nhận phiếu giao STT={stt}: trạng thái đã thay đổi hoặc không còn tồn tại.");

                // P13 / F-Y1: scan rows are scoped by PART (MAHANGFCC), not by STT.
                ExecuteNonQuery(
                    $@"INSERT INTO [{_luuDocQr}]
                        (LOTFCC, MAHANGFCC, SLTEMFCC, LOTHVN, MAHANGHVN, SLTEMHVN,
                         STATUS, MAFCC, STT, KETQUA, NGAYXUAT, GIOXUAT, NHAMAY)
                       SELECT LEFT(LOTFCC, 500), LEFT(MAHANGFCC, 60), SLTEMFCC,
                              LEFT(LOTHVN, 500), LEFT(MAHANGHVN, 60), SLTEMHVN,
                              STATUS, LEFT(MAFCC, 50), STT, KETQUA, @ng, @gio, @nm
                       FROM [{_docQr}] WHERE MAHANGFCC = @mh AND KETQUA = 'DG'",
                    new SqlParameter("@ng", SqlDbType.NVarChar, 50) { Value = ngay },
                    new SqlParameter("@gio", SqlDbType.NVarChar, 50) { Value = gio },
                    new SqlParameter("@nm", SqlDbType.NVarChar, 200) { Value = nhaMay },
                    new SqlParameter("@mh", SqlDbType.NVarChar, 100) { Value = maHang });
                ExecuteNonQuery(
                    $"DELETE FROM [{_docQr}] WHERE MAHANGFCC = @mh AND KETQUA = 'DG'",
                    new SqlParameter("@mh", SqlDbType.NVarChar, 100) { Value = maHang });

                closed++;
            }
            return closed;
        }
    }
}
