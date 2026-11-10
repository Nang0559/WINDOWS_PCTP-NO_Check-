using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Modules.XuLyHangLoi.Services;
using PCTP.Shared.Common;
using PCTP.Shared.Lookup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.UI
{
    /// <summary>
    /// Cầu nối giữa ô tra cứu chung (StockLookupControl, khối "Lọc theo ngày") và các form báo cáo.
    /// Ý nghĩa 3 kiểu ngày KHÔNG định nghĩa lại ở đây: luôn đi qua <see cref="StockLookupFilter"/>
    /// để báo cáo hiểu ngày giống hệt các form xử lý hàng lỗi.
    /// Khoảng Từ/Đến riêng của từng báo cáo (ngày kiểm tra / ngày giao dịch kho / NGAYGIAO) vẫn giữ nguyên;
    /// bộ lọc này là điều kiện bổ sung, chỉ có tác dụng khi người dùng tick "Lọc theo ngày".
    /// </summary>
    internal static class ReportLookupDateFilter
    {
        private static readonly Func<string, string, bool> All = (item, lot) => true;

        /// <summary>
        /// Dựng hàm (Mã hàng, LOT) → trong khoảng ngày hay không. Chưa tick lọc ngày → luôn true, không chạm DB.
        /// Chế độ cần DB truy vấn MỘT lần, chạy ngoài luồng UI (gọi bên trong WaitForm).
        /// </summary>
        public static Task<Func<string, string, bool>> BuildMatcherAsync(StockLookupCriteria criteria)
        {
            if (criteria == null || !criteria.DateEnabled)
                return Task.FromResult(All);

            return Task.Run(() => StockLookupFilter.CreateDateMatcher(
                criteria,
                criteria.DateMode == StockLookupDateMode.NgaySanXuat ? null : CreateRepository()));
        }

        /// <summary>
        /// Giữ các dòng có ÍT NHẤT MỘT LOT thỏa bộ lọc. LOT ghép "LOT1-100,LOT2-50" được tách trước khi so.
        /// Dòng không có LOT hợp lệ bị loại khi đang lọc ngày (không xác định được ngày).
        /// </summary>
        public static List<T> Apply<T>(
            IEnumerable<T> rows,
            StockLookupCriteria criteria,
            Func<string, string, bool> matcher,
            Func<T, string> itemOf,
            params Func<T, string>[] lotSources)
        {
            var list = (rows ?? Enumerable.Empty<T>()).ToList();
            if (criteria == null || !criteria.DateEnabled || matcher == null)
                return list;

            return list.Where(r => RowMatches(r, matcher, itemOf, lotSources)).ToList();
        }

        /// <summary>Số ngày gần nhất dùng khi người dùng KHÔNG tick "Lọc theo ngày" (giữ hành vi mặc định cũ).</summary>
        public const int DefaultDays = 30;

        /// <summary>
        /// Khoảng ngày gửi xuống truy vấn gốc của báo cáo, suy ra từ khối "Lọc theo ngày" (không còn ô From/To riêng):
        ///  - không tick lọc ngày        : <paramref name="defaultDays"/> ngày gần nhất (0 = không giới hạn);
        ///  - "Ngày xuất hàng" và truy vấn gốc tính theo NGAYGIAO (<paramref name="deliveryIsNative"/>): đúng khoảng đã chọn;
        ///  - còn lại                    : không giới hạn (null), vì ngày của truy vấn gốc không cùng nghĩa
        ///    với kiểu ngày đã chọn — việc khoanh vùng do bộ lọc sau truy vấn đảm nhiệm.
        /// </summary>
        public static void ResolveRange(
            StockLookupCriteria criteria, bool deliveryIsNative, int defaultDays,
            out DateTime? from, out DateTime? to)
        {
            from = null;
            to = null;

            if (criteria == null || !criteria.DateEnabled)
            {
                if (defaultDays > 0)
                {
                    from = DateTime.Today.AddDays(-defaultDays);
                    to = DateTime.Today;
                }
                return;
            }

            if (deliveryIsNative && criteria.DateMode == StockLookupDateMode.NgayXuatHang)
            {
                from = criteria.From.Date;
                to = criteria.To.Date;
            }
        }

        /// <summary>Có cần lọc thêm sau truy vấn không (false khi truy vấn gốc đã lọc đúng kiểu ngày đó rồi).</summary>
        public static bool NeedsPostFilter(StockLookupCriteria criteria, bool deliveryIsNative)
        {
            return criteria != null && criteria.DateEnabled
                && !(deliveryIsNative && criteria.DateMode == StockLookupDateMode.NgayXuatHang);
        }

        /// <summary>Mô tả khoảng ngày thực sự áp dụng, dùng cho trạng thái / file xuất.</summary>
        public static string DescribeApplied(StockLookupCriteria criteria)
        {
            string d = Describe(criteria);
            return d.Length > 0 ? d : "Không lọc ngày (mặc định " + DefaultDays + " ngày gần nhất)";
        }

        /// <summary>Mô tả ngắn cho dòng trạng thái / phần đầu file xuất. Rỗng nếu không lọc ngày.</summary>
        public static string Describe(StockLookupCriteria criteria)
        {
            if (criteria == null || !criteria.DateEnabled)
                return string.Empty;

            return string.Format("{0} {1:dd/MM/yyyy} – {2:dd/MM/yyyy}",
                StockLookupCriteria.DateModeLabel(criteria.DateMode), criteria.From, criteria.To);
        }

        private static bool RowMatches<T>(
            T row, Func<string, string, bool> matcher, Func<T, string> itemOf, Func<T, string>[] lotSources)
        {
            string item = itemOf(row);

            foreach (var source in lotSources)
            {
                string raw = source(row);
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                foreach (var part in LotCodeHelper.ParseCompositeLotTolerant(raw, null))
                {
                    if (part.IsValid && matcher(item, part.LotNo))
                        return true;
                }
            }

            return false;
        }

        private static INoiBoLookupRepository CreateRepository()
        {
            var provider = new SQLPROVIDER();
            return new NoiBoLookupRepository(new PhieuSqlExecutor(provider), new UnitOfWork(provider));
        }
    }
}
