using PCTP.Domain.Entities;
using PCTP.Modules.GiaoHangKhach.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.WorkingState
{
    public interface IDeliveryWorkingState
    {
        /// <summary>
        /// Kiểm tra phiên giao hàng hiện tại có dữ liệu QR đang hoạt động hay không.
        /// </summary>
        TrangThaiBan GetTrangThaiDangBan(OrderLoadContext context);

        /// <summary>
        /// Load đơn hàng hiện tại từ TMP/DOCQR.
        /// </summary>
        DataTable LoadFromQr(OrderLoadContext context);

        /// <summary>
        /// Load trực tiếp dữ liệu hiện tại trong TMP.
        /// </summary>
        DataTable LoadCurrentOrder(OrderLoadContext context);

        /// <summary>
        /// Đồng bộ Order Source vào Delivery Working State.
        /// Đây là facade cho LuuVaLoad().
        /// </summary>
        DataTable SaveFromSource(
            OrderLoadContext context,
            DataTable orders,
            string storedProcedure);

        /// <summary>
        /// Xóa dữ liệu TMP của phiên hiện tại.
        /// </summary>
        void ClearTmp(OrderLoadContext context);

        /// <summary>
        /// Xóa dữ liệu DOCQRCODE của phiên hiện tại.
        /// </summary>
        void ClearDocQr(OrderLoadContext context);


        bool HasQr(OrderLoadContext context);
    }
}
