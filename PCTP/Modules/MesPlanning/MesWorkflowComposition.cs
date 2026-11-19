using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using PCTP.Shared.UiMd;

namespace PCTP.Modules.MesPlanning
{
    /// <summary>Composition root adapter: reuses the application's shared workflow repository/service.</summary>
    public static class MesWorkflowComposition
    {
        public static IProductionWorkflowService Create()
        {
            var provider = new SQLPROVIDER();
            var executor = new PhieuSqlExecutor(provider);
            var unitOfWork = new UnitOfWork(provider);
            var shared = new WorkflowTransitionService(new WorkflowRepository(executor, unitOfWork));
            return new ProductionWorkflowService(new ProductionWorkflowRepository(), shared);
        }
    }
}