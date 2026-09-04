using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Enums
{
    /// <summary>
    /// Khách hàng trả hàng — enum ánh xạ 1-1 với CustomerConfig.CustomerNo.
    /// KHÔNG tự nhân bản dữ liệu khách hàng (tên hiển thị, cấu hình bảng...) — mọi
    /// thông tin chi tiết vẫn lấy từ CustomerTableConfig, enum này chỉ đóng vai trò
    /// khoá type-safe để dùng trong code (so sánh, switch-case) thay vì string thô.
    /// </summary>
    public enum NguonKhachTra
    {
        HVN = 1,   // CustomerConfig.CustomerNo = "100001"
        YMVN = 2,  // CustomerConfig.CustomerNo = "100002"
        HTN = 3    // CustomerConfig.CustomerNo = "100003"
    }

    public static class NguonKhachTraExtensions
    {
        private static readonly IReadOnlyDictionary<NguonKhachTra, string> _customerNos =
            new Dictionary<NguonKhachTra, string>
            {
                [NguonKhachTra.HVN] = "100001",
                [NguonKhachTra.YMVN] = "100002",
                [NguonKhachTra.HTN] = "100003"
            };

        /// <summary>
        /// Lấy CustomerNo tương ứng với nguồn khách trả.
        /// </summary>
        public static string GetCustomerNo(this NguonKhachTra nguon)
        {
            if (!_customerNos.TryGetValue(nguon, out var customerNo))
                throw new ArgumentOutOfRangeException(
                    nameof(nguon),
                    nguon,
                    "Nguồn khách trả chưa được cấu hình.");

            return customerNo;
        }

        /// <summary>
        /// Lấy CustomerConfig tương ứng.
        /// CustomerTableConfig là nguồn sự thật về cấu hình khách hàng.
        /// </summary>
        public static CustomerConfig GetConfig(this NguonKhachTra nguon)
        {
            return CustomerTableConfig.Get(nguon.GetCustomerNo());
        }

        /// <summary>
        /// Chuyển CustomerNo về NguonKhachTra.
        /// Dùng khi đọc dữ liệu từ DB/module khác.
        /// </summary>
        public static bool TryFromCustomerNo(
            string customerNo,
            out NguonKhachTra nguon)
        {
            nguon = default;

            if (string.IsNullOrWhiteSpace(customerNo))
                return false;

            foreach (var item in _customerNos)
            {
                if (string.Equals(
                        item.Value,
                        customerNo.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    nguon = item.Key;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Chuyển CustomerNo về NguonKhachTra.
        /// Trả null nếu không nhận diện được.
        /// </summary>
        public static NguonKhachTra? FromCustomerNo(string customerNo)
        {
            return TryFromCustomerNo(customerNo, out var nguon)
                ? nguon
                : (NguonKhachTra?)null;
        }
    }
}
