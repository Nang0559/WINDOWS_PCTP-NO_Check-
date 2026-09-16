using System;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    /// <summary>
    /// Kết quả QC lần kiểm tra ban đầu.
    /// NG được phân tách thành Rework và Loại bỏ; không chứa logic giao bù.
    /// </summary>
    public class QCInspectionResult
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }

        public int SoLuongAnhHuong { get; set; }
        public int SoLuongDaKiemTra { get; set; }
        public int SoLuongOK { get; set; }
        public int SoLuongNG { get; set; }
        public int SoLuongRework { get; set; }
        public int SoLuongLoaiBoBanDau { get; set; }

        public string NoiDungKiemTra { get; set; }
        public string KetLuan { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string ConfirmedBy { get; set; }

        public void Validate()
        {
            if (SoLuongAnhHuong < 0 || SoLuongDaKiemTra < 0 ||
                SoLuongOK < 0 || SoLuongNG < 0 || SoLuongRework < 0 ||
                SoLuongLoaiBoBanDau < 0)
                throw new InvalidOperationException("Số lượng QC không được âm.");

            if (SoLuongDaKiemTra > SoLuongAnhHuong)
                throw new InvalidOperationException("Số lượng kiểm tra không được lớn hơn số lượng bị ảnh hưởng.");

            if (SoLuongDaKiemTra != SoLuongOK + SoLuongNG)
                throw new InvalidOperationException("Số lượng kiểm tra phải bằng OK + NG.");

            if (SoLuongNG != SoLuongRework + SoLuongLoaiBoBanDau)
                throw new InvalidOperationException("NG phải bằng Rework + Loại bỏ ban đầu.");
        }
    }
}
