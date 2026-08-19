using PCTP.Models;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Kho.Models
{
    /// <summary>
    /// Đại diện cho một LOT nằm trong Slot.
    /// Đây là model dữ liệu dùng chung cho Slot / SlotLot.
    /// </summary>
    public sealed class LotInfo
    {
        // =========================================================
        // Vị trí
        // =========================================================

        /// <summary>
        /// Theo contract cũ:
        /// SlotId ở đây thực tế là SlotLotId.
        /// </summary>
        public int SlotId { get; set; }

        /// <summary>
        /// Slot.SlotId thật - dùng cho vị trí vật lý.
        /// </summary>
        public int SlotVatLyId { get; set; }

        // =========================================================
        // Thông tin phiếu
        // =========================================================

        public string MaPhieuKho { get; set; }

        public string ParentSoPhieuKho { get; set; }

        public PhieuStatus PhieuStatus { get; set; }

        // =========================================================
        // LOT
        // =========================================================

        public string ItemCode { get; set; }

        public string LotNo { get; set; }

        public int Quantity { get; set; }

        public string TemCode { get; set; }

        /// <summary>
        /// QR gốc nếu LOT xuất phát từ QR.
        /// </summary>
        public string RawQr { get; set; }

        public QRCodeInfo QRInfo { get; set; }

        public DateTime? ImportDate { get; set; }

        // =========================================================
        // Hiển thị vị trí
        // =========================================================

        public string WarehouseName { get; set; }

        public string RackName { get; set; }

        public int? SlotNumber { get; set; }

        // =========================================================
        // Clone
        // =========================================================

        public LotInfo Clone()
        {
            return new LotInfo
            {
                SlotId = SlotId,
                SlotVatLyId = SlotVatLyId,

                MaPhieuKho = MaPhieuKho,
                ParentSoPhieuKho = ParentSoPhieuKho,
                PhieuStatus = PhieuStatus,

                ItemCode = ItemCode,
                LotNo = LotNo,
                Quantity = Quantity,
                TemCode = TemCode,
                RawQr = RawQr,
                QRInfo = QRInfo,
                ImportDate = ImportDate,

                WarehouseName = WarehouseName,
                RackName = RackName,
                SlotNumber = SlotNumber
            };
        }
    }

}
