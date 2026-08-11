using DevExpress.XtraReports.Design;
using PCTP.Common;
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
    /// <summary>
    /// Điều chỉnh kho ảo A0 (BulkImportConfig) khi hàng đã nhập vào A0 sau đó được
    /// Cập Nhập Kho (CNK) xuất đi qua luồng HVN/YMVN/HTN thông thường — A0 phải tự
    /// trừ theo đúng LOT + số lượng đã xuất, KHÔNG chờ người dùng thao tác thủ công.
    /// </summary>
    public class BulkStockAdjustService
    {
        private readonly SlotHelper _slotHelper = new SlotHelper();
        private readonly StockService _stockService = new StockService();

        // ✅ Không tự định nghĩa hằng số riêng — luôn lấy từ LotCodeHelper để không
        // bao giờ lệch với các nơi khác (PhieuRepository, StockTpRepository...).
        //private static int KeyLen => LotCodeHelper.LEN_HEAD_FIXED; // = 20

        /// <summary>
        /// Trừ số lượng slXuat khỏi kho ảo A0 theo LotNo — có thể phải "ăn" qua NHIỀU
        /// dòng SlotLot cùng LotNo (vì mỗi lần nhập tạo 1 dòng riêng, không merge).
        ///
        /// FIX (bản mới): so khớp qua LotCodeHelper.AreLotKeysEquivalent thay vì
        /// TrimTo(...) + so bằng == cứng theo 1 độ dài cố định (20). Lý do: dữ liệu
        /// LOT trong A0 có thể là LOT cũ chỉ có 13 ký tự (Date+IdItem+Shift+Gear,
        /// KHÔNG có Line/Machine) trong khi lotNo truyền vào từ CapNhapKho có thể
        /// đã đủ 20 ký tự (hoặc ngược lại) — so bằng == theo TrimTo cố định 20 sẽ
        /// KHÔNG BAO GIỜ khớp giữa 1 chuỗi 13 và 1 chuỗi 20 ký tự dù về bản chất là
        /// cùng 1 LOT, dẫn đến A0 "quên trừ" và tồn ảo bị lệch dần theo thời gian.
        /// AreLotKeysEquivalent tự hạ về so khớp 13 ký tự khi 1 trong 2 bên là LOT
        /// cũ, và vẫn giữ so khớp chính xác 20 ký tự khi cả 2 bên đều đủ dữ liệu.
        /// </summary>
        public bool TruKhoAoTheoLot(string lotNo, int slXuat)
        {
            if (slXuat <= 0 || string.IsNullOrWhiteSpace(lotNo)) return false;

            string slotText = _stockService.GetOrCreateBulkImportSlotText();
            SlotHelper.ParseSlotString(slotText, out string wh, out string rack, out int slotNumber, out _);
            int slotId = _slotHelper.GetSlotID(wh, rack, slotNumber);

            // Luôn đọc tươi từ DB — mỗi lần gọi độc lập, đảm bảo idempotent khi
            // CapNhapKho gọi lặp nhiều lần cho cùng 1 LOT (nhiều dòng phiếu cùng LOT
            // trong 1 lần CNK).
            var lots = _slotHelper.GetSlotLots(slotId);

            // ✅ SỬA: dùng AreLotKeysEquivalent — tương thích cả LOT cũ (13 ký tự)
            // lẫn LOT mới (20 ký tự) ở cả 2 phía so sánh.
            var candidates = lots
                .Where(l => l.Quantity > 0) // bỏ rác — tránh dòng 0 SL lọt vào so khớp/log nhầm
                .Where(l => LotCodeHelper.AreLotKeysEquivalent(l.LotNo, lotNo))
                .OrderBy(l => l.QRInfo?.ImportDate ?? DateTime.MaxValue) // FIFO — nhập trước xuất trước
                .ToList();

            if (candidates.Count == 0) return false; // LOT không nằm trong A0 — không phải lỗi

            int conLaiCanTru = slXuat;

            // ⚠️ candidates chứa CÙNG object reference với lots (LotInfo là class), nên
            // sửa lot.Quantity ở đây cũng phản ánh luôn vào "lots" — không cần đồng bộ
            // lại thủ công.
            foreach (var lot in candidates)
            {
                if (conLaiCanTru <= 0) break;

                int tru = Math.Min(conLaiCanTru, lot.Quantity);
                lot.Quantity -= tru;
                conLaiCanTru -= tru;
            }

            var remaining = lots.Where(l => l.Quantity > 0).ToList();
            _slotHelper.SaveSlotLots(slotId, remaining, updateSlot: true);

            int slThucTeDaTru = slXuat - Math.Max(conLaiCanTru, 0);

            SlotHelper.SaveHistory(
                "EXPORT_AUTO_HVN",
                candidates.First().QRInfo?.ItemCode,
                new LotInfo { LotNo = lotNo, Quantity = slThucTeDaTru },
                slotId,
                toSlotId: null,
                performedBy: "SYSTEM_HVN_CNK");

            if (conLaiCanTru > 0)
            {
                // ⚠️ Đây là dấu hiệu LỆCH DỮ LIỆU giữa STOCKTP (đã trừ đủ slXuat) và
                // A0 (không đủ hàng để trừ) — KHÔNG được chỉ log Debug rồi im lặng, vì
                // FormBulkSlotView sẽ hiển thị sai và không ai biết để đối soát.
                // TODO: ghi vào bảng log DB riêng hoặc raise 1 event cảnh báo UI thay
                // vì chỉ Debug.WriteLine (dễ bị bỏ qua trong môi trường production).
                System.Diagnostics.Debug.WriteLine(
                    $"[BulkStockAdjust] CẢNH BÁO: A0 thiếu {conLaiCanTru} cho LOT {lotNo} " +
                    $"(cần trừ {slXuat}, chỉ trừ được {slThucTeDaTru}). Cần đối soát STOCKTP vs A0.");
            }

            return true;
        }
    }
}
