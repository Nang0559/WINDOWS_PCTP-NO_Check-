

using PCTP.Shared.Models;

namespace PCTP.Shared.Helpers
{
    public static class QRCodeBuilder
    {
        public static QRCodeInfo CloneWithQuantity(QRCodeInfo source, int quantity)
        {
            return new QRCodeInfo
            {
                LotNo = source.LotNo,
                ItemCode = source.ItemCode,
                NgaySX = source.NgaySX,
                Quantity = quantity,

                IsTongPhieu = source.IsTongPhieu,
                SoPhieuTong = source.SoPhieuTong,
                MaPhieu = source.MaPhieu,

                RawQr = source.RawQr,

                WarehouseCode = source.WarehouseCode,
                Unit = source.Unit
            };
        }

        public static string Build(QRCodeInfo info)
        {
            if (info.IsTongPhieu)
            {
                return $"{info.LotNo}:{info.ItemCode}:{info.NgaySX}:{info.Quantity}:{info.SoPhieuTong}:{info.MaPhieu}";
            }

            return $"{info.LotNo}:{info.ItemCode}:{info.NgaySX}:{info.Quantity}";
        }
    }
}
