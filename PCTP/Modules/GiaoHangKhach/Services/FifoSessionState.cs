using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// FIFO state for one QR-delivery session.
    /// It is an optimistic fast gate; STOCKTP is revalidated before stock update.
    /// </summary>
    public sealed class FifoSessionState
    {
        private readonly Dictionary<string, FifoPartState> _parts =
            new Dictionary<string, FifoPartState>(StringComparer.OrdinalIgnoreCase);

        public void Reset()
        {
            _parts.Clear();
        }

        public void Initialize(
            string maHang,
            int soLuongCanGiao,
            IEnumerable<FifoStockLine> fifoStock)
        {
            string part = Normalize(maHang);
            if (string.IsNullOrEmpty(part)) return;

            var stock = (fifoStock ?? Enumerable.Empty<FifoStockLine>())
                .Where(x => x != null && x.AvailableQty > 0 && !string.IsNullOrEmpty(x.LotKey))
                .OrderBy(x => x.FifoRank)
                .ThenBy(x => x.LotKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var state = new FifoPartState
            {
                ItemCode = part,
                NeedQty = Math.Max(soLuongCanGiao, 0)
            };

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

        public bool IsEnforced(string maHang)
        {
            return _parts.ContainsKey(Normalize(maHang));
        }

        public bool TryConsume(
            string maHang,
            string lot,
            int quantity,
            out string message)
        {
            message = string.Empty;

            string part = Normalize(maHang);
            string lotKey = NormalizeLot(lot);
            if (string.IsNullOrEmpty(part) || string.IsNullOrEmpty(lotKey) || quantity <= 0)
                return true;

            FifoPartState state;
            if (!_parts.TryGetValue(part, out state))
                return true; // EnforceFifo = 0 / item not configured.

            FifoLotState allowedLot = state.Lots
                .FirstOrDefault(x => string.Equals(x.LotKey, lotKey, StringComparison.OrdinalIgnoreCase));

            if (allowedLot == null || allowedLot.RemainingAllowedQty < quantity)
            {
                string requiredLot = state.Lots
                    .Where(x => x.RemainingAllowedQty > 0)
                    .OrderBy(x => x.FifoRank)
                    .Select(x => x.DisplayLot)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(requiredLot))
                    requiredLot = state.Lots.Count == 0 ? "(không còn LOT FIFO hợp lệ)" : state.Lots.Last().DisplayLot;

                message = $"FIFO: mã hàng {part} phải xuất LOT {requiredLot} trước. LOT đang chọn: {lotKey}, số lượng: {quantity}.";
                return false;
            }

            allowedLot.RemainingAllowedQty -= quantity;
            return true;
        }

        public void Release(string maHang, string lot, int quantity)
        {
            if (quantity <= 0) return;

            FifoPartState state;
            if (!_parts.TryGetValue(Normalize(maHang), out state)) return;

            string lotKey = NormalizeLot(lot);
            FifoLotState row = state.Lots
                .FirstOrDefault(x => string.Equals(x.LotKey, lotKey, StringComparison.OrdinalIgnoreCase));

            if (row == null) return;
            row.RemainingAllowedQty = Math.Min(
                row.OriginalAvailableQty,
                row.RemainingAllowedQty + quantity);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        public static string NormalizeLot(string value)
        {
            string lot = (value ?? string.Empty).Trim();
            if (lot.Length <= 13) return lot;
            return lot.Substring(0, 13);
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
    }

    public sealed class FifoStockLine
    {
        public string LotKey { get; set; }
        public string DisplayLot { get; set; }
        public int AvailableQty { get; set; }
        public int FifoRank { get; set; }
    }
}
