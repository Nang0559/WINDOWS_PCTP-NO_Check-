namespace PCTP.Modules.MesPlanning
{
    public static class MesProductionFlowComposition
    {
        public static IProductionFlowService Create()
        {
            return new ProductionFlowService(new ProductionFlowRepository());
        }
    }
}