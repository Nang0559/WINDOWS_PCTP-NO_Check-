using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Repositories
{
   

    public sealed class StockHistoryRepository
        : SqlRepositoryBase,
          IStockHistoryRepository
    {
        public StockHistoryRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        public void SaveHistory(
            string actionType,
            string itemCode,
            LotInfo lot,
            int? fromSlotId,
            int? toSlotId,
            string performedBy)
        {
            const string sql = @"
            INSERT INTO StockHistory
            (
                ActionType,
                ItemCode,
                TemCode,
                LotNo,
                Quantity,
                Date,
                FromSlotId,
                ToSlotId,
                QrData,
                MaPhieu,
                PerformedBy
            )
            VALUES
            (
                @ActionType,
                @ItemCode,
                @TemCode,
                @LotNo,
                @Quantity,
                GETDATE(),
                @FromSlotId,
                @ToSlotId,
                @QrData,
                @MaPhieu,
                @PerformedBy
            );";

            ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@ActionType",
                    SqlDbType.NVarChar,
                    20)
                {
                    Value = (object)actionType ?? DBNull.Value
                },

                new SqlParameter(
                    "@ItemCode",
                    SqlDbType.NVarChar,
                    50)
                {
                    Value = (object)itemCode ?? DBNull.Value
                },

                new SqlParameter(
                    "@TemCode",
                    SqlDbType.NVarChar,
                    200)
                {
                    Value = (object)lot?.TemCode ?? DBNull.Value
                },

                new SqlParameter(
                    "@LotNo",
                    SqlDbType.NVarChar,
                    200)
                {
                    Value = (object)lot?.LotNo ?? DBNull.Value
                },

                new SqlParameter(
                    "@Quantity",
                    SqlDbType.Int)
                {
                    Value = lot?.Quantity ?? 0
                },

                new SqlParameter(
                    "@FromSlotId",
                    SqlDbType.Int)
                {
                    Value = fromSlotId.HasValue
                        ? (object)fromSlotId.Value
                        : DBNull.Value
                },

                new SqlParameter(
                    "@ToSlotId",
                    SqlDbType.Int)
                {
                    Value = toSlotId.HasValue
                        ? (object)toSlotId.Value
                        : DBNull.Value
                },

                new SqlParameter(
                "@QrData",
                SqlDbType.NVarChar,
                -1)
                            {
                                Value =
                    (object)lot?.QRInfo?.RawQr
                    ?? (object)lot?.RawQr
                    ?? DBNull.Value
                },

                new SqlParameter(
                    "@MaPhieu",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value =
                        (object)lot?.QRInfo?.MaPhieu
                        ?? (object)lot?.MaPhieuKho
                        ?? DBNull.Value
                },

                new SqlParameter(
                    "@PerformedBy",
                    SqlDbType.NVarChar,
                    50)
                {
                    Value = !string.IsNullOrWhiteSpace(performedBy)
                        ? performedBy
                        : Environment.UserName
                });
        }
    }
}
