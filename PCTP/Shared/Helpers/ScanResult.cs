using PCTP.Domain.Entities;
using PCTP.Shared.Models;
using System.Collections.Generic;

namespace PCTP.Shared.Helpers
{
    public class ScanResult
    {
        public bool IsOK { get; private set; }
        public bool IsSlKhongKhop { get; private set; }
        public bool IsTrung { get; private set; }
        public bool IsLoi { get; private set; }
        public bool CanhBaoVuotSanLuong { get; private set; }
        public bool IsFifoViolation { get; private set; }
        public string Message { get; private set; }

        public DocQRCode Pending { get; private set; }
        public string CaseNo { get; private set; }
        public QRCodeInfo QRInfo { get; private set; }
        public NhapKhoItem NhapKhoItem { get; private set; }
        public List<StockTraHangInfo> NgList { get; private set; }

        public static ScanResult OK(string message = null)
        {
            return new ScanResult
            {
                IsOK = true,
                IsLoi = false,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = false,
                IsFifoViolation = false,
                Message = message ?? string.Empty
            };
        }

        public static ScanResult OK(DocQRCode item, string message = null)
        {
            return new ScanResult
            {
                IsOK = true,
                Pending = item,
                IsLoi = false,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = false,
                IsFifoViolation = false,
                Message = message ?? string.Empty
            };
        }

        public static ScanResult OKNhapKho(QRCodeInfo qr, NhapKhoItem nhapItem = null, string message = null)
        {
            return new ScanResult
            {
                IsOK = true,
                QRInfo = qr,
                NhapKhoItem = nhapItem,
                IsLoi = false,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = false,
                IsFifoViolation = false,
                Message = message ?? string.Empty
            };
        }

        public static ScanResult OKNgList(List<StockTraHangInfo> list, string message = null)
        {
            return new ScanResult
            {
                IsOK = true,
                NgList = list,
                IsLoi = false,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = false,
                IsFifoViolation = false,
                Message = message ?? string.Empty
            };
        }

        public static ScanResult Fail(string message)
        {
            return new ScanResult
            {
                IsOK = false,
                IsLoi = true,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = false,
                IsFifoViolation = false,
                Message = message ?? string.Empty
            };
        }

        public static ScanResult FifoFail(DocQRCode pending, string message)
        {
            return new ScanResult
            {
                IsOK = false,
                Pending = pending,
                IsLoi = true,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = false,
                IsFifoViolation = true,
                Message = message ?? string.Empty
            };
        }

        public static ScanResult Trung(string message)
        {
            return new ScanResult
            {
                IsOK = false,
                IsLoi = true,
                IsTrung = true,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = false,
                IsFifoViolation = false,
                Message = message ?? "Dữ liệu đã tồn tại."
            };
        }

        public static ScanResult SlKhongKhop(DocQRCode pending)
        {
            return new ScanResult
            {
                IsOK = false,
                IsLoi = false,
                IsTrung = false,
                IsSlKhongKhop = true,
                CanhBaoVuotSanLuong = false,
                IsFifoViolation = false,
                Pending = pending,
                Message = "Số lượng tem HVN khác FCC — cần xác nhận."
            };
        }

        public static ScanResult CanhBao(string message)
        {
            return new ScanResult
            {
                IsOK = false,
                IsLoi = false,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = true,
                IsFifoViolation = false,
                Message = message ?? string.Empty
            };
        }

        public static ScanResult CanhBaoVuot(string message, DocQRCode pending)
        {
            return new ScanResult
            {
                IsOK = false,
                IsLoi = false,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = true,
                IsFifoViolation = false,
                Pending = pending,
                Message = message ?? string.Empty
            };
        }
    }
}
