using System;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    /// <summary>
    /// Kết quả disposition cuối cùng của hàng NG.
    /// Rework NG được cộng vào loại bỏ cuối cùng, không quay lại hàng OK.
    /// </summary>
    public class DispositionResult
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }

        public int SoLuongLoaiBoBanDau { get; set; }
        public int SoLuongReworkNG { get; set; }
        public int SoLuongLoaiBoCuoi { get; set; }

        public string LyDo { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string ConfirmedBy { get; set; }

        public void CalculateAndValidate()
        {
            if (SoLuongLoaiBoBanDau < 0 || SoLuongReworkNG < 0 || SoLuongLoaiBoCuoi < 0)
                throw new InvalidOperationException("Số lượng loại bỏ không được âm.");

            var expected = SoLuongLoaiBoBanDau + SoLuongReworkNG;
            if (SoLuongLoaiBoCuoi != expected)
                throw new InvalidOperationException("Loại bỏ cuối cùng phải bằng Loại bỏ ban đầu + NG sau Rework.");
        }
    }
}
