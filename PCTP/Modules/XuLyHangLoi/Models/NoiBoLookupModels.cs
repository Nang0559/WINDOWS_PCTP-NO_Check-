using System;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    /// <summary>Bộ phận lấy từ B7R2_FCC.dbo.B20Dept — dùng cho LookupEdit (Code + Name).</summary>
    public sealed class DeptLookupItem
    {
        public string Code { get; set; }
        public string Name { get; set; }

        /// <summary>Text hiển thị trên ô đã chọn: "Code - Name".</summary>
        public string Display
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Code)) return Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(Name)) return Code;
                return Code + " - " + Name;
            }
        }
    }

    /// <summary>Mã hàng đang có trong Slot (kèm tên từ vB20Item).</summary>
    public sealed class ItemLookupItem
    {
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
    }

    /// <summary>Một dòng Slot đang chứa hàng (Slot + LOT) để chọn làm Slot nguồn.</summary>
    public sealed class SlotNguonLookupItem
    {
        public int SlotLotId { get; set; }
        public int SlotId { get; set; }

        /// <summary>Kho / Rack / Slot n.</summary>
        public string ViTri { get; set; }

        public string ItemCode { get; set; }
        public string TenHang { get; set; }
        public string LotNo { get; set; }
        public int Quantity { get; set; }
        public string TemCode { get; set; }
    }

    /// <summary>
    /// Kết quả "chọn nhanh từ Slot" (FormChonSlotNoiBo) trả về cho form tạo phiếu:
    /// chỉ gồm khóa SlotLot + số lượng + lý do NG. Mọi thông tin còn lại (Slot, mã hàng,
    /// tên hàng, LOT) do form tạo phiếu tự lấy từ lookup của nó để luôn cùng định dạng.
    /// </summary>
    public sealed class SlotNguonChon
    {
        public int SlotLotId { get; set; }
        public int SoLuong { get; set; }
        public string LyDoNg { get; set; }
    }
}
