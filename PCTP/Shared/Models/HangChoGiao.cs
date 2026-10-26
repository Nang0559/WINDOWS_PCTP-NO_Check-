using PCTP.Modules.XuatKho.Models;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    public sealed class HangChoGiao
    {
        /// <summary>
        /// ID hệ thống.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Lot thùng / Lot thực tế dùng khi giao.
        /// </summary>
        public string LotThung { get; set; }

        /// <summary>
        /// Lot gốc của hàng.
        /// </summary>
        public string LotGoc { get; set; }

        /// <summary>
        /// Mã hàng.
        /// </summary>
        public string MaHang { get; set; }

        /// <summary>
        /// Số lượng đã lấy ra khỏi Slot và đang chờ giao.
        /// </summary>
        public int SoLuong { get; set; }

        /// <summary>
        /// Slot nguồn trước khi xuất khỏi kho.
        /// Null nếu hàng xuất trực tiếp từ kho ảo A0.
        /// </summary>
        public int? SlotIdNguon { get; set; }

        /// <summary>
        /// Loại nghiệp vụ yêu cầu giao.
        /// Ví dụ:
        /// - GiaoHang
        /// - GiaoBuNG
        /// </summary>
        public HangChoGiaoLoai LoaiYeuCauGiao { get; set; }

        /// <summary>
        /// Trạng thái của hàng trong danh sách chờ giao.
        /// </summary>
        public HangChoGiaoStatus TrangThai { get; set; }
        public StockExportReferenceType? ReferenceType { get; set; }
        public int? ReferenceId { get; set; }

        /// <summary>
        /// ID phiếu / yêu cầu giao liên quan.
        /// Ví dụ:
        /// - ID phiếu giao thông thường
        /// - ID phiếu khách trả đối với giao bù NG
        /// </summary>
        public int? YeuCauGiaoId { get; set; }

        /// <summary>
        /// Thời điểm hàng được xuất khỏi Slot.
        /// </summary>
        public DateTime NgayXuatKho { get; set; }

        /// <summary>
        /// Người thực hiện lấy hàng khỏi kho.
        /// </summary>
        public string NguoiXuatKho { get; set; }

        /// <summary>
        /// Thời điểm giao thực tế.
        /// </summary>
        public DateTime? NgayGiao { get; set; }

        /// <summary>
        /// Người thực hiện giao.
        /// </summary>
        public string NguoiGiao { get; set; }

        /// <summary>
        /// Ghi chú nghiệp vụ.
        /// </summary>
        public string Note { get; set; }
    }
}
