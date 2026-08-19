using PCTP.Domain.Events;
using PCTP.Infrastructure;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.NhapKho.Repository
{
    public sealed class StockTpStatusRepository
     : SqlRepositoryBase,
       IStockTpStatusRepository
    {
        public StockTpStatusRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ============================================================
        // MỞ LẠI LOT
        // ============================================================
        //
        // Satus = 0:
        //   LOT được phép tiếp tục xử lý.
        //
        // find:
        //   Chỉ dùng để phát event cho UI/service đang theo dõi LOT.
        //   Không dùng để xác định LOT trong SQL.
        //
        public void MoLaiLot(
            string lot,
            string find = null)
        {
            if (string.IsNullOrWhiteSpace(lot))
                throw new ArgumentException(
                    "LOT không được rỗng.",
                    nameof(lot));

            const string sql = @"
UPDATE STOCKTP
SET Satus = 0
WHERE LOT = @Lot;";

            int affected = ExecuteNonQuery(
                sql,
                new SqlParameter(
                    "@Lot",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = lot.Trim()
                });

            // Không có LOT thì không nên âm thầm coi là thành công.
            if (affected == 0)
            {
                throw new InvalidOperationException(
                    "Không tìm thấy LOT [" + lot + "] trong STOCKTP.");
            }

            // Thông báo cho các Form / service đang theo dõi LOT.
            AppEventBus.Instance.Publish(
                new LotStatusResetEvent(
                    lot.Trim(),
                    find));
        }


        // ============================================================
        // ĐỒNG BỘ SLSX MES + MỞ LẠI LOT KHI SLSX THAY ĐỔI
        // ============================================================
        //
        // Quy tắc:
        //
        // 1. LOT chưa tồn tại trong STOCKTP
        //      -> không làm gì, trả false.
        //
        // 2. LOT không bị khóa (Satus != 1)
        //      -> không làm gì, trả false.
        //
        // 3. SLSX MES == SLSX đang lưu
        //      -> không làm gì, trả false.
        //
        // 4. LOT đang khóa + SLSX MES thay đổi
        //      -> cập nhật SLSX
        //      -> Satus = 0
        //      -> publish event
        //      -> trả true.
        //
        public bool DongBoSLSXVaMoLaiNeuThayDoi(
            string lot,
            string find,
            int slsxMoiTuMES)
        {
            if (string.IsNullOrWhiteSpace(lot))
                return false;

            if (slsxMoiTuMES < 0)
                throw new ArgumentException(
                    "SLSX từ MES không được âm.",
                    nameof(slsxMoiTuMES));

            const string selectSql = @"
SELECT
    Satus,
    ISNULL(SLSX, 0) AS SLSX
FROM STOCKTP
WHERE LOT = @Lot;";

            DataTable dt = LoadData(
                selectSql,
                new SqlParameter(
                    "@Lot",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = lot.Trim()
                });

            // LOT chưa từng tồn tại trong STOCKTP.
            if (dt == null || dt.Rows.Count == 0)
                return false;

            DataRow row = dt.Rows[0];

            int status = DbValueHelper.ToInt(
                row["Satus"]);

            int slsxDaLuu = DbValueHelper.ToInt(
                row["SLSX"]);

            // Chỉ xử lý LOT đang bị khóa.
            if (status != 1)
                return false;

            // SLSX MES không thay đổi.
            if (slsxMoiTuMES == slsxDaLuu)
                return false;

            const string updateSql = @"
UPDATE STOCKTP
SET
    Satus = 0,
    SLSX = @SLSXMoi
WHERE LOT = @Lot
  AND Satus = 1;";

            int affected = ExecuteNonQuery(
                updateSql,

                new SqlParameter(
                    "@SLSXMoi",
                    SqlDbType.Int)
                {
                    Value = slsxMoiTuMES
                },

                new SqlParameter(
                    "@Lot",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = lot.Trim()
                });

            // Có thể transaction/connection khác đã thay đổi trạng thái
            // trước khi UPDATE.
            if (affected == 0)
                return false;

            AppEventBus.Instance.Publish(
                new LotStatusResetEvent(
                    lot.Trim(),
                    find));

            return true;
        }
    }
}
