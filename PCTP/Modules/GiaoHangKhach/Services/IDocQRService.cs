using PCTP.Domain.Entities;
using PCTP.Shared.Helpers;
using System;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Services
{
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

        void InitializeFifo(DataTable orderRows);
        void ResetFifo();

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
