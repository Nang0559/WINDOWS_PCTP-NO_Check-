using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class CustomerConfig
    {
        public string CustomerNo { get; set; }
        public string DisplayName { get; set; }
        public string TmpTable { get; set; } 
        public string IfsTable { get; set; } 
        public string DocQRTable { get; set; }
        // ── Config riêng cho hàng SP ─────────────────────────────────────
        public string TmpTableSP { get; set; } = "";  // rỗng = không có SP
        public string IfsTableSP { get; set; } = "";
        public string DocQRTableSP { get; set; } = "";
        // ── Helper: có config SP không ──────────────────────────────────
        public bool CoConfigSP =>
            !string.IsNullOrEmpty(DocQRTableSP);

        // ── Lấy table theo chế độ ───────────────────────────────────────
        public string GetDocQRTable(bool isSP) =>
            isSP && CoConfigSP ? DocQRTableSP : DocQRTable;
        public string GetTmpTable(bool isSP) =>
            isSP && CoConfigSP ? TmpTableSP : TmpTable;
        public string GetIfsTable(bool isSP) =>
            isSP && CoConfigSP ? IfsTableSP : IfsTable;
        public string NhaMayCase { get; set; }
        public string ViewTablePrefix { get; set; }
        // ── Thêm cho 100002 ───────────────────────────────
        public bool CoGear { get; set; } = false;
        public bool CoLoaiSP { get; set; } = false;
        public string DockCodeSP { get; set; } = "";
        public string CustomerNoIFS { get; set; } = "";
        // ── Thêm cho HTN (100003) và YMVN (100002) ───────────────────────
        /// <summary>
        /// Tên bảng đơn hàng riêng thay vì IFS Oracle.
        /// YMVN: "Purchase_Order_YMVN"
        /// HTN:  "Purchase_Order_HTN"
        /// Rỗng = dùng IFS Oracle như HVN.
        /// </summary>
        public string LabelDocQR { get; set; } = "Đọc QRCode theo thứ tự: FCC → HVN";
        public string OrderTable { get; set; } = "";

        /// <summary>True khi load từ OrderTable thay vì IFS Oracle.</summary>
        public bool LoadTuBangRieng => !string.IsNullOrEmpty(OrderTable);

        // ── UI config ────────────────────────────────────────────────
        public bool CoNhieuNhaMay { get; set; }  // true=hiện tab VP/HN, false=ẩn tab
        public int AddNmMacDinh { get; set; }  // 1=VP, 2=HN — dùng khi 1 nhà máy
        public string TenNhaMay { get; set; }  // tên cố định khi 1 nhà
        public bool LoadTheoNgay { get; set; } = false;
        public bool RequirePoRelNo { get; set; } = true;// public bool LoadTheoNgay { get; set; } = false; 
        /// <summary>
        /// CoHoanThanhYMVN = true → dùng SP Usp_Qrcode_Take_LotYMVN2405
        ///                          cho cả 100002 (YMVN) và 100003 (HTN)
        /// </summary>
        public bool CoHoanThanhYMVN { get; set; }

        // ── Computed ─────────────────────────────────────────────────
        public string GetIfsViewTable(bool isSP = false) =>
        GetIfsTable(isSP) + "View";
        public string GetTmpViewTable(string hostName) =>
            $"{ViewTablePrefix}_{Sanitize(hostName)}";

        private static string Sanitize(string n) =>
            System.Text.RegularExpressions.Regex.Replace(n ?? "LOCAL", @"[^A-Za-z0-9_]", "_");
    }
}
