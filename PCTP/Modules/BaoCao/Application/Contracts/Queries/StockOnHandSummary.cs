using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    /// <summary>1 dòng = 1 LOT trong STOCKTP (SLCONLAI &gt; 0), kèm phần đã xếp / chưa xếp vào slot.</summary>
    public sealed class StockOnHandLotRow
    {
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string Model { get; set; }
        public string LotNo { get; set; }
        public string Status { get; set; }              // giá trị gốc của SP: "Dang mo" / "Da ket thuc"
        public DateTime? ProductionDate { get; set; }
        public int? ShiftNo { get; set; }
        public decimal? QtyProduced { get; set; }
        public decimal? QtyReceived { get; set; }
        public decimal? QtyIssued { get; set; }
        public decimal QtyOnHand { get; set; }          // STOCKTP.SLCONLAI
        public decimal QtySlotted { get; set; }         // tổng SL ở các SlotLot active
        public decimal QtyUnslotted { get; set; }       // phần chưa gán slot
        public int SlotCount { get; set; }
        public DateTime? ReceivedDate { get; set; }     // STOCKTP.NGAYNHAP
        public DateTime? IssuedDate { get; set; }
        public DateTime? LastReceivedDate { get; set; } // mới nhất của NGAYNHAP và ngày nhập các slot
        public bool LotNoFormatError { get; set; }
        public string Check { get; set; }               // "OK" hoặc mô tả điểm cần đối chiếu
    }

    /// <summary>1 dòng = 1 mã hàng, cộng dồn các LOT còn tồn.</summary>
    public sealed class StockOnHandItemRow
    {
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public int LotCount { get; set; }
        public int OpenLotCount { get; set; }
        public decimal QtyOnHand { get; set; }
        public decimal QtySlotted { get; set; }
        public decimal QtyUnslotted { get; set; }
        public DateTime? LastReceivedDate { get; set; }
        public int LotsToCheck { get; set; }
    }

    /// <summary>
    /// Dựng các góc nhìn tồn kho từ CÙNG 1 nguồn (sp_GetCurrentStockStatus qua CurrentStockRow),
    /// để báo cáo "Kho hiện tại" và form "Tồn kho thành phẩm" luôn cho cùng con số.
    /// Thứ tự mặc định: ngày nhập mới nhất lên trên.
    /// </summary>
    public static class StockOnHandAggregator
    {
        /// <summary>Giá trị LocationCode mà sp_GetCurrentStockStatus dùng cho phần tồn chưa gán slot.</summary>
        public const string UnslottedLocation = "Chua gan slot";

        public static bool IsUnslotted(string locationCode)
        {
            return string.Equals((locationCode ?? string.Empty).Trim(), UnslottedLocation, StringComparison.OrdinalIgnoreCase);
        }

        public static List<StockOnHandLotRow> BuildLotRows(IEnumerable<CurrentStockRow> rows)
        {
            var result = new List<StockOnHandLotRow>();
            if (rows == null) return result;

            foreach (var group in rows.Where(r => r != null).GroupBy(r => (r.LotNo ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var list = group.ToList();
                var first = list[0];

                var lot = new StockOnHandLotRow
                {
                    LotNo = group.Key,
                    ItemCode = FirstText(list, r => r.ItemCode),
                    ItemName = FirstText(list, r => r.ItemName),
                    Model = FirstText(list, r => r.Model),
                    Status = FirstText(list, r => r.Status),
                    ProductionDate = list.Select(r => r.ProductionDate).FirstOrDefault(d => d.HasValue),
                    ShiftNo = list.Select(r => r.ShiftNo).FirstOrDefault(d => d.HasValue),
                    QtyProduced = list.Select(r => r.QtyProduced).FirstOrDefault(d => d.HasValue),
                    QtyReceived = list.Select(r => r.QtyReceived).FirstOrDefault(d => d.HasValue),
                    QtyIssued = list.Select(r => r.QtyIssued).FirstOrDefault(d => d.HasValue),
                    ReceivedDate = list.Select(r => r.ReceivedDate).FirstOrDefault(d => d.HasValue),
                    IssuedDate = list.Select(r => r.IssuedDate).FirstOrDefault(d => d.HasValue),
                    QtySlotted = list.Where(r => !IsUnslotted(r.LocationCode)).Sum(r => r.Quantity),
                    QtyUnslotted = list.Where(r => IsUnslotted(r.LocationCode)).Sum(r => r.Quantity),
                    SlotCount = list.Count(r => !IsUnslotted(r.LocationCode)),
                    LotNoFormatError = CurrentStockRow.IsScientificLot(group.Key)
                };

                // SLCONLAI của LOT lặp lại ở mọi dòng; thiếu (SP cũ) thì lấy tổng các dòng.
                var onHand = list.Select(r => r.QtyOnHandLot).FirstOrDefault(d => d.HasValue);
                lot.QtyOnHand = onHand.HasValue ? onHand.Value : lot.QtySlotted + lot.QtyUnslotted;

                lot.LastReceivedDate = MaxDate(new[] { lot.ReceivedDate }.Concat(list.Select(r => r.SlotImportDate)));
                lot.Check = BuildCheck(lot);
                result.Add(lot);
            }

            // Ngày nhập mới nhất lên trên; LOT không có ngày nhập xuống cuối.
            return result
                .OrderByDescending(l => l.LastReceivedDate.HasValue)
                .ThenByDescending(l => l.LastReceivedDate)
                .ThenBy(l => l.LotNo, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<StockOnHandItemRow> BuildItemRows(IEnumerable<StockOnHandLotRow> lots)
        {
            var result = new List<StockOnHandItemRow>();
            if (lots == null) return result;

            foreach (var group in lots.Where(l => l != null).GroupBy(l => (l.ItemCode ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var list = group.ToList();
                result.Add(new StockOnHandItemRow
                {
                    ItemCode = group.Key,
                    ItemName = list.Select(l => l.ItemName).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
                    LotCount = list.Count,
                    OpenLotCount = list.Count(l => !IsClosed(l.Status)),
                    QtyOnHand = list.Sum(l => l.QtyOnHand),
                    QtySlotted = list.Sum(l => l.QtySlotted),
                    QtyUnslotted = list.Sum(l => l.QtyUnslotted),
                    LastReceivedDate = MaxDate(list.Select(l => l.LastReceivedDate)),
                    LotsToCheck = list.Count(l => l.Check != "OK")
                });
            }

            return result
                .OrderByDescending(i => i.LastReceivedDate.HasValue)
                .ThenByDescending(i => i.LastReceivedDate)
                .ThenBy(i => i.ItemCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool IsClosed(string status)
        {
            return string.Equals((status ?? string.Empty).Trim(), "Da ket thuc", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildCheck(StockOnHandLotRow lot)
        {
            var notes = new List<string>();
            if (lot.LotNoFormatError)
                notes.Add("LOT sai định dạng (số khoa học)");
            if (lot.QtyUnslotted > 0)
                notes.Add("Chưa gán slot: " + lot.QtyUnslotted.ToString("n0"));
            if (lot.QtySlotted > lot.QtyOnHand)
                notes.Add("SL slot lớn hơn tồn LOT (" + lot.QtySlotted.ToString("n0") + " > " + lot.QtyOnHand.ToString("n0") + ")");
            if (lot.QtyReceived.HasValue && lot.QtyIssued.HasValue)
            {
                var diff = lot.QtyReceived.Value - lot.QtyIssued.Value - lot.QtyOnHand;
                if (diff != 0)
                    notes.Add("Nhập - Xuất khác Tồn (lệch " + diff.ToString("n0") + ")");
            }
            return notes.Count == 0 ? "OK" : string.Join("; ", notes);
        }

        private static string FirstText(List<CurrentStockRow> rows, Func<CurrentStockRow, string> pick)
        {
            return rows.Select(pick).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
        }

        private static DateTime? MaxDate(IEnumerable<DateTime?> dates)
        {
            DateTime? max = null;
            foreach (var d in dates)
                if (d.HasValue && (!max.HasValue || d.Value > max.Value)) max = d;
            return max;
        }
    }
}
