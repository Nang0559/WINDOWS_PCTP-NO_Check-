using PCTP.Modules.XuLyHangLoi.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    /// <summary>Thông tin đọc từ tem QR FCC: Lot:MãHàng:Ngày:SL:...:MãTem</summary>
    public sealed class QrItemInfo
    {
        public string Lot { get; set; }
        public string ItemCode { get; set; }
        public int Quantity { get; set; }

        /// <summary>Quy ước giống MainStockSV: Last + parts[4] + "-" + parts[3]. Null nếu tem ngắn.</summary>
        public string TemCode { get; set; }
    }

    public static class QrItemParser
    {
        public static bool TryParse(string raw, out QrItemInfo info, out string error)
        {
            info = null;
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "Mã QR rỗng.";
                return false;
            }

            string[] parts = raw.Trim().Split(':');

            if (parts.Length < 4)
            {
                error = "Mã QR không đúng định dạng (Lot:Mã hàng:Ngày:SL:...).";
                return false;
            }

            string lot = parts[0].Trim();
            string item = parts[1].Trim();

            if (lot.Length == 0 || item.Length == 0)
            {
                error = "Mã QR thiếu Lot hoặc Mã hàng.";
                return false;
            }

            int qty;
            if (!int.TryParse(parts[3].Trim(), out qty) || qty <= 0)
            {
                error = "Số lượng trong mã QR không hợp lệ.";
                return false;
            }

            info = new QrItemInfo
            {
                Lot = lot,
                ItemCode = item,
                Quantity = qty,
                TemCode = parts.Length >= 5
                    ? parts[parts.Length - 1].Trim() + parts[4].Trim() + "-" + parts[3].Trim()
                    : null
            };

            return true;
        }

        /// <summary>
        /// Tìm các dòng Slot/LOT khớp với tem: ưu tiên TemCode,
        /// sau đó (Mã hàng + LotNo trùng hoặc lot này chứa lot kia).
        /// </summary>
        public static List<SlotNguonLookupItem> TimSlotKhop(
            IEnumerable<SlotNguonLookupItem> slots,
            QrItemInfo info)
        {
            var list = slots.ToList();

            if (!string.IsNullOrEmpty(info.TemCode))
            {
                var byTem = list.Where(s => string.Equals(
                    s.TemCode?.Trim(), info.TemCode, StringComparison.OrdinalIgnoreCase)).ToList();

                if (byTem.Count > 0)
                    return byTem;
            }

            return list.Where(s =>
                    string.Equals(s.ItemCode?.Trim(), info.ItemCode, StringComparison.OrdinalIgnoreCase)
                    && LotKhop(s.LotNo, info.Lot))
                .ToList();
        }

        public static bool LotKhop(string slotLot, string qrLot)
        {
            if (string.IsNullOrWhiteSpace(slotLot) || string.IsNullOrWhiteSpace(qrLot))
                return false;

            slotLot = slotLot.Trim();
            qrLot = qrLot.Trim();

            return string.Equals(slotLot, qrLot, StringComparison.OrdinalIgnoreCase)
                || qrLot.StartsWith(slotLot, StringComparison.OrdinalIgnoreCase)
                || slotLot.StartsWith(qrLot, StringComparison.OrdinalIgnoreCase);
        }
    }
}
