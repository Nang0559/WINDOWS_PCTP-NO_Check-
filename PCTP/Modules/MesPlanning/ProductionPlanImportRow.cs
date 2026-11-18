using System;
namespace PCTP.Modules.MesPlanning
{
    public sealed class ProductionPlanImportRow
    {
        public string SiteCode { get; set; }
        public string PlanNo { get; set; }
        public DateTime PlanDate { get; set; }
        public int LineNo { get; set; }
        public string ShiftCode { get; set; }
        public string ProductionLineCode { get; set; }
        public string ProductPartNo { get; set; }
        public decimal PlannedQty { get; set; }
        public string UomCode { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
    }
}