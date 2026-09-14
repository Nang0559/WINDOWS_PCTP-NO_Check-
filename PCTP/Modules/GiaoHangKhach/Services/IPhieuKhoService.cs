namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Business service cho nghiệp vụ cập nhật kho của phiếu giao (Phase 7).
    /// Không chứa UI; persistence do IPhieuRepository đảm nhiệm.
    /// Implementation: <see cref="PhieuKhoService"/>.
    /// </summary>
    public interface IPhieuKhoService
    {
        void CapNhapKho(string gioGiaoFcc, string nhaMay, string gioMa, bool isSP);
    }
}