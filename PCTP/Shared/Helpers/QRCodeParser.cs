using PCTP.Shared.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Fuction
{
    
    public class QRCodeParser
    {
        public static QRCodeInfo ParseQRCode(string qrText)
        {
            if (string.IsNullOrWhiteSpace(qrText))
                throw new FormatException("QR Code rỗng.");

            var parts = qrText.Split(':');

            // ── Tem thùng: 4 parts ─────────────────────────────────
            if (parts.Length == 4)
            {
                return new QRCodeInfo
                {
                    LotNo = parts[0].Trim(),
                    RawLotNo = parts[0].Trim(),
                    ItemCode = parts[1].Trim(),
                    NgaySX = parts[2].Trim(), // ✅ ImportDate tự tính từ NgaySX
                    Quantity = int.TryParse(parts[3].Trim(), out int q4) ? q4 : 0,
                    IsTongPhieu = false,
                    WarehouseCode = parts[0].Trim(),
                    Unit = "",
                    RawQr = qrText
                };
            }

            // ── Tem tổng: 6 parts ──────────────────────────────────
            if (parts.Length == 6)
            {
                return new QRCodeInfo
                {
                    LotNo = parts[0].Trim(),
                    RawLotNo = parts[0].Trim(),
                    ItemCode = parts[1].Trim(),
                    NgaySX = parts[2].Trim(), // ✅ ImportDate tự tính từ NgaySX
                    Quantity = int.TryParse(parts[3].Trim(), out int q6) ? q6 : 0,
                    SoPhieuTong = parts[4].Trim(),
                    MaPhieu = parts[5].Trim(),
                    IsTongPhieu = true,
                    WarehouseCode = parts[0].Trim(),
                    Unit = parts[4].Trim(),
                    RawQr = qrText
                };
            }

            throw new FormatException(
                $"QR Code không hợp lệ: cần 4 hoặc 6 phần, nhận được {parts.Length} phần.\nNội dung: {qrText}");
        }
        /// <summary>
        /// QR riêng cho tem TÁCH LOT: "LOT+SLTEMFCC:ITEM:NGAYSX:SLTEMFCC" (4 phần).
        /// Quirk: SLTEMFCC bị in dính vào cuối LOT (không tách dấu ':'), nên phải
        /// trừ ngược độ dài để lấy LOT sạch. LOT chuẩn 27 ký tự → luôn cắt 4 số cuối.
        /// </summary>
        public static TachLotQrInfo ParseTachLotQr(string qrText)
        {
            if (string.IsNullOrWhiteSpace(qrText))
                throw new FormatException("QR Code rỗng.");

            var parts = qrText.Split(':');
            if (parts.Length != 4)
                throw new FormatException(
                    $"QR Code TÁCH LOT không đúng định dạng: cần 4 phần, nhận được {parts.Length} phần.\nNội dung: {qrText}");

            string rawLotToken = parts[0].Trim();
            string itemCode = parts[1].Trim();
            string ngaySx = parts[2].Trim();
            string slTemRaw = parts[3].Trim();

            string lot = rawLotToken.Length == 27
                ? rawLotToken.Substring(0, rawLotToken.Length - 4)
                : rawLotToken.Substring(0, Math.Max(0, rawLotToken.Length - slTemRaw.Length));

            return new TachLotQrInfo
            {
                LotNo = lot,
                ItemCode = itemCode,
                NgaySX = ngaySx,
                SlTemFccRaw = slTemRaw
            };
        }
    }

}
