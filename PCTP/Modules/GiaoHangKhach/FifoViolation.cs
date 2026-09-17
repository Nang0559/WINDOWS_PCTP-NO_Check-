using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach
{
    public class FifoViolation
    {
        public int Stt { get; set; }
        public string MaHang { get; set; }
        public string LotDaChon { get; set; }
        public string LotDungRaPhaiChon { get; set; }
        public int SlotIdDungRaPhaiChon { get; set; }
        public int SoLuong { get; set; }
    }

    public static class FifoViolationExtensions
    {
        public static DataTable ToErrorTable(this List<FifoViolation> violations)
        {
            var dt = new DataTable();
            dt.Columns.Add("STT", typeof(int));
            dt.Columns.Add("MH", typeof(string));
            dt.Columns.Add("LOT", typeof(string));
            dt.Columns.Add("SLC", typeof(int));
            dt.Columns.Add("SLTK", typeof(int));
            dt.Columns.Add("SLT", typeof(int));
            dt.Columns.Add("STATUS", typeof(string));

            foreach (var v in violations ?? new List<FifoViolation>())
            {
                string message = $"FIFO: mã hàng {v.MaHang} phải xuất LOT {v.LotDungRaPhaiChon} trước. LOT đang chọn: {v.LotDaChon}.";
                dt.Rows.Add(v.Stt, v.MaHang, v.LotDaChon, v.SoLuong, 0, 0, message);
            }

            return dt;
        }
    }
}
