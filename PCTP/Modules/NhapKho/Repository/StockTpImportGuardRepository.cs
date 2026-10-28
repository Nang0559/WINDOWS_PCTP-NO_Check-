using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using System;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Modules.NhapKho.Repository
{
    public sealed class StockTpImportGuardRepository
        : SqlRepositoryBase,
          IStockTpImportGuardRepository
    {
        private const int LockTimeoutMs = 10000;

        public StockTpImportGuardRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        public bool ExistsImportedInHistory(string itemCode, string qrData)
        {
            if (string.IsNullOrWhiteSpace(qrData))
                return false;

            const string sql = @"
SELECT COUNT(1)
FROM StockHistory
WHERE ActionType = 'IMPORT'
  AND ItemCode = @ItemCode
  AND QrData = @QrData;";

            object result = ExecuteScalar(
                sql,
                new SqlParameter("@ItemCode", SqlDbType.NVarChar, 50)
                {
                    Value = (object)(itemCode ?? string.Empty).Trim()
                },
                new SqlParameter("@QrData", SqlDbType.NVarChar, -1)
                {
                    Value = qrData.Trim()
                });

            return DbValueHelper.ToInt(result) > 0;
        }

        public void AcquireImportLock(string qrData)
        {
            if (string.IsNullOrWhiteSpace(qrData))
                throw new ArgumentException("QR không được rỗng.", nameof(qrData));

            if (!HasTransaction)
                throw new InvalidOperationException(
                    "AcquireImportLock phải được gọi bên trong transaction (IUnitOfWork.Begin).");

            // Resource tối đa 255 ký tự → băm QR để đủ ngắn và ổn định.
            const string sql = @"
DECLARE @Res NVARCHAR(255) =
    N'NHAPTP_QR_' + CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', @QrData), 2);
DECLARE @Rc INT;
EXEC @Rc = sp_getapplock
    @Resource    = @Res,
    @LockMode    = 'Exclusive',
    @LockOwner   = 'Transaction',
    @LockTimeout = @TimeoutMs;
SELECT @Rc;";

            object result = ExecuteScalar(
                sql,
                new SqlParameter("@QrData", SqlDbType.NVarChar, -1) { Value = qrData.Trim() },
                new SqlParameter("@TimeoutMs", SqlDbType.Int) { Value = LockTimeoutMs });

            // 0 = lấy được ngay, 1 = lấy được sau khi chờ; < 0 = timeout / bị huỷ / deadlock.
            if (DbValueHelper.ToInt(result) < 0)
                throw new InvalidOperationException(
                    "Tem này đang được máy khác xử lý nhập kho. Vui lòng thử lại sau giây lát.");
        }
        public void AcquireLotImportLock(string lotNo)
        {
            if (string.IsNullOrWhiteSpace(lotNo))
                throw new ArgumentException("LOT không được rỗng.", nameof(lotNo));

            if (!HasTransaction)
                throw new InvalidOperationException(
                    "AcquireLotImportLock phải được gọi bên trong transaction (IUnitOfWork.Begin).");

            // Serialize different QR receipts that contribute to one STOCKTP LOT.
            const string sql = @"
DECLARE @Res NVARCHAR(255) =
    N'NHAPTP_LOT_' + CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', UPPER(LTRIM(RTRIM(@LotNo)))), 2);
DECLARE @Rc INT;
EXEC @Rc = sp_getapplock
    @Resource    = @Res,
    @LockMode    = 'Exclusive',
    @LockOwner   = 'Transaction',
    @LockTimeout = @TimeoutMs;
SELECT @Rc;";

            object result = ExecuteScalar(
                sql,
                new SqlParameter("@LotNo", SqlDbType.NVarChar, 100) { Value = lotNo.Trim() },
                new SqlParameter("@TimeoutMs", SqlDbType.Int) { Value = LockTimeoutMs });

            if (DbValueHelper.ToInt(result) < 0)
                throw new InvalidOperationException(
                    "LOT đang được máy khác xử lý nhập kho. Vui lòng thử lại sau giây lát.");
        }

    }
}
