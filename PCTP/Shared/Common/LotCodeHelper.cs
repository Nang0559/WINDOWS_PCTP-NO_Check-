using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

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
        public const int LEN_DATE = 6;
        public const int LEN_ID_ITEM = 5;
        public const int LEN_SHIFT = 1;
        public const int LEN_GEAR = 1;
        public const int LEN_LinesCode = 3;
        public const int LEN_MachinesCode = 4;
        public const int LEN_COUNTER = 4;
        public const int LEN_QTY = 4;
        public const int LEN_HEAD_FIXED = LEN_DATE + LEN_ID_ITEM + LEN_SHIFT + LEN_GEAR + LEN_LinesCode + LEN_MachinesCode;
        public const int LEN_TAIL_FIXED = LEN_COUNTER + LEN_QTY;
        public const int MIN_TOTAL_LEN = LEN_HEAD_FIXED + LEN_TAIL_FIXED;
        public const int LEN_LEGACY_KEY = LEN_DATE + LEN_ID_ITEM + LEN_SHIFT + LEN_GEAR;

        public static string StripCounterAndQty(string fullLot)
        {
            if (string.IsNullOrEmpty(fullLot)) return fullLot;
            if (fullLot.Length <= LEN_TAIL_FIXED) return fullLot;
            return fullLot.Substring(0, fullLot.Length - LEN_TAIL_FIXED);
        }

        public static string GetCounterPart(string fullLot)
        {
            if (string.IsNullOrEmpty(fullLot) || fullLot.Length < LEN_TAIL_FIXED) return "";
            return fullLot.Substring(fullLot.Length - LEN_TAIL_FIXED, LEN_COUNTER);
        }

        public static string GetQuantityPart(string fullLot)
        {
            if (string.IsNullOrEmpty(fullLot) || fullLot.Length < LEN_QTY) return "";
            return fullLot.Substring(fullLot.Length - LEN_QTY, LEN_QTY);
        }

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

        public static string GetLineCodePart(string lot)
        {
            int startIdx = LEN_DATE + LEN_ID_ITEM + LEN_SHIFT + LEN_GEAR;
            return lot != null && lot.Length >= startIdx + LEN_LinesCode
                ? lot.Substring(startIdx, LEN_LinesCode)
                : "";
        }

        public static string GetMachineCodePart(string lot)
        {
            int startIdx = LEN_DATE + LEN_ID_ITEM + LEN_SHIFT + LEN_GEAR + LEN_LinesCode;
            return lot != null && lot.Length >= startIdx + LEN_MachinesCode
                ? lot.Substring(startIdx, LEN_MachinesCode)
                : "";
        }

        public static string GetLineMachinePart(string lot)
        {
            if (string.IsNullOrEmpty(lot) || lot.Length <= LEN_HEAD_FIXED)
                return "";
            int end = lot.Length;
            if (end <= LEN_HEAD_FIXED) return "";
            return lot.Substring(LEN_HEAD_FIXED, end - LEN_HEAD_FIXED);
        }

        public static string PadIdItem(string rawId)
        {
            if (string.IsNullOrEmpty(rawId)) return "00000";
            if (!int.TryParse(rawId, out int idNum)) return rawId.PadLeft(LEN_ID_ITEM, '0');
            return idNum.ToString().PadLeft(LEN_ID_ITEM, '0');
        }

        public static List<string> BuildCandidateFinds(string rawLotSl, string idItemPadded)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(rawLotSl) || string.IsNullOrEmpty(idItemPadded))
                return result;
            if (!int.TryParse(idItemPadded, out int intId))
                return result;
            int prefixLen = LEN_DATE + idItemPadded.Length;
            if (rawLotSl.Length <= prefixLen) return result;
            string ca = rawLotSl.Substring(prefixLen, 1);
            result.Add(rawLotSl.Substring(0, LEN_DATE) + intId + ca);
            if (rawLotSl.Length >= 8)
            {
                string bp = rawLotSl.Substring(rawLotSl.Length - 8, 4);
                string gear = rawLotSl.Length > prefixLen + 1
                    ? rawLotSl.Substring(prefixLen + 1, 1) : "";
                result.Add(rawLotSl.Substring(0, LEN_DATE) + intId + ca + bp + gear);
            }
            if (rawLotSl.Length >= prefixLen + 6)
            {
                string bp2 = rawLotSl.Substring(prefixLen + 2, 4);
                string gear2 = rawLotSl.Substring(prefixLen + 1, 1);
                result.Add(rawLotSl.Substring(0, LEN_DATE) + intId + ca + bp2 + gear2);
            }
            if (rawLotSl.Length >= 20)
                result.Add(rawLotSl.Substring(0, 20));
            return result.Distinct().ToList();
        }

        public static string GetReliablePrefix(string rawLotSl, out string ca)
        {
            ca = "";
            if (string.IsNullOrEmpty(rawLotSl) || rawLotSl.Length < 11) return "";
            string prefix11 = rawLotSl.Substring(0, 11);
            ca = rawLotSl.Length > 11 ? rawLotSl.Substring(11, 1) : "";
            return prefix11;
        }

        public static string TrimTo(string lot, int trimLength)
        {
            if (string.IsNullOrEmpty(lot)) return lot;
            return lot.Length > trimLength ? lot.Substring(0, trimLength) : lot;
        }

        public static string TrimTail(string lot, int tailLength)
        {
            if (string.IsNullOrEmpty(lot) || lot.Length <= tailLength) return lot;
            return lot.Substring(0, lot.Length - tailLength);
        }

        public static string NormalizeLotForSlotDisplay(string rawLotNo)
        {
            if (string.IsNullOrWhiteSpace(rawLotNo)) return rawLotNo;
            if (rawLotNo.Length < 26) return rawLotNo;
            return rawLotNo.Substring(0, 19) + rawLotNo.Substring(rawLotNo.Length - 4);
        }

        public static bool AreLotKeysEquivalent(string lot1, string lot2)
        {
            string k1 = TrimTo(lot1 ?? "", LEN_HEAD_FIXED);
            string k2 = TrimTo(lot2 ?? "", LEN_HEAD_FIXED);
            if (k1.Length < LEN_LEGACY_KEY || k2.Length < LEN_LEGACY_KEY)
                return false;
            if (k1.Length == LEN_HEAD_FIXED && k2.Length == LEN_HEAD_FIXED)
                return string.Equals(k1, k2, StringComparison.OrdinalIgnoreCase);
            return string.Equals(
                k1.Substring(0, LEN_LEGACY_KEY),
                k2.Substring(0, LEN_LEGACY_KEY),
                StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildLotMatchSql(string columnExpr, string literalOrParam)
        {
            return
                "(" +
                "  (LEN(" + columnExpr + ") >= " + LEN_HEAD_FIXED + " AND LEN(" + literalOrParam + ") >= " + LEN_HEAD_FIXED +
                "   AND SUBSTRING(" + columnExpr + ",1," + LEN_HEAD_FIXED + ") = SUBSTRING(" + literalOrParam + ",1," + LEN_HEAD_FIXED + ")) " +
                "  OR " +
                "  ((LEN(" + columnExpr + ") < " + LEN_HEAD_FIXED + " OR LEN(" + literalOrParam + ") < " + LEN_HEAD_FIXED + ") " +
                "   AND SUBSTRING(" + columnExpr + ",1," + LEN_LEGACY_KEY + ") = SUBSTRING(" + literalOrParam + ",1," + LEN_LEGACY_KEY + ")) " +
                ")";
        }

        public static string BuildLegacyShortLot(string rawLot, string idPadded)
        {
            if (string.IsNullOrEmpty(rawLot) || string.IsNullOrEmpty(idPadded))
                return rawLot;
            return rawLot.Length > LEN_LEGACY_KEY
                ? rawLot.Substring(0, LEN_DATE) + idPadded + rawLot.Substring(LEN_LEGACY_KEY, 1)
                : rawLot;
        }

        /// <summary>
        /// Tách LOT ghép dạng "LOT1-100,LOT2-80" thành các cặp LOT/số lượng.
        /// Dùng dấu '-' cuối cùng làm dấu phân cách để LOT có thể tự chứa '-'.
        /// Đây là logic dùng chung; BaoCao không được tự tạo parser riêng.
        /// </summary>
        public static List<KeyValuePair<string, decimal>> ParseCompositeLot(string rawLot)
        {
            var result = new List<KeyValuePair<string, decimal>>();
            if (string.IsNullOrWhiteSpace(rawLot))
                return result;

            var tokens = rawLot
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();

            foreach (var token in tokens)
            {
                int separator = token.LastIndexOf('-');
                if (separator <= 0 || separator == token.Length - 1)
                    throw new FormatException(
                        "LOT ghép không hợp lệ: '" + token + "'. Định dạng yêu cầu LOT-Quantity, các LOT được ngăn bởi dấu phẩy.");

                string lot = token.Substring(0, separator).Trim();
                string quantityText = token.Substring(separator + 1).Trim();
                if (lot.Length == 0)
                    throw new FormatException("LOT ghép bị rỗng trong token: '" + token + "'.");

                decimal quantity;
                if (!decimal.TryParse(quantityText, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity) || quantity <= 0)
                {
                    if (!decimal.TryParse(quantityText, NumberStyles.Number, CultureInfo.CurrentCulture, out quantity) || quantity <= 0)
                        throw new FormatException("Số lượng LOT không hợp lệ trong token: '" + token + "'.");
                }

                result.Add(new KeyValuePair<string, decimal>(lot, quantity));
            }

            return result;
        }
    }
}
