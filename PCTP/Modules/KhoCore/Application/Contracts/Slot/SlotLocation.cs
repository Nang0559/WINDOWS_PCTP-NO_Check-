namespace PCTP.Modules.KhoCore.Application.Contracts.Slot
{
    /// <summary>
    /// Stable warehouse slot identity exposed outside KhoCore infrastructure.
    /// This contract deliberately contains no KhoVatLy/UI types.
    /// </summary>
    public sealed class SlotLocation
    {
        public int SlotId { get; set; }
        public string WarehouseName { get; set; }
        public string RackName { get; set; }
        public int SlotNumber { get; set; }
        public int Capacity { get; set; }
    }
}
