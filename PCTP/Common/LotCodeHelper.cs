using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Common
{
    /// <summary>
    /// Nguồn xử lý DUY NHẤT cho cấu trúc chuỗi LOTNO, khớp 1-1 với SP SQL build LOT:
    ///
    ///   YY(2) + MM(2) + DD(2)                       -- LEN_DATE = 6
    /// + Id_Item padded 5 ký tự (STUFF '00000',...)   -- LEN_ID_ITEM = 5
    /// + ShiftCode (1 ký tự, mặc định '9' nếu null)   -- LEN_SHIFT = 1
    /// + GearCode (1 ký tự, mặc định '0' nếu null)    -- LEN_GEAR = 1
    /// + LinesCode (độ dài BIẾN ĐỘNG theo cấu hình)   -- LinesLen = 3
    /// + MachinesCode (độ dài BIẾN ĐỘNG theo cấu hình)-- MachinesLen = 4
    /// + TemCounter padded 4 ký tự (STUFF '0000',...) -- LEN_COUNTER = 4
    /// + QuantityTem padded 4 ký tự (STUFF '0000',...)-- LEN_QTY = 4
    ///
    /// LinesCode/MachinesCode không có độ dài cố định toàn hệ thống (phụ thuộc dữ liệu
    /// B20Lines/B30AccDoc thực tế), nên các hàm cần phân biệt phần "đầu cố định" (Date+Id+Shift+Gear,
    /// luôn 9 ký tự) và "đuôi cố định" (Counter+Qty, luôn 8 ký tự) — phần giữa (Line+Machine)
    /// được suy ra bằng phép trừ độ dài, KHÔNG hardcode.
    /// </summary>
    public static class LotCodeHelper
    {
        // ── Độ dài từng phần cố định — SỬA DUY NHẤT Ở ĐÂY nếu công thức SQL đổi ──
        public const int LEN_DATE = 6;      // YYMMDD
        public const int LEN_ID_ITEM = 5;      // Id hàng đã pad '00000'
        public const int LEN_SHIFT = 1;      // Ca sản xuất
        public const int LEN_GEAR = 1;      // Mã Gear
        public const int LEN_LinesCode = 3;      // Mã Line
        public const int LEN_MachinesCode = 4;      // Mã Bộ phận
        public const int LEN_COUNTER = 4;      // STT tem trong lô (TemCounter)
        public const int LEN_QTY = 4;      // Số lượng trên tem (QuantityTem)

        /// <summary>Date+Id+Shift+Gear — phần đầu luôn cố định 9 ký tự.</summary>
        public const int LEN_HEAD_FIXED = LEN_DATE + LEN_ID_ITEM + LEN_SHIFT + LEN_GEAR + LEN_LinesCode + LEN_MachinesCode; // = 20

        /// <summary>Counter+Qty — phần đuôi luôn cố định 8 ký tự.</summary>
        public const int LEN_TAIL_FIXED = LEN_COUNTER + LEN_QTY; // = 8

        /// <summary>Độ dài tối thiểu hợp lệ của 1 chuỗi LOT đầy đủ (không có Line/Machine).</summary>
        public const int MIN_TOTAL_LEN = LEN_HEAD_FIXED + LEN_TAIL_FIXED; // = 20
        /// LEN_HEAD_FIXED (13 ký tự đầu của 20 ký tự mới) — không phải một chuẩn khác.
        
        public const int LEN_LEGACY_KEY = LEN_DATE + LEN_ID_ITEM + LEN_SHIFT + LEN_GEAR; // = 13

        // ══════════════════════════════════════════════════════════════════════
        // 1. TÁCH THEO ĐUÔI — dùng cho NHẬP KHO / LƯU KHO (bỏ Counter+Qty)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Cắt bỏ 8 ký tự cuối (TemCounter + QuantityTem) khỏi LOT đầy đủ.
        /// Input:  "26080800858100771700" + "0003" + "5000"  (= chuỗi đầy đủ 28 ký tự)
        /// Output: "26080800858100771700" (LOT gốc, dùng để so khớp / lưu kho)
        /// </summary>
        public static string StripCounterAndQty(string fullLot)
        {
            if (string.IsNullOrEmpty(fullLot)) return fullLot;
            if (fullLot.Length <= LEN_TAIL_FIXED) return fullLot; // quá ngắn, không cắt được -> trả nguyên
            return fullLot.Substring(0, fullLot.Length - LEN_TAIL_FIXED);
        }

        /// <summary>Lấy riêng phần TemCounter (4 ký tự) từ cuối LOT, nếu có.</summary>
        public static string GetCounterPart(string fullLot)
        {
            if (string.IsNullOrEmpty(fullLot) || fullLot.Length < LEN_TAIL_FIXED) return "";
            return fullLot.Substring(fullLot.Length - LEN_TAIL_FIXED, LEN_COUNTER);
        }

        /// <summary>Lấy riêng phần QuantityTem (4 ký tự cuối) từ LOT, nếu có.</summary>
        public static string GetQuantityPart(string fullLot)
        {
            if (string.IsNullOrEmpty(fullLot) || fullLot.Length < LEN_QTY) return "";
            return fullLot.Substring(fullLot.Length - LEN_QTY, LEN_QTY);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 2. TÁCH THEO ĐẦU — Date / Id item / Shift / Gear (luôn cố định 9 ký tự đầu)
        // ══════════════════════════════════════════════════════════════════════

        public static string GetDatePart(string lot)
            => lot != null && lot.Length >= LEN_DATE ? lot.Substring(0, LEN_DATE) : "";

        public static string GetIdItemPart(string lot)
            => lot != null && lot.Length >= LEN_DATE + LEN_ID_ITEM
                ? lot.Substring(LEN_DATE, LEN_ID_ITEM)
                : "";

        public static string GetShiftPart(string lot)
            => lot != null && lot.Length >= LEN_HEAD_FIXED
                ? lot.Substring(LEN_DATE + LEN_ID_ITEM, LEN_SHIFT)
                : "";

        public static string GetGearPart(string lot)
            => lot != null && lot.Length >= LEN_HEAD_FIXED
                ? lot.Substring(LEN_DATE + LEN_ID_ITEM + LEN_SHIFT, LEN_GEAR)
                : "";
        /// <summary>Lấy riêng phần LineCode (3 ký tự), bắt đầu sau phần đầu cố định 13 ký tự.</summary>
        public static string GetLineCodePart(string lot)
        {
            int startIdx = LEN_DATE + LEN_ID_ITEM + LEN_SHIFT + LEN_GEAR; // = 13
            return lot != null && lot.Length >= startIdx + LEN_LinesCode
                ? lot.Substring(startIdx, LEN_LinesCode)
                : "";
        }

        /// <summary>Lấy riêng phần MachinesCode (4 ký tự), bắt đầu sau LineCode.</summary>
        public static string GetMachineCodePart(string lot)
        {
            int startIdx = LEN_DATE + LEN_ID_ITEM + LEN_SHIFT + LEN_GEAR + LEN_LinesCode; // = 13 + 3 = 16
            return lot != null && lot.Length >= startIdx + LEN_MachinesCode
                ? lot.Substring(startIdx, LEN_MachinesCode)
                : "";
        }
        /// <summary>
        /// Phần giữa Line+Machine — suy ra bằng phép trừ (đầu cố định 9, đuôi cố định 8,
        /// phần còn lại ở giữa chính là Line+Machine gộp lại, độ dài biến động theo dữ liệu thực tế).
        /// Trả về "" nếu LOT không đủ dài để có phần giữa.
        /// </summary>
        public static string GetLineMachinePart(string lot)
        {
            if (string.IsNullOrEmpty(lot) || lot.Length <= LEN_HEAD_FIXED)
                return "";
            int end = lot.Length; // lot truyền vào đây PHẢI là LOT đã StripCounterAndQty (không có đuôi 8 ký tự)
            if (end <= LEN_HEAD_FIXED) return "";
            return lot.Substring(LEN_HEAD_FIXED, end - LEN_HEAD_FIXED);
        }

        /// <summary>
        /// Build id item đã pad 5 ký tự '0' — tương đương STUFF('00000', 5-LEN(id)+1, LEN(id), id).
        /// </summary>
        public static string PadIdItem(string rawId)
        {
            if (string.IsNullOrEmpty(rawId)) return "00000";
            if (!int.TryParse(rawId, out int idNum)) return rawId.PadLeft(LEN_ID_ITEM, '0');
            return idNum.ToString().PadLeft(LEN_ID_ITEM, '0');
        }

        // ══════════════════════════════════════════════════════════════════════
        // 3. DÒ TÌM PHIẾU SẢN XUẤT (vNhapTP) — thay LotNoHelper.BuildFindList
        //    và khối Substring thủ công trong NHAP_TP.cs (LOT1/LOTDD2/LOTDD3/LOTDD4/LOTCH/LOTCH2)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sinh danh sách các chuỗi FIND ứng viên để dò trong vNhapTP.FIND, khi độ dài
        /// thật của LinesCode/MachinesCode trên QR không biết trước (dữ liệu cũ có nhiều
        /// biến thể ghép). Logic port nguyên từ LotNoHelper.BuildFindList / NHAP_TP.cs cũ,
        /// chỉ gom về một chỗ và đặt tên rõ ràng theo từng "dạng" LOT.
        /// </summary>
        /// <param name="rawLotSl">Chuỗi LOT thô đọc từ QR (chưa cắt Counter/Qty).</param>
        /// <param name="idItemPadded">Id mã hàng ĐÃ PAD 5 ký tự (dùng PadIdItem để tạo).</param>
        public static List<string> BuildCandidateFinds(string rawLotSl, string idItemPadded)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(rawLotSl) || string.IsNullOrEmpty(idItemPadded))
                return result;

            if (!int.TryParse(idItemPadded, out int intId))
                return result;

            // prefixLen = độ dài phần Date+IdItem đã pad (6 + 5 = 11) tính trên LOT GỐC
            // (không phải trên id chưa pad) — đây là điểm mà bản gốc NHAP_TP.cs hay bị lệch.
            int prefixLen = LEN_DATE + idItemPadded.Length;
            if (rawLotSl.Length <= prefixLen) return result;

            // Ca sản xuất nằm ngay sau prefix đã pad
            string ca = rawLotSl.Substring(prefixLen, 1);

            // ── Dạng 1: Date + Id(không pad) + Ca ────────────────────────────────
            string lot = rawLotSl.Substring(0, LEN_DATE) + intId + ca;
            result.Add(lot);

            // ── Dạng 2: + BP lấy từ 8 ký tự cuối chuỗi gốc + Gear ở prefixLen+1 ──
            if (rawLotSl.Length >= 8)
            {
                string bp = rawLotSl.Substring(rawLotSl.Length - 8, 4);
                string gear = rawLotSl.Length > prefixLen + 1
                    ? rawLotSl.Substring(prefixLen + 1, 1) : "";
                result.Add(rawLotSl.Substring(0, LEN_DATE) + intId + ca + bp + gear);
            }

            // ── Dạng 3: BP2 ở vị trí prefixLen+2 (dài 4), Gear2 ở prefixLen+1 ────
            if (rawLotSl.Length >= prefixLen + 6)
            {
                string bp2 = rawLotSl.Substring(prefixLen + 2, 4);
                string gear2 = rawLotSl.Substring(prefixLen + 1, 1);
                result.Add(rawLotSl.Substring(0, LEN_DATE) + intId + ca + bp2 + gear2);
            }

            // ── Dạng 4 (LOTCH): 20 ký tự đầu — khớp trực tiếp vNhapTP.FIND khi
            //    LY_DO_TRA rỗng (trường hợp phổ biến nhất) ─────────────────────────
            if (rawLotSl.Length >= 20)
                result.Add(rawLotSl.Substring(0, 20));

            return result.Distinct().ToList();
        }

        /// <summary>
        /// Fallback match theo prefix "đáng tin cậy" khi BuildCandidateFinds không ra kết quả:
        /// prefix 11 ký tự (Date 6 + IdItem pad 5) LUÔN đúng vì được build cố định.
        /// Ký tự thứ 12 (nếu có) là Ca sản xuất.
        /// </summary>
        public static string GetReliablePrefix(string rawLotSl, out string ca)
        {
            ca = "";
            if (string.IsNullOrEmpty(rawLotSl) || rawLotSl.Length < 11) return "";

            string prefix11 = rawLotSl.Substring(0, 11);
            ca = rawLotSl.Length > 11 ? rawLotSl.Substring(11, 1) : "";
            return prefix11;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 4. NORMALIZE THEO CUSTOMER — thay DocQRService.NormalizeLotFCC* (giao hàng)
        //    Mỗi khách hàng có thể cắt độ dài khác nhau tuỳ cấu hình LinesCode/MachinesCode
        //    thực tế của họ -> nhận trimLength làm tham số thay vì hardcode 13/19/20/26...
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Cắt LOT về đúng <paramref name="trimLength"/> ký tự đầu — dùng chung cho
        /// mọi customer thay vì viết riêng NormalizeLotFCC_HTN / _YMVN / thường.
        /// Nếu chuỗi ngắn hơn trimLength thì trả nguyên (không throw).
        /// </summary>
        public static string TrimTo(string lot, int trimLength)
        {
            if (string.IsNullOrEmpty(lot)) return lot;
            return lot.Length > trimLength ? lot.Substring(0, trimLength) : lot;
        }

        /// <summary>
        /// Cắt bỏ N ký tự cuối — dùng cho các customer định nghĩa theo "bỏ đuôi"
        /// thay vì "giữ đầu" (ví dụ HTN cũ: bỏ 8 ký tự cuối).
        /// </summary>
        public static string TrimTail(string lot, int tailLength)
        {
            if (string.IsNullOrEmpty(lot) || lot.Length <= tailLength) return lot;
            return lot.Substring(0, lot.Length - tailLength);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 5. NORMALIZE HIỂN THỊ SLOT (khác mục đích — giữ riêng, không gộp với trên)
        //    Đây là bản port của LotNoHelper.NormalizeLot cũ: giữ 19 ký tự đầu +
        //    4 ký tự cuối, cho LOT hiển thị trong kho Slot/SlotLot. KHÔNG liên quan
        //    đến cấu trúc Date/Id/Shift/Gear/Line/Machine/Counter/Qty ở trên.
        /// ══════════════════════════════════════════════════════════════════════
        public static string NormalizeLotForSlotDisplay(string rawLotNo)
        {
            if (string.IsNullOrWhiteSpace(rawLotNo)) return rawLotNo;
            if (rawLotNo.Length < 26) return rawLotNo;

            return rawLotNo.Substring(0, 19) + rawLotNo.Substring(rawLotNo.Length - 4);


        }
        // ══════════════════════════════════════════════════════════════════════
        // 6. SO KHỚP TƯƠNG THÍCH NGƯỢC — dùng khi 1 bên là LOT cũ (13 ký tự),
        //    1 bên là LOT mới (20 ký tự). ĐÂY LÀ ĐIỂM DUY NHẤT xử lý khác biệt
        //    độ dài dữ liệu cũ/mới — mọi nơi so khớp LOT (STOCKTP, SlotLot,
        //    TMPCHOGIAO, LUUPHIEUGIAOHANG...) PHẢI gọi qua đây thay vì tự so
        //    sánh trực tiếp bằng == hoặc SQL "=".
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// So khớp 2 khoá LOT, tương thích cả khoá cũ (13 ký tự, thiếu Line/Machine)
        /// lẫn khoá mới (20 ký tự, đủ Line/Machine).
        ///
        /// Quy tắc:
        /// - Nếu CẢ HAI đủ 20 ký tự -> so khớp chính xác 20 ký tự (giữ độ chính xác cao
        ///   nhất khi có đủ dữ liệu — tránh gộp nhầm 2 LOT khác Line/Machine).
        /// - Nếu MỘT trong hai bên chỉ có 13 ký tự (dữ liệu lịch sử) -> hạ xuống so khớp
        ///   13 ký tự đầu (LEN_LEGACY_KEY), vì dữ liệu cũ không còn cách nào biết
        ///   Line/Machine để so chính xác hơn.
        /// - Không dùng OR giữa 2 điều kiện — nếu OR, hai LOT khác Line/Machine nhưng
        ///   trùng 13 ký tự đầu sẽ bị match nhầm dù cả hai đều có đủ 20 ký tự.
        /// </summary>
        public static bool AreLotKeysEquivalent(string lot1, string lot2)
        {
            string k1 = TrimTo(lot1 ?? "", LEN_HEAD_FIXED);
            string k2 = TrimTo(lot2 ?? "", LEN_HEAD_FIXED);

            if (k1.Length < LEN_LEGACY_KEY || k2.Length < LEN_LEGACY_KEY)
                return false; // quá ngắn, không đủ tin cậy để kết luận khớp

            if (k1.Length == LEN_HEAD_FIXED && k2.Length == LEN_HEAD_FIXED)
                return string.Equals(k1, k2, StringComparison.OrdinalIgnoreCase);

            return string.Equals(
                k1.Substring(0, LEN_LEGACY_KEY),
                k2.Substring(0, LEN_LEGACY_KEY),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Sinh fragment SQL tương đương AreLotKeysEquivalent, dùng khi build câu lệnh
        /// SQL động (không thể gọi hàm C# trong SQL Server không có CLR function).
        /// columnExpr: biểu thức cột LOT phía DB (vd "LOT", "s.LOT").
        /// literalOrParam: giá trị so sánh — literal đã escape (vd "'26080800858...'")
        /// hoặc tên tham số (vd "@lot").
        /// </summary>
        public static string BuildLotMatchSql(string columnExpr, string literalOrParam)
        {
            return
                $"(" +
                $"  (LEN({columnExpr}) >= {LEN_HEAD_FIXED} AND LEN({literalOrParam}) >= {LEN_HEAD_FIXED} " +
                $"   AND SUBSTRING({columnExpr},1,{LEN_HEAD_FIXED}) = SUBSTRING({literalOrParam},1,{LEN_HEAD_FIXED})) " +
                $"  OR " +
                $"  ((LEN({columnExpr}) < {LEN_HEAD_FIXED} OR LEN({literalOrParam}) < {LEN_HEAD_FIXED}) " +
                $"   AND SUBSTRING({columnExpr},1,{LEN_LEGACY_KEY}) = SUBSTRING({literalOrParam},1,{LEN_LEGACY_KEY})) " +
                $")";
        }
    }
}
