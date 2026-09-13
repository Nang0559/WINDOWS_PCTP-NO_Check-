using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;

namespace PCTP.Modules.GiaoHangKhach.WorkingState
{
    /// <summary>
    /// Compatibility facade for the Phase-4 implementation whose concrete
    /// namespace lives under OrderLoading.WorkingState.
    /// </summary>
    public sealed class DeliveryWorkingState
        : PCTP.Modules.GiaoHangKhach.OrderLoading.WorkingState.DeliveryWorkingState
    {
        public DeliveryWorkingState(
            IPhieuTmpRepository tmpRepo,
            IPhieuValidationRepository validationRepo)
            : base(tmpRepo, validationRepo)
        {
        }
    }
}