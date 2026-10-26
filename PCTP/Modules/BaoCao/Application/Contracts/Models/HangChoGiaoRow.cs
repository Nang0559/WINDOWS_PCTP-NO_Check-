using System;


namespace PCTP.Modules.BaoCao.Application.Contracts.Models
{

    /// <summary>
    /// 1 dòng "hàng chờ giao" (đã pick khỏi slot, chưa xác nhận giao xong),
    /// đọc từ bảng FVN_HangChoGiao — dùng cho màn hình tra cứu giao hàng.
    /// </summary>
    public sealed class HangChoGiaoRow
    {
        public int Id { get; set; }
        public string MaHang { get; set; }
        public string LotThung { get; set; }
        public string LotGoc { get; set; }
        public int SoLuong { get; set; }
        public int? SlotIdNguon { get; set; }
        public string TrangThai { get; set; }
        public DateTime NgayXuatKho { get; set; }
        public string NguoiXuatKho { get; set; }
        public DateTime? NgayGiao { get; set; }
        public string NguoiGiao { get; set; }
    }
}
