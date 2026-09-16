using System;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    /// <summary>
    /// Snapshot nguồn hàng bị ảnh hưởng tại thời điểm truy vết LOT.
    /// Không được dùng tồn kho hiện tại để thay thế snapshot này khi lập báo cáo.
    /// </summary>
    public class PhieuXuLyBatThuongAffectedLot
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }

        public AffectedLotSourceType SourceType { get; set; }
        public string SourceReference { get; set; }

        public int? SlotId { get; set; }
        public string LotNo { get; set; }
        public string MaSanPham { get; set; }
        public string Model { get; set; }

        /// <summary> Số lượng xác định là bị ảnh hưởng tại thời điểm snapshot. </summary>
        public int SoLuongAnhHuong { get; set; }

        /// <summary> Số lượng đã kiểm tra QC trên nguồn này. </summary>
        public int SoLuongDaKiemTra { get; set; }

        public int SoLuongOK { get; set; }
        public int SoLuongNG { get; set; }
        public int SoLuongRework { get; set; }
        public int SoLuongLoaiBo { get; set; }

        public DateTime SnapshotAt { get; set; }
        public string SnapshotBy { get; set; }

        public void ValidateQuantities()
        {
            if (SoLuongAnhHuong < 0 || SoLuongDaKiemTra < 0 ||
                SoLuongOK < 0 || SoLuongNG < 0 || SoLuongRework < 0 ||
                SoLuongLoaiBo < 0)
                throw new InvalidOperationException("Số lượng LOT bị ảnh hưởng không được âm.");

            if (SoLuongDaKiemTra > SoLuongAnhHuong)
                throw new InvalidOperationException("Số lượng đã kiểm tra không được lớn hơn số lượng bị ảnh hưởng.");

            if (SoLuongDaKiemTra == SoLuongAnhHuong && SoLuongOK + SoLuongNG != SoLuongAnhHuong)
                throw new InvalidOperationException("Khi kiểm tra đủ LOT, OK + NG phải bằng số lượng bị ảnh hưởng.");

            if (SoLuongNG != SoLuongRework + SoLuongLoaiBo)
                throw new InvalidOperationException("NG phải được phân bổ hết cho Rework hoặc Loại bỏ.");
        }
    }
}
