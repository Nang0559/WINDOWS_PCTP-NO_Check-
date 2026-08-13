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
    }
}
