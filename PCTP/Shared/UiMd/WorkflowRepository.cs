using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.UiMd
{
    public class WorkflowRepository : SqlRepositoryBase, IWorkflowRepository
    {
        private static readonly ConcurrentDictionary<string, List<WorkflowTransition>> _cache
        = new ConcurrentDictionary<string, List<WorkflowTransition>>();
       

        // Sửa Constructor: Nhận PhieuSqlExecutor và IUnitOfWork để truyền lên base
        public WorkflowRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        public IReadOnlyList<WorkflowTransition> GetTransitions(string processCode)
            => _cache.GetOrAdd(processCode, LoadFromDb);

        public void InvalidateCache(string processCode = null)
        {
            if (processCode == null) _cache.Clear();
            else _cache.TryRemove(processCode, out _);
        }

        private List<WorkflowTransition> LoadFromDb(string processCode)
        {
            // Thay Query(...) bằng LoadData(...) có sẵn ở SqlRepositoryBase
            var dt = LoadData(
                "SELECT FromStatus, ToStatus, ActionName, Description " +
                "FROM sys_WorkflowTransitions WHERE ProcessCode=@pc AND IsActive=1 ORDER BY Id",
                new SqlParameter("@pc", processCode ?? (object)DBNull.Value));

            return dt.Rows.Cast<DataRow>().Select(r => new WorkflowTransition
            {
                ProcessCode = processCode,
                FromStatus = Convert.ToInt32(r["FromStatus"]),
                ToStatus = Convert.ToInt32(r["ToStatus"]),
                ActionName = r["ActionName"] as string ?? string.Empty,
                Description = r["Description"] as string ?? string.Empty
            }).ToList();
        }
    }
}
