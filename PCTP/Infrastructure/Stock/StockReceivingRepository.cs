using System;
using System.Data;
using System.Data.SqlClient;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;

namespace PCTP.Infrastructure.Stock
{
    /// <summary>
    /// KhoCore receiving persistence cho dbo.FVN_STOCKTP (khoá SiteCode + LOT).
    /// Chỉ giữ siêu dữ liệu nhập (SLNHAP, NGAYNHAP, SATUS, MaterialLotId). Tồn kho nằm ở
    /// MES.FVN_InventoryBalance; SLCONLAI không bao giờ được ghi ở đây (đọc qua dbo.vFVN_STOCKTP).
    /// Mọi thao tác tham gia UnitOfWork nên FVN_STOCKTP, MES ledger và Slot/SlotLot cùng atomic.
    /// </summary>
    public sealed class StockReceivingRepository : SqlRepositoryBase, IStockReceivingRepository
    {
        private readonly string _stockTable;
        private readonly string _siteFilter;
        private readonly ISiteContext _site;

        public StockReceivingRepository(PhieuSqlExecutor db, IUnitOfWork uow, ISiteContext site = null)
            : this(db, uow, "dbo.FVN_STOCKTP", site) { }

        // Override bảng chỉ dành cho fixture tích hợp dùng #STOCKTP (fixture phải có cột SiteCode).
        public StockReceivingRepository(PhieuSqlExecutor db, IUnitOfWork uow, string stockTable, ISiteContext site = null)
            : base(db, uow)
        {
            if (stockTable != "dbo.FVN_STOCKTP" && stockTable != "#STOCKTP")
                throw new ArgumentException("Only dbo.FVN_STOCKTP or the isolated #STOCKTP fixture is supported.", nameof(stockTable));
            _stockTable = stockTable;
            _site = site ?? AppSettingsSiteContext.Default;
            _siteFilter = "SiteCode=@site";
        }

        private SqlParameter SiteParam()
        {
            return new SqlParameter("@site", SqlDbType.NVarChar, 30) { Value = _site.SiteCode };
        }

        public bool Exists(string lotNo)
        {
            EnsureTransaction();
            if (string.IsNullOrWhiteSpace(lotNo)) return false;
            object value = ExecuteScalar(
                "SELECT COUNT(1) FROM " + _stockTable + " WITH (UPDLOCK, HOLDLOCK) WHERE " + _siteFilter + " AND LOT=@lot",
                SiteParam(), new SqlParameter("@lot", lotNo.Trim()));
            int count = value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            if (count > 1) throw new InvalidOperationException(
                string.Format("FVN_STOCKTP vi phạm khóa (SiteCode, LOT): LOT [{0}] có {1} dòng.", lotNo, count));
            return count == 1;
        }

        public int GetReceivedQuantity(string lotNo)
        {
            EnsureTransaction();
            if (string.IsNullOrWhiteSpace(lotNo)) return 0;
            DataTable rows = LoadData(
                "SELECT ISNULL(SLNHAP,0) AS SLNHAP FROM " + _stockTable + " WITH (UPDLOCK, HOLDLOCK) WHERE " + _siteFilter + " AND LOT=@lot",
                SiteParam(), new SqlParameter("@lot", lotNo.Trim()));
            if (rows == null || rows.Rows.Count == 0) return 0;
            if (rows.Rows.Count != 1) throw new InvalidOperationException(
                string.Format("FVN_STOCKTP vi phạm khóa (SiteCode, LOT): LOT [{0}] có {1} dòng.", lotNo, rows.Rows.Count));
            return Convert.ToInt32(rows.Rows[0]["SLNHAP"]);
        }

        public void Insert(StockReceivingRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            EnsureTransaction();
            if (string.IsNullOrWhiteSpace(record.LotNo)) throw new ArgumentException("LOT không được để trống.", nameof(record));
            if (string.IsNullOrWhiteSpace(record.ItemCode)) throw new ArgumentException("PartNo (ItemCode) không được để trống.", nameof(record));
            if (record.MaterialLotId <= 0) throw new ArgumentException("MaterialLotId là bắt buộc (MES.FVN_MaterialLot).", nameof(record));
            if (record.ReceivedQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(record.ReceivedQuantity));
            if (Exists(record.LotNo)) throw new InvalidOperationException(
                string.Format("LOT [{0}] đã tồn tại trong FVN_STOCKTP; không được tạo dòng thứ hai.", record.LotNo));

            int caSx;
            if (!int.TryParse(record.ProductionCase, out caSx)) caSx = 0;

            // NAME/MODEL không lưu ở đây (IFS.FVN_InventoryPart / FVN_PartOperationalProfile là nguồn, xem vFVN_STOCKTP).
            // SLCONLAI để mặc định 0: chỉ là projection legacy, số thật = MES.FVN_InventoryBalance.
            int affected = ExecuteNonQuery(@"
INSERT INTO " + _stockTable + @"
    (SiteCode, LOT, PART, CASX, NGAYSX, SLSX, NGAYNHAP, SLNHAP, NGAYXUAT, SLXUAT, SLCONLAI, SATUS, MaterialLotId)
VALUES
    (@site, @lot, @part, @casx, @ngaysx, @slsx, GETDATE(), @slnhap, GETDATE(), 0, 0, @status, @materialLotId);",
                SiteParam(),
                new SqlParameter("@lot", record.LotNo.Trim()),
                new SqlParameter("@part", record.ItemCode.Trim()),
                new SqlParameter("@casx", caSx),
                new SqlParameter("@ngaysx", (object)record.ProductionDate ?? DBNull.Value),
                new SqlParameter("@slsx", record.ProductionQuantity),
                new SqlParameter("@slnhap", record.ReceivedQuantity),
                new SqlParameter("@status", record.Status),
                new SqlParameter("@materialLotId", SqlDbType.BigInt) { Value = record.MaterialLotId });
            if (affected != 1) throw new InvalidOperationException(
                string.Format("FVN_STOCKTP INSERT LOT [{0}] phải tạo đúng một dòng, thực tế {1}.", record.LotNo, affected));
        }

        public void Update(string lotNo, int receivedQuantityDelta, int status)
        {
            EnsureTransaction();
            if (string.IsNullOrWhiteSpace(lotNo)) throw new ArgumentException("LOT không được để trống.", nameof(lotNo));
            if (receivedQuantityDelta <= 0) throw new ArgumentOutOfRangeException(nameof(receivedQuantityDelta));
            int affected = ExecuteNonQuery(@"
UPDATE " + _stockTable + @"
SET SLNHAP = ISNULL(SLNHAP, 0) + @sl,
    NGAYNHAP = GETDATE(),
    SATUS = @status
WHERE " + _siteFilter + @" AND LOT = @lot;",
                SiteParam(),
                new SqlParameter("@sl", receivedQuantityDelta),
                new SqlParameter("@status", status),
                new SqlParameter("@lot", lotNo.Trim()));
            if (affected != 1) throw new InvalidOperationException(
                string.Format("FVN_STOCKTP yêu cầu đúng một dòng LOT [{0}], nhưng UPDATE tác động {1} dòng. Transaction phải rollback.", lotNo, affected));
        }

        private void EnsureTransaction()
        {
            if (!HasTransaction) throw new InvalidOperationException(
                "StockReceivingRepository chỉ được gọi trong UnitOfWork transaction của workflow nhập kho.");
        }
    }
}
