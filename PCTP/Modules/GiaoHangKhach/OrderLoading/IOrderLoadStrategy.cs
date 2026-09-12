using PCTP.Shared.Enums;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Interfaces
{
    /// <summary>
    /// Strategy pattern chọn NGUỒN đơn hàng gốc cho Giao Hàng Khách: từ IFS
    /// (<c>IfsOrderLoadStrategy</c> — luồng HVN thường) hoặc từ bảng riêng
    /// Purchase_Order_* (<c>OrderTableLoadStrategy</c>, dùng <c>ITableOrderRepository</c>
    /// — luồng YMVN/HTN và HVN "giao đặc biệt"). <c>OrderLoadStrategyFactory</c> chọn
    /// implementation nào dựa trên <see cref="OrderLoadContext.CheDoGiaoDacBiet"/> và
    /// <c>CustomerConfig.LoadTuBangRieng</c>. Xem WORKFLOW_GIAOHANGKHACH.md mục 3.
    /// </summary>
    public interface IOrderLoadStrategy
    {
        /// <summary>Load đơn hàng gốc (chưa có LOT) — từ IFS, bảng riêng, hoặc file upload.</summary>
        DataTable LoadDonHangGoc(OrderLoadContext ctx);

        /// <summary>
        /// Merge LOT đã lưu trong LUUPHIEUGIAOHANG vào donHang — bắt buộc chạy sau LoadDonHangGoc
        /// mỗi lần load lại (F5, đổi ngày/giờ, quay lại từ CNK...). Đây là chỗ trước đây
        /// nằm rải rác (MergeLotTuBangRieng cho YMVN/HTN, GetSavedLot cho HVN) — nay 1 interface.
        /// </summary>
        void MergeLotDaLuu(DataTable donHang, OrderLoadContext ctx);

        /// <summary>Đồng bộ đơn hàng chưa CNK vào bảng TMP tương ứng, chuẩn bị cho bắn QR.</summary>
        void SyncChoDocQR(DataTable donHang, OrderLoadContext ctx);
        /// <summary>
        /// Đối chiếu đơn hàng đã load với dữ liệu IFS thật (nếu nguồn không phải
        /// IFS gốc) — trả về danh sách chênh lệch (SL bảng riêng khác SL IFS,
        /// hoặc mã hàng có ở bên này mà không có ở bên kia). Với IFS-native
        /// strategy, trả về DataTable rỗng (không có gì để so sánh với chính nó).
        /// </summary>
        DataTable SoSanhVoiIFS(DataTable donHang, OrderLoadContext ctx);
    }

    public class OrderLoadContext
    {
        public CustomerConfig Cfg { get; set; }
        public DateTime NgayGiao { get; set; }
        public string NhaMay { get; set; }
        public int AddNm { get; set; }
        public string GioFcc { get; set; }
        public string GioFccMoTa { get; set; }
        public List<string> CheckedGios { get; set; }       // YMVN
        public bool IsLoaiSP { get; set; }
        public MachineRole MachineRole { get; set; }         // ← THAY isMayBanQR bool bằng enum (mục 2)
        public bool IsBanQR { get; set; }
        /// <summary>
        /// True khi người dùng chọn "giao đặc biệt" (upload PO ngoài lịch IFS
        /// thường — trước đây là nhập tay trên grid, nay bắt buộc qua upload
        /// giống pattern HTN/YMVN). Chỉ có ý nghĩa với Cfg.CoGiaoDacBiet == true.
        /// </summary>
        public bool CheDoGiaoDacBiet { get; set; }
    }
}
