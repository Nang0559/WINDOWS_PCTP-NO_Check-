using PCTP.Models;
using PCTP.Shared.Models;
using System.Collections.Generic;

namespace PCTP.Modules.KhoCore.Application.Contracts.Tracking
{
    /// <summary>
    /// Read-only access to legacy SlotLot tracking data.
    /// New read callers must use this contract instead of IPhieuTrackingRepository.
    /// </summary>
    public interface IPhieuTrackingReadRepository
    {
        List<PhieuLocationInfo> GetPhieuTheoLot(string lotNo);

        int GetTongSlActiveTheoLot(string lotNo);

        bool ExistsQrData(string qrData);
    }
}
