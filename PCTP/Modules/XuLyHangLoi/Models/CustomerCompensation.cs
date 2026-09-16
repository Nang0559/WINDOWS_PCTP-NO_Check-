using System;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    /// <summary>
    /// Nghĩa vụ giao bù cho khách hàng.
    /// SoLuongCanGiaoBu lấy từ đơn giao/đơn hàng bị ảnh hưởng, không tính từ NG sau Rework.
    /// </summary>
    public class CustomerCompensation
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }

        public int? PhieuKhachTraId { get; set; }
        public int? OrderId { get; set; }
        public int? OrderLineId { get; set; }
        public string OriginalDeliveryReference { get; set; }

        public int SoLuongCanGiaoBu { get; set; }
        public int SoLuongDaGiaoBu { get; set; }

        public int SoLuongConLai
        {
            get { return Math.Max(0, SoLuongCanGiaoBu - SoLuongDaGiaoBu); }
        }

        public bool IsCompleted
        {
            get { return SoLuongDaGiaoBu >= SoLuongCanGiaoBu; }
        }

        public DateTime? CompletedAt { get; set; }
        public string CompletedBy { get; set; }

        public void Validate()
        {
            if (SoLuongCanGiaoBu < 0 || SoLuongDaGiaoBu < 0)
                throw new InvalidOperationException("Số lượng giao bù không được âm.");

            if (SoLuongDaGiaoBu > SoLuongCanGiaoBu)
                throw new InvalidOperationException("Số lượng đã giao bù không được lớn hơn số lượng cần giao bù.");
        }
    }
}
