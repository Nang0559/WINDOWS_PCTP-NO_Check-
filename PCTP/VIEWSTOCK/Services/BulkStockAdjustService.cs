using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Services
{
    public class BulkStockAdjustService
    {
        private readonly SlotHelper _slotHelper = new SlotHelper();
        private readonly StockService _stockService = new StockService();

        private const int LOT_MATCH_LEN = 12; // đồng bộ với SUBSTRING(LOT,1,12) trong SP

        public bool TruKhoAoTheoLot(string lotNo, int slXuat)
        {
            if (slXuat <= 0 || string.IsNullOrWhiteSpace(lotNo)) return false;

            string slotText = _stockService.GetOrCreateBulkImportSlotText();
            SlotHelper.ParseSlotString(slotText, out string wh, out string rack, out int slotNumber, out _);
            int slotId = _slotHelper.GetSlotID(wh, rack, slotNumber);

            var lots = _slotHelper.GetSlotLots(slotId);
            string prefix = Prefix(lotNo);
            var candidates = lots.Where(l => Prefix(l.LotNo) == prefix)
                                  .OrderBy(l => l.QRInfo?.ImportDate ?? DateTime.MaxValue)
                                  .ToList();

            if (candidates.Count == 0) return false; // LOT không nằm trong A0 — không phải lỗi

            int conLaiCanTru = slXuat;
            foreach (var lot in candidates)
            {
                if (conLaiCanTru <= 0) break;
                int tru = Math.Min(conLaiCanTru, lot.Quantity);
                lot.Quantity -= tru;
                conLaiCanTru -= tru;
            }

            var remaining = lots.Where(l => l.Quantity > 0).ToList();
            _slotHelper.SaveSlotLots(slotId, remaining, updateSlot: true); // ✅ cập nhật SlotLot + Slot

            SlotHelper.SaveHistory("EXPORT_AUTO_HVN",
               candidates.First().QRInfo?.ItemCode,
               new LotInfo { LotNo = lotNo, Quantity = slXuat - Math.Max(conLaiCanTru, 0) },
               slotId, toSlotId: null,
               performedBy: "SYSTEM_HVN_CNK");

            if (conLaiCanTru > 0)
                System.Diagnostics.Debug.WriteLine(
                    $"[BulkStockAdjust] CẢNH BÁO: A0 thiếu {conLaiCanTru} cho LOT {lotNo}.");

            return true;
        }

        //private static string Prefix(string lot) =>
        //    string.IsNullOrEmpty(lot) ? "" : (lot.Length >= 12 ? lot.Substring(0, 12) : lot);

        private static string Prefix(string lot) =>
            string.IsNullOrEmpty(lot) ? "" : (lot.Length >= LOT_MATCH_LEN ? lot.Substring(0, LOT_MATCH_LEN) : lot);
    }
}
