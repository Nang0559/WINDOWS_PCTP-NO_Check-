using PCTP.Common;
using PCTP.Modules.BaoCao.Application.Contracts;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Infrastructure.Queries;
using PCTP.Modules.BaoCao.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.UI
{
    /// <summary>
    /// Dựng LotTraceTimelineBuilder với các nguồn dữ liệu mặc định (giao hàng, sản xuất, lịch sử kho).
    /// Các form ngoài BaoCao (vd. FormDoiChieuPhieuGiao) dùng chung để khỏi lặp cách ghép nguồn.
    /// </summary>
    public static class LotTraceTimelineBuilderFactory
    {
        public static LotTraceTimelineBuilder CreateDefault()
        {
            var delivery = new DeliveryTraceReadService();
            return new LotTraceTimelineBuilder(
                delivery,
                delivery,
                new ProductionLotTraceRepository(),
                new StockHistoryQueryService());
        }
    }

    public sealed class TraceTimelineResult
    {
        public List<TraceTimelineNode> Nodes = new List<TraceTimelineNode>();
        public int LotCount;
        public int DeliveryCount;
        public string Summary;
    }

    /// <summary>
    /// Dựng timeline truy vết dùng chung cho 2 màn hình:
    ///  - Truy vết NGƯỢC: phiếu giao hàng -> tách LOT ghép (LOT1-SL1,LOT2-SL2...) -> sản xuất + lịch sử kho của TỪNG LOT.
    ///  - Truy vết XUÔI : LOT sản xuất / lịch sử kho -> sản xuất + lịch sử kho -> các phiếu giao hàng chứa LOT đó.
    /// Khóa tra cứu luôn là từng LotNo (20 ký tự đầu, hoặc 13 ký tự với LOT chuẩn cũ), không dùng chuỗi LOT ghép.
    /// Read-only.
    /// </summary>
    public sealed class LotTraceTimelineBuilder
    {
        private sealed class LotRef
        {
            public string LotNo;
            public decimal? Quantity;
            public int Index;
            public int Total;
        }

        private readonly IQrTraceQuery _qrQuery;
        private readonly ILotTraceQuery _lotQuery;
        private readonly IProductionLotTraceQuery _productionQuery;
        private readonly IStockHistoryQuery _stockQuery;

        public LotTraceTimelineBuilder(
            IQrTraceQuery qrQuery,
            ILotTraceQuery lotQuery,
            IProductionLotTraceQuery productionQuery,
            IStockHistoryQuery stockQuery)
        {
            if (qrQuery == null) throw new ArgumentNullException("qrQuery");
            if (lotQuery == null) throw new ArgumentNullException("lotQuery");
            if (productionQuery == null) throw new ArgumentNullException("productionQuery");
            if (stockQuery == null) throw new ArgumentNullException("stockQuery");
            _qrQuery = qrQuery;
            _lotQuery = lotQuery;
            _productionQuery = productionQuery;
            _stockQuery = stockQuery;
        }

        /// <summary>Truy vết ngược: từ 1 dòng giao hàng.</summary>
        public async Task<TraceTimelineResult> BuildForDeliveryAsync(DeliveryTraceRow row, CancellationToken ct)
        {
            if (row == null) throw new ArgumentNullException("row");
            var result = new TraceTimelineResult();

            IReadOnlyList<DeliveryLotTraceRow> allocations = await _qrQuery.GetLotsAsync(row.DeliveryKey, row.QRCode, ct);
            var parts = (allocations ?? new List<DeliveryLotTraceRow>()).Where(x => x != null).ToList();

            var valid = new List<LotRef>();
            foreach (DeliveryLotTraceRow a in parts)
            {
                if (!string.IsNullOrWhiteSpace(a.ParseError) || string.IsNullOrWhiteSpace(a.LotNo)) continue;
                valid.Add(new LotRef { LotNo = a.LotNo, Quantity = a.Quantity > 0 ? (decimal?)a.Quantity : null });
            }
            for (int i = 0; i < valid.Count; i++) { valid[i].Index = i + 1; valid[i].Total = valid.Count; }

            result.Nodes.Add(TraceTimelineNode.ForDelivery(row, valid.Count));
            foreach (DeliveryLotTraceRow a in parts.Where(x => !string.IsNullOrWhiteSpace(x.ParseError)))
                result.Nodes.Add(TraceTimelineNode.InvalidLot(row, a.LotNo, a.ParseError));

            foreach (LotRef lot in valid)
            {
                DeliveryLotTraceRow a = parts.First(x => string.Equals(x.LotNo, lot.LotNo, StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(x.ParseError));
                result.Nodes.Add(TraceTimelineNode.ForDeliveryLot(row, a, lot.Index, lot.Total));
            }

            var distinctLots = valid
                .GroupBy(x => LotCodeHelper.GetSearchKey(x.LotNo), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First()).ToList();
            await AddLotEvidenceAsync(result.Nodes, distinctLots, row.PartNo, row.QRCode, row.CustomerName, row.DocumentNo, ct);

            result.LotCount = valid.Count;
            result.DeliveryCount = 1;
            Finish(result, "Truy vết ngược: " + valid.Count + " LOT trong LOT ghép");
            return result;
        }

        /// <summary>Truy vết xuôi: từ 1 LOT (hoặc LOT ghép nhập tay) + mã hàng.</summary>
        public async Task<TraceTimelineResult> BuildForLotAsync(
            string lotInput, string partNo, DateTime? deliveryFrom, DateTime? deliveryTo, CancellationToken ct)
        {
            var result = new TraceTimelineResult();
            var requested = new List<LotRef>();
            foreach (LotAllocation p in LotCodeHelper.ParseCompositeLotTolerant(lotInput, null))
            {
                if (!p.IsValid) continue;
                string key = LotCodeHelper.GetSearchKey(p.LotNo);
                if (requested.Any(x => string.Equals(x.LotNo, key, StringComparison.OrdinalIgnoreCase))) continue;
                requested.Add(new LotRef { LotNo = key, Quantity = p.HasQuantity ? (decimal?)p.Quantity : null });
            }
            for (int i = 0; i < requested.Count; i++) { requested[i].Index = i + 1; requested[i].Total = requested.Count; }
            if (requested.Count == 0)
            {
                result.Summary = "Không nhận diện được LOT để truy vết.";
                return result;
            }

            // Sản xuất + lịch sử kho của (các) LOT đang tra cứu.
            await AddLotEvidenceAsync(result.Nodes, requested, partNo, null, null, null, ct);

            // Các phiếu giao chứa LOT: mỗi phiếu chỉ 1 lần (kết quả có thể nhân theo số lần đọc QR).
            IReadOnlyList<DeliveryTraceRow> hits = await _lotQuery.SearchAsync(
                string.Join(",", requested.Select(x => x.LotNo)), partNo, null, deliveryFrom, deliveryTo, ct);
            var deliveries = (hits ?? new List<DeliveryTraceRow>())
                .Where(x => x != null)
                .GroupBy(x => x.DeliveryKey ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            foreach (DeliveryTraceRow d in deliveries)
            {
                ct.ThrowIfCancellationRequested();
                IReadOnlyList<DeliveryLotTraceRow> allocations = await _qrQuery.GetLotsAsync(d.DeliveryKey, null, ct);
                var all = (allocations ?? new List<DeliveryLotTraceRow>())
                    .Where(x => x != null && string.IsNullOrWhiteSpace(x.ParseError) && !string.IsNullOrWhiteSpace(x.LotNo)).ToList();
                var mine = all.Where(x => requested.Any(r => LotCodeHelper.AreLotKeysEquivalent(x.LotNo, r.LotNo))).ToList();

                TraceTimelineNode delivery = TraceTimelineNode.ForDelivery(d, all.Count);
                if (all.Count > 1)
                    delivery.Note += Environment.NewLine + Environment.NewLine + "Các LOT trong cùng phiếu giao: " +
                        string.Join(", ", all.Select(x => x.LotNo + " (SL " + x.Quantity.ToString("N0") + ")"));
                result.Nodes.Add(delivery);

                for (int i = 0; i < mine.Count; i++)
                {
                    int idx = all.IndexOf(mine[i]) + 1;
                    result.Nodes.Add(TraceTimelineNode.ForDeliveryLot(d, mine[i], idx, all.Count));
                }
            }

            result.LotCount = requested.Count;
            result.DeliveryCount = deliveries.Count;
            Finish(result, "Truy vết xuôi: " + requested.Count + " LOT → " + deliveries.Count + " phiếu giao");
            return result;
        }

        /// <summary>Sản xuất + lịch sử kho cho TẤT CẢ LOT của LOT ghép (1 lần gọi sp_GetStockHistory cho cả nhóm).</summary>
        private async Task AddLotEvidenceAsync(
            List<TraceTimelineNode> nodes, IList<LotRef> lots, string partNo,
            string qr, string customer, string document, CancellationToken ct)
        {
            if (lots == null || lots.Count == 0) return;

            foreach (LotRef lot in lots)
            {
                ct.ThrowIfCancellationRequested();
                IReadOnlyList<ProductionLotTraceRow> production = await _productionQuery.FindByLotAsync(lot.LotNo, partNo, ct);
                if (production != null && production.Count > 0)
                {
                    foreach (ProductionLotTraceRow p in production)
                        nodes.Add(TraceTimelineNode.ForProduction(lot.LotNo, partNo, p, qr, customer, document));
                }
                else
                    nodes.Add(TraceTimelineNode.MissingProduction(lot.LotNo, partNo, lot.Quantity, qr, customer, document));
            }

            var criteria = new HistorySearchCriteria
            {
                LotNos = lots.Select(x => x.LotNo).ToList(),
                PartNo = string.IsNullOrWhiteSpace(partNo) ? null : partNo.Trim(),
                From = null,
                To = null
            };
            IReadOnlyList<ItemHistoryRow> history = await _stockQuery.SearchAsync(criteria, ct);
            var rows = (history ?? new List<ItemHistoryRow>()).Where(x => x != null).ToList();

            foreach (LotRef lot in lots)
            {
                int matched = 0;
                foreach (ItemHistoryRow item in rows)
                {
                    if (!LotCodeHelper.AreLotKeysEquivalent(item.LotNo, lot.LotNo)) continue;
                    nodes.Add(TraceTimelineNode.ForStock(item, lot.LotNo, customer, document));
                    // Sự kiện phiếu hàng lỗi không phải giao dịch kho: không được che cảnh báo "Thiếu lịch sử kho".
                    if (!item.IsDefectTicketEvent) matched++;
                }
                if (matched == 0)
                    nodes.Add(TraceTimelineNode.MissingStockHistory(lot.LotNo, partNo, lot.Quantity, qr, customer, document));
            }
        }

        private static void Finish(TraceTimelineResult result, string prefix)
        {
            result.Nodes = result.Nodes
                .OrderBy(x => x.SortTime)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.LotNo ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.Summary = prefix + "; timeline " + result.Nodes.Count + " node.";
        }
    }
}
