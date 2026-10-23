using PCTP.Shared.Models;
using System;
using System.Globalization;


namespace PCTP.Shared.Helpers
{
    
    public class QRCodeParser
    {
        public static QRCodeInfo ParseQRCode(string qrText)
        {
            if (string.IsNullOrWhiteSpace(qrText))
                throw new FormatException("QR Code rỗng.");

            var parts = qrText.Split(':');
            ValidateRequiredFields(parts);

            // ── Tem thùng: 4 parts ─────────────────────────────────
            if (parts.Length == 4)
            {
                return new QRCodeInfo
                {
                    LotNo = parts[0].Trim(),
                    RawLotNo = parts[0].Trim(),
                    ItemCode = parts[1].Trim(),
                    NgaySX = ValidateManufacturingDate(parts[2].Trim()),
                    Quantity = ParsePositiveQuantity(parts[3].Trim()),
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
                    NgaySX = ValidateManufacturingDate(parts[2].Trim()),
                    Quantity = ParsePositiveQuantity(parts[3].Trim()),
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
        private static void ValidateRequiredFields(string[] parts)
        {
            if (parts.Length != 4 && parts.Length != 6)
                throw new FormatException(
                    $"QR Code không hợp lệ: cần 4 hoặc 6 phần, nhận được {parts.Length} phần.");

            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                    throw new FormatException($"QR field {i + 1} không được để trống.");
            }
        }

        private static int ParsePositiveQuantity(string value)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int quantity) ||
                quantity <= 0)
                throw new FormatException("Số lượng QR phải là số nguyên dương.");
            return quantity;
        }

        private static string ValidateManufacturingDate(string value)
        {
            if (!DateTime.TryParseExact(
                    value,
                    new[] { "dd/MM/yyyy", "d/M/yyyy", "dd/M/yyyy", "d/MM/yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
                throw new FormatException("Ngày sản xuất QR không hợp lệ; định dạng yêu cầu dd/MM/yyyy.");
            return value;
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
