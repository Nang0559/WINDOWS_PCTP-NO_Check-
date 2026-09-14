using System.Data;

namespace PCTP.Modules.GiaoHangKhach.HVN
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
