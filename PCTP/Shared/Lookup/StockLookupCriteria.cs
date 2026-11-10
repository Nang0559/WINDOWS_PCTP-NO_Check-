using System;
using System.Collections.Generic;

namespace PCTP.Shared.Lookup
{
    /// <summary>
    /// Cột mốc thời gian dùng để khoanh vùng khi tra cứu hàng/LOT.
    /// Mỗi chế độ trả lời một câu hỏi khác nhau nên KHÔNG dùng chung một cột ngày.
    /// </summary>
    public enum StockLookupDateMode
    {
        /// <summary>Ngày sản xuất: 6 ký tự đầu của LOT (YYMMDD).</summary>
        NgaySanXuat = 0,

        /// <summary>Ngày xuất hàng: ngày giao khách (LUUPHIEUGIAOHANG.NGAYGIAO).</summary>
        NgayXuatHang = 1,

        /// <summary>Ngày lập phiếu xử lý hàng lỗi (phiếu trả / phiếu xử lý bất thường).</summary>
        NgayLapPhieuXuLy = 2
    }

    /// <summary>
    /// Ảnh chụp trạng thái ô tra cứu (<see cref="StockLookupControl"/>) tại thời điểm bấm Tra cứu.
    /// Form mẹ truyền nguyên đối tượng này cho form con (vd. FormChonSlotNoiBo) để form con nhận
    /// thẳng tiêu chí, không bắt người dùng nhập lại.
    /// </summary>
    public sealed class StockLookupCriteria
    {
        /// <summary>true = tab "Tìm theo mã"; false = tab "Tìm theo LOT / QR".</summary>
        public bool IsItemSearch { get; set; }

        /// <summary>Nội dung đang nhập (mã hàng / LOT / chuỗi QR).</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Mã hàng đã chọn trong popup đề xuất (chỉ có ở tab mã).</summary>
        public string ItemCode { get; set; } = string.Empty;

        public bool DateEnabled { get; set; }
        public StockLookupDateMode DateMode { get; set; } = StockLookupDateMode.NgaySanXuat;
        public DateTime From { get; set; } = DateTime.Today;
        public DateTime To { get; set; } = DateTime.Today;

        public bool HasText
        {
            get { return !string.IsNullOrWhiteSpace(Text); }
        }

        /// <summary>Không có chữ nhập và không bật lọc ngày.</summary>
        public bool IsEmpty
        {
            get { return !HasText && !DateEnabled; }
        }

        /// <summary>Mã hàng dùng để lọc ở tab mã: ưu tiên mã đã chọn, nếu không lấy chữ đã nhập.</summary>
        public string EffectiveItemCode
        {
            get
            {
                if (!IsItemSearch) return string.Empty;
                string code = string.IsNullOrWhiteSpace(ItemCode) ? Text : ItemCode;
                return (code ?? string.Empty).Trim();
            }
        }

        public static string DateModeLabel(StockLookupDateMode mode)
        {
            switch (mode)
            {
                case StockLookupDateMode.NgayXuatHang: return "Ngày xuất hàng";
                case StockLookupDateMode.NgayLapPhieuXuLy: return "Ngày lập phiếu xử lý hàng lỗi";
                default: return "Ngày sản xuất";
            }
        }

        /// <summary>Mô tả ngắn để hiện trên form con: "Mã X • Ngày sản xuất 01/10/2026 – 31/10/2026".</summary>
        public string Describe()
        {
            var parts = new List<string>();

            if (HasText)
            {
                string label = IsItemSearch ? "Mã " : "LOT/QR ";
                parts.Add(label + (IsItemSearch ? EffectiveItemCode : Text.Trim()));
            }

            if (DateEnabled)
            {
                parts.Add(string.Format("{0} {1:dd/MM/yyyy} – {2:dd/MM/yyyy}",
                    DateModeLabel(DateMode), From, To));
            }

            return parts.Count == 0 ? "Tất cả" : string.Join("  •  ", parts);
        }
    }
}
