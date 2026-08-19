using PCTP.Domain.Entities;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Helpers
{
    public class ScanResult
    {
        // ============================================================
        // TRẠNG THÁI CHUNG
        // ============================================================

        public bool IsOK { get; private set; }

        public bool IsSlKhongKhop { get; private set; }

        public bool IsTrung { get; private set; }

        public bool IsLoi { get; private set; }

        public bool CanhBaoVuotSanLuong { get; private set; }

        public string Message { get; private set; }


        // ============================================================
        // PAYLOAD - GIAO HÀNG
        // ============================================================

        public DocQRCode Pending { get; private set; }

        public string CaseNo { get; private set; }


        // ============================================================
        // PAYLOAD - NHẬP KHO
        // ============================================================

        public QRCodeInfo QRInfo { get; private set; }

        public NhapKhoItem NhapKhoItem { get; private set; }


        // ============================================================
        // PAYLOAD - TRẢ HÀNG / NG
        // ============================================================

        public List<StockTraHangInfo> NgList { get; private set; }


        // ============================================================
        // SUCCESS CHUNG
        // ============================================================

        public static ScanResult OK(
            string message = null)
        {
            return new ScanResult
            {
                IsOK = true,
                IsLoi = false,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = false,
                Message = message ?? string.Empty
            };
        }


        // ============================================================
        // SUCCESS - GIAO HÀNG
        // ============================================================

        public static ScanResult OK(
            DocQRCode item,
            string message = null)
        {
            return new ScanResult
            {
                IsOK = true,
                Pending = item,
                IsLoi = false,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = false,
                Message = message ?? string.Empty
            };
        }


        // ============================================================
        // SUCCESS - NHẬP KHO
        // ============================================================

        public static ScanResult OKNhapKho(
            QRCodeInfo qr,
            NhapKhoItem nhapItem = null,
            string message = null)
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
                Message = message ?? string.Empty
            };
        }


        // ============================================================
        // SUCCESS - NG / TRẢ HÀNG
        // ============================================================

        public static ScanResult OKNgList(
            List<StockTraHangInfo> list,
            string message = null)
        {
            return new ScanResult
            {
                IsOK = true,
                NgList = list,
                IsLoi = false,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = false,
                Message = message ?? string.Empty
            };
        }


        // ============================================================
        // FAIL - LỖI NGHIỆP VỤ
        // ============================================================

        public static ScanResult Fail(
            string message)
        {
            return new ScanResult
            {
                IsOK = false,
                IsLoi = true,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = false,
                Message = message ?? string.Empty
            };
        }


        // ============================================================
        // FAIL - TRÙNG
        // ============================================================

        public static ScanResult Trung(
            string message)
        {
            return new ScanResult
            {
                IsOK = false,
                IsLoi = true,
                IsTrung = true,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = false,
                Message = message ?? "Dữ liệu đã tồn tại."
            };
        }


        // ============================================================
        // FAIL - SL KHÔNG KHỚP
        // ============================================================

        public static ScanResult SlKhongKhop(
            DocQRCode pending)
        {
            return new ScanResult
            {
                IsOK = false,
                IsLoi = false,
                IsTrung = false,
                IsSlKhongKhop = true,
                CanhBaoVuotSanLuong = false,
                Pending = pending,
                Message = "Số lượng tem HVN khác FCC — cần xác nhận."
            };
        }


        // ============================================================
        // CẢNH BÁO VƯỢT SẢN LƯỢNG
        // ============================================================

        public static ScanResult CanhBao(
            string message)
        {
            return new ScanResult
            {
                IsOK = false,
                IsLoi = false,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = true,
                Message = message ?? string.Empty
            };
        }


        // ============================================================
        // CẢNH BÁO + PAYLOAD
        // ============================================================

        public static ScanResult CanhBaoVuot(
            string message,
            DocQRCode pending)
        {
            return new ScanResult
            {
                IsOK = false,
                IsLoi = false,
                IsTrung = false,
                IsSlKhongKhop = false,
                CanhBaoVuotSanLuong = true,
                Pending = pending,
                Message = message ?? string.Empty
            };
        }
    }
}
