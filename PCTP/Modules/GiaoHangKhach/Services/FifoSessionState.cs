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

            List<LotSelection> selections = ParseLotSelections(lot);
            if (selections.Count == 0)
            {
                selections.Add(new LotSelection
                {
                    LotKey = NormalizeLot(lot),
                    Lot = lot.Trim(),
                    Quantity = quantity
                });
            }

            int parsedQuantity = selections.Sum(x => x.Quantity);
            if (parsedQuantity != quantity)
            {
                message = string.Format(
                    "CẢNH BÁO FIFO\n\nMã hàng: {0}\nLOT đang quét: {1}\nSố lượng: {2}\n" +
                    "Tổng số lượng trong LOT ghép: {3}\n\nDữ liệu LOT ghép không khớp số lượng QR.",
                    part, lot.Trim(), quantity, parsedQuantity);
                return false;
            }

            // Validate the complete compound LOT against the current RAM allocation first.
            // Nothing is consumed until every LOT component passes.
            var trial = state.Lots.ToDictionary(x => x.LotKey, x => x.RemainingAllowedQty, StringComparer.OrdinalIgnoreCase);
            var consumed = new List<FifoReservationLine>();

            foreach (LotSelection selection in selections)
            {
                FifoLotState allowedLot = state.Lots.FirstOrDefault(x =>
                    string.Equals(x.LotKey, selection.LotKey, StringComparison.OrdinalIgnoreCase));

                int remaining;
                if (allowedLot == null || !trial.TryGetValue(selection.LotKey, out remaining) || remaining < selection.Quantity)
                {
                    FifoLotState required = state.Lots
                        .Where(x => trial.ContainsKey(x.LotKey) && trial[x.LotKey] > 0)
                        .OrderBy(x => x.FifoRank)
                        .FirstOrDefault();

                    string requiredLot = required != null
                        ? required.DisplayLot
                        : (state.Lots.Count == 0 ? "(không còn LOT FIFO hợp lệ)" : state.Lots[0].DisplayLot);
                    int allowedQty = required != null && trial.ContainsKey(required.LotKey)
                        ? trial[required.LotKey]
                        : 0;

                    message = string.Format(
                        "CẢNH BÁO FIFO\n\n" +
                        "Mã hàng: {0}\n" +
                        "LOT đang quét: {1}\n" +
                        "LOT thành phần sai FIFO: {2}\n" +
                        "Số lượng LOT thành phần: {3}\n" +
                        "LOT FIFO hiện tại: {4}\n" +
                        "Số lượng còn được phép: {5}\n\n" +
                        "Không thể xuất LOT ghép này vì chưa đúng thứ tự FIFO.",
                        part,
                        lot.Trim(),
                        selection.Lot,
                        selection.Quantity,
                        requiredLot,
                        allowedQty);
                    return false;
                }

                trial[selection.LotKey] = remaining - selection.Quantity;
                consumed.Add(new FifoReservationLine
                {
                    LotKey = selection.LotKey,
                    Quantity = selection.Quantity
                });
            }

            // Commit the whole compound LOT atomically to RAM.
            foreach (FifoReservationLine line in consumed)
            {
                FifoLotState row = state.Lots.First(x =>
                    string.Equals(x.LotKey, line.LotKey, StringComparison.OrdinalIgnoreCase));
                row.RemainingAllowedQty -= line.Quantity;
            }

            _reservations[stt] = new FifoReservation
            {
                Stt = stt,
                ItemCode = part,
                Lines = consumed
            };
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

        private static List<LotSelection> ParseLotSelections(string value)
        {
            var result = new List<LotSelection>();
            if (string.IsNullOrWhiteSpace(value)) return result;

            foreach (string token in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string part = token.Trim();
                int separator = part.LastIndexOf('-');
                if (separator <= 0 || separator >= part.Length - 1) return new List<LotSelection>();

                string lot = part.Substring(0, separator).Trim();
                int quantity;
                if (!int.TryParse(part.Substring(separator + 1).Trim(), out quantity) || quantity <= 0)
                    return new List<LotSelection>();

                result.Add(new LotSelection
                {
                    LotKey = NormalizeLot(lot),
                    Lot = lot,
                    Quantity = quantity
                });
            }

            return result;
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

    public sealed class FifoStockLine
    {
        public string LotKey { get; set; }
        public string DisplayLot { get; set; }
        public int AvailableQty { get; set; }
        public int FifoRank { get; set; }
    }
}
