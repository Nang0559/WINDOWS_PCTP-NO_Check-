using System;

namespace PCTP.Modules.NhapKho.Repository
{
    /// <summary>
    /// Chống nhập trùng tem TỔNG. Thay thế logic Case cũ dựa trên bảng NHAP_TP_HIS.
    /// Nguồn sự thật: SlotLot.QrData (tồn hiện tại, kiểm tra qua IPhieuTrackingRepository.ExistsQrData)
    /// + StockHistory(ActionType='IMPORT').QrData (nhật ký bất biến, còn nguyên khi hàng đã xuất khỏi SlotLot).
    /// </summary>
    public interface IStockTpImportGuardRepository
    {
        /// <summary>
        /// Tem này đã từng được nhập (ghi trong StockHistory) hay chưa — kể cả khi dòng SlotLot đã bị xoá do xuất hết.
        /// </summary>
        bool ExistsImportedInHistory(string itemCode, string qrData);

        /// <summary>
        /// Khoá ứng dụng (sp_getapplock, owner = Transaction) theo QR để 2 máy quét cùng 1 tem
        /// bị xếp hàng; máy đến sau sẽ thấy dữ liệu đã commit khi kiểm tra trùng.
        /// BẮT BUỘC gọi sau IUnitOfWork.Begin() và TRƯỚC khi kiểm tra trùng.
        /// </summary>
        void AcquireImportLock(string qrData);
    }
}
