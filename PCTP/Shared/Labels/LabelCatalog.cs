using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DevExpress.XtraReports.UI;

namespace PCTP.Shared.Labels
{
    /// <summary>
    /// Khai báo MỘT loại tem/phiếu in dùng chung toàn hệ thống: mã, cách dựng báo cáo gốc (XtraReport biên dịch sẵn),
    /// cách đổi dữ liệu thô (kết quả stored procedure) sang bảng gắn vào bố cục, và dữ liệu mẫu cho Designer / in thử.
    /// Layout tùy chỉnh (.repx) do <see cref="LabelLayoutStore"/> giữ; không có thì dùng bố cục gốc trong code.
    /// </summary>
    public sealed class LabelDefinition
    {
        public LabelDefinition(string code, string title, string paper, Func<XtraReport> createReport,
                               Func<DataTable> createSampleData, Func<DataTable, DataTable> prepareData = null)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Thiếu mã tem.", nameof(code));
            Code = code.Trim();
            Title = title ?? Code;
            Paper = paper ?? string.Empty;
            CreateReport = createReport ?? throw new ArgumentNullException(nameof(createReport));
            CreateSampleData = createSampleData ?? throw new ArgumentNullException(nameof(createSampleData));
            PrepareData = prepareData;
        }

        /// <summary>Chỉ chữ, số, gạch dưới (dùng làm tên file .repx).</summary>
        public string Code { get; }
        public string Title { get; }
        /// <summary>Mô tả khổ giấy / số tem mỗi trang, chỉ để hiển thị.</summary>
        public string Paper { get; }
        public Func<XtraReport> CreateReport { get; }
        public Func<DataTable> CreateSampleData { get; }
        /// <summary>Dữ liệu thô → bảng gắn vào bố cục (thêm cột chữ đã định dạng...). Null = dùng nguyên bảng.</summary>
        public Func<DataTable, DataTable> PrepareData { get; }
    }

    /// <summary>Danh mục các loại tem. Mỗi module đăng ký tem của mình tại <see cref="Initialize"/>; đăng ký lặp lại cùng mã là ghi đè.</summary>
    public static class LabelCatalog
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, LabelDefinition> Items =
            new Dictionary<string, LabelDefinition>(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized;

        public static void Register(LabelDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            lock (Gate) Items[definition.Code] = definition;
        }

        public static IReadOnlyList<LabelDefinition> All()
        {
            Initialize();
            lock (Gate) return Items.Values.OrderBy(d => d.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static LabelDefinition Get(string code)
        {
            Initialize();
            LabelDefinition d;
            lock (Gate)
                if (Items.TryGetValue(code ?? string.Empty, out d)) return d;
            throw new InvalidOperationException("Chưa đăng ký loại tem '" + code + "' trong LabelCatalog.");
        }

        /// <summary>Nơi duy nhất liệt kê module có tem. Thêm tem mới: viết lớp đăng ký trong module rồi gọi ở đây.</summary>
        public static void Initialize()
        {
            lock (Gate)
            {
                if (_initialized) return;
                _initialized = true;   // đặt trước: Register() chỉ ghi vào Items, không gọi lại Initialize
                PCTP.Modules.KhoLinhKien.ComponentLabelCatalog.Register();
                GhepLotLabelCatalog.Register();
            }
        }
    }
}
