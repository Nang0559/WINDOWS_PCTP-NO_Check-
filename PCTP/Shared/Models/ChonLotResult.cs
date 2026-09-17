namespace PCTP.Shared.Models
{
    /// <summary>
    /// Result returned by the warehouse LOT selection dialog.
    /// </summary>
    public sealed class ChonLotResult
    {
        public bool Confirmed { get; set; }
        public string LotGhep { get; set; }
    }
}
