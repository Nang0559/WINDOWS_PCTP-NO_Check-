using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Models;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoCore.Repositories
{
    public sealed class InspectionLogRepository
    : SqlRepositoryBase, IInspectionLogRepository
    {
        public InspectionLogRepository(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow) { }

        public void SaveLog(InspectionLogEntry e)
        {
            const string sql = @"
            INSERT INTO InspectionLog
                (InspectionCode, ItemCode, TemCodeTong, LotNoTong, NSXTong,
                 SoLuongTong, BoxTemCode, BoxLotNo, BoxNSX,
                 IsMatch, CheckedAt, FinalResult, MaPhieu)
            VALUES
                (@InspectionCode, @ItemCode, @TemCodeTong, @LotNoTong, @NSXTong,
                 @SoLuongTong, @BoxTemCode, @BoxLotNo, @BoxNSX,
                 @IsMatch, @CheckedAt, @FinalResult, @MaPhieu);";

            ExecuteNonQuery(sql,
                new SqlParameter("@InspectionCode", SqlDbType.NVarChar, 50)
                { Value = e.InspectionCode },
                new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100)
                { Value = e.ItemCode },
                new SqlParameter("@TemCodeTong", SqlDbType.NVarChar, 200)
                { Value = (object)e.TemCodeTong ?? DBNull.Value },
                new SqlParameter("@LotNoTong", SqlDbType.NVarChar, 200)
                { Value = (object)e.LotNoTong ?? DBNull.Value },
                new SqlParameter("@NSXTong", SqlDbType.NVarChar, 50)
                { Value = (object)e.NSXTong ?? DBNull.Value },
                new SqlParameter("@SoLuongTong", SqlDbType.Int)
                { Value = e.SoLuongTong },
                new SqlParameter("@BoxTemCode", SqlDbType.NVarChar, 200)
                { Value = (object)e.BoxTemCode ?? DBNull.Value },
                new SqlParameter("@BoxLotNo", SqlDbType.NVarChar, 200)
                { Value = (object)e.BoxLotNo ?? DBNull.Value },
                new SqlParameter("@BoxNSX", SqlDbType.NVarChar, 50)
                { Value = (object)e.BoxNSX ?? DBNull.Value },
                new SqlParameter("@IsMatch", SqlDbType.Bit)
                { Value = e.IsMatch },
                new SqlParameter("@CheckedAt", SqlDbType.DateTime)
                { Value = e.CheckedAt },
                new SqlParameter("@FinalResult", SqlDbType.NVarChar, 10)
                { Value = e.FinalResult },
                new SqlParameter("@MaPhieu", SqlDbType.NVarChar, 100)
                { Value = (object)e.MaPhieu ?? DBNull.Value });
        }

        public List<InspectionLogEntry> GetByInspectionCode(string code)
        {
            DataTable dt = LoadData(
                "SELECT * FROM InspectionLog WHERE InspectionCode = @Code",
                new SqlParameter("@Code", SqlDbType.NVarChar, 50) { Value = code });

            var list = new List<InspectionLogEntry>();
            if (dt == null) return list;

            foreach (DataRow r in dt.Rows)
                list.Add(new InspectionLogEntry
                {
                    InspectionCode = r["InspectionCode"]?.ToString(),
                    ItemCode = r["ItemCode"]?.ToString(),
                    TemCodeTong = r["TemCodeTong"]?.ToString(),
                    LotNoTong = r["LotNoTong"]?.ToString(),
                    NSXTong = r["NSXTong"]?.ToString(),
                    SoLuongTong = DbValueHelper.ToInt(r["SoLuongTong"]),
                    BoxTemCode = r["BoxTemCode"]?.ToString(),
                    BoxLotNo = r["BoxLotNo"]?.ToString(),
                    BoxNSX = r["BoxNSX"]?.ToString(),
                    IsMatch = r["IsMatch"] != DBNull.Value
                                     && Convert.ToBoolean(r["IsMatch"]),
                    CheckedAt = r["CheckedAt"] == DBNull.Value
                                     ? DateTime.MinValue
                                     : Convert.ToDateTime(r["CheckedAt"]),
                    FinalResult = r["FinalResult"]?.ToString(),
                    MaPhieu = r["MaPhieu"]?.ToString()
                });
            return list;
        }

        public DataTable GetHistoryMaster(DateTime from, DateTime to, string itemCode, string result)
        {
            string where = "WHERE CheckedAt BETWEEN @From AND @To";
            var pars = new List<SqlParameter>
            {
                new SqlParameter("@From", SqlDbType.DateTime) { Value = from },
                new SqlParameter("@To",   SqlDbType.DateTime) { Value = to   }
            };

            if (!string.IsNullOrEmpty(itemCode))
            {
                where += " AND ItemCode = @ItemCode";
                pars.Add(new SqlParameter("@ItemCode", SqlDbType.NVarChar) { Value = itemCode });
            }

            if (!string.IsNullOrEmpty(result) && result != "Tất cả")
            {
                where += " AND FinalResult = @Result";
                pars.Add(new SqlParameter("@Result", SqlDbType.NVarChar) { Value = result });
            }

            string sql = $@"
                SELECT 
                    InspectionCode,
                    MAX(ItemCode)    AS ItemCode,
                    MAX(LotNoTong)   AS LotNoTong,
                    MAX(NSXTong)     AS NSXTong,
                    MAX(SoLuongTong) AS SoLuongTong,
                    MAX(MaPhieu)     AS MaPhieu,
                    COUNT(*)         AS TotalBox,
                    SUM(CASE WHEN IsMatch = 1 THEN 1 ELSE 0 END) AS PassCount,
                    SUM(CASE WHEN IsMatch = 0 THEN 1 ELSE 0 END) AS FailCount,
                    MAX(FinalResult) AS FinalResult,
                    MIN(CheckedAt)   AS CheckedAt
                FROM InspectionLog
                {where}
                GROUP BY InspectionCode
                ORDER BY MIN(CheckedAt) DESC";

            return LoadData(sql, pars.ToArray());
        }

        public DataTable GetHistoryDetail(string inspectionCode)
        {
            const string sql = @"
                SELECT BoxLotNo, BoxNSX, IsMatch, MismatchFields, CheckedAt
                FROM   InspectionLog
                WHERE  InspectionCode = @Code
                ORDER  BY LogId";

            return LoadData(sql,
                new SqlParameter("@Code", SqlDbType.NVarChar) { Value = inspectionCode });
        }

        private static InspectionLogEntry MapEntry(DataRow r) => new InspectionLogEntry
        {
            InspectionCode = r["InspectionCode"] as string,
            ItemCode = r["ItemCode"] as string,
            LotNoTong = r["LotNoTong"] as string,
            NSXTong = r["NSXTong"] as string,
            SoLuongTong = r["SoLuongTong"] == DBNull.Value ? 0 : Convert.ToInt32(r["SoLuongTong"]),
            BoxLotNo = r["BoxLotNo"] as string,
            BoxNSX = r["BoxNSX"] as string,
            IsMatch = r["IsMatch"] != DBNull.Value && Convert.ToBoolean(r["IsMatch"]),
            MismatchFields = r["MismatchFields"] as string,
            CheckedAt = r["CheckedAt"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["CheckedAt"]),
            FinalResult = r["FinalResult"] as string,
            MaPhieu = r["MaPhieu"] as string
        };
    }
}

