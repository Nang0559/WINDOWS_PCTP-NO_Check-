using PCTP.Common;
using PCTP.VIEWSTOCK.Models;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Fuction
{

    public static class LotNoHelper
    {
        /// <summary>
        /// Khoá HIỂN THỊ dùng riêng cho module kho Slot/SlotLot (Warehouse/Rack/Slot) —
        /// KHÔNG liên quan STOCKTP, KHÔNG dùng để so khớp tồn kho.
        /// </summary>
        public static string NormalizeLot(string rawLotNo)
        => PCTP.Common.LotCodeHelper.NormalizeLotForSlotDisplay(rawLotNo);

        /// <summary>
        /// Khoá CHUẨN DUY NHẤT dùng để ghi/so khớp cột STOCKTP.LOT (20 ký tự đầu:
        /// Date+ItemId+Shift+Gear+Line+Machine). Mọi nơi ghi vào STOCKTP hoặc so khớp
        /// với STOCKTP đều PHẢI gọi qua hàm này — KHÔNG tự Substring riêng.
        /// </summary>
        public static string GetStockTpKey(string rawLotNo)
            => PCTP.Common.LotCodeHelper.StripCounterAndQty(rawLotNo);
        // Giữ nguyên logic cũ từ NHAP_TP — build danh sách FIND để tìm grid
        public static List<string> BuildFindList(string lotNoSL, string idSP)
         => PCTP.Common.LotCodeHelper.BuildCandidateFinds(lotNoSL, idSP);
        public static PrintLotResult CreatePrintData(List<LotInfo> lots)
        {
            var result = new PrintLotResult();

            if (lots == null || lots.Count == 0)
                return result;

            // Lấy thông tin chung từ Lot đầu tiên
            var first = lots.First();

            result.ItemCode = first.QRInfo?.ItemCode;
            result.ImportDate = first.QRInfo?.ImportDate;

            result.Quantity = lots.Sum(x => x.Quantity);

            result.LotNo = string.Join(",",
                lots.Select(x => $"{x.LotNo}-{x.Quantity}"));

            result.TemCode = string.Join(",",
                lots.Where(x => !string.IsNullOrWhiteSpace(x.TemCode))
                    .Select(x => $"{x.TemCode}-{x.Quantity}"));

            result.QrData = string.Join(
                Environment.NewLine,
                lots.Where(x => !string.IsNullOrWhiteSpace(x.RawQr))
                    .Select(x => x.RawQr));

            result.Lots = CloneLots(lots);

            return result;
        }

        public static LotSplitResult SubtractLots(
        List<LotInfo> lots,
        int exportQty)
        {
            var result = new LotSplitResult();

            foreach (var lot in lots)
            {
                if (exportQty <= 0)
                {
                    // Không xuất phần này -> Quantity không đổi -> RawQr gốc vẫn đúng, không cần build lại
                    result.RemainingLots.Add(new LotInfo
                    {
                        LotNo = lot.LotNo,
                        Quantity = lot.Quantity,
                        TemCode = lot.TemCode,
                        RawQr = lot.RawQr,
                        QRInfo = lot.QRInfo
                    });

                    continue;
                }

                // xuất hết lot -> Quantity không đổi -> RawQr gốc vẫn đúng, không cần build lại
                if (lot.Quantity <= exportQty)
                {
                    result.ExportLots.Add(new LotInfo
                    {
                        LotNo = lot.LotNo,
                        Quantity = lot.Quantity,
                        TemCode = lot.TemCode,
                        RawQr = lot.RawQr,
                        QRInfo = lot.QRInfo
                    });

                    exportQty -= lot.Quantity;
                }
                // xuất một phần -> Quantity bị chia đôi -> PHẢI build lại RawQr theo Quantity mới,
                // nếu không QR code in ra sẽ vẫn encode số lượng gốc (sai với số in trên phiếu).
                else
                {
                    result.ExportLots.Add(CreateSplitLot(lot, exportQty));
                    result.RemainingLots.Add(CreateSplitLot(lot, lot.Quantity - exportQty));

                    exportQty = 0;
                }
            }

            return result;
        }

        /// <summary>
        /// Tạo 1 LotInfo mới với Quantity đã chia nhỏ, đồng thời build lại RawQr + QRInfo.Quantity
        /// theo đúng Quantity mới (thay vì copy nguyên RawQr gốc — vốn vẫn encode số lượng CHƯA tách).
        /// </summary>
        private static LotInfo CreateSplitLot(LotInfo source, int quantity)
        {
            var newLot = new LotInfo
            {
                LotNo = source.LotNo,
                Quantity = quantity,
                TemCode = source.TemCode,
                RawQr = source.RawQr, // fallback nếu không có QRInfo để build lại
                QRInfo = source.QRInfo
            };

            if (source.QRInfo != null)
            {
                var newQrInfo = QRCodeBuilder.CloneWithQuantity(source.QRInfo, quantity);
                newQrInfo.RawQr = QRCodeBuilder.Build(newQrInfo); // build lại chuỗi QR theo Quantity mới

                newLot.QRInfo = newQrInfo;
                newLot.RawQr = newQrInfo.RawQr;
            }
            // Nếu source.QRInfo == null (LotInfo không được tạo qua LotNoHelper.CreateLot) thì
            // không đủ dữ liệu (ItemCode, NgaySX...) để build lại QR đúng chuẩn -> giữ tạm RawQr gốc.

            return newLot;
        }

        public static int GetTotalQuantity(List<LotInfo> lots)
        {
            if (lots == null)
                return 0;

            return lots.Sum(x => x.Quantity);
        }
        public static List<LotInfo> CloneLots(List<LotInfo> lots)
        {
            return lots.Select(x => new LotInfo
            {
                LotNo = x.LotNo,
                Quantity = x.Quantity,
                TemCode = x.TemCode,
                RawQr = x.RawQr,
                QRInfo = x.QRInfo
            }).ToList();
        }
        public static LotInfo FindLot(
        List<LotInfo> lots,
        string lotNo)
        {
            return lots.FirstOrDefault(x => x.LotNo == lotNo);
        }

        public static List<LotInfo> MergeLotInfos(
         List<LotInfo> oldLots,
         List<LotInfo> newLots)
        {
            var result = new List<LotInfo>();

            if (oldLots != null)
                result.AddRange(oldLots);

            foreach (var newLot in newLots)
            {
                var exist = result.FirstOrDefault(x => x.LotNo == newLot.LotNo);

                if (exist == null)
                {
                    result.Add(newLot);
                    continue;
                }

                exist.Quantity += newLot.Quantity;

                if (string.IsNullOrWhiteSpace(exist.TemCode))
                    exist.TemCode = newLot.TemCode;

                // ✅ FIX: build lại RawQr theo TỔNG Quantity mới sau khi cộng dồn — nếu chỉ
                // giữ RawQr cũ (như code gốc) thì QR sẽ vẫn encode số lượng TRƯỚC khi merge,
                // sai với Quantity thực tế của lot sau khi gộp.
                var qrSource = exist.QRInfo ?? newLot.QRInfo;
                if (qrSource != null)

                {
                    var mergedQr = QRCodeBuilder.CloneWithQuantity(qrSource, exist.Quantity);
                    mergedQr.RawQr = QRCodeBuilder.Build(mergedQr);

                    exist.QRInfo = mergedQr;
                    exist.RawQr = mergedQr.RawQr;
                }
                else if (string.IsNullOrWhiteSpace(exist.RawQr))
                {
                    // Không có QRInfo ở cả 2 bên để build lại -> fallback giữ RawQr của newLot (nếu có)
                    exist.RawQr = newLot.RawQr;
                }
            }

            return result
                .OrderBy(x => x.LotNo)
                .ToList();
        }
        public static LotInfo CreateLot(QRCodeInfo qr)
        {
            return new LotInfo
            {
                LotNo = GetStockTpKey(qr.RawLotNo),

                Quantity = qr.Quantity,

                TemCode = qr.MaPhieu,

                RawQr = qr.RawQr,

                QRInfo = qr
            };
        }
        // ✅ SỬA — thêm helper cục bộ tách LOT trong chuỗi ghép rồi so bằng AreLotKeysEquivalent
        public static bool LotStringContainsMatch(string ghepLotString, string targetLot)
        {
            if (string.IsNullOrEmpty(ghepLotString) || string.IsNullOrEmpty(targetLot)) return false;
            foreach (var p in ghepLotString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string item = p.Trim();
                int dashIdx = item.LastIndexOf('-');
                string lotPart = dashIdx > 0 ? item.Substring(0, dashIdx).Trim() : item;
                if (LotCodeHelper.AreLotKeysEquivalent(lotPart, targetLot))
                    return true;
            }
            return false;
        }

    }
}
