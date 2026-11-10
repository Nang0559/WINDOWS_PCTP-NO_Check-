using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace PCTP.Common
{
    /// <summary>Chuẩn LOT của một chuỗi nhập vào.</summary>
    public enum LotKeyKind
    {
        /// <summary>Quá ngắn hoặc rỗng, không nhận diện được.</summary>
        Unknown = 0,
        /// <summary>Chuẩn cũ: mã gốc 13 ký tự (YYMMDD + Id_Item + Ca + Gear). Có thể ứng với NHIỀU LOT vật lý.</summary>
        Legacy13 = 1,
        /// <summary>Chuẩn mới: 20 ký tự (mã gốc 13 + Line + Machine) = khóa duy nhất của STOCKTP.</summary>
        Full20 = 2
    }

    /// <summary>
    /// Một phần của LOT ghép: "LOT-SL". Parse từng token độc lập, token lỗi không làm hỏng các token khác.
    /// </summary>
    public sealed class LotAllocation
    {
        public string RawToken { get; set; }
        public string LotNo { get; set; }
        public decimal Quantity { get; set; }
        public bool HasQuantity { get; set; }
        public string Error { get; set; }
        public bool IsValid { get { return string.IsNullOrEmpty(Error) && !string.IsNullOrWhiteSpace(LotNo); } }
    }

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
            if (string.IsNullOrWhiteSpace(fullLot)) return fullLot;

            // STOCKTP.LOT uses the 20-character base key (Date + Item + Shift +
            // Gear + Line + Machine). The UI and return-detail tables often pass
            // this key directly. Only strip Counter+Qty from a full printed LOT
            // that is longer than the base key; stripping every value longer than
            // 8 chars corrupts a valid 20-char key (e.g. 26080700748100851600)
            // into a 12-char value and makes stock searches return no rows.
            string lot = fullLot.Trim();
            if (lot.Length <= LEN_HEAD_FIXED) return lot;
            return lot.Substring(0, lot.Length - LEN_TAIL_FIXED);
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

        /// <summary>
        /// Ngày sản xuất = 6 ký tự đầu của LOT theo YYMMDD (cả LOT chuẩn mới và LOT cũ).
        /// Trả false nếu LOT quá ngắn hoặc 6 ký tự đầu không phải ngày hợp lệ.
        /// </summary>
        public static bool TryGetProductionDate(string lot, out DateTime date)
        {
            date = default(DateTime);
            if (string.IsNullOrWhiteSpace(lot)) return false;
            string head = GetDatePart(lot.Trim());
            if (head.Length != LEN_DATE) return false;
            return DateTime.TryParseExact(head, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        public static string GetIdItemPart(string lot)
            => lot != null && lot.Length >= LEN_DATE + LEN_ID_ITEM
                ? lot.Substring(LEN_DATE, LEN_ID_ITEM)
                : "";

        /// <summary>
        /// Ca sản xuất = ký tự thứ 12 của LOT (sau YYMMDD + Id_Item 5 ký tự). '0' = hành chính.
        /// Chỉ cần LOT đủ 12 ký tự (khoá cũ 13 ký tự vẫn đọc được), khác <see cref="GetShiftPart"/>
        /// vốn đòi đủ 20 ký tự đầu. Trả false nếu LOT quá ngắn hoặc ký tự đó không phải chữ số.
        /// </summary>
        public static bool TryGetShiftFromLot(string lot, out int shift)
        {
            shift = 0;
            if (string.IsNullOrWhiteSpace(lot)) return false;
            string t = lot.Trim();
            int idx = LEN_DATE + LEN_ID_ITEM;           // 11 (0-based) = ký tự thứ 12
            if (t.Length <= idx) return false;
            char c = t[idx];
            if (c < '0' || c > '9') return false;
            shift = c - '0';
            return true;
        }

        public static string GetShiftPart(string lot)
            => lot != null && lot.Length >= LEN_HEAD_FIXED
                ? lot.Substring(LEN_DATE + LEN_ID_ITEM, LEN_SHIFT)
                : "";

        public static string GetGearPart(string lot)
            => lot != null && lot.Length >= LEN_HEAD_FIXED
                ? lot.Substring(LEN_DATE + LEN_ID_ITEM + LEN_SHIFT, LEN_GEAR)
                : "";

        /// <summary>
        /// Gear = ký tự thứ 13 của LOT (index 12), dùng được cho cả khóa 13 ký tự lẫn LOT đầy đủ.
        /// Trả "" nếu LOT ngắn hơn 13 ký tự. Dùng để tách hàng đợi FIFO theo Gear.
        /// </summary>
        public static string GetGearFromKey(string lot)
        {
            if (string.IsNullOrWhiteSpace(lot)) return "";
            string t = lot.Trim();
            int idx = LEN_DATE + LEN_ID_ITEM + LEN_SHIFT;   // 12 (0-based) = ký tự thứ 13
            return t.Length > idx ? t.Substring(idx, LEN_GEAR) : "";
        }

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

        /// <summary>
        /// Phân loại chuẩn LOT, cùng quy ước với <see cref="AreLotKeysEquivalent"/>:
        /// từ 20 ký tự trở lên (lấy 20 ký tự đầu) = chuẩn mới; 13..19 ký tự = chuẩn cũ (so 13 ký tự đầu);
        /// ngắn hơn 13 = không nhận diện.
        /// </summary>
        public static LotKeyKind ClassifyLotKey(string lot)
        {
            if (string.IsNullOrWhiteSpace(lot)) return LotKeyKind.Unknown;
            string key = TrimTo(lot.Trim(), LEN_HEAD_FIXED);
            if (key.Length >= LEN_HEAD_FIXED) return LotKeyKind.Full20;
            if (key.Length >= LEN_LEGACY_KEY) return LotKeyKind.Legacy13;
            return LotKeyKind.Unknown;
        }

        /// <summary>Khóa 20 ký tự (chuẩn mới). Trả về rỗng nếu chuỗi ngắn hơn 20.</summary>
        public static string GetFullKey(string lot)
        {
            if (string.IsNullOrWhiteSpace(lot)) return string.Empty;
            string t = lot.Trim();
            return t.Length >= LEN_HEAD_FIXED ? t.Substring(0, LEN_HEAD_FIXED) : string.Empty;
        }

        /// <summary>Khóa 13 ký tự (chuẩn cũ). Trả về rỗng nếu chuỗi ngắn hơn 13.</summary>
        public static string GetLegacyKey(string lot)
        {
            if (string.IsNullOrWhiteSpace(lot)) return string.Empty;
            string t = lot.Trim();
            return t.Length >= LEN_LEGACY_KEY ? t.Substring(0, LEN_LEGACY_KEY) : string.Empty;
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

        /// <summary>
        /// Parse LOT ghép "LOT1-1,LOT2-2,..." theo từng token (tolerant): mỗi LOT trả về 1 <see cref="LotAllocation"/>.
        /// - Phân tách bằng ',' ';' '|' hoặc xuống dòng.
        /// - Số lượng = phần sau dấu '-' CUỐI CÙNG nếu là số dương; nếu không thì cả token là LOT (không có SL).
        /// - Nếu chỉ có 1 LOT và không có SL thì dùng <paramref name="fallbackQuantity"/>.
        /// - Token có LOT quá ngắn (&lt; 13 ký tự) được đánh dấu Error nhưng không chặn các token còn lại.
        /// </summary>
        public static List<LotAllocation> ParseCompositeLotTolerant(string rawLot, decimal? fallbackQuantity)
        {
            var result = new List<LotAllocation>();
            if (string.IsNullOrWhiteSpace(rawLot)) return result;

            string[] tokens = rawLot.Split(new[] { ',', ';', '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string t in tokens)
            {
                string token = t.Trim();
                if (token.Length == 0) continue;

                var item = new LotAllocation { RawToken = token, LotNo = token };
                int sep = token.LastIndexOf('-');
                if (sep > 0 && sep < token.Length - 1)
                {
                    string qtyText = token.Substring(sep + 1).Trim();
                    decimal qty;
                    if ((decimal.TryParse(qtyText, NumberStyles.Number, CultureInfo.InvariantCulture, out qty) ||
                         decimal.TryParse(qtyText, NumberStyles.Number, CultureInfo.CurrentCulture, out qty)) && qty > 0)
                    {
                        item.LotNo = token.Substring(0, sep).Trim();
                        item.Quantity = qty;
                        item.HasQuantity = true;
                    }
                }

                if (ClassifyLotKey(item.LotNo) == LotKeyKind.Unknown)
                    item.Error = "LOT '" + item.LotNo + "' không nhận diện được (ngắn hơn " + LEN_LEGACY_KEY + " ký tự).";
                result.Add(item);
            }

            if (result.Count == 1 && !result[0].HasQuantity && fallbackQuantity.HasValue && fallbackQuantity.Value > 0)
            {
                result[0].Quantity = fallbackQuantity.Value;
                result[0].HasQuantity = true;
            }
            return result;
        }

        /// <summary>
        /// Khóa dùng để tìm LOT trong SQL/so khớp: 20 ký tự đầu (chuẩn mới) hoặc 13 ký tự đầu (chuẩn cũ).
        /// LOT in đầy đủ (kèm Counter+Qty) cũng quy về khóa 20 ký tự.
        /// </summary>
        public static string GetSearchKey(string lot)
        {
            if (string.IsNullOrWhiteSpace(lot)) return string.Empty;
            string t = lot.Trim();
            switch (ClassifyLotKey(t))
            {
                case LotKeyKind.Full20: return t.Substring(0, LEN_HEAD_FIXED);
                case LotKeyKind.Legacy13: return t.Substring(0, LEN_LEGACY_KEY);
                default: return t;
            }
        }
    }
}
