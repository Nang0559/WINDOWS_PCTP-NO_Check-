using System.Data;
using PCTP.Framework.Documents;

namespace PCTP.Modules.MesPlanning.Documents
{
    /// <summary>
    /// Layer 1 registration of the component-demand document: the only views, columns and procedure
    /// the Document Runtime may use for MES component demand / issue. The read views are created by
    /// migration 050; the issue procedure is the existing MES.usp_FVN_ComponentIssue_Post, which
    /// remains the enforcement point (RBAC/scope, demand state, stock, idempotency, audit).
    /// </summary>
    public static class ComponentDemandDefinition
    {
        public const string ModuleCode = "COMPONENT_DEMAND";
        public const string IssueCommand = "ISSUE_COMPONENT";

        public static DocumentFormDefinition Create()
        {
            var fields = new[]
            {
                H("ComponentDemandId", DocumentFieldType.Number),
                H("SiteCode", DocumentFieldType.Text),
                H("ProductionPlanLineId", DocumentFieldType.Number),
                H("ProductPartNo", DocumentFieldType.Text),
                H("PlannedQty", DocumentFieldType.Number),
                H("BomEngChgLevel", DocumentFieldType.Text),
                H("BomTypeDb", DocumentFieldType.Text),
                H("AlternativeNo", DocumentFieldType.Text),
                H("Status", DocumentFieldType.Text),
                H("CreatedAtUtc", DocumentFieldType.DateTime),
                H("CreatedBy", DocumentFieldType.Text),

                D("ComponentDemandLineId", DocumentFieldType.Number),
                D("ComponentDemandId", DocumentFieldType.Number),
                D("SiteCode", DocumentFieldType.Text),
                D("ComponentPartNo", DocumentFieldType.Text),
                D("RequiredQty", DocumentFieldType.Number),
                D("PartAvailableQty", DocumentFieldType.Number),
                D("NetRequiredQty", DocumentFieldType.Number),
                D("IssuedQty", DocumentFieldType.Number),
                D("UomCode", DocumentFieldType.Text),
                D("OperationNo", DocumentFieldType.Text)
            };

            var issue = new DocumentCommandDefinition(
                IssueCommand, "MES.COMPONENT_ISSUE", "CONFIRM", "MES.usp_FVN_ComponentIssue_Post",
                false, new[]
                {
                    new DocumentParameterDefinition("SiteCode", SqlDbType.NVarChar, DocumentParameterSource.Site, 30),
                    new DocumentParameterDefinition("IssueNo", SqlDbType.NVarChar, DocumentParameterSource.Input, 80),
                    new DocumentParameterDefinition("ComponentDemandLineId", SqlDbType.BigInt, DocumentParameterSource.Input,
                        sourceFieldCode: "ComponentDemandLineId"),
                    new DocumentParameterDefinition("MaterialLotId", SqlDbType.BigInt, DocumentParameterSource.Input),
                    new DocumentParameterDefinition("FromSlotId", SqlDbType.Int, DocumentParameterSource.Input),
                    new DocumentParameterDefinition("ToSlotId", SqlDbType.Int, DocumentParameterSource.Input),
                    new DocumentParameterDefinition("Quantity", SqlDbType.Decimal, DocumentParameterSource.Input,
                        precision: 19, scale: 6),
                    new DocumentParameterDefinition("IssuedBy", SqlDbType.NVarChar, DocumentParameterSource.Actor, 100),
                    new DocumentParameterDefinition("IdempotencyKey", SqlDbType.NVarChar, DocumentParameterSource.IdempotencyKey, 180)
                });

            return new DocumentFormDefinition(
                ModuleCode,
                "MES.vw_FVN_ComponentDemandHeader",
                "MES.vw_FVN_ComponentDemandLine",
                DocumentFormDefinition.NoWorkflow, // demand status is a SQL status machine, not a workflow-engine entity
                "MES.COMPONENT_DEMAND",
                "ComponentDemandId",
                "SiteCode",
                fields,
                new[] { issue });
        }

        private static DocumentFieldDefinition H(string code, DocumentFieldType type)
        {
            return new DocumentFieldDefinition(code, DocumentRegion.Header, type);
        }

        private static DocumentFieldDefinition D(string code, DocumentFieldType type)
        {
            return new DocumentFieldDefinition(code, DocumentRegion.Detail, type);
        }
    }
}
