using System;

namespace PCTP.VIEWSTOCK.Models
{
    public class StockItem
    {
        // ── LOT / Part ───────────────────────────────────────────────────
        public string Lot { get; set; }  // LOT (nvarchar 50)
        public string Part { get; set; }  // PART (nvarchar 50)
        public string Name { get; set; }  // NAME (nvarchar 100)
        public string Model { get; set; }  // MODEL (nvarchar 30)
        public string SP { get; set; }  // SP (nvarchar 100)

        // ── Số lượng sản xuất ────────────────────────────────────────────
        public int? SlSX { get; set; }  // SLSX
        public short? CaSX { get; set; }  // CASX (smallint)
        public DateTime? NgaySX { get; set; } // NGAYSX (smalldatetime)

        // ── Nhập kho lần 1 (chính) ───────────────────────────────────────
        public DateTime? NgayNhap { get; set; }  // NGAYNHAP
        public int? SlNhap { get; set; }  // SLNHAP

        // ── Nhập kho bổ sung lần 2-5 ─────────────────────────────────────
        public DateTime? NgayNhap1 { get; set; }  // NGAYNHAP1
        public int? SlNhap1 { get; set; }  // SLNHAP1
        public DateTime? NgayNhap2 { get; set; }  // NGAYNHAP2
        public int? SlNhap2 { get; set; }  // SLNHAP2
        public DateTime? NgayNhap3 { get; set; }  // NGAYNHAP3
        public int? SlNhap3 { get; set; }  // SLNHAP3
        public DateTime? NgayNhap4 { get; set; }  // NGAYNHAP4
        public int? SlNhap4 { get; set; }  // SLNHAP4
        public DateTime? NgayNhap5 { get; set; }  // NGAYNHAP5
        public int? SlNhap5 { get; set; }  // SLNHAP5

        // ── Xuất kho ─────────────────────────────────────────────────────
        public DateTime? NgayXuat { get; set; }  // NGAYXUAT
        public int? SlXuat { get; set; }  // SLXUAT

        // ── Tồn kho ──────────────────────────────────────────────────────
        public int? SlConLai { get; set; }  // SLCONLAI
        public int? SlConLaiTmp { get; set; }  // SLCONLAITMP — xuất tạm

        // ── Trạng thái ───────────────────────────────────────────────────
        public short? Satus { get; set; }  // SATUS (smallint) ← typo DB
        public string StatusNhap { get; set; }  // STATUSNHAP (nvarchar 50)

        // ── Phân loại ────────────────────────────────────────────────────
        public string LineCodes { get; set; }  // LineCodes (nvarchar 10)
        public string DeptCode { get; set; }  // DeptCode  (nvarchar 10)

        // ── Không có trong STOCKTP — dùng nội bộ C# ──────────────────────
        // (không map lên DB, chỉ dùng trong Service/Presenter)
        public string Find { get; set; }  // key tìm kiếm
        public string SoPhieu { get; set; }  // số phiếu nhập
        public string CaseNo { get; set; }  // case number từ QR
        public string Ca { get; set; }  // ca sản xuất (string)
        public string GioXuat { get; set; }  // giờ xuất
        public string LyDoNG { get; set; }  // lý do NG

        // ── Rack/Slot — từ bảng Slot/SlotLot ─────────────────────────────
        public int? SlotId { get; set; }  // Slot.SlotId
        public int? RackId { get; set; }  // Rack.RackId
        public string RackName { get; set; }  // Rack.RackName
        public int? SlotNumber { get; set; } // Slot.SlotNumber
        public string TemCode { get; set; }  // SlotLot.TemCode
        public string QrData { get; set; }  // SlotLot.QrData

        // ── Computed helpers ─────────────────────────────────────────────
        /// <summary>Tổng đã nhập kể cả bổ sung</summary>
        public int TongSlNhap =>
            (SlNhap ?? 0) + (SlNhap1 ?? 0) + (SlNhap2 ?? 0) +
            (SlNhap3 ?? 0) + (SlNhap4 ?? 0) + (SlNhap5 ?? 0);

        /// <summary>Slot còn trống (dùng khi chưa có trong STOCKTP)</summary>
        public bool ConTonKho => (SlConLai ?? 0) > 0;
    }
}