using PCTP.Modules.GiaoHangKhach.Configuration;
using System;
using System.Collections.Generic;

namespace PCTP.Shared.Models
{
    /// <summary>
    /// Registry/resolve boundary cho CustomerConfig.
    /// Mọi entry-point của GiaoHangKhach phải đi qua class này để tránh
    /// customerNo rải rác và để bảo đảm customer thực sự có Delivery config.
    /// </summary>
    public static class CustomerTableConfig
    {
        // CustomerNo là business key dùng xuyên suốt Shell -> Delivery boundary.
        // Không để Main_APP / Form tự rải literal customer number.
        public const string HondaVietnam = "100001";
        public const string YamahaVietnam = "100002";
        public const string Customer100003 = "100003";

        private static readonly Dictionary<string, CustomerConfig> _configs =
            new Dictionary<string, CustomerConfig>
            {
                [HondaVietnam] = new CustomerConfig
                {
                    CustomerNo = HondaVietnam,
                    DisplayName = "HVN (100001)",
                    NhaMayMatchPatterns = new[] { "HON DA" },
                    Delivery = new GiaoHangKhachCustomerOptions
                    {
                        TmpTable = "TMPPHIEUGIAOHANG",
                        IfsTable = "IFSPHIEUGIAOHANG",
                        ViewTablePrefix = "TMPPHIEUGIAOHANGView",
                        LabelDocQR = "Đọc QRCode theo thứ tự: FCC → HVN",
                        DocQRTable = "DOCQRCODE",
                        TmpTableSP = "TMPPHIEUGIAOHANG_SP",
                        IfsTableSP = "IFSPHIEUGIAOHANG_SP",
                        DocQRTableSP = "DOCQRCODE_SP",
                        CoNhieuNhaMay = true,
                        AddNmMacDinh = 1,
                        TenNhaMay = "",
                        LoadTheoNgay = false,
                        CoLoaiSP = true,
                        DockCodeSP = "HVN",
                        RequirePoRelNo = true,
                        NhaMayCase =
                            "CASE WHEN col.SHIP_ADDR_NO = 1 " +
                            "THEN 'HON DA -VIET NAM- (NHA MAY VINH PHUC)' " +
                            "ELSE 'HON DA -VIET NAM- (NHA MAY HA NAM)' END",
                        OrderTableGiaoDacBiet = "Purchase_Order_HVNDB",
                    },
                },

                [Customer100003] = new CustomerConfig
                {
                    CustomerNo = Customer100003,
                    DisplayName = "Customer 100003",
                    NhaMayMatchPatterns = new[] { "100003", "HONDA TRADING" },
                    Delivery = new GiaoHangKhachCustomerOptions
                    {
                        CoGear = false,
                        CoHoanThanhYMVN = true,
                        TmpTable = "TMPPHIEUGIAOHANG_100003",
                        IfsTable = "IFSPHIEUGIAOHANG_100003",
                        LabelDocQR = "Đọc QRCode theo thứ tự: FCC → HTN",
                        ViewTablePrefix = "TMPPHIEUGIAOHANGView_100003",
                        DocQRTable = "DOCQRCODE_100003",
                        CoNhieuNhaMay = false,
                        AddNmMacDinh = 1,
                        TenNhaMay = "Honda Trading 100003",
                        LoadTheoNgay = true,
                        RequirePoRelNo = false,
                        NhaMayCase = "'NHA MAY 100003'",
                        OrderTable = "Purchase_Order_HTN",
                    },
                },

                [YamahaVietnam] = new CustomerConfig
                {
                    CustomerNo = YamahaVietnam,
                    DisplayName = "YMVN (100002)",
                    NhaMayMatchPatterns = new[] { "YAMAHA" },
                    Delivery = new GiaoHangKhachCustomerOptions
                    {
                        TmpTable = "TMPPHIEUGIAOHANG_100002",
                        IfsTable = "IFSPHIEUGIAOHANG_100002",
                        LabelDocQR = "Đọc QRCode theo thứ tự: FCC → YMVN",
                        ViewTablePrefix = "TMPPHIEUGIAOHANGView_100002",
                        DocQRTable = "YMVN_DOCQRCODE",
                        CoHoanThanhYMVN = true,
                        TmpTableSP = "SP_TMPPHIEUGIAOHANG",
                        DocQRTableSP = "SP_DOCQRCODE",
                        CoNhieuNhaMay = false,
                        AddNmMacDinh = 1,
                        TenNhaMay = "'YAMAHA - VIET NAM'",
                        LoadTheoNgay = false,
                        RequirePoRelNo = false,
                        CoGear = true,
                        CoLoaiSP = true,
                        DockCodeSP = "VSP1",
                        CustomerNoIFS = YamahaVietnam,
                        NhaMayCase = "'YAMAHA - VIET NAM'",
                        OrderTable = "Purchase_Order_YMVN",
                    },
                }
            };

        /// <summary>
        /// Chuẩn hoá customerNo ngay tại boundary của module.
        /// Không tự động fallback sang customer khác vì fallback có thể giao nhầm dữ liệu.
        /// </summary>
        public static string NormalizeCustomerNo(string customerNo)
        {
            if (string.IsNullOrWhiteSpace(customerNo))
                throw new ArgumentException("CustomerNo không được để trống.", nameof(customerNo));

            return customerNo.Trim();
        }

        public static CustomerConfig Get(string customerNo)
        {
            var normalized = NormalizeCustomerNo(customerNo);

            if (!_configs.TryGetValue(normalized, out var config))
                throw new KeyNotFoundException($"Chưa cấu hình customer: {normalized}");

            return config;
        }

        /// <summary>
        /// Entry-point dành riêng cho GiaoHangKhach.
        /// Chặn customer hợp lệ nhưng chưa có Delivery config.
        /// </summary>
        public static CustomerConfig GetForDelivery(string customerNo)
        {
            var config = Get(customerNo);

            if (config.Delivery == null)
                throw new InvalidOperationException(
                    $"Customer {config.CustomerNo} chưa được cấu hình Delivery cho GiaoHangKhach.");

            ValidateDeliveryConfig(config);
            return config;
        }

        private static void ValidateDeliveryConfig(CustomerConfig config)
        {
            var delivery = config.Delivery;

            if (string.IsNullOrWhiteSpace(delivery.TmpTable))
                throw new InvalidOperationException($"Customer {config.CustomerNo} thiếu Delivery.TmpTable.");

            if (string.IsNullOrWhiteSpace(delivery.IfsTable))
                throw new InvalidOperationException($"Customer {config.CustomerNo} thiếu Delivery.IfsTable.");

            if (string.IsNullOrWhiteSpace(delivery.DocQRTable))
                throw new InvalidOperationException($"Customer {config.CustomerNo} thiếu Delivery.DocQRTable.");

            if (string.IsNullOrWhiteSpace(delivery.ViewTablePrefix))
                throw new InvalidOperationException($"Customer {config.CustomerNo} thiếu Delivery.ViewTablePrefix.");

            if (delivery.AddNmMacDinh < 0)
                throw new InvalidOperationException($"Customer {config.CustomerNo} có AddNmMacDinh không hợp lệ.");

            if (string.IsNullOrWhiteSpace(config.CustomerNo))
                throw new InvalidOperationException("Delivery config không có CustomerNo.");
        }

        public static IEnumerable<CustomerConfig> All => _configs.Values;

        public static CustomerConfig ResolveByNhaMay(string nhaMay)
        {
            if (string.IsNullOrWhiteSpace(nhaMay)) return null;

            foreach (var config in _configs.Values)
            {
                foreach (var pattern in config.NhaMayMatchPatterns ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(pattern) &&
                        nhaMay.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        return config;
                }
            }

            return null;
        }
    }
}