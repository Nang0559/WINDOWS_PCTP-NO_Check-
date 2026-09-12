using PCTP.Modules.GiaoHangKhach.Configuration;   // ★ THÊM
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;

namespace PCTP.Shared.Models   // ★ SỬA — namespace đúng vị trí file
{
    public static class CustomerTableConfig
    {
        private static readonly Dictionary<string, CustomerConfig> _configs =
            new Dictionary<string, CustomerConfig>
            {
                ["100001"] = new CustomerConfig
                {
                    CustomerNo = "100001",
                    DisplayName = "HVN (100001)",
                    NhaMayMatchPatterns = new[] { "HON DA" },
                    Delivery = new GiaoHangKhachCustomerOptions
                    {
                        TmpTable = "TMPPHIEUGIAOHANG",
                        IfsTable = "IFSPHIEUGIAOHANG",
                        ViewTablePrefix = "TMPPHIEUGIAOHANGView",
                        LabelDocQR = "Đọc QRCode theo thứ tự: FCC → HVN",
                        DocQRTable = "DOCQRCODE",
                        // ── bảng riêng cho hàng SP ────────────────────────
                        TmpTableSP = "TMPPHIEUGIAOHANG_SP",
                        IfsTableSP = "IFSPHIEUGIAOHANG_SP",
                        DocQRTableSP = "DOCQRCODE_SP",
                        //--------------------------------------------------
                        CoNhieuNhaMay = true,          // ← có tab VP / HN
                        AddNmMacDinh = 1,
                        TenNhaMay = "",
                        LoadTheoNgay = false,          // không dùng
                        RequirePoRelNo = true,
                        NhaMayCase =
                            "CASE WHEN col.SHIP_ADDR_NO = 1 " +
                            "THEN 'HON DA -VIET NAM- (NHA MAY VINH PHUC)' " +
                            "ELSE 'HON DA -VIET NAM- (NHA MAY HA NAM)' END",
                        OrderTableGiaoDacBiet = "Purchase_Order_HVNDB",
                    },
                },

                ["100003"] = new CustomerConfig
                {
                    CustomerNo = "100003",
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
                        CoNhieuNhaMay = false,          // ← chỉ 1 nhà máy, ẩn tab
                        AddNmMacDinh = 1,               // SHIP_ADDR_NO cố định
                        TenNhaMay = "Honda Trading 100003",
                        LoadTheoNgay = true,
                        RequirePoRelNo = false,
                        NhaMayCase = "'NHA MAY 100003'", // không cần CASE, trả thẳng
                        OrderTable = "Purchase_Order_HTN",
                    },
                },

                ["100002"] = new CustomerConfig
                {
                    CustomerNo = "100002",
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
                        // ── SP riêng ──────────────────────────────────────
                        TmpTableSP = "SP_TMPPHIEUGIAOHANG",
                        DocQRTableSP = "SP_DOCQRCODE",

                        CoNhieuNhaMay = false,
                        AddNmMacDinh = 1,
                        TenNhaMay = "'YAMAHA - VIET NAM'",
                        LoadTheoNgay = false,       // có chọn giờ theo Purchase_Order_YMVN
                        RequirePoRelNo = false,

                        // ── Đặc thù 100002 ────────────────────────────────
                        CoGear = true,
                        CoLoaiSP = true,            // có phân biệt MP/SP
                        DockCodeSP = "VSP1",        // filter SP theo DOCK_CODE
                        CustomerNoIFS = "100002",

                        NhaMayCase = "'YAMAHA - VIET NAM'",
                        OrderTable = "Purchase_Order_YMVN",
                    },
                }
            };

        public static CustomerConfig Get(string customerNo) =>
            _configs.TryGetValue(customerNo, out var c) ? c
            : throw new KeyNotFoundException($"Chưa cấu hình customer: {customerNo}");

        public static IEnumerable<CustomerConfig> All => _configs.Values;

        public static CustomerConfig ResolveByNhaMay(string nhaMay)
        {
            if (string.IsNullOrWhiteSpace(nhaMay)) return null;

            foreach (var cfg in _configs.Values)
            {
                foreach (var pattern in cfg.NhaMayMatchPatterns)
                {
                    if (nhaMay.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        return cfg;
                }
            }
            return null;
        }
    }
}