using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public sealed class FifoSessionState
    {
        private readonly Dictionary<string, FifoPartState> _parts = new Dictionary<string, FifoPartState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, FifoReservation> _reservations = new Dictionary<int, FifoReservation>();

        public void Reset()
        {
            _parts.Clear();
            _reservations.Clear();
        }

        public void Initialize(string maHang, int soLuongCanGiao, IEnumerable<FifoStockLine> fifoStock)
        {
            string part = Normalize(maHang);
            if (string.IsNullOrEmpty(part)) return;

            var stock = (fifoStock ?? Enumerable.Empty<FifoStockLine>())
                .Where(x => x != null && x.AvailableQty > 0 && !string.IsNullOrEmpty(x.LotKey))
                .OrderBy(x => x.FifoRank)
                .ThenBy(x => x.LotKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var state = new FifoPartState { ItemCode = part, NeedQty = Math.Max(soLuongCanGiao, 0) };
            int remainingNeed = state.NeedQty;

            foreach (FifoStockLine row in stock)
            {
                if (remainingNeed <= 0) break;
                int allowed = Math.Min(remainingNeed, row.AvailableQty);
                if (allowed <= 0) continue;

                state.Lots.Add(new FifoLotState
                {
                    LotKey = NormalizeLot(row.LotKey),
                    DisplayLot = row.DisplayLot,
                    OriginalAvailableQty = row.AvailableQty,
                    RemainingAllowedQty = allowed,
                    FifoRank = row.FifoRank
                });
                remainingNeed -= allowed;
            }

            _parts[part] = state;
        }

        public bool TryConsume(int stt, string maHang, string lot, int quantity, out string message)
        {
            message = string.Empty;
            string part = Normalize(maHang);
            if (string.IsNullOrEmpty(part) || string.IsNullOrWhiteSpace(lot) || quantity <= 0)
                return true;

            FifoPartState state;
            if (!_parts.TryGetValue(part, out state))
                return true;

            Release(stt);

            List<LotSelection> selections;
            string parseError;
            if (!TryParseLotSelections(lot, quantity, out selections, out parseError))
            {
                message = string.Format("CẢNH BÁO FIFO\n\nMã hàng: {0}\nLOT đang quét: {1}\nSố lượng: {2}\n\nLOT ghép không hợp lệ: {3}\n\nKhông thể xuất LOT này vì dữ liệu LOT không hợp lệ.", part, lot.Trim(), quantity, parseError);
                return false;
            }

            int parsedQuantity = selections.Sum(x => x.Quantity);
            if (parsedQuantity != quantity)
            {
                message = string.Format("CẢNH BÁO FIFO\n\nMã hàng: {0}\nLOT đang quét: {1}\nSố lượng: {2}\nTổng số lượng trong LOT ghép: {3}\n\nDữ liệu LOT ghép không khớp số lượng QR.", part, lot.Trim(), quantity, parsedQuantity);
                return false;
            }

            var trial = state.Lots.ToDictionary(x => x.LotKey, x => x.RemainingAllowedQty, StringComparer.OrdinalIgnoreCase);
            var consumed = new List<FifoReservationLine>();

            foreach (LotSelection selection in selections)
            {
                FifoLotState allowedLot = state.Lots.FirstOrDefault(x => string.Equals(x.LotKey, selection.LotKey, StringComparison.OrdinalIgnoreCase));
                int remaining;
                if (allowedLot == null || !trial.TryGetValue(selection.LotKey, out remaining) || remaining < selection.Quantity)
                {
                    FifoLotState required = state.Lots.Where(x => trial.ContainsKey(x.LotKey) && trial[x.LotKey] > 0).OrderBy(x => x.FifoRank).FirstOrDefault();
                    string requiredLot = required != null ? required.DisplayLot : (state.Lots.Count == 0 ? "(không còn LOT FIFO hợp lệ)" : state.Lots[0].DisplayLot);
                    int allowedQty = required != null && trial.ContainsKey(required.LotKey) ? trial[required.LotKey] : 0;
                    message = string.Format("CẢNH BÁO FIFO\n\nMã hàng: {0}\nLOT đang quét: {1}\nLOT thành phần sai FIFO: {2}\nSố lượng LOT thành phần: {3}\nLOT FIFO hiện tại: {4}\nSố lượng còn được phép: {5}\n\nKhông thể xuất LOT này vì chưa đúng thứ tự FIFO.", part, lot.Trim(), selection.Lot, selection.Quantity, requiredLot, allowedQty);
                    return false;
                }

                trial[selection.LotKey] = remaining - selection.Quantity;
                consumed.Add(new FifoReservationLine { LotKey = selection.LotKey, Quantity = selection.Quantity });
            }

            foreach (FifoReservationLine line in consumed)
            {
                FifoLotState row = state.Lots.First(x => string.Equals(x.LotKey, line.LotKey, StringComparison.OrdinalIgnoreCase));
                row.RemainingAllowedQty -= line.Quantity;
            }

            _reservations[stt] = new FifoReservation { Stt = stt, ItemCode = part, Lines = consumed };
            return true;
        }

        public void Release(int stt)
        {
            FifoReservation reservation;
            if (!_reservations.TryGetValue(stt, out reservation)) return;
            foreach (FifoReservationLine line in reservation.Lines)
                Release(reservation.ItemCode, line.LotKey, line.Quantity);
            _reservations.Remove(stt);
        }

        public void Release(string maHang, string lot, int quantity)
        {
            if (quantity <= 0) return;
            FifoPartState state;
            if (!_parts.TryGetValue(Normalize(maHang), out state)) return;
            string lotKey = NormalizeLot(lot);
            FifoLotState row = state.Lots.FirstOrDefault(x => string.Equals(x.LotKey, lotKey, StringComparison.OrdinalIgnoreCase));
            if (row == null) return;
            row.RemainingAllowedQty = Math.Min(row.OriginalAvailableQty, row.RemainingAllowedQty + quantity);
        }

        public IReadOnlyList<FifoRamEntry> GetRamSnapshot()
        {
            var result = new List<FifoRamEntry>();
            foreach (FifoPartState part in _parts.Values.OrderBy(x => x.ItemCode, StringComparer.OrdinalIgnoreCase))
            {
                foreach (FifoLotState lot in part.Lots.OrderBy(x => x.FifoRank).ThenBy(x => x.LotKey, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(new FifoRamEntry
                    {
                        ItemCode = part.ItemCode,
                        NeedQty = part.NeedQty,
                        LotKey = lot.LotKey,
                        DisplayLot = lot.DisplayLot,
                        OriginalAvailableQty = lot.OriginalAvailableQty,
                        RemainingAllowedQty = lot.RemainingAllowedQty,
                        FifoRank = lot.FifoRank
                    });
                }
            }
            return result;
        }

        private static bool TryParseLotSelections(string value, int qrQuantity, out List<LotSelection> result, out string error)
        {
            result = new List<LotSelection>();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "LOT rỗng.";
                return false;
            }

            string[] tokens = value.Split(new[] { ',' }, StringSplitOptions.None);
            bool isCompound = tokens.Length > 1;

            foreach (string token in tokens)
            {
                string part = token.Trim();
                if (string.IsNullOrEmpty(part))
                {
                    error = "LOT ghép chứa thành phần rỗng.";
                    return false;
                }

                int separator = part.LastIndexOf('-');
                string lot;
                int lotQuantity;
                if (separator > 0 && separator < part.Length - 1 && int.TryParse(part.Substring(separator + 1).Trim(), out lotQuantity) && lotQuantity > 0)
                {
                    lot = part.Substring(0, separator).Trim();
                }
                else if (!isCompound)
                {
                    lot = part;
                    lotQuantity = qrQuantity;
                }
                else
                {
                    error = string.Format("Thành phần '{0}' phải có định dạng LOT-SỐ_LƯỢNG.", part);
                    return false;
                }

                if (string.IsNullOrEmpty(lot))
                {
                    error = string.Format("Thành phần '{0}' không có mã LOT.", part);
                    return false;
                }

                result.Add(new LotSelection { LotKey = NormalizeLot(lot), Lot = lot, Quantity = lotQuantity });
            }

            if (result.Count == 0)
            {
                error = "Không có thành phần LOT hợp lệ.";
                return false;
            }
            return true;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        public static string NormalizeLot(string value)
        {
            string lot = (value ?? string.Empty).Trim();
            return lot.Length <= 13 ? lot : lot.Substring(0, 13);
        }

        private sealed class FifoPartState
        {
            public string ItemCode { get; set; }
            public int NeedQty { get; set; }
            public List<FifoLotState> Lots { get; } = new List<FifoLotState>();
        }

        private sealed class FifoLotState
        {
            public string LotKey { get; set; }
            public string DisplayLot { get; set; }
            public int OriginalAvailableQty { get; set; }
            public int RemainingAllowedQty { get; set; }
            public int FifoRank { get; set; }
        }

        private sealed class LotSelection
        {
            public string LotKey { get; set; }
            public string Lot { get; set; }
            public int Quantity { get; set; }
        }

        private sealed class FifoReservation
        {
            public int Stt { get; set; }
            public string ItemCode { get; set; }
            public List<FifoReservationLine> Lines { get; set; }
        }

        private sealed class FifoReservationLine
        {
            public string LotKey { get; set; }
            public int Quantity { get; set; }
        }
    }

    public sealed class FifoRamEntry
    {
        public string ItemCode { get; set; }
        public int NeedQty { get; set; }
        public string LotKey { get; set; }
        public string DisplayLot { get; set; }
        public int OriginalAvailableQty { get; set; }
        public int RemainingAllowedQty { get; set; }
        public int FifoRank { get; set; }
    }

    public sealed class FifoStockLine
    {
        public string LotKey { get; set; }
        public string DisplayLot { get; set; }
        public int AvailableQty { get; set; }
        public int FifoRank { get; set; }
    }
}