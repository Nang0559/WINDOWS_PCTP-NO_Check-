using System.Data;

namespace PCTP.QRCODE_HVN.PGH
{
    public partial class HVN_PGH
    {
        /// <summary>
        /// View-bound duplicate LOT selection. ListView construction is owned by
        /// PhieuDialogControl, not by the presenter/service.
        /// </summary>
        public int ShowChonSttTrungMa(DataTable danhSachTrung)
            => _phieuDialogControl.ShowChonSttTrungMa(danhSachTrung);
    }
}
