using System;
using System.Data;

namespace PCTP.Modules.MesPlanning
{
    public enum ProductionFlowTaskCode
    {
        None = 0, ValidatePlan, FirmPlan, IssueComponents, CreateOrder, ReleaseOrder,
        CreateLot, ResumeOrder, ResumeOperation, CompleteOrder, CloseOrder, ExecuteOperation
    }

    public enum ProductionFlowAccessMode { ReadOnly = 0, Actionable = 1 }

    public sealed class ProductionFlowTaskDecision
    {
        public ProductionFlowTaskCode TaskCode { get; private set; }
        public ProductionFlowAccessMode AccessMode { get; private set; }
        public string TaskLabel { get; private set; }
        public bool IsActionable { get { return AccessMode == ProductionFlowAccessMode.Actionable; } }

        public ProductionFlowTaskDecision(ProductionFlowTaskCode code, string label)
        {
            TaskCode = code;
            TaskLabel = label ?? string.Empty;
            AccessMode = code == ProductionFlowTaskCode.None
                ? ProductionFlowAccessMode.ReadOnly : ProductionFlowAccessMode.Actionable;
        }
    }

    /// <summary>Pure task policy: no UI strings are used as identifiers and all authority is injected.</summary>
    public static class ProductionFlowTaskPolicy
    {
        public static ProductionFlowTaskDecision Evaluate(
            DataRow row,
            Func<string, string, bool> can,
            Func<string, string, string, string, bool> inScope)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (can == null) throw new ArgumentNullException(nameof(can));
            if (inScope == null) throw new ArgumentNullException(nameof(inScope));
            var none = new ProductionFlowTaskDecision(ProductionFlowTaskCode.None, "Theo dõi");
            if (GetBool(row, "IsRelatedDepartment")) return none;

            string stage = GetText(row, "FlowStage");
            string planStatus = GetText(row, "PlanStatus");
            string site = GetText(row, "SiteCode");
            string department = GetText(row, "DepartmentCode");
            bool productionScope = !string.IsNullOrWhiteSpace(department) &&
                inScope("MES.PRODUCTION_EXECUTION", "SITE", site, site) &&
                inScope("MES.PRODUCTION_EXECUTION", "DEPARTMENT", department, site);

            if (stage == "PLAN" && planStatus == "IMPORTED" &&
                can("MES.PLAN_UPLOAD", "VIEW") && can("MES.PLAN_UPLOAD", "CONFIRM") &&
                inScope("MES.PLAN_UPLOAD", "SITE", site, site))
                return New(ProductionFlowTaskCode.ValidatePlan, "Kiểm tra kế hoạch");
            if (stage == "PLAN" && planStatus == "VALIDATED" &&
                can("MES.PLAN_UPLOAD", "VIEW") && can("MES.PLAN_UPLOAD", "APPROVE") &&
                inScope("MES.PLAN_UPLOAD", "SITE", site, site))
                return New(ProductionFlowTaskCode.FirmPlan, "FIRM kế hoạch");
            if (stage == "COMPONENTS" && !string.IsNullOrWhiteSpace(GetText(row, "DemandStatus")) &&
                GetDecimal(row, "OutstandingComponentQty") > 0m &&
                can("MES.COMPONENT_DEMAND", "VIEW") && can("MES.COMPONENT_ISSUE", "VIEW") &&
                can("MES.COMPONENT_ISSUE", "CONFIRM") &&
                inScope("MES.COMPONENT_ISSUE", "SITE", site, site))
                return New(ProductionFlowTaskCode.IssueComponents, "Cấp linh kiện");

            if (!productionScope || !can("MES.PRODUCTION_EXECUTION", "VIEW")) return none;
            string orderStatus = GetText(row, "OrderStatus");
            bool hasOrder = row.Table.Columns.Contains("ProductionOrderId") &&
                row["ProductionOrderId"] != DBNull.Value;
            if (stage == "ORDER")
            {
                if (!hasOrder && can("MES.PRODUCTION_EXECUTION", "CREATE"))
                    return New(ProductionFlowTaskCode.CreateOrder, "Tạo lệnh sản xuất");
                if (orderStatus == "CREATED" && can("MES.PRODUCTION_EXECUTION", "APPROVE"))
                    return New(ProductionFlowTaskCode.ReleaseOrder, "RELEASE lệnh");
                if (orderStatus == "RELEASED" && GetInt(row, "ProductionLotCount") == 0 &&
                    can("MES.PRODUCTION_EXECUTION", "CREATE"))
                    return New(ProductionFlowTaskCode.CreateLot, "Tạo LOT sản xuất");
            }
            if (stage == "DONE" && orderStatus == "COMPLETED" &&
                can("MES.PRODUCTION_EXECUTION", "APPROVE"))
                return New(ProductionFlowTaskCode.CloseOrder, "CLOSE lệnh");
            if (stage == "PRODUCTION")
            {
                int operationCount = GetInt(row, "OperationCount");
                int completed = GetInt(row, "CompletedOperationCount");
                if (orderStatus == "PAUSED" && can("MES.PRODUCTION_EXECUTION", "CONFIRM"))
                    return New(ProductionFlowTaskCode.ResumeOrder, "RESUME lệnh");
                if (GetInt(row, "PausedOperationCount") > 0 && can("MES.PRODUCTION_EXECUTION", "CONFIRM"))
                    return New(ProductionFlowTaskCode.ResumeOperation, "RESUME công đoạn");
                if (operationCount > 0 && completed >= operationCount &&
                    can("MES.PRODUCTION_EXECUTION", "APPROVE"))
                    return New(ProductionFlowTaskCode.CompleteOrder, "COMPLETE lệnh");
                if (can("MES.PRODUCTION_EXECUTION", "CONFIRM"))
                    return New(ProductionFlowTaskCode.ExecuteOperation, "Thực hiện công đoạn");
            }
            return none;
        }

        private static ProductionFlowTaskDecision New(ProductionFlowTaskCode code, string label)
            => new ProductionFlowTaskDecision(code, label);
        private static string GetText(DataRow row, string name)
            => row.Table.Columns.Contains(name) && row[name] != DBNull.Value ? Convert.ToString(row[name]) : string.Empty;
        private static bool GetBool(DataRow row, string name)
            => row.Table.Columns.Contains(name) && row[name] != DBNull.Value && Convert.ToBoolean(row[name]);
        private static int GetInt(DataRow row, string name)
        {
            int value;
            return int.TryParse(GetText(row, name), out value) ? value : 0;
        }
        private static decimal GetDecimal(DataRow row, string name)
        {
            decimal value;
            return decimal.TryParse(GetText(row, name), out value) ? value : 0m;
        }
    }
}