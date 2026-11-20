using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Chặn xuất kho vượt số lượng đơn: tổng SL các LOT trong ô LOT ("LOT-SL,LOT-SL") không được lớn hơn SOLUONG
    /// của dòng phiếu giao. Dùng chung cho chọn LOT thủ công và Cập Nhập Kho (HVN/YMVN).
    /// </summary>
    public static class LotQuantityGuard
    {
        public sealed class OverItem
        {
            public int Stt { get; set; }
            public string MaHang { get; set; }
            public string Lot { get; set; }
            public int SoLuongDon { get; set; }
            public int TongLot { get; set; }
        }

        /// <summary>Cộng SL các phần "LOT-SL". false nếu có phần không đúng định dạng (không kết luận được).</summary>
        public static bool TryParseTotal(string lotSl, out int total)
        {
            total = 0;
            if (string.IsNullOrWhiteSpace(lotSl)) return false;
            foreach (string raw in lotSl.Split(','))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                int sep = part.LastIndexOf('-');
                int sl;
                if (sep <= 0 || sep >= part.Length - 1 || !int.TryParse(part.Substring(sep + 1).Trim(), out sl) || sl < 0)
                    return false;
                total += sl;
            }
            return true;
        }

        /// <summary>Các dòng TMP chưa cập nhật kho (STATUS khác OK) có tổng SL LOT lớn hơn SL đơn hàng.</summary>
        public static List<OverItem> FindOver(DataTable tmpRows)
        {
            var result = new List<OverItem>();
            if (tmpRows == null || !tmpRows.Columns.Contains("LOT") || !tmpRows.Columns.Contains("SOLUONG"))
                return result;

            bool coStatus = tmpRows.Columns.Contains("STATUS");
            foreach (DataRow row in tmpRows.Rows)
            {
                string status = coStatus ? Convert.ToString(row["STATUS"]).Trim() : string.Empty;
                if (string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase)) continue;

                string lot = Convert.ToString(row["LOT"]).Trim();
                int soLuongDon;
                int tong;
                if (lot.Length == 0 || !int.TryParse(Convert.ToString(row["SOLUONG"]).Trim(), out soLuongDon)) continue;
                if (!TryParseTotal(lot, out tong) || tong <= soLuongDon) continue;

                int stt;
                int.TryParse(tmpRows.Columns.Contains("STT") ? Convert.ToString(row["STT"]).Trim() : "0", out stt);
                result.Add(new OverItem
                {
                    Stt = stt,
                    MaHang = tmpRows.Columns.Contains("MAHANG") ? Convert.ToString(row["MAHANG"]).Trim() : string.Empty,
                    Lot = lot,
                    SoLuongDon = soLuongDon,
                    TongLot = tong
                });
            }
            return result;
        }

        /// <summary>Bảng lỗi cùng cột với màn hình lỗi Cập Nhập Kho (MH, LOT, SLC, SLTK, SLT, STATUS).</summary>
        public static DataTable ToErrorTable(List<OverItem> items)
        {
            var dt = new DataTable();
            dt.Columns.Add("MH");
            dt.Columns.Add("LOT");
            dt.Columns.Add("SLC", typeof(int));
            dt.Columns.Add("SLTK", typeof(int));
            dt.Columns.Add("SLT", typeof(int));
            dt.Columns.Add("STATUS");
            foreach (var i in items ?? new List<OverItem>())
                dt.Rows.Add(i.MaHang, i.Lot, i.SoLuongDon, i.TongLot, i.TongLot - i.SoLuongDon,
                    "STT " + i.Stt + ": số lượng LOT (" + i.TongLot.ToString("n0") + ") lớn hơn số lượng đơn hàng ("
                    + i.SoLuongDon.ToString("n0") + "). Chưa cập nhật kho - bấm vào ô LOT để chọn lại.");
            return dt;
        }
    }
}
