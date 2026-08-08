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

        // ✅ SỬA: không tự định nghĩa hằng số riêng nữa — luôn lấy từ LotCodeHelper
        // để không bao giờ lệch với các nơi khác (PhieuRepository, StockTpRepository...).
        private static int KeyLen => PCTP.Common.LotCodeHelper.LEN_HEAD_FIXED; // = 20

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
            _slotHelper.SaveSlotLots(slotId, remaining, updateSlot: true);

            SlotHelper.SaveHistory("EXPORT_AUTO_HVN",
               candidates.First().QRInfo?.ItemCode,
               new LotInfo { LotNo = lotNo, Quantity = slXuat - Math.Max(conLaiCanTru, 0) },
               slotId, toSlotId: null,
               performedBy: "SYSTEM_HVN_CNK"); // ← cần thêm tham số này ở SlotHelper (mục 2)

            if (conLaiCanTru > 0)
                System.Diagnostics.Debug.WriteLine(
                    $"[BulkStockAdjust] CẢNH BÁO: A0 thiếu {conLaiCanTru} cho LOT {lotNo}.");

            return true;
        }

        // ✅ SỬA: dùng LotCodeHelper.TrimTo thay vì Substring tay + hằng số riêng.
        // TrimTo tự an toàn nếu chuỗi ngắn hơn KeyLen, không cần check length thủ công.
        private static string Prefix(string lot) =>
            PCTP.Common.LotCodeHelper.TrimTo(lot, KeyLen);
    }
}
