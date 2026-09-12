using PCTP.Modules.GiaoHangKhach.Configuration;
using PCTP.Modules.GiaoHangKhach.Mode;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



    namespace PCTP.Shared.Models   // ★ SỬA — khớp đúng vị trí file, thay cho PCTP.VIEWSTOCK.Models sai
    {
        /// <summary>
        /// Định danh khách hàng — dùng chéo giữa nhiều module (GiaoHangKhach, XuLyHangLoi...).
        /// Chỉ chứa thông tin nhận diện/hiển thị. Mọi cơ chế nạp đơn hàng/giao hàng riêng
        /// của module GiaoHangKhach nằm trong Delivery (GiaoHangKhachCustomerOptions),
        /// null nếu khách hàng không giao hàng qua GiaoHangKhach.
        /// </summary>
        public class CustomerConfig
        {
            public string CustomerNo { get; set; }
            public string DisplayName { get; set; }

            /// <summary>
            /// Các chuỗi con (không phân biệt hoa/thường) từng xuất hiện trong cột
            /// LUUPHIEUGIAOHANG.NHAMAY / vWDinhDanhPhieuGiao.NHAMAY khi khách hàng này
            /// giao hàng — dùng để resolve ngược từ dữ liệu view về đúng CustomerConfig
            /// khi không có cột CustomerNo trực tiếp.
            /// </summary>
            public string[] NhaMayMatchPatterns { get; set; } = Array.Empty<string>();

            /// <summary>
            /// Cấu hình cơ chế giao hàng — chỉ có giá trị nếu khách hàng này
            /// thực sự giao hàng qua module GiaoHangKhach. Null nếu không.
            /// </summary>
            public GiaoHangKhachCustomerOptions Delivery { get; set; }
        }
    }

