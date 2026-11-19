using System;
using System.Data;
using PCTP.Modules.MesPlanning;
using Xunit;

namespace PCTP.QC.Tests
{
    public sealed class ProductionFlowTaskPolicyTests
    {
        private static DataRow Row(string stage, bool related = false)
        {
            var table = new DataTable();
            table.Columns.Add("FlowStage", typeof(string));
            table.Columns.Add("IsRelatedDepartment", typeof(bool));
            table.Columns.Add("PlanStatus", typeof(string));
            table.Columns.Add("SiteCode", typeof(string));
            table.Columns.Add("DepartmentCode", typeof(string));
            table.Columns.Add("DemandStatus", typeof(string));
            table.Columns.Add("OutstandingComponentQty", typeof(decimal));
            table.Columns.Add("ProductionOrderId", typeof(long));
            table.Columns.Add("OrderStatus", typeof(string));
            table.Columns.Add("ProductionLotCount", typeof(int));
            table.Columns.Add("OperationCount", typeof(int));
            table.Columns.Add("CompletedOperationCount", typeof(int));
            table.Columns.Add("PausedOperationCount", typeof(int));
            var row = table.NewRow();
            row["FlowStage"] = stage; row["IsRelatedDepartment"] = related;
            row["PlanStatus"] = "IMPORTED"; row["SiteCode"] = "S1"; row["DepartmentCode"] = "D1";
            row["DemandStatus"] = "OPEN"; row["OutstandingComponentQty"] = 5m;
            row["ProductionOrderId"] = DBNull.Value; row["OrderStatus"] = "";
            row["ProductionLotCount"] = 0; row["OperationCount"] = 0;
            row["CompletedOperationCount"] = 0; row["PausedOperationCount"] = 0;
            table.Rows.Add(row);
            return row;
        }

        private static ProductionFlowTaskDecision Evaluate(DataRow row, string allowed)
        {
            return ProductionFlowTaskPolicy.Evaluate(row,
                (function, action) => allowed == function + ":" + action,
                (function, type, value, site) => site == "S1" && value == "D1" ||
                    type == "SITE" && value == "S1");
        }

        [Fact]
        public void RelatedDepartmentIsAlwaysReadOnlyEvenWhenUserHasActionPermission()
        {
            var decision = ProductionFlowTaskPolicy.Evaluate(Row("COMPONENTS", true),
                (f, a) => true, (f, t, v, s) => true);
            Assert.Equal(ProductionFlowTaskCode.None, decision.TaskCode);
            Assert.Equal(ProductionFlowAccessMode.ReadOnly, decision.AccessMode);
        }

        [Fact]
        public void ComponentIssueRequiresExistingDemandAndOutstandingQuantity()
        {
            var row = Row("COMPONENTS");
            var allowed = ProductionFlowTaskPolicy.Evaluate(row,
                (f, a) => f == "MES.COMPONENT_DEMAND" && a == "VIEW" ||
                          f == "MES.COMPONENT_ISSUE" && (a == "VIEW" || a == "CONFIRM"),
                (f, t, v, s) => true);
            Assert.Equal(ProductionFlowTaskCode.IssueComponents, allowed.TaskCode);
            row["OutstandingComponentQty"] = 0m;
            var noTask = ProductionFlowTaskPolicy.Evaluate(row, (f, a) => true, (f, t, v, s) => true);
            Assert.Equal(ProductionFlowTaskCode.None, noTask.TaskCode);
        }

        [Fact]
        public void ImportedPlanNeedsViewConfirmAndSiteScope()
        {
            var row = Row("PLAN");
            var denied = Evaluate(row, "MES.PLAN_UPLOAD:VIEW");
            Assert.Equal(ProductionFlowTaskCode.None, denied.TaskCode);
            var allowed = ProductionFlowTaskPolicy.Evaluate(row,
                (f, a) => f == "MES.PLAN_UPLOAD" && (a == "VIEW" || a == "CONFIRM"),
                (f, t, v, s) => t == "SITE" && v == "S1");
            Assert.Equal(ProductionFlowTaskCode.ValidatePlan, allowed.TaskCode);
            Assert.Equal(ProductionFlowAccessMode.Actionable, allowed.AccessMode);
        }

        [Fact]
        public void ProductionOrderReleaseRequiresDepartmentScopeAndApprove()
        {
            var row = Row("ORDER");
            row["ProductionOrderId"] = 99L; row["OrderStatus"] = "CREATED";
            var denied = ProductionFlowTaskPolicy.Evaluate(row,
                (f, a) => f == "MES.PRODUCTION_EXECUTION" && a == "VIEW",
                (f, t, v, s) => true);
            Assert.Equal(ProductionFlowTaskCode.None, denied.TaskCode);
            var allowed = ProductionFlowTaskPolicy.Evaluate(row,
                (f, a) => f == "MES.PRODUCTION_EXECUTION" && (a == "VIEW" || a == "APPROVE"),
                (f, t, v, s) => true);
            Assert.Equal(ProductionFlowTaskCode.ReleaseOrder, allowed.TaskCode);
        }

        [Fact]
        public void TaskIdentifiersAreEnumsAndNotLocalizedLabels()
        {
            var row = Row("PLAN");
            var decision = ProductionFlowTaskPolicy.Evaluate(row,
                (f, a) => true, (f, t, v, s) => true);
            Assert.Equal(ProductionFlowTaskCode.ValidatePlan, decision.TaskCode);
            Assert.Equal("Kiểm tra kế hoạch", decision.TaskLabel);
        }
    }
}