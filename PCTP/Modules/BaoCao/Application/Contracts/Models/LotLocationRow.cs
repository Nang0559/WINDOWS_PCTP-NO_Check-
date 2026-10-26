namespace PCTP.Modules.BaoCao.Application.Contracts.Models
{
    /// <summary>
    /// Read-only projection of the current physical location of a LOT.
    /// This model is intentionally independent from the legacy PhieuTrackingRepository.
    /// </summary>
    public sealed class LotLocationRow
    {
        public int SlotId { get; set; }
        public string LotNo { get; set; }
        public int Quantity { get; set; }
        public string MaPhieu { get; set; }
        public string ParentSoPhieu { get; set; }
        public string SoPhieuTong { get; set; }
        public int PhieuStatus { get; set; }
        public System.DateTime? ImportDate { get; set; }
        public int SlotNumber { get; set; }
        public string RackName { get; set; }
        public string WarehouseName { get; set; }
    }
}
