using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Models
{
    /// <summary>
    /// Ghép LOT đã lưu (LUUPHIEUGIAOHANG) vào các dòng đơn lấy từ bảng đơn riêng.
    ///
    /// Tách nguyên khối từ TableOrderRepo.MergeLotTuBangRieng để test được, đồng thời sửa
    /// 2 lỗi định danh:
    ///   1. Giờ bị cắt về HH (NormalizeGio) nên 08:00 và 08:30 cùng PO/mã hàng dùng chung LOT.
    ///   2. Khoá dự phòng "mã hàng|giờ" bỏ PO nên LOT của PO này có thể gán cho PO khác.
    ///
    /// Quy tắc (theo thứ tự):
    ///   1. Khớp đủ: mã hàng + PO + HH:mm.
    ///   2. Dữ liệu cũ chỉ có giờ (GIOGIAO = "08"): khớp mã hàng + PO + HH, và CHỈ khi trong đơn
    ///      hiện tại có đúng một dòng như vậy (nếu nhiều dòng khác phút thì không đoán).
    ///   3. Dữ liệu cũ chưa có PO (PO_NO rỗng): khớp mã hàng + giờ, và CHỈ khi không mơ hồ.
    /// Dòng đã lưu có PO khác PO của dòng đơn thì KHÔNG bao giờ được ghép.
    /// </summary>
    public static class OrderLotMerger
    {
        private sealed class Saved
        {
            public DataRow Row;
            public string MaHang;
            public string Po;
            public TimeSpan Gio;
            public bool HasMinute;
            public string Lot;
        }

        private sealed class Current
        {
            public DataRow Row;
            public string MaHang;
            public string Po;
            public TimeSpan Gio;
        }

        /// <returns>Số dòng đơn đã được gán LOT.</returns>
        public static int Merge(DataTable orderRows, DataTable savedRows)
        {
            if (orderRows == null || orderRows.Rows.Count == 0) return 0;
            if (savedRows == null || savedRows.Rows.Count == 0) return 0;
            if (!orderRows.Columns.Contains("GIO")) return 0;

            if (!orderRows.Columns.Contains("GearSuDung"))
                orderRows.Columns.Add("GearSuDung", typeof(string));

            var saved = new List<Saved>();
            foreach (DataRow r in savedRows.Rows)
            {
                TimeSpan gio;
                bool hasMinute;
                if (!OrderLineKey.TryParseGio(Text(r, "GIOGIAO"), out gio, out hasMinute))
                    continue;

                string lot = Text(r, "LOT");
                if (lot.Length == 0) continue;

                saved.Add(new Saved
                {
                    Row = r,
                    MaHang = Text(r, "MAHANG"),
                    Po = Text(r, "PO_NO"),
                    Gio = gio,
                    HasMinute = hasMinute,
                    Lot = lot
                });
            }

            if (saved.Count == 0) return 0;

            var current = new List<Current>();
            foreach (DataRow r in orderRows.Rows)
            {
                TimeSpan gio;
                bool hasMinute;
                if (!OrderLineKey.TryParseGio(Text(r, "GIO"), out gio, out hasMinute))
                    continue;

                current.Add(new Current
                {
                    Row = r,
                    MaHang = Text(r, "MAHANG"),
                    Po = orderRows.Columns.Contains("PO_NO") ? Text(r, "PO_NO") : string.Empty,
                    Gio = gio
                });
            }

            int merged = 0;
            foreach (Current c in current)
            {
                Saved match = FindMatch(c, saved, current);
                if (match == null) continue;

                c.Row["LOT"] = match.Lot;
                c.Row["STATUS"] = match.Row["STATUS"]?.ToString() ?? "NG";
                c.Row["STATUSDOC"] = match.Row["STATUSDOC"]?.ToString() ?? "NG";

                if (orderRows.Columns.Contains("GEAR"))
                {
                    string gearHienTai = Text(c.Row, "GEAR");
                    string gearLuu = Text(match.Row, "GEAR");
                    if (gearHienTai.Length == 0 && gearLuu.Length > 0)
                        c.Row["GEAR"] = gearLuu;
                }

                merged++;
            }

            return merged;
        }

        private static Saved FindMatch(Current c, List<Saved> saved, List<Current> allCurrent)
        {
            // 1. Khớp đủ: mã hàng + PO + HH:mm.
            foreach (Saved s in saved)
            {
                if (s.HasMinute && Same(s.MaHang, c.MaHang) && Same(s.Po, c.Po) && s.Gio == c.Gio)
                    return s;
            }

            // 2. Dữ liệu cũ chỉ có giờ: cùng PO + HH, và chỉ một dòng đơn hiện tại như vậy.
            foreach (Saved s in saved)
            {
                if (!s.HasMinute && Same(s.MaHang, c.MaHang) && Same(s.Po, c.Po)
                    && s.Gio.Hours == c.Gio.Hours
                    && Count(allCurrent, x => Same(x.MaHang, c.MaHang) && Same(x.Po, c.Po)
                                              && x.Gio.Hours == c.Gio.Hours) == 1)
                    return s;
            }

            // 3. Dữ liệu cũ chưa có PO: chỉ khi không mơ hồ giữa các dòng đơn hiện tại.
            if (c.Po.Length > 0)
            {
                foreach (Saved s in saved)
                {
                    if (s.Po.Length != 0 || !Same(s.MaHang, c.MaHang)) continue;

                    bool timeMatches = s.HasMinute ? s.Gio == c.Gio : s.Gio.Hours == c.Gio.Hours;
                    if (!timeMatches) continue;

                    int competitors = Count(allCurrent, x => Same(x.MaHang, c.MaHang)
                        && (s.HasMinute ? x.Gio == c.Gio : x.Gio.Hours == c.Gio.Hours));
                    if (competitors == 1) return s;
                }
            }

            return null;
        }

        private static int Count(List<Current> items, Func<Current, bool> predicate)
        {
            int n = 0;
            foreach (Current item in items)
                if (predicate(item)) n++;
            return n;
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static string Text(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column)) return string.Empty;
            object v = row[column];
            return v == null || v == DBNull.Value ? string.Empty : v.ToString().Trim();
        }
    }
}
