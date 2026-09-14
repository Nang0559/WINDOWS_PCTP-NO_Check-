using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PCTP.Modules.BaoCao.Infrastructure.Traceability
{
    /// <summary>
    /// Parses LUUPHIEUGIAOHANG.LOT when one delivery/carton stores
    /// multiple LOT allocations in one string, for example:
    ///     LOT001-100,LOT002-80
    ///
    /// The quantity separator is the LAST '-' so LOT identifiers may still
    /// contain '-' characters. Invalid tokens are rejected explicitly.
    /// </summary>
    public static class DeliveryLotStringParser
    {
        public static IReadOnlyList<DeliveryLotTraceRow> Parse(
            string deliveryKey,
            string qrCode,
            string rawLot)
        {
            var result = new List<DeliveryLotTraceRow>();

            if (string.IsNullOrWhiteSpace(rawLot))
                return result;

            var tokens = rawLot
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();

            foreach (var token in tokens)
            {
                int separator = token.LastIndexOf('-');
                if (separator <= 0 || separator == token.Length - 1)
                    throw new FormatException(
                        "LOT giao hàng không hợp lệ: '" + token + "'. " +
                        "Định dạng yêu cầu LOT-Quantity, các LOT được ngăn bởi dấu phẩy.");

                string lot = token.Substring(0, separator).Trim();
                string quantityText = token.Substring(separator + 1).Trim();

                if (lot.Length == 0)
                    throw new FormatException("LOT giao hàng bị rỗng trong token: '" + token + "'.");

                decimal quantity;
                if (!decimal.TryParse(
                    quantityText,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out quantity) || quantity <= 0)
                {
                    if (!decimal.TryParse(
                        quantityText,
                        NumberStyles.Number,
                        CultureInfo.CurrentCulture,
                        out quantity) || quantity <= 0)
                    {
                        throw new FormatException(
                            "Số lượng LOT không hợp lệ trong token: '" + token + "'.");
                    }
                }

                result.Add(new DeliveryLotTraceRow
                {
                    DeliveryKey = deliveryKey,
                    QRCode = qrCode,
                    LotNo = lot,
                    Quantity = quantity
                });
            }

            return result;
        }
    }
}
