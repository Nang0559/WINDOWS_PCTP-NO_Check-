using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Views
{
    public interface INhapKhoView
    {
        // Events
        event EventHandler FormLoaded;
        event EventHandler<QRScanEventArgs> QRSubmitted;
        event EventHandler NhapKhoClicked;
        event EventHandler ResetClicked;
        event EventHandler RefreshClicked;

        // UI state
        string LoaiHinhNhap { get; }  // "N" = mới, "NG" = nhập lại

        // Grid binding
        void BindPhieuNhap(DataTable dt);
        void CapNhapSlSeNhap(string find, int sl);

        // Grid read
        PhieuNhapInfo FindPhieu(string find);
        List<NhapKhoItem> GetDanhSachSeNhap();

        // Dialog
        NGResult ShowChonNG(string lot, DataTable tbNG);

        // QR
        void ClearQRInput();
        void FocusQRInput();

        // Feedback
        void ShowLoading(bool show, string caption = "Đang xử lý...");
        void ShowError(string msg);
        void ShowInfo(string msg);
        bool Confirm(string msg);
    }
    // ── QR event args ─────────────────────────────────────────────────────────
    public class QRScanEventArgs : EventArgs
    {
        public string RawQR { get; set; }
        public string CaseNo { get; set; }
        public string LotNoSL { get; set; }
        public string MaHang { get; set; }
        public int SoLuong { get; set; }
        public string SoPhieu { get; set; }
        public string SPNhap { get; set; }
        public string IDSP { get; set; }
    }
}
