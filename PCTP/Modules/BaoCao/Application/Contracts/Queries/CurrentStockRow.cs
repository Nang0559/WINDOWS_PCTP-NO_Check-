using System;
using System.Text.RegularExpressions;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    public sealed class CurrentStockRow
    {
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string Model { get; set; }
        public string LotNo { get; set; }
        public string QrCode { get; set; }
        public decimal Quantity { get; set; }          // SL tại vị trí (SlotLot) / phần chưa gán slot
        public string LocationCode { get; set; }
        public string Status { get; set; }

        // Thông tin LOT (STOCKTP) — lặp lại ở mọi dòng của cùng LOT để đối chiếu nhập/xuất/tồn.
        public DateTime? ProductionDate { get; set; }
        public int? ShiftNo { get; set; }
        public decimal? QtyProduced { get; set; }      // SLSX
        public decimal? QtyReceived { get; set; }      // SLNHAP (tổng đã nhập)
        public decimal? QtyIssued { get; set; }        // SLXUAT (tổng đã xuất)
        public decimal? QtyOnHandLot { get; set; }     // SLCONLAI (tồn của cả LOT)
        public DateTime? ReceivedDate { get; set; }    // NGAYNHAP
        public DateTime? IssuedDate { get; set; }      // NGAYXUAT
        public DateTime? SlotImportDate { get; set; }  // ngày nhập vào slot
        public string DocumentNo { get; set; }         // MaPhieu của SlotLot

        /// <summary>LOT dạng số khoa học (vd 2.61021E+19): dữ liệu gốc đã bị Excel làm hỏng, không phải lỗi hiển thị.</summary>
        public bool LotNoFormatError { get { return IsScientificLot(LotNo); } }

        public string Warning
        {
            get { return LotNoFormatError ? "LOT sai định dạng (số khoa học) - cần sửa dữ liệu gốc" : null; }
        }

        private static readonly Regex Scientific =
            new Regex(@"^\s*\d+(\.\d+)?E\+\d+\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool IsScientificLot(string lotNo)
        {
            return !string.IsNullOrEmpty(lotNo) && Scientific.IsMatch(lotNo);
        }
    }
}
