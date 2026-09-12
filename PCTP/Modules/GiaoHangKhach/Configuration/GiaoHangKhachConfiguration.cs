// PCTP/Modules/GiaoHangKhach/Configuration/GiaoHangKhachCustomerOptions.cs
using PCTP.Shared.Enums;
using System.Collections.Generic;

namespace PCTP.Modules.GiaoHangKhach.Configuration
{
    /// <summary>
    /// Toàn bộ cấu hình cơ chế nạp/xử lý đơn hàng riêng của module GiaoHangKhach
    /// cho 1 khách hàng cụ thể. Tách khỏi CustomerConfig (định danh, dùng chéo module)
    /// vì các field này chỉ có ý nghĩa trong ngữ cảnh GiaoHangKhach.
    /// </summary>
    public class GiaoHangKhachCustomerOptions
    {
        public string TmpTable { get; set; }
        public string IfsTable { get; set; }
        public string DocQRTable { get; set; }

        // ── Config riêng cho hàng SP ─────────────────────────────────────
        public string TmpTableSP { get; set; } = "";
        public string IfsTableSP { get; set; } = "";
        public string DocQRTableSP { get; set; } = "";
        public bool CoConfigSP => !string.IsNullOrEmpty(DocQRTableSP);

        public string GetDocQRTable(bool isSP) => isSP && CoConfigSP ? DocQRTableSP : DocQRTable;
        public string GetTmpTable(bool isSP) => isSP && CoConfigSP ? TmpTableSP : TmpTable;
        public string GetIfsTable(bool isSP) => isSP && CoConfigSP ? IfsTableSP : IfsTable;

        public string NhaMayCase { get; set; }
        public string ViewTablePrefix { get; set; }

        public bool CoGear { get; set; } = false;
        public bool CoLoaiSP { get; set; } = false;
        public string DockCodeSP { get; set; } = "";
        public string CustomerNoIFS { get; set; } = "";

        public string LabelDocQR { get; set; } = "Đọc QRCode theo thứ tự: FCC → HVN";

        /// <summary>Tên bảng đơn hàng riêng (TableOrder) thay vì IFS Oracle. Rỗng = dùng IFS.</summary>
        public string OrderTable { get; set; } = "";
        public bool LoadTuBangRieng => !string.IsNullOrEmpty(OrderTable);

        public bool CoNhieuNhaMay { get; set; }
        public int AddNmMacDinh { get; set; }
        public string TenNhaMay { get; set; }

        public bool LoadTheoNgay { get; set; } = false;
        public bool RequirePoRelNo { get; set; } = true;
        public bool CoHoanThanhYMVN { get; set; }

        public string OrderTableGiaoDacBiet { get; set; }
        public bool CoGiaoDacBiet => !string.IsNullOrWhiteSpace(OrderTableGiaoDacBiet);

        public IfsOrderQueryMode QueryMode { get; set; }
        public string DockFilterExpr { get; set; }
        public List<int> DanhSachAddNm { get; set; } = new List<int> { 1 };

        public string GetIfsViewTable(bool isSP = false) => GetIfsTable(isSP) + "View";

        public string GetTmpViewTable(string hostName) =>
            $"{ViewTablePrefix}_{Sanitize(hostName)}";

        private static string Sanitize(string n) =>
            System.Text.RegularExpressions.Regex.Replace(n ?? "LOCAL", @"[^A-Za-z0-9_]", "_");
    }
}