using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// YMVN business validation: Gear is derived from character 13 of the FCC LOT
    /// and quantity is always the FCC quantity. Order quantity is aggregated by
    /// PART + GEAR so a correct total with a wrong Gear can never pass.
    /// </summary>
    internal static class YmvnGearQuantityValidator
    {
        private static readonly string[] PartColumns =
        {
            "MAHANGFCC", "MAHANG", "PART", "PARTNO", "PART NO", "ITEM", "ITEMNO", "CODE", "MÃ HÀNG"
        };

        private static readonly string[] GearColumns =
        {
            "GEAR", "Gear", "GEARCODE", "GEARNAME", "Gears"
        };

        private static readonly string[] QuantityColumns =
        {
            "SLTEMFCC", "SLTEM", "SLGIAO", "SOLUONG", "SỐ LƯỢNG", "SL", "QTY", "QUANTITY"
        };

        public static bool ValidateScan(
            string rawQr,
            DataTable orderRows,
            DataTable scannedRows,
            out string error)
        {
            error = null;

            string part;
            string gear;
            int quantity;
            if (!TryParseQr(rawQr, out part, out gear, out quantity, out error))
                return false;

            int required;
            if (!TryGetRequiredQuantity(orderRows, part, gear, out required, out error))
                return false;

            int alreadyScanned = SumScannedQuantity(scannedRows, part, gear);
            if (alreadyScanned + quantity > required)
            {
                error = string.Format(
                    "YMVN: Gear {0} của mã hàng [{1}] vượt số lượng giao.\r\n\r\n" +
                    "Đã bắn: {2}\r\nTem hiện tại: {3}\r\nĐược giao: {4}",
                    gear, part, alreadyScanned, quantity, required);
                return false;
            }

            return true;
        }

        public static bool ValidateCompletion(
            DataTable orderRows,
            DataTable scannedRows,
            out string error)
        {
            error = null;

            Dictionary<string, int> required = AggregateOrder(orderRows, out error);
            if (required == null)
                return false;

            Dictionary<string, int> actual = AggregateScanned(scannedRows);

            foreach (KeyValuePair<string, int> pair in required)
            {
                int actualQty;
                actual.TryGetValue(pair.Key, out actualQty);
                if (actualQty != pair.Value)
                {
                    string[] key = pair.Key.Split('|');
                    error = string.Format(
                        "Chưa đủ số lượng YMVN theo Gear.\r\n\r\nMã hàng: [{0}]\r\nGear: {1}\r\nĐã bắn: {2}\r\nPhải giao: {3}",
                        key[0], key[1], actualQty, pair.Value);
                    return false;
                }
            }

            foreach (KeyValuePair<string, int> pair in actual)
            {
                if (!required.ContainsKey(pair.Key))
                {
                    string[] key = pair.Key.Split('|');
                    error = string.Format(
                        "QR YMVN có Gear không tồn tại trong Order.\r\n\r\nMã hàng: [{0}]\r\nGear: {1}.",
                        key[0], key[1]);
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseQr(
            string rawQr,
            out string part,
            out string gear,
            out int quantity,
            out string error)
        {
            part = string.Empty;
            gear = string.Empty;
            quantity = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(rawQr))
            {
                error = "QR YMVN rỗng.";
                return false;
            }

            string[] parts = rawQr.Trim().Split(':');
            if (parts.Length < 6)
            {
                error = "Mã QR không đúng định dạng YMVN!";
                return false;
            }

            string lot = parts[0].Trim();
            part = parts[1].Trim();

            if (!int.TryParse(parts[3].Trim(), out quantity) || quantity <= 0)
            {
                error = "Số lượng TEM YMVN không hợp lệ!";
                return false;
            }

            if (lot.Length < 13)
            {
                error = string.Format("LOT FCC [{0}] không đủ 13 ký tự để xác định Gear.", lot);
                return false;
            }

            char gearCode = lot[12];
            switch (gearCode)
            {
                case '1': gear = "A"; break;
                case '2': gear = "B"; break;
                case '3': gear = "D"; break;
                default:
                    error = string.Format(
                        "Gear của LOT [{0}] không hợp lệ. Ký tự thứ 13 phải là 1(A), 2(B) hoặc 3(D).",
                        lot);
                    return false;
            }

            if (parts.Length > 6 && !string.IsNullOrWhiteSpace(parts[6]))
            {
                string qrGear = NormalizeGear(parts[6]);
                if (!string.IsNullOrEmpty(qrGear) && !string.Equals(qrGear, gear, StringComparison.OrdinalIgnoreCase))
                {
                    error = string.Format(
                        "Gear QR không khớp Gear xác định từ ký tự thứ 13 của LOT. LOT={0}, QR Gear={1}, Gear={2}.",
                        lot, parts[6].Trim(), gear);
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetRequiredQuantity(
            DataTable orderRows,
            string part,
            string gear,
            out int required,
            out string error)
        {
            required = 0;
            error = null;

            Dictionary<string, int> aggregate = AggregateOrder(orderRows, out error);
            if (aggregate == null)
                return false;

            string key = MakeKey(part, gear);
            if (!aggregate.TryGetValue(key, out required))
            {
                error = string.Format(
                    "Mã hàng [{0}] không có Gear [{1}] trong Order YMVN.",
                    part, gear);
                return false;
            }

            return true;
        }

        private static Dictionary<string, int> AggregateOrder(DataTable table, out string error)
        {
            error = null;
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (table == null || table.Rows.Count == 0)
            {
                error = "Không có dữ liệu Order YMVN để kiểm tra Gear/số lượng.";
                return null;
            }

            string partColumn = FindColumn(table, PartColumns);
            string gearColumn = FindColumn(table, GearColumns);
            string quantityColumn = FindColumn(table, QuantityColumns);

            if (partColumn == null || gearColumn == null || quantityColumn == null)
            {
                error = string.Format(
                    "Không xác định được cột Order YMVN cho Gear/số lượng. Part=[{0}], Gear=[{1}], Quantity=[{2}].",
                    partColumn ?? "?", gearColumn ?? "?", quantityColumn ?? "?");
                return null;
            }

            foreach (DataRow row in table.Rows)
            {
                string part = NormalizePart(row[partColumn]);
                string gear = NormalizeGear(row[gearColumn]);
                int quantity;

                if (string.IsNullOrEmpty(part) || string.IsNullOrEmpty(gear))
                    continue;
                if (!TryGetInt(row[quantityColumn], out quantity) || quantity <= 0)
                    continue;

                string key = MakeKey(part, gear);
                int old;
                result.TryGetValue(key, out old);
                result[key] = old + quantity;
            }

            if (result.Count == 0)
            {
                error = "Order YMVN không có dòng Gear/số lượng hợp lệ.";
                return null;
            }

            return result;
        }

        private static Dictionary<string, int> AggregateScanned(DataTable table)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (table == null) return result;

            string partColumn = FindColumn(table, PartColumns);
            string lotColumn = FindColumn(table, "LOTFCC", "LotFCC", "LOTHVN", "LotHVN");
            string quantityColumn = FindColumn(table, "SLTEMFCC", "SlTemFCC", "SLTEM", "SlTemHVN");

            if (partColumn == null || lotColumn == null || quantityColumn == null)
                return result;

            foreach (DataRow row in table.Rows)
            {
                string part = NormalizePart(row[partColumn]);
                string lot = row[lotColumn] == DBNull.Value ? string.Empty : row[lotColumn].ToString().Trim();
                int quantity;

                if (string.IsNullOrEmpty(part) || lot.Length < 13)
                    continue;
                if (!TryGetInt(row[quantityColumn], out quantity) || quantity <= 0)
                    continue;

                string gear = GearFromLot(lot);
                if (string.IsNullOrEmpty(gear))
                    continue;

                string key = MakeKey(part, gear);
                int old;
                result.TryGetValue(key, out old);
                result[key] = old + quantity;
            }

            return result;
        }

        private static int SumScannedQuantity(DataTable table, string part, string gear)
        {
            Dictionary<string, int> actual = AggregateScanned(table);
            int value;
            return actual.TryGetValue(MakeKey(part, gear), out value) ? value : 0;
        }

        private static string GearFromLot(string lot)
        {
            if (string.IsNullOrEmpty(lot) || lot.Length < 13) return string.Empty;
            switch (lot[12])
            {
                case '1': return "A";
                case '2': return "B";
                case '3': return "D";
                default: return string.Empty;
            }
        }

        private static string NormalizeGear(object value)
        {
            if (value == null || value == DBNull.Value) return string.Empty;
            string raw = value.ToString().Trim().ToUpperInvariant();
            if (raw == "1" || raw == "A" || raw == "GEAR A") return "A";
            if (raw == "2" || raw == "B" || raw == "GEAR B") return "B";
            if (raw == "3" || raw == "D" || raw == "GEAR D") return "D";
            return raw.Replace("GEAR", string.Empty).Trim();
        }

        private static string NormalizePart(object value)
        {
            if (value == null || value == DBNull.Value) return string.Empty;
            return value.ToString().Trim().ToUpperInvariant().Replace(" ", string.Empty);
        }

        private static string MakeKey(string part, string gear)
        {
            return NormalizePart(part) + "|" + NormalizeGear(gear);
        }

        private static string FindColumn(DataTable table, params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                foreach (DataColumn column in table.Columns)
                {
                    if (string.Equals(column.ColumnName, candidate, StringComparison.OrdinalIgnoreCase))
                        return column.ColumnName;
                }
            }
            return null;
        }

        private static bool TryGetInt(object value, out int result)
        {
            result = 0;
            if (value == null || value == DBNull.Value) return false;
            return int.TryParse(value.ToString().Trim(), out result);
        }
    }
}
