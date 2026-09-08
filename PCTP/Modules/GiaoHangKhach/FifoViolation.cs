using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach
{
    public class FifoViolation
    {
        public string MaHang { get; set; }
        public string LotDaChon { get; set; }
        public string LotDungRaPhaiChon { get; set; }
        public int SlotIdDungRaPhaiChon { get; set; }
    }
    public static class FifoViolationExtensions
    {
        /// <summary>
        /// Build DataTable "errors" đúng shape cột (MH, LOT, SLC, SLTK, SLT, STATUS) mà
        /// SP Usp_Qrcode_Update_Stock2405 trả về — để CapNhapKho có thể trả violation FIFO
        /// ra ngoài qua CÙNG 1 kênh "out DataTable errors" mà UI đang xử lý sẵn, mà không
        /// cần đụng vào STOCKTP/Slot (chặn TRƯỚC khi gọi SP — xem WORKFLOW_GIAOHANGKHACH.md).
        /// </summary>
        public static DataTable ToErrorTable(this List<FifoViolation> violations)
        {
            var dt = new DataTable();
            dt.Columns.Add("MH", typeof(string));
            dt.Columns.Add("LOT", typeof(string));
            dt.Columns.Add("SLC", typeof(int));
            dt.Columns.Add("SLTK", typeof(int));
            dt.Columns.Add("SLT", typeof(int));
            dt.Columns.Add("STATUS", typeof(string));

            foreach (var v in violations ?? new List<FifoViolation>())
            {
                dt.Rows.Add(
                    v.MaHang,
                    v.LotDaChon,
                    0,
                    0,
                    0,
                    $"FIFO: phải xuất LOT {v.LotDungRaPhaiChon} (Slot {v.SlotIdDungRaPhaiChon}) trước — không phải {v.LotDaChon}");
            }

            return dt;
        }
    }
}
