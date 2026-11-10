using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Infrastructure.Repositories;
using System;

namespace PCTP.Modules.BaoCao.UI
{
    public enum TraceNodeKind
    {
        Delivery,
        DeliveryLot,
        Production,
        StockIn,
        StockOut,
        StockOther,
        Missing,
        Invalid
    }

    /// <summary>Một node trên timeline truy vết (giao hàng / phân bổ LOT / sản xuất / lịch sử kho / cảnh báo thiếu).</summary>
    public sealed class TraceTimelineNode
    {
        public TraceNodeKind Kind;
        public string Title;
        public string EventType;
        public DateTime? EventTime;
        public string LotNo;
        public string PartNo;
        public decimal? Quantity;
        public string FromLocation;
        public string ToLocation;
        public string QrCode;
        public string CustomerLabel;
        public string CustomerName;
        public string DocumentNo;
        public string UserName;
        public string SourceName;
        public string SourceKey;
        public string EvidenceStatus;
        public string Subtitle;
        public string Note;
        /// <summary>Khóa sắp xếp. Cảnh báo thiếu SX đứng đầu, thiếu lịch sử kho đứng cuối.</summary>
        public DateTime SortTime;
        public int SortOrder;

        public static string ValueOrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private static string Qty(decimal? q)
        {
            return q.HasValue ? q.Value.ToString("N0") : "-";
        }

        /// <summary>Giờ giao: NGAYGIAO (ngày) + GIOGIAO nếu parse được.</summary>
        public static DateTime? DeliveryMoment(DeliveryTraceRow row)
        {
            if (row == null || !row.DeliveryDate.HasValue) return null;
            TimeSpan t;
            if (!string.IsNullOrWhiteSpace(row.DeliveryTime) && TimeSpan.TryParse(row.DeliveryTime.Trim(), out t) &&
                t >= TimeSpan.Zero && t < TimeSpan.FromDays(1))
                return row.DeliveryDate.Value.Date + t;
            return row.DeliveryDate.Value.Date;
        }

        public string DetailText()
        {
            string nl = Environment.NewLine;
            return
                "Trạng thái bằng chứng : " + EvidenceStatus + nl +
                "Loại sự kiện          : " + EventType + nl +
                "Thời gian             : " + (EventTime.HasValue ? EventTime.Value.ToString("dd/MM/yyyy HH:mm:ss") : "Không có dữ liệu") + nl +
                "LOT                   : " + ValueOrDash(LotNo) + nl +
                "Mã hàng               : " + ValueOrDash(PartNo) + nl +
                "Số lượng              : " + Qty(Quantity) + nl +
                "Vị trí từ             : " + ValueOrDash(FromLocation) + nl +
                "Vị trí đến            : " + ValueOrDash(ToLocation) + nl +
                "QR                    : " + ValueOrDash(QrCode) + nl +
                "Tem khách hàng        : " + ValueOrDash(CustomerLabel) + nl +
                "Khách hàng            : " + ValueOrDash(CustomerName) + nl +
                "Chứng từ              : " + ValueOrDash(DocumentNo) + nl +
                "Người thực hiện       : " + ValueOrDash(UserName) + nl +
                "Nguồn dữ liệu         : " + ValueOrDash(SourceName) + nl +
                "Khóa tra cứu/đối chiếu: " + ValueOrDash(SourceKey) + nl +
                nl + Note;
        }

        public static TraceTimelineNode ForDelivery(DeliveryTraceRow row, int lotCount)
        {
            DateTime? when = DeliveryMoment(row);
            return new TraceTimelineNode
            {
                Kind = TraceNodeKind.Delivery,
                Title = "Giao hàng",
                EventType = "DELIVERY",
                EventTime = when,
                LotNo = row.LotNoRaw,
                PartNo = row.PartNo,
                Quantity = row.Quantity,
                QrCode = row.QRCode,
                CustomerLabel = row.CustomerLabelData,
                CustomerName = row.CustomerName,
                DocumentNo = row.DocumentNo,
                SourceName = "dbo.LUUPHIEUGIAOHANG + dbo.LUUDOCQRCODE",
                SourceKey = row.DeliveryKey,
                EvidenceStatus = "Có phiếu giao; LOT ghép gồm " + lotCount + " LOT",
                Subtitle = "Phiếu: " + ValueOrDash(row.DocumentNo) + " | KH: " + ValueOrDash(row.CustomerName) +
                    " | " + lotCount + " LOT | SL giao: " + Qty(row.Quantity),
                Note = "LOT trên phiếu giao (LUUPHIEUGIAOHANG.LOT) là LOT ghép dạng LOT1-SL1,LOT2-SL2. Mỗi LOT được tách riêng và tra cứu sản xuất / lịch sử kho độc lập. QR/tem lấy từ LUUDOCQRCODE qua nhà máy, mã hàng, ngày và giờ giao (không phải khóa ngoại duy nhất nên cần đối soát khi trùng điều kiện).",
                SortTime = when ?? DateTime.MaxValue,
                SortOrder = 30
            };
        }

        public static TraceTimelineNode ForDeliveryLot(DeliveryTraceRow row, DeliveryLotTraceRow lot, int index, int total)
        {
            DateTime? when = DeliveryMoment(row);
            return new TraceTimelineNode
            {
                Kind = TraceNodeKind.DeliveryLot,
                Title = "Phân bổ LOT " + index + "/" + total,
                EventType = "DELIVERY_LOT",
                EventTime = when,
                LotNo = lot.LotNo,
                PartNo = row.PartNo,
                Quantity = lot.Quantity > 0 ? (decimal?)lot.Quantity : null,
                QrCode = lot.QRCode,
                CustomerLabel = row.CustomerLabelData,
                CustomerName = row.CustomerName,
                DocumentNo = row.DocumentNo,
                SourceName = "LOT ghép trong LUUPHIEUGIAOHANG.LOT",
                SourceKey = row.DeliveryKey + " / " + lot.LotNo,
                EvidenceStatus = "Có bằng chứng phân bổ LOT",
                Subtitle = "SL giao từ LOT này: " + (lot.Quantity > 0 ? lot.Quantity.ToString("N0") : "-") + " | LOT " + index + "/" + total,
                Note = "LOT và số lượng được tách từ trường LOT ghép của phiếu giao. Khóa tra cứu: 20 ký tự đầu của LOT.",
                SortTime = (when ?? DateTime.MaxValue),
                SortOrder = 31
            };
        }

        public static TraceTimelineNode InvalidLot(DeliveryTraceRow row, string rawToken, string error)
        {
            DateTime? when = DeliveryMoment(row);
            return new TraceTimelineNode
            {
                Kind = TraceNodeKind.Invalid,
                Title = "LOT không hợp lệ",
                EventType = "INVALID_DELIVERY_LOT",
                EventTime = when,
                LotNo = rawToken,
                PartNo = row.PartNo,
                CustomerName = row.CustomerName,
                DocumentNo = row.DocumentNo,
                SourceName = "dbo.LUUPHIEUGIAOHANG.LOT",
                SourceKey = row.DeliveryKey,
                EvidenceStatus = "Không nhận diện được LOT",
                Subtitle = error,
                Note = "Token này không được dùng để tra cứu. Các LOT hợp lệ khác trong LOT ghép vẫn được truy vết bình thường.",
                SortTime = (when ?? DateTime.MaxValue),
                SortOrder = 29
            };
        }

        public static TraceTimelineNode ForProduction(string lotNo, string partNo, ProductionLotTraceRow prod,
            string qr, string customer, string document)
        {
            DateTime? when = prod.ProductionDate;
            return new TraceTimelineNode
            {
                Kind = TraceNodeKind.Production,
                Title = "Sản xuất",
                EventType = "PRODUCTION",
                EventTime = when,
                LotNo = prod.Lot,
                PartNo = prod.PartNo,
                Quantity = prod.ProducedQty,
                QrCode = qr,
                CustomerName = customer,
                DocumentNo = document,
                SourceName = "dbo.STOCKTP (cột sản xuất)",
                SourceKey = "LOT=" + prod.Lot,
                EvidenceStatus = "Có bản ghi sản xuất cho LOT",
                Subtitle = "Ca: " + (prod.Shift.HasValue ? prod.Shift.Value.ToString() : "-") +
                    " | Line: " + ValueOrDash(prod.LineCode) +
                    " | SL SX: " + Qty(prod.ProducedQty) +
                    " | Nhập TP: " + (prod.ReceivedDate.HasValue ? prod.ReceivedDate.Value.ToString("dd/MM/yyyy") : "-") + " (" + Qty(prod.ReceivedQty) + ")",
                Note = "Dữ kiện sản xuất lấy từ STOCKTP theo khóa LOT (13/20 ký tự). Chỉ dùng ngày/ca/SL sản xuất, ngày/SL nhập TP, line và bộ phận; không dùng tồn hiện tại làm bằng chứng lịch sử. Model: " +
                    ValueOrDash(prod.Model) + " | Bộ phận: " + ValueOrDash(prod.DeptCode),
                SortTime = when ?? DateTime.MinValue,
                SortOrder = 5
            };
        }

        public static TraceTimelineNode MissingProduction(string lotNo, string partNo, decimal? qty, string qr, string customer, string document)
        {
            return new TraceTimelineNode
            {
                Kind = TraceNodeKind.Missing,
                Title = "Thiếu dữ liệu sản xuất",
                EventType = "MISSING_PRODUCTION",
                EventTime = null,
                LotNo = lotNo,
                PartNo = partNo,
                Quantity = qty,
                QrCode = qr,
                CustomerName = customer,
                DocumentNo = document,
                SourceName = "dbo.STOCKTP (cột sản xuất)",
                SourceKey = "LOT=" + lotNo,
                EvidenceStatus = "Thiếu bằng chứng sản xuất",
                Subtitle = "Không có bản ghi STOCKTP khớp LOT này (khóa 13/20 ký tự)",
                Note = "Đây là cảnh báo thiếu bằng chứng, không phải một sự kiện sản xuất.",
                SortTime = DateTime.MinValue,
                SortOrder = 19
            };
        }

        public static TraceTimelineNode MissingStockHistory(string lotNo, string partNo, decimal? qty, string qr, string customer, string document)
        {
            return new TraceTimelineNode
            {
                Kind = TraceNodeKind.Missing,
                Title = "Thiếu lịch sử kho",
                EventType = "MISSING_STOCK_HISTORY",
                EventTime = null,
                LotNo = lotNo,
                PartNo = partNo,
                Quantity = qty,
                QrCode = qr,
                CustomerName = customer,
                DocumentNo = document,
                SourceName = "dbo.StockHistory (sp_GetStockHistory)",
                SourceKey = "LOT=" + lotNo,
                EvidenceStatus = "Thiếu bằng chứng lịch sử",
                Subtitle = "Chưa truy xuất được bản ghi nhập/xuất kho cho LOT này",
                Note = "Đây là cảnh báo thiếu bằng chứng, không phải một giao dịch kho. Không suy diễn từ STOCKTP hoặc tồn kho hiện tại.",
                SortTime = DateTime.MaxValue,
                SortOrder = 20
            };
        }

        /// <summary>Bảng nguồn của từng loại sự kiện phiếu hàng lỗi (hiển thị ở chi tiết node để đối chiếu).</summary>
        private static string DefectEventSource(string actionType)
        {
            switch ((actionType ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "XLBT_LAP_PHIEU_TRA": return "dbo.FVN_PhieuTraHang / FVN_PhieuTraHangCT";
                case "XLBT_TRUY_VET_LOT": return "dbo.FVN_PhieuXuLyBatThuongAffectedLot";
                case "XLBT_QC_BAN_DAU": return "dbo.FVN_PhieuXuLyBatThuongQCInitial";
                case "XLBT_GIAO_SX": return "dbo.FVN_TraHangQTChungGiao";
                case "XLBT_QC_CUOI": return "dbo.FVN_TraHangQTChungQC";
                default: return "dbo.FVN_PhieuXuLyBatThuong";
            }
        }

        /// <summary>
        /// Sự kiện của phiếu trả / phiếu xử lý bất thường (lập phiếu, truy vết LOT, QC, giao sản xuất, hủy).
        /// Đây KHÔNG phải giao dịch nhập/xuất kho nên không gắn nhãn "Kho:" và không có vị trí nhập/xuất.
        /// </summary>
        private static TraceTimelineNode ForDefectTicket(ItemHistoryRow row, string requestedLot, string customer, string document)
        {
            string label = row.DefectEventLabel;
            return new TraceTimelineNode
            {
                Kind = TraceNodeKind.StockOther,
                Title = "Phiếu hàng lỗi: " + ValueOrDash(label),
                EventType = row.ActionType,
                EventTime = row.EventTime,
                LotNo = row.LotNo,
                PartNo = row.ItemCode,
                Quantity = row.Quantity,
                FromLocation = row.FromLocation,
                ToLocation = row.ToLocation,
                QrCode = row.QrCode,
                CustomerName = customer,
                DocumentNo = string.IsNullOrWhiteSpace(row.DocumentNo) ? document : row.DocumentNo,
                UserName = row.UserName,
                SourceName = DefectEventSource(row.ActionType),
                SourceKey = "LOT=" + requestedLot + " | Phiếu=" + ValueOrDash(row.DocumentNo) +
                    " | " + (row.EventTime.HasValue ? row.EventTime.Value.ToString("O") : "no-time"),
                EvidenceStatus = "Có bản ghi phiếu xử lý hàng lỗi",
                Subtitle = "Phiếu: " + ValueOrDash(row.DocumentNo) + " | SL: " + row.Quantity.ToString("N0") +
                    (string.IsNullOrWhiteSpace(row.Note) ? string.Empty : " | " + row.Note.Trim()),
                Note = "Sự kiện của phiếu xử lý hàng lỗi (không phải giao dịch nhập/xuất kho)." +
                    (string.IsNullOrWhiteSpace(row.Note) ? string.Empty : Environment.NewLine + row.Note.Trim()),
                SortTime = row.EventTime ?? DateTime.MaxValue,
                SortOrder = 10
            };
        }

        public static TraceTimelineNode ForStock(ItemHistoryRow row, string requestedLot, string customer, string document)
        {
            if (row.IsDefectTicketEvent)
                return ForDefectTicket(row, requestedLot, customer, document);

            TraceNodeKind kind = row.Direction == "Nhập" ? TraceNodeKind.StockIn
                : row.Direction == "Xuất" ? TraceNodeKind.StockOut
                : TraceNodeKind.StockOther;
            return new TraceTimelineNode
            {
                Kind = kind,
                Title = (row.Direction == "Khác" ? "Kho" : row.Direction + " kho") + ": " + ValueOrDash(row.ActionType),
                EventType = row.ActionType,
                EventTime = row.EventTime,
                LotNo = row.LotNo,
                PartNo = row.ItemCode,
                Quantity = row.Quantity,
                FromLocation = row.FromLocation,
                ToLocation = row.ToLocation,
                QrCode = row.QrCode,
                CustomerName = customer,
                DocumentNo = string.IsNullOrWhiteSpace(row.DocumentNo) ? document : row.DocumentNo,
                UserName = row.UserName,
                SourceName = "dbo.StockHistory (sp_GetStockHistory)",
                SourceKey = "LOT=" + requestedLot + " | Phiếu=" + ValueOrDash(row.DocumentNo) +
                    " | " + (row.EventTime.HasValue ? row.EventTime.Value.ToString("O") : "no-time"),
                EvidenceStatus = "Có bản ghi lịch sử kho",
                Subtitle = "SL: " + row.Quantity.ToString("N0") + " | " +
                    ValueOrDash(row.FromLocation) + " → " + ValueOrDash(row.ToLocation),
                Note = "Sự kiện lấy từ read model lịch sử kho. Không coi tồn kho hiện tại là bằng chứng lịch sử.",
                SortTime = row.EventTime ?? DateTime.MaxValue,
                SortOrder = 10
            };
        }
    }
}
