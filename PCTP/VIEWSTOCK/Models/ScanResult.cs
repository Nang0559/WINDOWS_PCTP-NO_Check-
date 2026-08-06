using PCTP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class ScanResult
    {
        // ── Trạng thái ───────────────────────────────────────────────────
        public bool IsOK { get;  set; }
        public bool IsSlKhongKhop { get; private set; }  // SL HVN ≠ SL FCC
        public bool IsTrung { get; private set; }  // trùng case/lot
        public bool IsLoi { get; private set; }  // lỗi nghiệp vụ khác
        public string Message { get;  set; }
        public bool CanhBaoVuotSanLuong { get; set; }
        public string CaseNo { get; set; }
        public List<StockTraHangInfo> NgList { get; set; }
        public static ScanResult OKNgList(List<StockTraHangInfo> list) => new ScanResult { IsOK = true, NgList = list };
        // ── Payload — tuỳ loại scan ──────────────────────────────────────
        /// <summary>Giao hàng: item đang chờ xác nhận SL khác biệt</summary>
        public DocQRCode Pending { get; private set; }

        /// <summary>Nhập kho: thông tin QR đã parse thành công</summary>
        public QRCodeInfo QRInfo { get; private set; }

        /// <summary>Nhập kho: item đã build sẵn để insert</summary>
        public NhapKhoItem NhapKhoItem { get; private set; }

        // ── Factory methods ──────────────────────────────────────────────

        /// <summary>Scan thành công — giao hàng</summary>
        public static ScanResult OK(DocQRCode item) => new ScanResult
        {
            IsOK = true,
            Pending = item
        };

        /// <summary>Scan thành công — nhập kho</summary>
        public static ScanResult OKNhapKho(QRCodeInfo qr,
                                            NhapKhoItem nhapItem = null)
            => new ScanResult
            {
                IsOK = true,
                QRInfo = qr,
                NhapKhoItem = nhapItem
            };

        /// <summary>Scan thất bại — lỗi nghiệp vụ</summary>
        public static ScanResult Fail(string message) => new ScanResult
        {
            IsOK = false,
            IsLoi = true,
            Message = message
        };

        /// <summary>SL HVN ≠ SL FCC — cần hỏi user xác nhận (giao hàng)</summary>
        public static ScanResult SlKhongKhop(DocQRCode pending) => new ScanResult
        {
            IsOK = false,
            IsSlKhongKhop = true,
            Pending = pending,
            Message = "Số lượng tem HVN khác FCC — cần xác nhận"
        };

        /// <summary>Case/LOT bị trùng trong session hoặc DB</summary>
        public static ScanResult Trung(string message) => new ScanResult
        {
            IsOK = false,
            IsTrung = true,
            IsLoi = true,
            Message = message
        };
    }
}
