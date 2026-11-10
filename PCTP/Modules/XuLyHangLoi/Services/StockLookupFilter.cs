using PCTP.Common;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Lookup;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    /// <summary>
    /// Nguồn DUY NHẤT quy định "khoảng ngày" của ô tra cứu chung (<see cref="StockLookupControl"/>)
    /// nghĩa là gì theo từng chế độ, để mọi form lọc giống hệt nhau:
    ///  - Ngày sản xuất        : 6 ký tự đầu của LOT (YYMMDD) — tính trực tiếp, không cần DB.
    ///  - Ngày xuất hàng       : (Mã, LOT) có trong phiếu giao NGAYGIAO thuộc khoảng.
    ///  - Ngày lập phiếu xử lý : (Mã, LOT) có trên phiếu trả / phiếu xử lý bất thường lập trong khoảng.
    /// </summary>
    public static class StockLookupFilter
    {
        private static readonly Func<string, string, bool> TatCa = (item, lot) => true;

        /// <summary>
        /// Trả hàm (Mã hàng, LOT) → có nằm trong khoảng ngày của tiêu chí không.
        /// Không bật lọc ngày → luôn true. Chế độ cần DB sẽ truy vấn MỘT lần lúc tạo hàm.
        /// </summary>
        public static Func<string, string, bool> CreateDateMatcher(
            StockLookupCriteria criteria,
            INoiBoLookupRepository repo)
        {
            if (criteria == null || !criteria.DateEnabled)
                return TatCa;

            DateTime tu = criteria.From.Date;
            DateTime den = criteria.To.Date;

            if (tu > den)
            {
                DateTime t = tu; tu = den; den = t;
            }

            if (criteria.DateMode == StockLookupDateMode.NgaySanXuat)
            {
                return (item, lot) =>
                {
                    DateTime d;
                    return LotCodeHelper.TryGetProductionDate(lot, out d)
                        && d.Date >= tu && d.Date <= den;
                };
            }

            if (repo == null)
                throw new InvalidOperationException(
                    "Không có nguồn dữ liệu để lọc theo "
                    + StockLookupCriteria.DateModeLabel(criteria.DateMode) + ".");

            // Tab mã: chỉ lấy chứng từ của mã đó cho nhẹ; tab LOT/QR: mọi mã.
            string itemCode = criteria.IsItemSearch ? criteria.EffectiveItemCode : null;

            List<ItemLotRef> refs = criteria.DateMode == StockLookupDateMode.NgayXuatHang
                ? repo.GetLotsDeliveredBetween(tu, den, itemCode)
                : repo.GetLotsOfDefectDocsBetween(tu, den, itemCode);

            // Gom theo (Mã + 13 ký tự đầu của LOT) rồi so khóa LOT chuẩn (13/20 ký tự).
            var byHead = (refs ?? new List<ItemLotRef>())
                .Where(r => r != null
                    && !string.IsNullOrWhiteSpace(r.ItemCode)
                    && !string.IsNullOrWhiteSpace(r.LotNo))
                .GroupBy(r => Head(r.ItemCode, r.LotNo), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.LotNo).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            return (item, lot) =>
            {
                if (string.IsNullOrWhiteSpace(item) || string.IsNullOrWhiteSpace(lot))
                    return false;

                List<string> candidates;
                if (!byHead.TryGetValue(Head(item, lot), out candidates))
                    return false;

                return candidates.Any(c => LotCodeHelper.AreLotKeysEquivalent(c, lot));
            };
        }

        private static string Head(string item, string lot)
        {
            string l = (lot ?? string.Empty).Trim();
            if (l.Length > LotCodeHelper.LEN_LEGACY_KEY)
                l = l.Substring(0, LotCodeHelper.LEN_LEGACY_KEY);

            return (item ?? string.Empty).Trim() + "|" + l;
        }
    }
}
