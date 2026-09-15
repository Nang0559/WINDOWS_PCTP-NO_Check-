using PCTP.Models;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;


namespace PCTP.Infrastructure.Repositories
{
    public interface IPhieuTrackingRepository
    {
        // ============================================================
        // ĐỌC
        // ============================================================

        List<PhieuLocationInfo> GetPhieuTheoLot(
            string lotNo);

        int GetTongSlActiveTheoLot(
            string lotNo);

        bool ExistsQrData(
            string qrData);

        // ============================================================
        // GHI
        // ============================================================

        void InsertPhieuMoi(
            int slotId,
            string itemCode,
            string lotNo,
            int quantity,
            string temCode,
            string qrData,
            DateTime? importDate,
            string ngaySX,
            string soPhieuTong,
            string maPhieuMoi,
            string parentSoPhieu,
            PhieuStatus status);

        void CapNhatTrangThai(
            string maPhieu,
            PhieuStatus status);

        void CapNhatQuantity(
            string maPhieu,
            int quantityMoi);
    }
}
