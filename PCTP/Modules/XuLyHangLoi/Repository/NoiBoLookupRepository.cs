using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    public sealed class NoiBoLookupRepository
        : SqlRepositoryBase, INoiBoLookupRepository
    {
        public NoiBoLookupRepository(PhieuSqlExecutor sql, IUnitOfWork unitOfWork)
            : base(sql, unitOfWork)
        {
        }

        public List<DeptLookupItem> GetActiveDepartments()
        {
            const string sql = @"
SELECT Code, Name
FROM B7R2_FCC.dbo.B20Dept
WHERE ISNULL(IsActive, 1) = 1
  AND Code IS NOT NULL
ORDER BY Code;";

            DataTable dt = LoadData(sql);
            var list = new List<DeptLookupItem>(dt.Rows.Count);

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new DeptLookupItem
                {
                    Code = Convert.ToString(row["Code"])?.Trim(),
                    Name = Convert.ToString(row["Name"])?.Trim()
                });
            }

            return list;
        }

        public string GetItemName(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                return null;

            const string sql = @"
SELECT TOP (1) Name
FROM B7R2_FCC.dbo.vB20Item
WHERE Code = @ItemCode;";

            DataTable dt = LoadData(
                sql,
                new System.Data.SqlClient.SqlParameter("@ItemCode", SqlDbType.NVarChar, 100)
                {
                    Value = itemCode.Trim()
                });

            if (dt.Rows.Count == 0)
                return null;

            return dt.Rows[0]["Name"] == DBNull.Value
                ? string.Empty
                : Convert.ToString(dt.Rows[0]["Name"]);
        }

        public List<ItemLookupItem> GetItemsInSlots()
        {
            // Mã hàng lấy từ Slot/SlotLot đang chứa hàng; tên lấy từ vB20Item (nếu có).
            const string sql = @"
SELECT x.ItemCode, MAX(i.Name) AS ItemName
FROM (
    SELECT DISTINCT LTRIM(RTRIM(sl.ItemCode)) AS ItemCode
    FROM SlotLot sl
    WHERE sl.Quantity > 0
      AND sl.ItemCode IS NOT NULL
      AND LTRIM(RTRIM(sl.ItemCode)) <> ''
) x
LEFT JOIN B7R2_FCC.dbo.vB20Item i ON i.Code = x.ItemCode
GROUP BY x.ItemCode
ORDER BY x.ItemCode;";

            DataTable dt = LoadData(sql);
            var list = new List<ItemLookupItem>(dt.Rows.Count);

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new ItemLookupItem
                {
                    ItemCode = Convert.ToString(row["ItemCode"]),
                    ItemName = row["ItemName"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(row["ItemName"])
                });
            }

            return list;
        }
    }
}
