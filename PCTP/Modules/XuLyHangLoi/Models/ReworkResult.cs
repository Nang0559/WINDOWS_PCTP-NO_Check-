using System;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    /// <summary>
    /// Kết quả QC sau Rework. Đây là kết quả riêng, không ghi đè QC ban đầu.
    /// </summary>
    public class ReworkResult
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }

        public int SoLuongRework { get; set; }
        public int SoLuongOK { get; set; }
        public int SoLuongNG { get; set; }

        public string KetLuan { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string ConfirmedBy { get; set; }

        public void Validate()
        {
            if (SoLuongRework < 0 || SoLuongOK < 0 || SoLuongNG < 0)
                throw new InvalidOperationException("Số lượng Rework không được âm.");

            if (SoLuongOK + SoLuongNG != SoLuongRework)
                throw new InvalidOperationException("Kết quả Rework phải bằng OK + NG.");
        }
    }
}
