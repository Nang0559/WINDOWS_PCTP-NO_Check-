using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Shared.Enums;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading
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

   
}
