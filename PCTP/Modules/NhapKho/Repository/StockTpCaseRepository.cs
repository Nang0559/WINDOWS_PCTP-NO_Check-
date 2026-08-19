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
   

    public sealed class StockTpCaseRepository
        : SqlRepositoryBase,
          IStockTpCaseRepository
    {
        public StockTpCaseRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ============================================================
        // KIỂM TRA CASE ĐÃ NHẬP HAY CHƯA
        // ============================================================

        public bool ExistsCaseHistory(
            string caseNo)
        {
            if (string.IsNullOrWhiteSpace(caseNo))
                return false;

            const string sql = @"
SELECT COUNT(1)
FROM NHAP_TP_HIS
WHERE LOTCASE = @CaseNo;";

            object result = ExecuteScalar(
                sql,
                new SqlParameter(
                    "@CaseNo",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = caseNo.Trim()
                });

            return DbValueHelper.ToInt(result) > 0;
        }


        // ============================================================
        // GHI NHẬN CASE ĐÃ NHẬP
        // ============================================================

        public void InsertCaseHistory(
            string caseNo)
        {
            if (string.IsNullOrWhiteSpace(caseNo))
            {
                throw new ArgumentException(
                    "CaseNo không được rỗng.",
                    nameof(caseNo));
            }

            const string sql = @"
                INSERT INTO NHAP_TP_HIS
                (
                    LOTCASE
                )
                VALUES
                (
                    @CaseNo
                );";

            try
            {
                ExecuteNonQuery(
                    sql,
                    new SqlParameter(
                        "@CaseNo",
                        SqlDbType.NVarChar,
                        100)
                    {
                        Value = caseNo.Trim()
                    });
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException(
                    $"Không thể ghi lịch sử CASE [{caseNo}].",
                    ex);
            }
        }
    }
}
