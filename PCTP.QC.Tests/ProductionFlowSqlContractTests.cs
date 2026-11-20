using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests
{
    public sealed class ProductionFlowSqlContractTests
    {
        private static string Read(string name)
        {
            string path = Path.Combine(AppContext.BaseDirectory, name);
            Assert.True(File.Exists(path), "Missing SQL source fixture: " + path);
            return File.ReadAllText(path);
        }

        [Fact]
        public void DashboardCteDoesNotReferenceOuterPfAlias()
        {
            string sql = Read("047_MES_ProductionFlowControlTower.sql");
            int cte = sql.IndexOf(";WITH PlanFlow", StringComparison.OrdinalIgnoreCase);
            int outerSelect = sql.IndexOf("SELECT pf.ProductionPlanLineId", cte, StringComparison.OrdinalIgnoreCase);
            Assert.True(cte >= 0 && outerSelect > cte);
            string cteBody = sql.Substring(cte, outerSelect - cte);
            Assert.DoesNotContain("pf.ProductPartNo", cteBody, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void BomVisibilityDoesNotDependOnComponentDemandHeader()
        {
            string sql = Read("049_MES_ProductionFlowChangeVersionAndBomVisibility.sql");
            int cte = sql.IndexOf(";WITH PlanFlow", StringComparison.OrdinalIgnoreCase);
            int outerSelect = sql.IndexOf("SELECT pf.ProductionPlanLineId", cte, StringComparison.OrdinalIgnoreCase);
            string cteBody = sql.Substring(cte, outerSelect - cte);
            Assert.DoesNotContain("JOIN MES.FVN_ComponentDemandHeader demand", cteBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("IFS.FVN_BomComponent", cteBody, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CrossMachineVersionTriggersCoverCoreFlowEntities()
        {
            string sql = Read("049_MES_ProductionFlowChangeVersionAndBomVisibility.sql");
            Assert.Contains("FVN_ProductionFlowChangeVersion", sql);
            Assert.Contains("FVN_FlowVersion_DemandLine", sql);
            Assert.Contains("FVN_FlowVersion_Order", sql);
            Assert.Contains("FVN_FlowVersion_Lot", sql);
            Assert.Contains("FVN_FlowVersion_Operation", sql);
        }
    }
}