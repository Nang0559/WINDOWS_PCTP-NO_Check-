using PCTP.Modules.GiaoHangKhach.Intefaces;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    /// <summary>
    /// Repository cho tbl_QR_ComparePart (mapping mã HVN -> mã FCC).
    /// Đọc dữ liệu qua SqlRepositoryBase; ghi dữ liệu dùng connection riêng vì cần
    /// transaction Serializable (UnitOfWork mặc định chỉ ReadCommitted).
    /// </summary>
    public sealed class ComparePartRepository : SqlRepositoryBase, IComparePartRepository
    {
        public ComparePartRepository(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow)
        {
        }

        public DataTable GetSourceParts()
            => LoadData(@"
                SELECT DISTINCT Code AS code, Name AS name
                FROM B20Item
                WHERE ISNULL(Code, '') <> ''
                ORDER BY code");

        public DataTable GetTargetParts()
        {
            // Không lọc theo tiền tố PartNo: mã khách và mã kho có thể hoàn toàn khác nhau.
            // Danh sách đầy đủ cho phép một PartNo ánh xạ tới nhiều PartNoCompare.
            return LoadData(@"
                SELECT DISTINCT Code AS code, Name AS name
                FROM B20Item
                WHERE ISNULL(Code, '') <> ''
                ORDER BY code");
        }

        public DataTable GetAll()
            => LoadData(@"
                SELECT
                    c.STT,
                    c.PartNo,
                    ISNULL(NULLIF(c.PartName, ''), ISNULL(a.Name, '')) AS PartName,
                    c.PartNoCompare AS ToPartNo,
                    ISNULL(b.Name, '') AS ToPartName,
                    c.timeSet,
                    c.IsActive
                FROM tbl_QR_ComparePart c
                OUTER APPLY (SELECT TOP 1 Name FROM B20Item WHERE Code = c.PartNo) a
                OUTER APPLY (SELECT TOP 1 Name FROM B20Item WHERE Code = c.PartNoCompare) b
                ORDER BY c.STT DESC, c.timeSet DESC");

        public bool TryInsert(string partNo, string partNoCompare, DateTime timeSet)
        {
            if (string.IsNullOrWhiteSpace(partNo))
                throw new ArgumentException("partNo is required.", nameof(partNo));
            if (string.IsNullOrWhiteSpace(partNoCompare))
                throw new ArgumentException("partNoCompare is required.", nameof(partNoCompare));

            // The existence check, STT allocation and INSERT must share a serializable transaction;
            // otherwise two users can both pass the old check-then-insert sequence
            // (or get the same MAX(STT) + 1).
            using (SqlConnection connection = new SqlConnection(Db.Sql.B7R2_FCCdb))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        // STT trong schema là int NULL (không identity) -> tự cấp MAX(STT)+1.
                        // Nếu DB thật đặt STT là IDENTITY thì bỏ qua cột STT khi INSERT.
                        // PartName là nvarchar(30) -> cắt 30 ký tự để không bị lỗi truncate.
                        const string sql = @"
DECLARE @Stt int, @PartName nvarchar(30);
IF EXISTS
(
    SELECT 1
    FROM tbl_QR_ComparePart WITH (UPDLOCK, HOLDLOCK)
    WHERE PartNo = @PartNo AND PartNoCompare = @PartNoCompare
)
    SELECT CAST(0 AS int);
ELSE
BEGIN
    SELECT TOP 1 @PartName = LEFT(Name, 30) FROM B20Item WHERE Code = @PartNo;

    IF COLUMNPROPERTY(OBJECT_ID(N'tbl_QR_ComparePart'), 'STT', 'IsIdentity') = 1
    BEGIN
        INSERT INTO tbl_QR_ComparePart (PartNo, PartName, PartNoCompare, timeSet, IsActive)
        VALUES (@PartNo, @PartName, @PartNoCompare, @TimeSet, 1);
    END
    ELSE
    BEGIN
        SELECT @Stt = ISNULL(MAX(STT), 0) + 1 FROM tbl_QR_ComparePart WITH (UPDLOCK, HOLDLOCK);

        INSERT INTO tbl_QR_ComparePart (STT, PartNo, PartName, PartNoCompare, timeSet, IsActive)
        VALUES (@Stt, @PartNo, @PartName, @PartNoCompare, @TimeSet, 1);
    END

    SELECT CAST(1 AS int);
END";
                        int inserted;
                        using (SqlCommand command = new SqlCommand(sql, connection, transaction))
                        {
                            command.CommandTimeout = CommandTimeoutSeconds;
                            command.Parameters.Add("@PartNo", SqlDbType.NVarChar, 100).Value = partNo.Trim();
                            command.Parameters.Add("@PartNoCompare", SqlDbType.NVarChar, 100).Value = partNoCompare.Trim();
                            command.Parameters.Add("@TimeSet", SqlDbType.DateTime).Value = timeSet;
                            inserted = Convert.ToInt32(command.ExecuteScalar());
                        }
                        transaction.Commit();
                        return inserted == 1;
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

        public int Delete(IEnumerable<ComparePartKey> keys)
        {
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            using (SqlConnection connection = new SqlConnection(Db.Sql.B7R2_FCCdb))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Khớp NULL-safe vì STT/PartNo/PartNoCompare đều cho phép NULL trong schema.
                        const string sql = @"
DELETE FROM tbl_QR_ComparePart
WHERE ISNULL(PartNo, '') = @PartNo
  AND ISNULL(PartNoCompare, '') = @PartNoCompare
  AND ((@STT IS NULL AND STT IS NULL) OR STT = @STT)";

                        int deleted = 0;
                        foreach (ComparePartKey key in keys.Distinct())
                        {
                            if (key == null)
                                continue;

                            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
                            {
                                command.CommandTimeout = CommandTimeoutSeconds;
                                command.Parameters.Add("@PartNo", SqlDbType.NVarChar, 100).Value = key.PartNo;
                                command.Parameters.Add("@PartNoCompare", SqlDbType.NVarChar, 100).Value = key.PartNoCompare;
                                command.Parameters.Add("@STT", SqlDbType.Int).Value =
                                    key.Stt.HasValue ? (object)key.Stt.Value : DBNull.Value;
                                deleted += command.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                        return deleted;
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
    }
}
