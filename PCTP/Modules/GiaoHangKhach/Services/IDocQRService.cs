using PCTP.Domain.Entities;
using PCTP.Shared.Helpers;
using System;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Thin facade cho các thao tác QR delivery (bắn QR, đối chiếu, xoá dòng...).
    /// Implementation: <see cref="DocQRService"/>.
    /// </summary>
    public interface IDocQRService
    {
        bool IsBanSP { get; }
        bool IsBanOType { get; }

        void SetCheDoBanSP(bool isSP);

        void SetCheDoBan(string gioMoTa);

        int CountChuaDG();

        bool CoDocQRNao();

        DataTable LoadAll();

        void XoaDong(int stt);

        void XoaToanBo();

        void CapNhapSlHvn(int stt, int slMoi);

        ScanResult ProcessScan(
            string rawQr,
            Func<string, bool> kiemTraMaTrongPhieu,
            Func<string, int, bool> kiemTraSlDaBan);

        ScanResult ProcessScanYMVN(
            string rawQr,
            Func<string, bool> kiemTraMaTrongPhieu,
            Func<string, int, bool> kiemTraSlDaBan);

        ScanResult ConfirmSlKhacBiet(DocQRCode pending);

        bool KiemTraSlDaBan(string maHang, int slBan);
    }
}